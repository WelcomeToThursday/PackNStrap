using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // force Examined=true for anything in our belt holder hierarchy. when
    // dragging a belt off a corpse, Slot.method_2 looks up the BOT's
    // controller which has Examined=false for the belt tpl - vanilla
    // would block with "doesn't allow removing X when it's not examined".
    // our items conceptually belong to the player (the holder is injected
    // into every bot's pockets too) so we always pass the gate.
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
