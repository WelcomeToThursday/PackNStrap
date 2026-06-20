using System.Collections.Generic;
using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // appends belt grenades to the two extension methods on
    // InventoryController that vanilla uses to enumerate throwables:
    //   - GetThrowablePriorityGrenadesList: drives the hold-G + scroll
    //     grenade-cycle UI (GrenadeSelector).
    //   - GetThrowableGrenadesNonAlloc: feeds the grouped grenades dict
    //     vanilla uses for the quickbar/TopPriorityGrenade refresh.
    //
    // both walk only TacticalVest + Pockets natively, so belt grenades
    // would otherwise be invisible to scroll-select and the quickbar.
    // BeltCallSiteTranspilers already handles the press-G reload-style
    // path on Class1725.vmethod_1; these patches cover the parallel
    // UI/selection paths.

    public class BeltGrenadePriorityListPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var t = AccessTools.TypeByName("GClass3373");
            if (t == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] GClass3373 not found; belt grenades won't appear in grenade-cycle");
                return null;
            }
            return AccessTools.Method(t, "GetThrowablePriorityGrenadesList", new[] { typeof(InventoryController) });
        }

        [PatchPostfix]
        private static void Postfix(InventoryController inventoryController, ref List<ThrowWeapItemClass> __result)
        {
            if (inventoryController == null || __result == null) return;
            var beltSlot = BeltHolderHelper.GetBeltSlot(inventoryController.Inventory?.Equipment);
            var belt = beltSlot?.ContainedItem as CompoundItem;
            if (belt == null) return;

            foreach (var container in belt.Containers)
            {
                foreach (var item in container.Items)
                {
                    if (item is ThrowWeapItemClass throwable && inventoryController.Examined(throwable))
                    {
                        __result.Add(throwable);
                    }
                }
            }
        }
    }

    public class BeltGrenadeNonAllocPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            var t = AccessTools.TypeByName("GClass3373");
            if (t == null) return null;
            return AccessTools.Method(t, "GetThrowableGrenadesNonAlloc",
                new[] { typeof(InventoryController), typeof(List<CompoundItem>), typeof(SortedDictionary<string, List<ThrowWeapItemClass>>) });
        }

        [PatchPostfix]
        private static void Postfix(InventoryController inventoryController, SortedDictionary<string, List<ThrowWeapItemClass>> grenadesDictionary)
        {
            if (inventoryController == null || grenadesDictionary == null) return;
            var beltSlot = BeltHolderHelper.GetBeltSlot(inventoryController.Inventory?.Equipment);
            var belt = beltSlot?.ContainedItem as CompoundItem;
            if (belt == null) return;

            foreach (var container in belt.Containers)
            {
                foreach (var item in container.Items)
                {
                    if (item is not ThrowWeapItemClass throwable) continue;
                    if (!inventoryController.Examined(throwable)) continue;

                    string key = throwable.TemplateId;
                    if (grenadesDictionary.TryGetValue(key, out var list))
                        list.Add(throwable);
                    else
                        grenadesDictionary[key] = new List<ThrowWeapItemClass> { throwable };
                }
            }
        }
    }
}
