using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // postfix the player search controller's IsItemKnown to return true for
    // anything inside our belt holder hierarchy. side-effects:
    //   - ContainsUnknownItems on the pockets stops counting our hidden
    //     grid's holder/contents as unknowns, so the "needs search"
    //     indicator on a corpse still fires when there are real unknowns
    //     in the visible vanilla grids but not for our internal slot.
    //   - the per-item "reveal" animation on pockets-search skips ours,
    //     which keeps the search sound from being louder than it should be.
    public class BeltHolderIsItemKnownPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(PlayerSearchControllerClass), nameof(PlayerSearchControllerClass.IsItemKnown), new[] { typeof(Item), typeof(ItemAddress) });
        }

        [PatchPostfix]
        private static void Postfix(Item item, ref bool __result)
        {
            if (__result || item == null) return;
            if (BeltHolderHelper.BelongsToBeltHolder(item)) __result = true;
        }
    }
}
