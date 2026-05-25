using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // alt-click "quick equip" goes through InventoryEquipment.FindSlotToPickUp
    // (extension method in GClass3373). vanilla's cascade has hardcoded
    // item-type -> equipment-slot mappings and doesn't know about our
    // mod_belt slot - belts fall through to the Weapon/Container branch,
    // get an empty slot list, and the call returns null -> "No free slot
    // for that item" notification.
    //
    // postfix: when vanilla returned null, ask our mod_belt slot directly
    // if it accepts the item. the slot's own filter (defined in the
    // holder template) decides eligibility, so we don't need a type list
    // here.
    public class BeltFindSlotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // FindSlotToPickUp(InventoryEquipment, Item) - extension method
            // living on GClass3373.
            var t = AccessTools.TypeByName("GClass3373");
            if (t == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] GClass3373 not found; quick-equip-into-belt disabled");
                return null;
            }
            return AccessTools.Method(t, "FindSlotToPickUp", new[] { typeof(InventoryEquipment), typeof(Item) });
        }

        [PatchPostfix]
        private static void Postfix(InventoryEquipment equipment, Item item, ref ItemAddress __result)
        {
            if (__result != null) return;
            if (equipment == null || item == null) return;

            var beltSlot = BeltHolderHelper.GetBeltSlot(equipment);
            if (beltSlot == null) return;
            if (beltSlot.ContainedItem != null) return; // already occupied

            var address = beltSlot.FindLocationForItem(item, out _);
            if (address != null) __result = address;
        }
    }
}
