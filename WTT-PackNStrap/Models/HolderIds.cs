namespace WTTPackNStrap.Models;

// ids for the hidden belt-holder pattern (mirrors LegArmor's HolderTpl approach).
// the holder lives in a hidden 1x1 pocket grid we inject into every pockets
// template; its mod_belt slot is what the BELT SlotView in containers panel
// actually binds to. that decouples the belt from the armband so players
// can wear both at once.
//
// kept in WTTPackNStrap.Models so server and client (via project ref) share
// the same string. client patches re-declare it as a private const to avoid
// pulling the whole server assembly into BepInEx.
public static class HolderIds
{
    // the holder item template id (clones a PACA-ish armor and overrides
    // every slot/property to be inert except the single mod_belt slot).
    public const string BeltHolderTpl = "6815465859b8c6ff13f94100";

    // the mod_belt slot's id inside the holder template - referenced from
    // the client when re-binding the SlotView.
    public const string BeltSlotName = "mod_belt";

    // the hidden grid we inject into every pockets template. one cell, only
    // accepts the holder. server marks IsSearched=true so the player never
    // has to search anything to see what's inside.
    public const string HiddenGridName = "packnstrap_belt_holder_grid";
}
