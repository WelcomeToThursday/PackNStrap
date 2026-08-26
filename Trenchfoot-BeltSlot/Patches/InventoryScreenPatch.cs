using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    internal class InventoryScreenPatch : ModulePatch // all patches must inherit ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // one way methods can be patched is by targeting both their class name and the name of the method itself
            // the example in this patch is the Jump() method in the Player class
            return AccessTools.Method(typeof(InventoryScreen), nameof(InventoryScreen.ShowWithTab));
        }

        [PatchPostfix]
        static void Postfix(InventoryScreen __instance)
        {
            if (Plugin.Instance == null) 
                return;

            Plugin.Instance.InventoryScreen = __instance;
            Plugin.Instance.InventoryScreenLoaded = true;
        }
    }
}