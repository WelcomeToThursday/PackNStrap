using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace WTTPackNStrap.Patches;

public class IsItemKeptAfterDeathPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(InRaidHelper).GetMethod(
            "IsItemKeptAfterDeath",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
    }

    [PatchPostfix]
    public static void Postfix(PmcData pmcData, Item itemToCheck, ref bool __result)
    {
        if (!__result && IsItemInArmBand(pmcData, itemToCheck))
        {
            __result = true;
        }
    }

    private static bool IsItemInArmBand(PmcData pmcData, Item item)
    {
        var inventoryItems = pmcData.Inventory?.Items ?? [];
    
        // Find the ArmBand container
        var armBandItem = inventoryItems.FirstOrDefault(i => i.SlotId == "ArmBand");
        if (armBandItem == null)
        {
            return false;
        }

        // Check if item is the ArmBand itself or a child of it
        return item.Id == armBandItem.Id || 
               inventoryItems.GetItemWithChildren(armBandItem.Id).Any(i => i.Id == item.Id);
    }
}