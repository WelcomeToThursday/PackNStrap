using System.Reflection;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services;
using Range = SemanticVersioning.Range;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using WTTPackNStrap.Models;
using WTTPackNStrap.Patches;
using WTTPackNStrap.Services;
using Path = System.IO.Path;

namespace WTTPackNStrap;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.wtt.packnstrap";
    public override string Name { get; init; } = "WTT-PackNStrapServer";
    public override string Author { get; init; } = "GrooveypenguinX";
    public override List<string>? Contributors { get; init; } = null;
    public override SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version?.ToString(3));
    public override Range SptVersion { get; init; } = new("~4.0.2");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~2.0.0") }
    };
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = true;
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class WTTPackNStrap(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    DatabaseService databaseService,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    ConfigServer configServer,
    PocketsGridInjectorService pocketsGridInjector,
    GameStartHolderInjectPatch gameStartHolderPatch,
    BotGenerateBeltHolderPatch botBeltHolderPatch) : IOnLoad
{
    private Assembly _assembly;
    private Dictionary<MongoId, TemplateItem> _itemsDb;
    private Dictionary<MongoId, Trader> _traderDb;

    public async Task OnLoad()
    {
        _assembly = Assembly.GetExecutingAssembly();
        _itemsDb = databaseService.GetItems();
        _traderDb = databaseService.GetTraders();

        CreateCustomItemsAndTemplates();
        ConfigureCustomItemsToTraders();
        AddToInventorySlots();

        await wttCommon.CustomItemServiceExtended.CreateCustomItems(_assembly);
        wttCommon.CustomRigLayoutService.CreateRigLayouts(_assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(_assembly);

        // belts used to go in the vanilla ArmBand slot - WTTCommonLib happily
        // added every belt id there via each belt JSON's addtoInventorySlots.
        // for the holder-pattern refactor we want belts to ONLY live in the
        // BeltHolder.mod_belt slot, so strip them back out after CommonLib
        // is done. ArmBand still works as a normal armband slot.
        RemoveBeltsFromArmbandSlot();

        // inject the hidden 1x1 grid into every pockets template - must run
        // AFTER custom items so the holder tpl is in the db for the grid
        // filter's whitelist.
        pocketsGridInjector.Inject();

        // hook GameStart so every PMC profile has the holder seeded into
        // its pockets hidden grid before the client downloads inventory.
        // also hook BotGenerator.GenerateBot so every spawned bot gets an
        // empty holder in their pockets - lets the corpse-loot view show
        // a BELT slot (otherwise BeltSlotInjector finds no holder and skips).
        var harmony = new Harmony("com.wtt.packnstrap.server");
        gameStartHolderPatch.Apply(harmony);
        botBeltHolderPatch.Apply(harmony);

        ApplyConfigSettings();
    }

    private void ApplyConfigSettings()
    {

        var modPath = modHelper.GetAbsolutePathToModFolder(_assembly);
        var configPath = Path.Join(modPath, "config", "config.jsonc");

        if (!File.Exists(configPath))
        {
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var config = jsonUtil.Deserialize<PackNStrapConfig>(configJson);

        if (config is { loseArmbandOnDeath: false })
        {
            new IsItemKeptAfterDeathPatch().Enable();
            new HandleInsuredItemLostEventPatch().Enable();
            // discover belts by parent class rather than the hardcoded
            // BeltIds list - same self-healing rationale as
            // RemoveBeltsFromArmbandSlot. BeltIds had drifted to 22 of 46
            // entries, so half the belts kept their (default-enabled)
            // insurance even when the config asked us to disable it.
            const string beltParent = "6815465859b8c6ff13f94026";
            foreach (var (_, item) in _itemsDb)
            {
                if (item?.Parent == beltParent)
                {
                    item.Properties?.InsuranceDisabled = true;
                }
            }
        }
        else
        {
            var lostOnDeathConfig = configServer.GetConfig<LostOnDeathConfig>();
            lostOnDeathConfig.Equipment.ArmBand = true;
        }

        if (config is { addCasesToSecureContainers: true })
        {
            foreach (var caseId in ContainerIds.Items)
            {
                foreach (var item in _itemsDb.Values)
                {
                    if (item.Parent == "5448bf274bdc2dfc2f8b456a" || item.Parent == "68154651f849fb4e7d816738")
                    {
                        if (item.Id == "5c0a794586f77461c458f892")
                        {
                            continue;
                        }

                        var grids = item.Properties?.Grids?.ToList();
                        if (grids?.Count > 0)
                        {
                            var filters = grids[0].Properties?.Filters?.FirstOrDefault();
                            if (filters != null)
                            {
                                filters.Filter ??= [];
                                filters.Filter.Add((MongoId)caseId);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddToInventorySlots()
    {
        var defaultInventory = _itemsDb["55d7217a4bdc2d86028b456d"];

        foreach (var slot in defaultInventory.Properties.Slots)
        {
            if (slot.Name == "SecuredContainer")
            {
                slot.Properties?.Filters?.First().Filter?.Add("68154651f849fb4e7d816738");
            }
            // ArmBand no longer gets the belt parent - belts live in the
            // BeltHolder.mod_belt slot inside the hidden pockets grid now.
            // see RemoveBeltsFromArmbandSlot for the cleanup of per-belt ids
            // that CustomItemServiceExtended writes via addtoInventorySlots.
        }
    }

    // post-CreateCustomItems cleanup: WTTCommonLib obeys each belt JSON's
    // "addtoInventorySlots": ["ArmBand"] and adds every belt tpl to the
    // default inventory's ArmBand filter. we want the ArmBand slot back to
    // a vanilla armband slot, so strip all belt tpls AND the belt parent
    // from the filter here.
    //
    // discover belts by their Parent in _itemsDb rather than the hardcoded
    // BeltIds list - the list drifts whenever a new belt JSON is added
    // (it lagged real belts 22 vs 46 the last time we checked, leaving
    // half of them equippable as armbands). filtering by parent class
    // self-heals.
    private void RemoveBeltsFromArmbandSlot()
    {
        var defaultInventory = _itemsDb["55d7217a4bdc2d86028b456d"];
        var beltParent = new MongoId("6815465859b8c6ff13f94026");

        // collect every item in the db whose parent is the CustomBelt class.
        // ToString() compare so MongoId vs string parents both work.
        var beltTpls = new HashSet<MongoId>();
        foreach (var (id, item) in _itemsDb)
        {
            if (item?.Parent == beltParent.ToString())
            {
                beltTpls.Add(id);
            }
        }

        foreach (var slot in defaultInventory.Properties.Slots)
        {
            if (slot.Name != "ArmBand") continue;
            var filter = slot.Properties?.Filters?.FirstOrDefault()?.Filter;
            if (filter == null) continue;

            filter.RemoveWhere(id => id == beltParent || beltTpls.Contains(id));
        }
    }

    private void ConfigureCustomItemsToTraders()
    {
        // Belts to Ragman
        _traderDb["5ac3b934156ae10c4430e83c"].Base.ItemsBuy?.Category.Add("6815465859b8c6ff13f94026");
        // Cases and Secure Containers to Therapist
        _traderDb["54cb57776803fa99248b456e"].Base.ItemsBuy?.Category.Add("680fd1dae5044e670a092e16");
        _traderDb["54cb57776803fa99248b456e"].Base.ItemsBuy?.Category.Add("68154651f849fb4e7d816738");

    }

    private void CreateCustomItemsAndTemplates()
    {
        _itemsDb["680fce2ec7b9b222270f074c"] = new TemplateItem()
        {
            Id = "680fce2ec7b9b222270f074c",
            Name = "CustomContainerTemplate",
            Parent = "566162e44bdc2d3f298b4573",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["680fd1dae5044e670a092e16"] = new TemplateItem()
        {
            Id = "680fd1dae5044e670a092e16",
            Name = "CustomContainerItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["68154651f849fb4e7d816738"] = new TemplateItem()
        {
            Id = "68154651f849fb4e7d816738",
            Name = "CustomSecureContainerItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["6815465859b8c6ff13f94026"] = new TemplateItem()
        {
            Id = "6815465859b8c6ff13f94026",
            Name = "CustomBeltItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };

    }
}
