using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Range = SemanticVersioning.Range;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using WTTPackNStrap.Models;
using WTTPackNStrap.Patches;
using Path = System.IO.Path;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Helpers.Server;

namespace WTTPackNStrap;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.wtt.packnstrap";
    public string Name { get; init; } = "WTT-PackNStrapServer";
    public string Author { get; init; } = "GrooveypenguinX";
    public List<string>? Contributors { get; init; } = null;
    public SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version?.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.1");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.2") }
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class WTTPackNStrap(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    TemplateTable templateTable,
    LostOnDeathConfig lostOnDeathConfig,
    TradersTable tradersTable) : IOnLoad
{
    private Assembly _assembly;
    private Dictionary<MongoId, TemplateItem> _itemsDb;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _assembly = Assembly.GetExecutingAssembly();
        _itemsDb = templateTable.Items;

        await wttCommon.CustomItemParentService.CreateCustomParents(_assembly);
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(_assembly);
        wttCommon.CustomRigLayoutService.CreateRigLayouts(_assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(_assembly);

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
            foreach (var caseId in BeltIds.Items)
            {
                if (_itemsDb.TryGetValue(caseId, out var item))
                {
                    item.Properties?.InsuranceDisabled = true;
                }
            }
        }
        else
        {
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

}

