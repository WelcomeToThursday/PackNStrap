using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // force IsSearched=true on the GridItemView for belt-holder items.
    // GridItemView.NewGridItemView would otherwise leave them un-clickable
    // on corpse views (player search controller's IsItemKnown returns
    // false until the user explicitly searches the pockets).
    public class BeltHolderIsSearchedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GridItemView), nameof(GridItemView.NewGridItemView));
        }

        [PatchPostfix]
        private static void Postfix(GridItemView __instance, Item item)
        {
            if (__instance == null || item == null) return;
            if (__instance.IsSearched) return;
            if (!BeltHolderHelper.BelongsToBeltHolder(item)) return;

            __instance.IsSearched = true;
        }
    }
}
