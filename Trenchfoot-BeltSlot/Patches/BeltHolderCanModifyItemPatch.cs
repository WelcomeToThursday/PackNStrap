using System.Reflection;
using BeltSlot.Helpers;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // bypass the UnknownItemManipulation gate for items in the belt
    // holder hierarchy. on a looted corpse, items inside the bot's
    // pockets get wrapped and report Unknown until pockets are searched -
    // but our holder/belt should be freely interactable because the
    // hidden grid is conceptually a backend extension of the pockets.
    public class BeltHolderCanModifyItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InteractionsHandlerClass), nameof(InteractionsHandlerClass.CanModifyItem));
        }

        [PatchPostfix]
        private static void Postfix(Item item, ItemAddress from, ref Error error, ref bool __result)
        {
            if (__result || error == null) return;

            var ownedByItem = item != null && BeltHolderHelper.BelongsToBeltHolder(item);
            var ownedByAddr = from != null && BeltHolderHelper.AddressContainsHolder(from);
            if (!ownedByItem && !ownedByAddr) return;

            error = null;
            __result = true;
        }
    }
}
