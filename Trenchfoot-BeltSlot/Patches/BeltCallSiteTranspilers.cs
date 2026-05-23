using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;

namespace BeltSlot.Patches
{
    // makes magazines, ammo, and throwables in the belt reachable for
    // reload/throw. patches the specific (non-generic) caller methods
    // rather than InventoryController.GetReachableItemsOfTypeNonAlloc<T>
    // because HarmonyX corrupts dispatch across closed instantiations of
    // a generic method.
    //
    // targets:
    //   Class1725.vmethod_1                - throwables (G key)
    //   Class1730.method_14/15/16           - barrel / cylinder / standard ammo
    //   Class1730.method_17                 - magazine reload
    //
    // arg-capture strategy: walk the IL backward from the GetReachable
    // callvirt to find the three arg-pushes (controller field load, list
    // field load, predicate newobj). after each push, insert a pass-through
    // "stash" helper that takes the value off the stack, saves it to a
    // static field, and returns it unchanged - stack-neutral, side-effect
    // captures the arg. then after the callvirt, invoke our helper which
    // reads the static stash and walks the belt.
    //
    // earlier attempt used transpiler-declared locals; first invocation
    // worked but subsequent ones produced null on ldloc (HarmonyX/Mono
    // quirk we couldn't pin down). statics survive across invocations
    // by definition and EFT game logic is single-threaded so no contention.
    public static class BeltCallSiteTranspilers
    {
        private static readonly Harmony _harmony = new Harmony("com.trenchfoot.beltslot.callsites");

        [ThreadStatic] private static System.Collections.IList _stashedList;
        [ThreadStatic] private static object _stashedPredicate;
        [ThreadStatic] private static InventoryController _stashedController;

        public static InventoryController StashController(InventoryController c) { _stashedController = c; return c; }
        public static System.Collections.IList StashList(System.Collections.IList l) { _stashedList = l; return l; }
        public static object StashPredicate(object p) { _stashedPredicate = p; return p; }

        public static void RunHelperFromStash(Type itemType)
        {
            var list = _stashedList;
            var pred = _stashedPredicate;
            var ctrl = _stashedController;
            _stashedList = null;
            _stashedPredicate = null;
            _stashedController = null;
            BeltCallSiteHelper.AddBeltItems(list, pred, ctrl, itemType);
        }

