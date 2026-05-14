using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using WTTPackNStrap.Models;

namespace WTTPackNStrap.Services;

// adds a hidden grid to every pockets template - holds one BeltHolder per
// profile. ContainedGridsView still allocates a GridView for our grid so
// the client patch HideHolderGridPatch toggles its GameObject off.
//
// also flips HideEntrails so CloneVisibleItem traverses pockets when the
// client builds the visual-only equipment clone for the Time-Has-Come
// player model view. otherwise the holder gets stripped and the (admittedly
// invisible) belt prefab never has a parent to mount under.
[Injectable(InjectionType.Singleton)]
public class PocketsGridInjectorService(
    DatabaseService databaseService,
    ISptLogger<PocketsGridInjectorService> logger)
{
    // matches client HideHolderGridPatch + BeltHolderService.
    public const string HiddenGridName = HolderIds.HiddenGridName;
    private const string PocketsParentClass = "557596e64bdc2dc2118b4571";
    private static readonly MongoId HolderTpl = new(HolderIds.BeltHolderTpl);

    public void Inject()
    {
        var items = databaseService.GetTables().Templates?.Items;
        if (items == null)
        {
            logger.Error("[PackNStrap] item templates unavailable; cannot inject pockets grid");
            return;
        }

        var injected = 0;
        foreach (var (id, tpl) in items)
        {
            if (tpl.Parent != PocketsParentClass) continue;
            if (tpl.Properties == null) continue;

            // safe in singleplayer; flag only matters for MP pocket privacy.
            tpl.Properties.HideEntrails = false;

            var grids = tpl.Properties.Grids?.ToList() ?? new List<Grid>();
            if (grids.Any(g => g.Name == HiddenGridName)) continue;

            grids.Add(new Grid
            {
                // deterministic id so successive server runs reuse it.
                Id = $"{id.ToString().Substring(0, 16)}be17c01d",
                Name = HiddenGridName,
                Parent = id.ToString(),
                Properties = new GridProperties
                {
                    CellsH = 1,
                    CellsV = 1,
                    MinCount = 0,
                    MaxCount = 0,
                    MaxWeight = 0,
                    IsSortingTable = false,
                    Filters = new[]
                    {
                        new GridFilter
                        {
                            Filter = new HashSet<MongoId> { HolderTpl },
                            ExcludedFilter = new HashSet<MongoId>(),
                            Locked = false,
                        },
                    },
                },
                Prototype = "55d329c24bdc2d892f8b4567",
            });
            tpl.Properties.Grids = grids;
            injected++;
        }
        logger.Info($"[PackNStrap] hidden belt-holder grid injected into {injected} pockets template(s)");
    }
}
