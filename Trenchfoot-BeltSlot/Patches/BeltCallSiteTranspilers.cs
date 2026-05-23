using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;

namespace BeltSlot.Patches
{
    // call-site transpilers that extend AMMO and THROWABLE reachability
    // into the belt. why transpilers and not Harmony patches on
    // GetReachableItemsOfTypeNonAlloc<T>: HarmonyX has a confirmed bug
    // where patching multiple closed generics of the same open method
    // makes only the last-registered patch's hook fire AND even a single
    // closed-generic patch fires on ALL other closed instantiations
    // (verified in raid: ArrayTypeMismatchException). magazines win the
    // single guarded postfix via BeltReloadPatches.cs; ammo and throwables
    // have to be hooked at their specific (non-generic) caller methods
    // so they get their own patch target and don't compete.
    //
    // each transpiler does the same thing: find the call to
    // GetReachableItemsOfTypeNonAlloc inside the target method, then
    // immediately AFTER that call, emit IL that re-loads the same args
    // (controller, list, predicate) and calls our non-generic helper
    // (BeltCallSiteHelper.AddBeltItems) which walks the belt and appends
    // matching items to the same list the original call just populated.
    //
    // arg capture strategy: instead of re-deriving each arg from method
    // fields (per-call-site plumbing, fragile, and the @class closure
    // predicate isn't cleanly reloadable for ammo), we DUP the call's
    // own args off the stack just before the callvirt and stash them in
    // transpiler-declared locals, then re-push them after. one shared
    // transpiler works for every call site. crucially this preserves the
    // ORIGINAL predicate (e.g. caliber filter for ammo) instead of
    // passing null and trusting downstream filtering that doesn't exist.
    //
    // covered targets:
    //   - Class1725.vmethod_1                    -> throwables (G-key throw)
    //   - Class1730.method_14 (barrel reload)    -> shotgun shells in barrel
    //   - Class1730.method_15 (cylinder reload)  -> revolver rounds
    //   - Class1730.method_16 (standard ammo)    -> internal-mag/bolt-action rounds
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
            // bind itemType to the transpiler via a closure delegate. Harmony
            // accepts a HarmonyMethod with a MethodInfo, but it doesn't have
            // a clean way to pass context. workaround: stash itemType in a
            // per-target static field via a tiny dispatcher class. simpler:
            // each closed-generic gets its own transpiler entry point.
            // since we have only 2 unique types (ThrowWeap, Ammo), just two
            // entry points + one shared engine works.
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

        // entry points - each just calls the shared engine with its bound
        // itemType. Harmony auto-supplies ILGenerator when you declare the
        // param on the transpiler.
        public static IEnumerable<CodeInstruction> TranspileThrowables(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => InjectAfterReachableCall(instructions, generator, typeof(ThrowWeapItemClass));

        public static IEnumerable<CodeInstruction> TranspileAmmo(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            => InjectAfterReachableCall(instructions, generator, typeof(AmmoItemClass));

        // shared transpiler engine. locates the closed-generic call, dups
        // each of its 3 args (controller, list, predicate) into newly-
        // declared locals just before the call, then re-loads them after
        // and calls our helper. uses the ORIGINAL predicate so caliber
        // filtering (etc) is preserved - critical for ammo reload.
        private static IEnumerable<CodeInstruction> InjectAfterReachableCall(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator,
            Type itemType)
        {
            var open = AccessTools.Method(typeof(InventoryController), nameof(InventoryController.GetReachableItemsOfTypeNonAlloc));
            var closed = open.MakeGenericMethod(itemType);
            var helper = AccessTools.Method(typeof(BeltCallSiteHelper), nameof(BeltCallSiteHelper.AddBeltItems));
            var getTypeFromHandle = AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle));

            // local types match the closed generic so the IL stays
            // verification-friendly.
            var predicateType = typeof(Predicate<>).MakeGenericType(itemType);
            var listType = typeof(IList<>).MakeGenericType(itemType);
            var controllerType = typeof(InventoryController);

            var predicateLocal = generator.DeclareLocal(predicateType);
            var listLocal = generator.DeclareLocal(listType);
            var controllerLocal = generator.DeclareLocal(controllerType);

            var matcher = new CodeMatcher(instructions);
            matcher.MatchForward(false, new CodeMatch(i => i.opcode == OpCodes.Callvirt && i.operand is MethodInfo m && m == closed));
            if (!matcher.IsValid)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler couldn't find call to GetReachableItemsOfTypeNonAlloc<{itemType.Name}>; injection skipped");
                return matcher.InstructionEnumeration();
            }

            // stack just before callvirt: [controller, list, predicate]
            // we want to keep all 3 in locals so we can rebuild the call's
            // arg stack AND re-use them in our helper call right after.
            //
            // insert at the callvirt position: pop all 3 into locals (reverse
            // order since stack is LIFO), then push them back to restore the
            // stack for the original callvirt, then the original callvirt
            // runs unchanged.
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

            // advance past our 6 inserts AND the original callvirt to land
            // right AFTER the call.
            matcher.Advance(preCall.Count + 1);

            // after the call: stack is empty (call returns void). push our
            // helper's args and invoke it. AddBeltItems(IList, object,
            // InventoryController, Type) - IList parameter accepts the
            // List<T> (List<T> implements both IList<T> and IList), and
            // object accepts the typed Predicate<T>.
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

    // non-generic runtime helper - takes the list as IList (works for any
    // List<T> since List<T> implements IList) and uses the supplied Type
    // to filter belt items via IsInstanceOfType. predicate is type-erased
    // (object) and invoked via reflection so the IL emit doesn't have to
    // know about closed generics. now that the transpiler captures the
    // ORIGINAL predicate from the call site (rather than passing null),
    // caliber/etc filters apply to belt items same as pocket items.
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
