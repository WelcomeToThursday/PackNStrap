using BepInEx.Configuration;

namespace BeltSlot.Helpers
{
    internal enum BeltSlotLocationOption
    {
        AbovePockets,
        BelowPockets
    }

    internal class Settings
    {
        private const string BeltLocationSettings = "A. Belt Location";

        public static ConfigEntry<BeltSlotLocationOption> BeltSlotLocation { get; set; }

        public static void Init(ConfigFile Config)
        {
            BeltSlotLocation = Config.Bind(
                BeltLocationSettings,
                "Belt slot location",
                BeltSlotLocationOption.AbovePockets,
                "Adjust the belt slot location, requires restart."
            );
        }
    }
}
