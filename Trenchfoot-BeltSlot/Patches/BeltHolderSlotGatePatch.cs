using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // patches Slot.method_2 (the private examined-gate inside RemoveItem/
    // AddItem). more targeted than BeltHolderExaminedPatch - this is the
    // exact site that throws GClass1576 for slot moves. covers mod_belt
    // itself + any grids inside the belt.
    public class BeltHolderSlotGatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Slot), "method_2", new[] { typeof(Item) });
        }

        [PatchPostfix]
        private static void Postfix(Slot __instance, Item item, ref bool __result)
        {
            if (__result || __instance == null) return;

            var slotParent = __instance.ParentItem;
            var matchedViaSlot = slotParent != null && BeltHolderHelper.BelongsToBeltHolder(slotParent);
            var matchedViaItem = item != null && BeltHolderHelper.BelongsToBeltHolder(item);
            if (!matchedViaSlot && !matchedViaItem) return;

            __result = true;
        }
    }
}
