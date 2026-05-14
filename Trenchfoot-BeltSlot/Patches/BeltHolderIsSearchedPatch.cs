using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // GridItemView.NewGridItemView decides IsSearched based on whether the
    // container is a grid marker AND the search controller "knows" the
    // item. for items inside the bot's pockets - including our hidden
    // grid's holder/belt - the player search controller's IsItemKnown
    // returns false until the player explicitly searches the pockets. that
    // makes the belt/holder un-clickable on corpse views.
    //
    // bypass: postfix and force IsSearched=true when the item descends
    // from our belt holder. only affects items in our hierarchy; everything
    // else in the corpse stays gated as vanilla.
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
