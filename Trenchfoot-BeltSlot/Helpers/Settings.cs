using BepInEx.Configuration;

namespace BeltSlot.Helpers
{
    internal enum BeltSlotLocationOption
    {
        AbovePockets,
        BelowPockets
    }

    // hardcoded layout values picked once, then locked in. only
    // BeltSlotLocation stays as a BepInEx binding because position
    // preference is genuinely user-facing.
    //
    // two value sets so the panel looks right both standalone and with
    // Manimal-LegArmor installed (LegArmor's body-silhouette shift
    // cascades into the containers panel layout).
    internal class Settings
    {
        public static ConfigEntry<BeltSlotLocationOption> BeltSlotLocation { get; private set; }

        public static bool InjectBeltSpacer;
        public static float BeltSpacerHeight;
        public static float BeltSlotOffsetY;
        public static float PocketsSlotOffsetY;

        private struct Defaults
        {
            public bool InjectSpacer;
            public float SpacerHeight;
            public float SlotOffsetY;
            public float PocketsOffsetY;
        }

        private static readonly Defaults Solo = new Defaults
        {
            InjectSpacer = false,
            SpacerHeight = 0f,
            SlotOffsetY = 0f,
            PocketsOffsetY = 0f,
        };

        // Belt owns the pockets offset in this case; LegArmor's
        // WithBeltSlot.PocketsSlot is zeroed to avoid stacking.
        private static readonly Defaults WithLegArmor = new Defaults
        {
            InjectSpacer = true,
            SpacerHeight = 0f,
            SlotOffsetY = 16f,
            PocketsOffsetY = 60f,
        };

        public static void Init(ConfigFile Config, bool isLegArmorInstalled = false)
        {
            BeltSlotLocation = Config.Bind(
                "A. Belt Location",
                "Belt slot location",
                BeltSlotLocationOption.AbovePockets,
                "Place the belt slot above or below pockets in the containers panel. Requires restart."
            );

            var d = isLegArmorInstalled ? WithLegArmor : Solo;
            InjectBeltSpacer = d.InjectSpacer;
            BeltSpacerHeight = d.SpacerHeight;
            BeltSlotOffsetY = d.SlotOffsetY;
            PocketsSlotOffsetY = d.PocketsOffsetY;
        }
    }
}
