using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // direct patch on Slot.method_2 (the private examined-gate inside
    // Slot.RemoveItem/AddItem). more targeted than the InventoryController
    // .Examined patch - this is the exact site that produces GClass1576
    // ("doesn't allow removing X when it's not examined") for slot moves.
    //
    // returns true unconditionally for slots whose ParentItem descends from
    // our belt holder. covers the mod_belt slot itself, and any grids
    // inside the belt (e.g. when the belt has its own pouch grids).
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
