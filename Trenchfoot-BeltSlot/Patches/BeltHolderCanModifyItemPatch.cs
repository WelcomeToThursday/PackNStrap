using System.Reflection;
using BeltSlot.Helpers;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // bypass the "InventoryError/UnknownItemManipulation" gate (GClass1566 -
    // "Cannot interact with an unexamined item") for items in the belt
    // holder hierarchy. when looting a bot corpse, items inside the bot's
    // pockets get wrapped (GClass3367) and report state Unknown until the
    // pockets are searched. our holder + the belt it contains should be
    // freely interactable because the holder grid is conceptually a
    // backend extension of the pockets we always control.
    //
    // gate path:
    //   ItemView.UpdateRemoveError -> InteractionsHandlerClass.Remove
    //     -> InteractionsHandlerClass.CanModifyItem
    //     -> searchController.GetObserverItemState(item, from)
    //     -> if Unknown, returns GClass1566 (UnknownItemManipulation)
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
