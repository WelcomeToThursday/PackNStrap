namespace WTTPackNStrap.Models;

// ids for the hidden belt-holder pattern (mirrors LegArmor's HolderTpl
// approach). the holder lives in a hidden 1x1 pocket grid we inject into
// every pockets template; its mod_belt slot is what the BELT SlotView
// binds to. decouples belts from the armband so players can wear both.
//
// client patches re-declare these as private consts to avoid pulling the
// server assembly into BepInEx.
public static class HolderIds
{
    // PACA-derived holder clone with everything inert except the mod_belt slot.
    public const string BeltHolderTpl = "6815465859b8c6ff13f94100";

    public const string BeltSlotName = "mod_belt";

    // 1x1 grid injected into every pockets template. only accepts the
    // holder; server marks IsSearched=true so the player never has to
    // search anything to see what's inside.
    public const string HiddenGridName = "packnstrap_belt_holder_grid";
}
