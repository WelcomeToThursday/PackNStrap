using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using WTTPackNStrap.Models;

namespace WTTPackNStrap.Services;

// injects an empty BeltHolder into every generated bot's pockets. without
// this, bots have the hidden pockets grid (injected by PocketsGridInjectorService)
// but no holder inside it - the corpse loot view's BeltSlotInjector then
// short-circuits because GetBeltSlot returns null, so the player never sees
// a BELT slot on bot corpses.
//
// simpler than LegArmor's equivalent (LegArmorBotInjectorService) because we
// never auto-equip an item in the slot - the holder is always empty on bots.
// players who want belts on corpses can loot them from elsewhere; we just
// give them the slot to drop loot into.
//
// also clears any random loot SPT might have placed in the hidden grid before
// adding the holder (same reason LegArmor does it - the loot generator
// doesnt know our grid is internal).
[Injectable(InjectionType.Singleton)]
public class BeltHolderBotInjectorService(
    ISptLogger<BeltHolderBotInjectorService> logger)
{
    private static readonly MongoId HolderTpl = new(HolderIds.BeltHolderTpl);

    public void InjectIntoBot(BotBase bot)
    {
        if (bot?.Inventory?.Items == null) return;

        var equipmentId = bot.Inventory.Equipment?.ToString();
        if (string.IsNullOrEmpty(equipmentId)) return;

        var pockets = bot.Inventory.Items.FirstOrDefault(i =>
            i.ParentId == equipmentId && i.SlotId == "Pockets");
        if (pockets == null) return;

        // guard against double-fires (re-applied hook, etc).
        if (bot.Inventory.Items.Any(i => i.Template == HolderTpl)) return;

        // clear anything SPT's loot generator dropped into the hidden grid.
        // recursive to take child items too (e.g. ammo in a magazine in a
        // randomly-placed weapon, however unlikely).
        ClearHiddenGrid(bot.Inventory.Items, pockets.Id.ToString());

        bot.Inventory.Items.Add(new Item
        {
            Id = new MongoId(),
            Template = HolderTpl,
            ParentId = pockets.Id.ToString(),
            SlotId = HolderIds.HiddenGridName,
            Location = new ItemLocation { X = 0, Y = 0, R = ItemRotation.Horizontal, IsSearched = true },
        });
    }

    private static void ClearHiddenGrid(List<Item> items, string pocketsId)
    {
        var stale = items
            .Where(i => i.ParentId == pocketsId && i.SlotId == HolderIds.HiddenGridName)
            .ToList();
        if (stale.Count == 0) return;

        var toRemove = new HashSet<string>();
        foreach (var item in stale) CollectDescendants(items, item.Id.ToString(), toRemove);
        items.RemoveAll(i => toRemove.Contains(i.Id.ToString()));
    }

    private static void CollectDescendants(List<Item> items, string parentId, HashSet<string> sink)
    {
        sink.Add(parentId);
        foreach (var child in items.Where(i => i.ParentId == parentId).ToList())
            CollectDescendants(items, child.Id.ToString(), sink);
    }
}
