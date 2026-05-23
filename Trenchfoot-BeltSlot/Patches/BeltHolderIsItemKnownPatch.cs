using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // PlayerSearchController.IsItemKnown returns true for items in our
    // hierarchy so:
    //   - pockets' ContainsUnknownItems ignores our hidden grid (the
    //     "needs search" indicator on corpses still fires for real
    //     unknowns elsewhere, just not for our internal slot)
    //   - per-item reveal animation on pockets-search skips ours
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
