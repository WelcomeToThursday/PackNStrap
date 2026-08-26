using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PackNStrap.Patches;

internal class GetThrowablePriorityGrenadesListPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(InventoryExtension),
            nameof(InventoryExtension.GetThrowablePriorityGrenadesList)
        );
    }

    [PatchPostfix]
    public static void Postfix(
        InventoryController inventoryController,
        ref List<ThrowWeap> __result)
    {
        if (inventoryController?.Inventory?.Equipment == null || __result == null)
            return;

        if (inventoryController.Inventory.Equipment
                .GetSlot(EquipmentSlot.ArmBand)
                .ContainedItem is not CompoundItem armBand)
        {
            return;
        }

        var containers = new List<CompoundItem> { armBand };

        var armBandGrenades = containers
            .GetTopLevelItems()
            .OfType<ThrowWeap>()
            .Where(inventoryController.Examined);

        __result.AddRange(armBandGrenades);
        __result.Sort(InventoryExtension.CG_Class2411.CG_Class2411.method_3);
    }
}