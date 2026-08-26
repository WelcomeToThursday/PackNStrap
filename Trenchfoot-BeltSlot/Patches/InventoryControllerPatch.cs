using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace BeltSlot.Patches
{
    public class InventoryControllerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryController), nameof(InventoryController.ReplaceInventory));
        }
        [PatchPrefix]
        public static bool Prefix(InventoryController __instance, Inventory newInventory)
        {
            ItemController baseType = __instance;

            if (__instance.Inventory is not null)
            {
                baseType.RemoveItemEvent -= __instance.Inventory.UpdateTotalWeight;
                baseType.AddItemEvent -= __instance.Inventory.UpdateTotalWeight;
                baseType.RefreshItemEvent -= __instance.Inventory.UpdateTotalWeight;
                __instance.Inventory = null;
            }
            
            __instance.Inventory = newInventory;
            baseType.RemoveItemEvent += __instance.Inventory.UpdateTotalWeight;
            baseType.AddItemEvent += __instance.Inventory.UpdateTotalWeight;
            baseType.RefreshItemEvent += __instance.Inventory.UpdateTotalWeight;
            
            if (__instance.Inventory.Stash is not null &&
                __instance.Inventory.Stash.CurrentAddress is null)
                __instance.Inventory.Stash.CurrentAddress = baseType.CreateItemAddress();
            
            return false; // Skip the original method execution
        }
    }
}