using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using WTTPackNStrap.Models;

namespace WTTPackNStrap.Services;

// injects the BeltHolder into the player's pockets hidden grid (lives
// inside pockets so it's part of every save's inventory tree without
// touching profile structure).
//
// idempotent: returns existing id and repairs stale parent/slot/location.
// also flags the holder tpl in Encyclopedia so examined-gates don't trip.
[Injectable(InjectionType.Singleton)]
public class BeltHolderService(
    ProfileHelper profileHelper,
    ISptLogger<BeltHolderService> logger)
{
    public static readonly MongoId HolderTpl = new(HolderIds.BeltHolderTpl);

    public void FlagExaminedForProfile(MongoId sessionId)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId);
        if (pmc?.Encyclopedia == null) return;

        // we only flag the holder itself; belts inherit ExaminedByDefault
        // from their JSON. minimal surface to keep this from interfering
        // with players' real examined progression.
        if (!pmc.Encyclopedia.ContainsKey(HolderTpl))
        {
            pmc.Encyclopedia[HolderTpl] = false;
            logger.Info($"[PackNStrap] flagged belt-holder tpl as examined for profile {sessionId}");
        }
    }

    public MongoId EnsureForProfile(MongoId sessionId)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId);
        if (pmc?.Inventory?.Items == null)
        {
            logger.Warning($"[PackNStrap] profile {sessionId} has no inventory; cannot inject belt holder");
            return MongoId.Empty();
        }

        var equipmentId = pmc.Inventory.Equipment?.ToString();
        if (string.IsNullOrEmpty(equipmentId))
        {
            logger.Error($"[PackNStrap] profile {sessionId} has no equipment id");
            return MongoId.Empty();
        }
        var pockets = pmc.Inventory.Items.FirstOrDefault(i =>
            i.ParentId == equipmentId && i.SlotId == "Pockets");
        if (pockets == null)
        {
            logger.Error($"[PackNStrap] profile {sessionId} has no Pockets item under equipment");
            return MongoId.Empty();
        }
        var pocketsIdStr = pockets.Id.ToString();

        var existing = pmc.Inventory.Items.FirstOrDefault(i => i.Template == HolderTpl);
        if (existing != null)
        {
            // repair holders that drifted to a different parent/slot
            // (e.g. from an older version of this mod or a manual edit).
            var repaired = false;
            if (existing.ParentId != pocketsIdStr) { existing.ParentId = pocketsIdStr; repaired = true; }
            if (existing.SlotId != HolderIds.HiddenGridName)
            {
                existing.SlotId = HolderIds.HiddenGridName;
                repaired = true;
            }
            if (existing.Location == null)
            {
                existing.Location = new ItemLocation { X = 0, Y = 0, R = ItemRotation.Horizontal, IsSearched = true };
                repaired = true;
            }
            if (repaired) logger.Info($"[PackNStrap] repaired stale belt-holder {existing.Id} for profile {sessionId}");
            return existing.Id;
        }

        var holder = new Item
        {
            Id = new MongoId(),
            Template = HolderTpl,
            ParentId = pocketsIdStr,
            SlotId = HolderIds.HiddenGridName,
            Location = new ItemLocation
            {
                X = 0,
                Y = 0,
                R = ItemRotation.Horizontal,
                IsSearched = true,
            },
        };
        pmc.Inventory.Items.Add(holder);
        logger.Info($"[PackNStrap] injected belt-holder {holder.Id} into pockets of profile {sessionId}");
        return holder.Id;
    }
}
