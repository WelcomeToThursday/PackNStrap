using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;

namespace BeltSlot.Patches
{
    // makes ammo + throwables in the belt reachable for reload/throw.
    // patches the specific (non-generic) caller methods rather than the
    // generic GetReachableItemsOfTypeNonAlloc<T> because HarmonyX misfires
    // postfixes across all closed instantiations of a generic method
    // (see BeltReloadPatches.cs for the mag-only postfix path).
    //
    // targets:
    //   Class1725.vmethod_1     - throwables (G key)
    //   Class1730.method_14/15/16 - shotgun barrel / cylinder / standard ammo
    public static class BeltCallSiteTranspilers
    {
        private static readonly Harmony _harmony = new Harmony("com.trenchfoot.beltslot.callsites");

        public static void Apply()
        {
            ApplyTranspiler(AccessTools.TypeByName("Class1725"), "vmethod_1", typeof(ThrowWeapItemClass), "Class1725.vmethod_1 (throwables)");

            var class1730 = AccessTools.TypeByName("Class1730");
            ApplyTranspiler(class1730, "method_14", typeof(AmmoItemClass), "Class1730.method_14 (barrel ammo)");
            ApplyTranspiler(class1730, "method_15", typeof(AmmoItemClass), "Class1730.method_15 (cylinder ammo)");
            ApplyTranspiler(class1730, "method_16", typeof(AmmoItemClass), "Class1730.method_16 (standard ammo)");
        }

        private static void ApplyTranspiler(Type targetType, string targetMethodName, Type itemType, string label)
        {
            if (targetType == null)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler target type for {label} not found; skipping");
                return;
            }
            var target = AccessTools.Method(targetType, targetMethodName);
            if (target == null)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler target method for {label} not found; skipping");
                return;
            }
            // one transpiler entry per item type so HarmonyMethod can bind
            // the right one without us needing to pass itemType as state.
            MethodInfo transpilerEntry;
            if (itemType == typeof(ThrowWeapItemClass))
                transpilerEntry = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(TranspileThrowables));
            else if (itemType == typeof(AmmoItemClass))
                transpilerEntry = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(TranspileAmmo));
            else
            {
                Plugin.Instance?.Log?.LogError($"[Belt Slots] no transpiler entry for itemType {itemType.Name}; skipping {label}");
                return;
            }
            try
            {
                _harmony.Patch(target, transpiler: new HarmonyMethod(transpilerEntry));
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log?.LogError($"[Belt Slots] failed to apply transpiler for {label}: {ex.Message}");
            }
        }

        public static IEnumerable<CodeInstruction> TranspileThrowables(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => InjectAfterReachableCall(instructions, generator, typeof(ThrowWeapItemClass));

        public static IEnumerable<CodeInstruction> TranspileAmmo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => InjectAfterReachableCall(instructions, generator, typeof(AmmoItemClass));

        // dups the (controller, list, predicate) args off the stack into
        // locals just before the GetReachableItemsOfTypeNonAlloc callvirt,
        // restores them for the original call, then re-uses the locals
        // after the call to invoke our helper. preserves the caller's
        // ORIGINAL predicate (caliber filter etc) - critical, downstream
        // ammo code does no re-filtering.
        private static IEnumerable<CodeInstruction> InjectAfterReachableCall(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator,
            Type itemType)
        {
            var open = AccessTools.Method(typeof(InventoryController), nameof(InventoryController.GetReachableItemsOfTypeNonAlloc));
            var closed = open.MakeGenericMethod(itemType);
            var helper = AccessTools.Method(typeof(BeltCallSiteHelper), nameof(BeltCallSiteHelper.AddBeltItems));
            var getTypeFromHandle = AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle));

            var predicateLocal = generator.DeclareLocal(typeof(Predicate<>).MakeGenericType(itemType));
            var listLocal = generator.DeclareLocal(typeof(IList<>).MakeGenericType(itemType));
            var controllerLocal = generator.DeclareLocal(typeof(InventoryController));

            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false, new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo m && m == closed));
            if (!matcher.IsValid)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler couldn't find call to GetReachableItemsOfTypeNonAlloc<{itemType.Name}>; injection skipped");
                return matcher.InstructionEnumeration();
            }

            // stack before callvirt is [controller, list, predicate]. pop
            // into locals (LIFO), then push back to restore for the call.
            var preCall = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Stloc, predicateLocal),
                new CodeInstruction(OpCodes.Stloc, listLocal),
                new CodeInstruction(OpCodes.Stloc, controllerLocal),
                new CodeInstruction(OpCodes.Ldloc, controllerLocal),
                new CodeInstruction(OpCodes.Ldloc, listLocal),
                new CodeInstruction(OpCodes.Ldloc, predicateLocal),
            };
            matcher.Insert(preCall);
            matcher.Advance(preCall.Count + 1);

            var postCall = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldloc, listLocal),
                new CodeInstruction(OpCodes.Ldloc, predicateLocal),
                new CodeInstruction(OpCodes.Ldloc, controllerLocal),
                new CodeInstruction(OpCodes.Ldtoken, itemType),
                new CodeInstruction(OpCodes.Call, getTypeFromHandle),
                new CodeInstruction(OpCodes.Call, helper),
            };
            matcher.Insert(postCall);

            return matcher.InstructionEnumeration();
        }
    }

    // type-erased helper invoked from the transpiled IL. predicate is
    // boxed as object and called via reflection so the emit doesn't have
    // to know about closed generics.
    public static class BeltCallSiteHelper
    {
        public static void AddBeltItems(System.Collections.IList list, object predicate, InventoryController controller, Type itemType)
        {
            if (list == null || controller?.Inventory?.Equipment == null || itemType == null) return;

            var beltSlot = BeltHolderHelper.GetBeltSlot(controller.Inventory.Equipment);
            var belt = beltSlot?.ContainedItem as CompoundItem;
            if (belt == null) return;

            MethodInfo invoke = predicate?.GetType().GetMethod("Invoke");

            foreach (var container in belt.Containers)
            {
                foreach (var item in container.Items)
                {
                    if (!itemType.IsInstanceOfType(item)) continue;
                    if (invoke != null)
                    {
                        try
                        {
                            if (!(bool)invoke.Invoke(predicate, new object[] { item })) continue;
                        }
                        catch { continue; }
                    }
                    list.Add(item);
                }
            }
        }
    }
}
