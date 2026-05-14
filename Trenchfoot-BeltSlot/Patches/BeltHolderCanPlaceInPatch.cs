using System.Reflection;
using BeltSlot.Helpers;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // bypass "Cannot change the contents of an unsearched container"
    // (GClass1565 / TransferToUnknownLocation) when placing items INTO our
    // belt holder hierarchy from a corpse - e.g. shoving a magazine into a
    // looted belt's pocket grid.
    //
    // gate is at InteractionsHandlerClass.smethod_24, the destination-side
    // counterpart of CanModifyItem.
    public class BeltHolderCanPlaceInPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InteractionsHandlerClass), "smethod_24");
        }

        [PatchPostfix]
        private static void Postfix(ItemAddress to, ref Error error, ref bool __result)
        {
            if (__result || error == null) return;
            if (!BeltHolderHelper.AddressContainsHolder(to)) return;

            error = null;
            __result = true;
        }
    }
}
