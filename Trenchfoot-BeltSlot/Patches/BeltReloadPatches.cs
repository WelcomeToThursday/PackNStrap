using System;
using System.Collections.Generic;
using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;

namespace BeltSlot.Patches
{
    // makes MAGAZINES in the BELT reachable for reload. vanilla walks
    // FastAccessSlots = [Pockets, TacticalVest] one level deep, so mags
    // inside a belt (itself inside our hidden pockets grid) are
    // unreachable.
    //
    // HarmonyX quirk hard-confirmed: when multiple patches target different
    // closed generics of the same open method (GetReachableItemsOfTypeNonAlloc<T>),
    // only the LAST-registered patch's hook effectively fires. tried:
    //   - separate ModulePatches (each with own Harmony)  -> last wins
    //   - shared Harmony, three separate postfixes        -> last wins
    //   - shared Harmony, ONE shared postfix              -> last wins
    // collision is on the TARGET methodinfo (closed generic), not the
    // postfix. so this file only patches MagazineItemClass - the single
    // most-impactful reload type.
    //
    // AmmoItemClass (shell-by-shell shotgun/bolt-action) and ThrowWeapItemClass
    // (grenades) would need caller-method patches via transpiler at the
    // specific call sites in Class1730 / Class1725 to dodge the HarmonyX
    // closed-generic collision. not done yet.
    public static class BeltReloadPatches
    {
        private static readonly Harmony _harmony = new Harmony("com.trenchfoot.beltslot.reload");

        public static void Apply()
        {
            var open = AccessTools.Method(typeof(InventoryController), nameof(InventoryController.GetReachableItemsOfTypeNonAlloc));
            if (open == null)
            {
                Plugin.Instance?.Log?.LogError("[Belt Slots] InventoryController.GetReachableItemsOfTypeNonAlloc not found; reload-from-belt disabled");
                return;
            }

            var closed = open.MakeGenericMethod(typeof(MagazineItemClass));
            var postfix = AccessTools.Method(typeof(BeltReloadPatches), nameof(PostfixMagazine));
            _harmony.Patch(closed, postfix: new HarmonyMethod(postfix));
        }

        public static void PostfixMagazine(IList<MagazineItemClass> preAllocatedList, Predicate<MagazineItemClass> predicate, InventoryController __instance)
        {
            if (__instance?.Inventory?.Equipment == null) return;
            if (preAllocatedList == null) return;

            // HarmonyX closed-generic bug strikes again, more dangerously this
            // time: even though we ONLY patched <MagazineItemClass>, this
            // postfix actually fires on ALL closed generics of the open
            // method (incl. <ThrowWeapItemClass> and <AmmoItemClass>). the C#
            // signature still says IList<MagazineItemClass> so the compiler
            // doesn't complain, but the runtime backing array is e.g.
            // ThrowWeapItemClass[]. Add(mag) then throws
            // ArrayTypeMismatchException which kills the caller (G key for
            // grenades, R for shell-by-shell reload) BEFORE its own logic
            // runs. observed in-raid: pressing G did nothing, shotgun
            // shell reload broke, NRE spam in logs.
            //
            // guard: inspect the actual List<T>'s generic argument and bail
            // if it's not really MagazineItemClass. cheap reflection on a
            // type object - happens once per call, no per-item cost.
            var listType = preAllocatedList.GetType();
            if (!listType.IsGenericType || listType.GetGenericArguments()[0] != typeof(MagazineItemClass)) return;

            var beltSlot = BeltHolderHelper.GetBeltSlot(__instance.Inventory.Equipment);
            var belt = beltSlot?.ContainedItem as CompoundItem;
            if (belt == null) return;

            foreach (var container in belt.Containers)
            {
                foreach (var item in container.Items)
                {
                    if (item is MagazineItemClass mag && (predicate == null || predicate(mag)))
                    {
                        preAllocatedList.Add(mag);
                    }
                }
            }
        }
    }
}
