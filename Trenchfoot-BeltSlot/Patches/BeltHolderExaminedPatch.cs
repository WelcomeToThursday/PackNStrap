using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // bypass the "is this item examined?" gate for any item that descends
    // from our belt holder.
    //
    // when the user drags a belt out of the holder's mod_belt slot on a
    // corpse, Slot.method_2 looks up the BOT's InventoryController and
    // calls .Examined(item). bots' controllers have Examined=false and the
    // bot's profile doesnt know the belt tpls, so the check fails with
    // GClass1576 ("doesn't allow removing X when it's not examined").
    //
    // since our items conceptually belong to the player (we inject the
    // holder into every bot's pockets too), force Examined=true for
    // anything in our hierarchy.
    public class BeltHolderExaminedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryController), nameof(InventoryController.Examined), new[] { typeof(Item) });
        }

        [PatchPostfix]
        private static void Postfix(Item item, ref bool __result)
        {
            if (item == null || __result) return;
            if (!BeltHolderHelper.BelongsToBeltHolder(item)) return;

            __result = true;
        }
    }
}
