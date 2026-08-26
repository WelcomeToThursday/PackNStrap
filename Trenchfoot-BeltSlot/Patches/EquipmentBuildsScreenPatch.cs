using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    public class EquipmentBuildsScreenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentBuildsScreen), nameof(EquipmentBuildsScreen.SelectBuildHandler));
        }
        [PatchPostfix]
        static void Postfix(EquipmentBuildsScreen __instance)
        {
            Plugin.Instance.SetBuildsArmbandSlot();
        }
    }
}
