using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using WTTPackNStrap.Services;

namespace WTTPackNStrap.Patches;

// inject the belt-holder at /client/game/start so it lands in the profile
// json before the client fetches its inventory. lazy injection on first
// inventory load is too late - the client has already cached.
//
// wired by hand from WTTPackNStrap.OnLoad because PatchAll cant inject
// the BeltHolderService dependency.
[Injectable(InjectionType.Singleton)]
public class GameStartHolderInjectPatch(
    BeltHolderService holderService,
    ISptLogger<GameStartHolderInjectPatch> logger)
{
    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(GameController), nameof(GameController.GameStart));
        if (target == null)
        {
            logger.Error("[PackNStrap] GameController.GameStart not found; belt-holder will not auto-inject");
            return;
        }

        var postfix = new HarmonyMethod(typeof(GameStartHolderInjectPatch), nameof(PostfixStatic));
        harmony.Patch(target, postfix: postfix);

        _instance = this;
    }

    private static GameStartHolderInjectPatch? _instance;

    public static void PostfixStatic(MongoId sessionId)
    {
        try
        {
            _instance?.holderService.EnsureForProfile(sessionId);
            _instance?.holderService.FlagExaminedForProfile(sessionId);
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[PackNStrap] belt-holder inject on GameStart failed for {sessionId}: {ex}");
        }
    }

    private BeltHolderService holderService { get; } = holderService;
    private ISptLogger<GameStartHolderInjectPatch> logger { get; } = logger;
}
