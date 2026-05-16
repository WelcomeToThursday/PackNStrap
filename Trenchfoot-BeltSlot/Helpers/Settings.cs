using BepInEx.Configuration;

namespace BeltSlot.Helpers
{
    internal enum BeltSlotLocationOption
    {
        AbovePockets,
        BelowPockets
    }

    // BepInEx config for the belt slot. lives under one class so we can bind
    // everything from Plugin.Awake in one call. layout entries mirror
    // LegArmor's pattern: a Spacer Height (positive value = pushes the slot
    // further down, used to clear room between the belt and an adjacent
    // slot) and a Slot Offset Y (fine-tune the slot's anchoredPosition Y
    // via a per-frame watchdog so VLG re-runs don't erase it).
    //
    // both layout entries only affect the CORPSE LOOT view. the player's own
    // inventory uses the simpler AbovePockets/BelowPockets SetSiblingIndex
    // placement and doesn't need fine-tuning. ranges + defaults copied from
    // LegArmor's equivalents so the F12 sliders feel familiar.
    //
    // when LegArmor is also installed, its body-silhouette shift cascades
    // into the containers panel and the belt's natural Y changes - so
    // Init() takes an isLegArmorInstalled flag and picks a different
    // default set. user can still override either set via F12.
    internal class Settings
    {
        private const string BeltLocationSettings = "A. Belt Location";
        private const string CorpseLayoutSettings = "B. Corpse View Layout";

        public static ConfigEntry<BeltSlotLocationOption> BeltSlotLocation { get; set; }
        public static ConfigEntry<bool> InjectBeltSpacer { get; set; }
        public static ConfigEntry<float> BeltSpacerHeight { get; set; }
        public static ConfigEntry<float> BeltSlotOffsetY { get; set; }
        public static ConfigEntry<float> PocketsSlotOffsetY { get; set; }

        // tuned defaults. standalone Belt doesn't need any layout tweaks -
        // vanilla containers panel positions the injected belt slot fine
        // on its own. so Solo turns the spacer off entirely and zeros the
        // offsets. WithLegArmor is the only case that needs non-zero
        // values, since LegArmor's body-silhouette shift cascades into the
        // containers panel layout.
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

        // defaults when LegArmor is also installed. tuned via F12 with
        // both mods active. PocketsOffsetY = 60 matches the value LegArmor
        // used to apply via its own WithBeltSlot.PocketsSlot - we've moved
        // pockets ownership to Belt here, and LegArmor's WithBeltSlot
        // PocketsSlot was zeroed out to avoid stacking. spacer left on so
        // BeltSpacerHeight remains tunable; height defaults to 0 so it
        // contributes nothing until raised.
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
                BeltLocationSettings,
                "Belt slot location",
                BeltSlotLocationOption.AbovePockets,
                "Adjust the belt slot location, requires restart."
            );

            var d = isLegArmorInstalled ? WithLegArmor : Solo;

            InjectBeltSpacer = Config.Bind(
                CorpseLayoutSettings,
                "Inject Belt Spacer",
                d.InjectSpacer,
                "When enabled, a spacer GameObject is injected before the Belt Slot in the corpse loot layout flow (height controlled by 'Belt Spacer Height'). Disable to skip spacer injection entirely and test what the layout looks like with no spacer at all - existing spacer is destroyed on the next panel Show."
            );

            BeltSpacerHeight = Config.Bind(
                CorpseLayoutSettings,
                "Belt Spacer Height",
                d.SpacerHeight,
                new ConfigDescription(
                    "Height of the spacer placed before the Belt Slot in the corpse loot layout flow. Positive = pushes the belt (and everything below it) further down. Only applies when 'Inject Belt Spacer' is enabled.",
                    new AcceptableValueRange<float>(0f, 600f))
            );

            BeltSlotOffsetY = Config.Bind(
                CorpseLayoutSettings,
                "Belt Slot Offset Y",
                d.SlotOffsetY,
                new ConfigDescription(
                    "Fine-tune offset applied to the Belt Slot itself on top of the spacer. Positive = push slot down further, negative = pull slot up. VLG re-applies natural Y each frame so we re-apply this delta after.",
                    new AcceptableValueRange<float>(-200f, 200f))
            );

            PocketsSlotOffsetY = Config.Bind(
                CorpseLayoutSettings,
                "Pockets Slot Offset Y",
                d.PocketsOffsetY,
                new ConfigDescription(
                    "Fine-tune offset applied to the Pockets Slot in the corpse loot view (on top of VLG's natural Y). Positive = down, negative = up. Belt mod owns this offset across both standalone and LegArmor-installed scenarios.",
                    new AcceptableValueRange<float>(-200f, 200f))
            );
        }
    }
}
