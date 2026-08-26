using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.InRaid;

namespace WTTPackNStrap.Patches;

public class HandleInsuredItemLostEventPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(LocationLifecycleService).GetMethod(
            "HandleInsuredItemLostEvent",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
    }

    [PatchPrefix]
    public static void Prefix(
        MongoId sessionId,
        PmcData preRaidPmcProfile,
        EndLocalRaidRequestData request,
        string locationName
    )
    {
        if (request.LostInsuredItems is null || !request.LostInsuredItems.Any())
        {
            return;
        }

        var inventoryItems = preRaidPmcProfile.Inventory?.Items ?? [];
        
        var armBandItem = inventoryItems.FirstOrDefault(i => i.SlotId == "ArmBand");
        if (armBandItem == null)
        {
            return;
        }

        var armBandDescendants = GetAllDescendants(armBandItem.Id, inventoryItems).ToList();
        
        request.LostInsuredItems = request.LostInsuredItems
            .Where(item => !armBandDescendants.Contains(item.Id))
            .ToList();
    }
    
    private static IEnumerable<string> GetAllDescendants(string parentId, IEnumerable<Item> allItems)
    {
        var items = allItems.ToList();
        var children = items.Where(i => i.ParentId == parentId);

        foreach (var child in children)
        {
            yield return child.Id;
            foreach (var descendant in GetAllDescendants(child.Id, items))
            {
                yield return descendant;
            }
        }
    }
}
