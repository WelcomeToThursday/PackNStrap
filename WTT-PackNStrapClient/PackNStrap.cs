#if !UNITY_EDITOR
using BepInEx;
using System;
using System.IO;
using PackNStrap.Patches;
using EFT.UI;
using EFT;
using SPT.Reflection.Utils;
using Comfort.Common;
using PackNStrap.Helpers;
using WTTClientCommonLib.Services;


namespace PackNStrap
{
    [BepInDependency("com.cj.useFromAnywhere", "1.3.2")]
    [BepInPlugin("com.wtt.packnstrap", "WTT-PackNStrap", "2.1.0")]

    internal class PackNStrap : BaseUnityPlugin
    {
        public static PackNStrap Instance;
        private static GameWorld _gameWorld;
        public static Player Player;
        public static string PlayerNickname;
        private static GameUI _gameUI;
        private static Profile _playerProfile;
        public static ISession BackEndSession;

        public static readonly string PluginPath = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins");

        internal void Awake()
        {
            Instance = this;
            CustomTemplateIdToObjectService.AddNewTemplateIdToObjectMapping(NewTemplateIdToObjectIdClass.CustomMappings);
            new GetPrioritizedGridsForUnloadedObjectPatch().Enable();
            new MergeContainerWithChildrenPatch().Enable();
            new UnloadWeaponPatch().Enable();
            new FindSlotForPickupPatch().Enable();
            new RegisterCustomItemTypesPatch().Enable();
        }

        internal void Update()
        {
            if (Singleton<GameWorld>.Instantiated && (_gameWorld == null || _gameUI == null || Player == null))
            {
                _gameWorld = Singleton<GameWorld>.Instance;
                _gameUI = MonoBehaviourSingleton<GameUI>.Instance;
                Player = Singleton<GameWorld>.Instance.MainPlayer;
                _playerProfile = PatchConstants.BackEndSession.Profile;
                PlayerNickname = _playerProfile.Nickname;
                BackEndSession = PatchConstants.BackEndSession;
            }
        }
    }
}
#endif