using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;
using System.Reflection;

namespace PackNStrap.Patches
{
    public class IsAtReachablePlacePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryController),
                nameof(InventoryController.IsAtReachablePlace)
            );
        }

        [PatchPostfix]
        private static void Postfix(InventoryController __instance, ref bool __result, Item item)
        {
            __result = Common.IsItemInReachableLocation(item, __instance);
        }
    }
}