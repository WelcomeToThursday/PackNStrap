using System;
using System.Collections.Generic;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;

namespace BeltSlot.Patches
{
    // makes magazines in the belt reachable for reload. vanilla walks
    // FastAccessSlots (pockets + rig) one level deep so the belt - which
    // lives inside a hidden pockets grid - is out of reach.
    //
    // patches the closed generic GetReachableItemsOfTypeNonAlloc<MagazineItemClass>.
    // ammo and throwables can't use the same trick: HarmonyX fires this
    // postfix on EVERY closed instantiation of the open method (verified -
    // pressing G with mags + grenades crashed with ArrayTypeMismatch),
    // so they go through caller-method transpilers instead (see
    // BeltCallSiteTranspilers.cs). the guard below makes this postfix
    // safe under that misfiring.
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

            // HarmonyX bug: this postfix fires on all closed instantiations
            // of the open method, not just <MagazineItemClass>. the
            // declared param type lies; the backing array can be e.g.
            // ThrowWeapItemClass[]. Add(mag) then throws
            // ArrayTypeMismatchException and crashes the caller.
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
