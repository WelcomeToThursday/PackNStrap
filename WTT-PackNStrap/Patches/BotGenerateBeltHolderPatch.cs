using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using WTTPackNStrap.Services;

namespace WTTPackNStrap.Patches;

// postfix BotGenerator.GenerateBot - fires for both regular wave bots
// (PrepareAndGenerateBot -> GenerateBot) AND player scavs (PlayerScavGenerator
// calls GenerateBot directly, skipping PrepareAndGenerateBot). hooking the
// inner method catches all paths. mirrors LegArmor's BotGenerateHolderInjectPatch.
//
// wired by hand from WTTPackNStrap.OnLoad because PatchAll cant inject the
// service.
[Injectable(InjectionType.Singleton)]
public class BotGenerateBeltHolderPatch(
    BeltHolderBotInjectorService injector,
    ISptLogger<BotGenerateBeltHolderPatch> logger)
{
    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(BotGenerator), "GenerateBot");
        if (target == null)
        {
            logger.Error("[PackNStrap] BotGenerator.GenerateBot not found; bot corpses will not show belt slot");
            return;
        }

        var postfix = new HarmonyMethod(typeof(BotGenerateBeltHolderPatch), nameof(PostfixStatic));
        harmony.Patch(target, postfix: postfix);

        _instance = this;
    }

    private static BotGenerateBeltHolderPatch? _instance;

    public static void PostfixStatic(BotBase __result)
    {
        try
        {
            if (_instance == null || __result == null) return;
            _instance.injector.InjectIntoBot(__result);
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[PackNStrap] bot belt-holder inject failed: {ex}");
        }
    }

    private BeltHolderBotInjectorService injector { get; } = injector;
    private ISptLogger<BotGenerateBeltHolderPatch> logger { get; } = logger;
}