        public static void Apply()
        {
            ApplyTranspiler(AccessTools.TypeByName("Class1725"), "vmethod_1", typeof(ThrowWeapItemClass), "Class1725.vmethod_1 (throwables)");

            var class1730 = AccessTools.TypeByName("Class1730");
            ApplyTranspiler(class1730, "method_14", typeof(AmmoItemClass), "Class1730.method_14 (barrel ammo)");
            ApplyTranspiler(class1730, "method_15", typeof(AmmoItemClass), "Class1730.method_15 (cylinder ammo)");
            ApplyTranspiler(class1730, "method_16", typeof(AmmoItemClass), "Class1730.method_16 (standard ammo)");
            ApplyTranspiler(class1730, "method_17", typeof(MagazineItemClass), "Class1730.method_17 (magazines)");
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
            MethodInfo transpilerEntry;
            if (itemType == typeof(ThrowWeapItemClass))
                transpilerEntry = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(TranspileThrowables));
            else if (itemType == typeof(AmmoItemClass))
                transpilerEntry = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(TranspileAmmo));
            else if (itemType == typeof(MagazineItemClass))
                transpilerEntry = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(TranspileMagazines));
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

        public static IEnumerable<CodeInstruction> TranspileThrowables(IEnumerable<CodeInstruction> instructions)
            => InjectAfterReachableCall(instructions, typeof(ThrowWeapItemClass));

        public static IEnumerable<CodeInstruction> TranspileAmmo(IEnumerable<CodeInstruction> instructions)
            => InjectAfterReachableCall(instructions, typeof(AmmoItemClass));

        public static IEnumerable<CodeInstruction> TranspileMagazines(IEnumerable<CodeInstruction> instructions)
            => InjectAfterReachableCall(instructions, typeof(MagazineItemClass));

        private static IEnumerable<CodeInstruction> InjectAfterReachableCall(
            IEnumerable<CodeInstruction> instructions,
            Type itemType)
        {
            var open = AccessTools.Method(typeof(InventoryController), nameof(InventoryController.GetReachableItemsOfTypeNonAlloc));
            var closed = open.MakeGenericMethod(itemType);
            var runHelper = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(RunHelperFromStash));
            var stashCtrl = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(StashController));
            var stashList = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(StashList));
            var stashPred = AccessTools.Method(typeof(BeltCallSiteTranspilers), nameof(StashPredicate));
            var getTypeFromHandle = AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle));
            var predicateCtor = typeof(Predicate<>).MakeGenericType(itemType).GetConstructor(new[] { typeof(object), typeof(IntPtr) });

            var code = new List<CodeInstruction>(instructions);

            // find the closed-generic callvirt.
            int callIdx = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Callvirt
                    && code[i].operand is MethodInfo m
                    && m == closed)
                {
                    callIdx = i;
                    break;
                }
            }
            if (callIdx < 0)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler couldn't find call to GetReachableItemsOfTypeNonAlloc<{itemType.Name}>; injection skipped");
                return code;
            }

            // walk backward from the callvirt to find each arg's last push.
            // for all our target methods the IL pattern is:
            //   <load this.controller>      ; ldarg.0 + ldfld   OR  ldarg.0 + ldfld + callvirt-getter chain
            //   ldsfld <static List_X>      ; list push
            //   <load closure>              ; ldarg.0/ldsfld/ldloc
            //   ldftn <method>
            //   newobj Predicate<T>::.ctor  ; predicate push
            //   callvirt GetReachable<T>    ; the call we're matching
            int predicateNewobjIdx = FindPredicateNewobj(code, callIdx, predicateCtor);
            int listLoadIdx = (predicateNewobjIdx > 0) ? FindListLoad(code, predicateNewobjIdx) : -1;
            int ctrlLoadIdx = (listLoadIdx > 0) ? FindControllerLoad(code, listLoadIdx) : -1;

            if (predicateNewobjIdx < 0 || listLoadIdx < 0 || ctrlLoadIdx < 0)
            {
                Plugin.Instance?.Log?.LogWarning($"[Belt Slots] transpiler couldn't identify arg pushes for {itemType.Name} (pred={predicateNewobjIdx} list={listLoadIdx} ctrl={ctrlLoadIdx}); injection skipped");
                return code;
            }

            // insert post-call helper invocation FIRST (so indices below
            // the callvirt aren't affected by our pre-call inserts).
            code.InsertRange(callIdx + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldtoken, itemType),
                new CodeInstruction(OpCodes.Call, getTypeFromHandle),
                new CodeInstruction(OpCodes.Call, runHelper),
            });

            // insert stash calls AFTER each arg-push (reverse order so
            // earlier indices don't shift while we work).
            code.Insert(predicateNewobjIdx + 1, new CodeInstruction(OpCodes.Call, stashPred));
            code.Insert(listLoadIdx + 1, new CodeInstruction(OpCodes.Call, stashList));
            code.Insert(ctrlLoadIdx + 1, new CodeInstruction(OpCodes.Call, stashCtrl));

            return code;
        }

        private static int FindPredicateNewobj(List<CodeInstruction> code, int beforeIdx, ConstructorInfo predicateCtor)
        {
            for (int i = beforeIdx - 1; i >= 0; i--)
            {
                if (code[i].opcode == OpCodes.Newobj
                    && code[i].operand is ConstructorInfo c
                    && c == predicateCtor)
                    return i;
            }
            return -1;
        }

        private static int FindListLoad(List<CodeInstruction> code, int beforeIdx)
        {
            // single ldsfld that pushes an IList. true for all our targets
            // (vanilla uses Class1730.List_0 / List_1 / this.List_1).
            for (int i = beforeIdx - 1; i >= 0; i--)
            {
                if (code[i].opcode == OpCodes.Ldsfld
                    && code[i].operand is FieldInfo sf
                    && typeof(System.Collections.IList).IsAssignableFrom(sf.FieldType))
                    return i;
                // Class1725.vmethod_1 uses this.List_1 (instance field).
                if (code[i].opcode == OpCodes.Ldfld
                    && code[i].operand is FieldInfo inf
                    && typeof(System.Collections.IList).IsAssignableFrom(inf.FieldType))
                    return i;
            }
            return -1;
        }

        private static int FindControllerLoad(List<CodeInstruction> code, int beforeIdx)
        {
            // last instruction whose result is an InventoryController.
            // either ldfld of an InventoryController field (Class1730 case)
            // or callvirt of a Player.InventoryController getter
            // (Class1725.vmethod_1 case, where the controller comes from
            // this.Player_0.InventoryController).
            for (int i = beforeIdx - 1; i >= 0; i--)
            {
                if (code[i].opcode == OpCodes.Ldfld
                    && code[i].operand is FieldInfo f
                    && typeof(InventoryController).IsAssignableFrom(f.FieldType))
                    return i;
                if (code[i].opcode == OpCodes.Callvirt
                    && code[i].operand is MethodInfo m
                    && typeof(InventoryController).IsAssignableFrom(m.ReturnType))
                    return i;
            }
            return -1;
        }
    }

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
