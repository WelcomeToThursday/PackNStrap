using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Core.Items;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;

namespace PackNStrap.Patches;

internal class GetPrioritizedGridsForUnloadedObjectPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(InventoryEquipmentExtension), nameof(InventoryEquipmentExtension.GetPrioritizedGridsForUnloadedObject));
    }

    [PatchPrefix]
    public static bool PatchPrefix(ref InventoryEquipment equipment, bool backpackIncluded, ref IEnumerable<Grid> __result)
    {
        Slot tacticalVestSlot = equipment.GetSlot(EquipmentSlot.TacticalVest);
        Slot pocketsSlot = equipment.GetSlot(EquipmentSlot.Pockets);
        Slot backpackSlot = equipment.GetSlot(EquipmentSlot.Backpack);
        Slot armbandSlot = equipment.GetSlot(EquipmentSlot.ArmBand);

        Vest tacticalVestItem = tacticalVestSlot?.ContainedItem as Vest;
        Pockets pocketsItem = pocketsSlot?.ContainedItem as Pockets;
        Backpack backpackItem = backpackSlot?.ContainedItem as Backpack;
        CustomBeltItemClass armbandItem = armbandSlot?.ContainedItem as CustomBeltItemClass;

        Grid[] tacticalVestGrids = tacticalVestItem?.Grids ?? Array.Empty<Grid>();
        Grid[] pocketsGrids = pocketsItem?.Grids ?? Array.Empty<Grid>();
        Grid[] backpackGrids = backpackItem?.Grids ?? Array.Empty<Grid>();
        Grid[] armbandGrids = armbandItem?.Grids ?? Array.Empty<Grid>();

        List<CustomContainerItemClass> magDumpPouches = Common.GetMagDumpPouches(equipment, backpackIncluded);

        List<Grid> magDumpPouchGrids = magDumpPouches
            .SelectMany(pouch => pouch.Grids ?? Array.Empty<Grid>())
            .Where(Common.CanAcceptItems)
            .ToList();

        if (magDumpPouchGrids.Count > 0)
        {
#if DEBUG
            Console.WriteLine("Returning only MagDumpPouch grids that can accept items.");
#endif
            __result = magDumpPouchGrids;
            return false;
        }
#if DEBUG
        Console.WriteLine("No valid MagDumpPouch grids found.");
#endif
        __result = backpackIncluded
            ? tacticalVestGrids.Concat(pocketsGrids).Concat(backpackGrids).Concat(armbandGrids)
            : tacticalVestGrids.Concat(pocketsGrids).Concat(armbandGrids);

        return false; 
    }

}