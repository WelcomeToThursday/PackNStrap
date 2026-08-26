using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    // Create the submenu options (inventory screen)
    public class InventoryEquipmentPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryEquipment), nameof(InventoryEquipment.GetSlot));
        }

        [PatchPrefix]
        public static bool Prefix(InventoryEquipment __instance)
        {
            if(Plugin.Instance != null)
            {
                Plugin.Instance.InventoryEquipment = __instance;
            }
            return true;
        }
    }
}