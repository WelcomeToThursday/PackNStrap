using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    // Create the submenu options (inventory screen)
    public class ItemUiContextPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.ShowPlayerEquipmentWindow));
        }

        [PatchPostfix]
        public static void Postfix(ItemUiContext __instance)
        {
            if(Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] PlayerEquipmentWindowPatch.Postfix called");
            }
            Plugin.Instance.IsScav = false;
            Plugin.Instance.SetDeployArmbandSlot();
        }
    }
}