using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // appends mod_belt to InventoryEquipment.GrenadeThrowingSlots so the
    // FastAccessGrenadeItemView add/remove hooks recognise belt grenades.
    // without this, throwing a belt grenade doesn't refresh the G-key
    // quickbar - the FastAccessGrenadeItemView's list_1 still references
    // the gone grenade and "press G" tries to throw a stale item.
    //
    // returns a NEW list rather than mutating the cached one (vanilla
    // caches via PaymentSlots_1 field which is shared with the payment-
    // slots use); small per-call alloc but keeps the cache clean.
    public class BeltGrenadeThrowingSlotsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), nameof(InventoryEquipment.GrenadeThrowingSlots));
        }

        [PatchPostfix]
        private static void Postfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            if (__instance == null || __result == null) return;
            var beltSlot = BeltHolderHelper.GetBeltSlot(__instance);
            if (beltSlot == null) return;
            if (__result.Contains(beltSlot)) return;

            __result = new List<Slot>(__result) { beltSlot };
        }
    }
}
