using BepInEx.Configuration;

namespace BeltSlot.Helpers
{
    internal enum BeltSlotLocationOption
    {
        AbovePockets,
        BelowPockets
    }

    // hardcoded layout values for the belt slot. previously these were
    // BepInEx ConfigEntry<T>s exposed in F12 ConfigurationManager; we
    // tuned via the sliders, locked in the numbers, then converted to
    // plain static fields so users don't accidentally drift the layout
    // and we don't bother writing a cfg file just for read-once values.
    //
    // BeltSlotLocation (AbovePockets/BelowPockets) is the one exception -
    // it's a genuine user preference, not a layout tuning value, so it
    // stays as a BepInEx binding so users can toggle position via F12.
    //
    // detection (Trenchfoot-BeltSlot + Manimal-LegArmor installed together)
    // still picks between two value sets: Solo for standalone and
    // WithLegArmor when LegArmor is also present (LegArmor's body-silhouette
    // shift cascades into the containers panel layout).
    internal class Settings
    {
        // user-facing: F12-tunable position preference. kept as a binding
        // because the right choice depends on the player's layout taste,
        // not on what makes the panel look right structurally.
        public static ConfigEntry<BeltSlotLocationOption> BeltSlotLocation { get; private set; }

        // hardcoded layout values - set by Init() at startup. patches read
        // these directly. these were ConfigEntry<T>.Value reads before;
        // now plain values with no .Value indirection.
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

        // standalone Belt - vanilla containers panel positions the injected
        // belt slot fine on its own, so spacer off and all offsets zero.
        private static readonly Defaults Solo = new Defaults
        {
            InjectSpacer = false,
            SpacerHeight = 0f,
            SlotOffsetY = 0f,
            PocketsOffsetY = 0f,
        };

        // Belt + LegArmor. LegArmor pushes the containers panel down a bit
        // via its body-silhouette shift, so the belt needs a small Y offset
        // and pockets needs +60 (Belt mod owns the pockets offset in this
        // case, see LegArmorConfig.WithBeltSlot.PocketsSlot which was
        // zeroed out to hand ownership over to Belt).
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
