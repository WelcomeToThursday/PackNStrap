using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Core.Items;
using SPT.Reflection.Patching;

namespace PackNStrap.Patches
{
    internal class FindSlotForPickupPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryExtension), 
                nameof(InventoryExtension.FindSlotToPickUp),
                new[] { typeof(InventoryEquipment), typeof(Item) }
            );
        }

        [PatchPostfix]
        public static void Postfix(
            ref ItemAddress __result,
            InventoryEquipment equipment,
            Item item)
        {
            if (__result != null || !(item is CustomBeltItemClass))
            {
                return;
            }

            foreach (var slot in InventoryExtension.equipmentSlot_8) // ArmBand slots
            {
                var equipmentSlot = equipment.GetSlot(slot);
                if (equipmentSlot.Deleted || !equipmentSlot.CheckCompatibility(item))
                {
                    continue;
                }

                var address = equipmentSlot.FindLocationForItem(item, out _);
                if (address != null)
                {
                    __result = address;
                    return;
                }
            }
        }
    }
}