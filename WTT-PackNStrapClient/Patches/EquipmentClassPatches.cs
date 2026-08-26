using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PackNStrap.Patches
{
    internal class ContainerSlotsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "ContainerSlots");
        }

        [PatchPostfix]
        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            List<Slot> newResult = __result.ToList();
            var armbandSlot = __instance.GetSlot(EquipmentSlot.ArmBand);
            newResult.Add(armbandSlot);

            __result = newResult;
        }
    }

    internal class PaymentSlotsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "PaymentSlots");
        }

        [PatchPostfix]
        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            List<Slot> newResult = __result.ToList();
            newResult.Add(__instance.GetSlot(EquipmentSlot.ArmBand));
            __result = newResult;
        }
    }


    internal class GrenadeThrowingSlotsPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "GrenadeThrowingSlots");
        }

        [PatchPostfix]

        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            List<Slot> newResult = __result.ToList();
            newResult.Add(__instance.GetSlot(EquipmentSlot.ArmBand));
            __result = newResult;
        }
    }

}