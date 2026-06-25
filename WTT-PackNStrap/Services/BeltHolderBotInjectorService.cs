using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using WTTPackNStrap.Models;

namespace WTTPackNStrap.Services;

// injects an empty BeltHolder into every generated bot's pockets so the
// corpse loot view shows a BELT slot. holder is always empty on bots -
// players who want belts on corpses loot them from elsewhere; we just
// provide the slot.
//
// also clears any stale state SPT's loot generator might leave behind:
//   - items dropped into the hidden grid directly
//   - items dropped into an existing holder's mod_belt slot (and any
//     descendant items, e.g. an Assaulter belt with mags inside)
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

        var pocketsIdStr = pockets.Id.ToString();

        // clear anything SPT's loot generator dropped into the hidden grid.
        // recursive: also gets any descendant items inside (e.g. a stray
        // belt that happens to be sitting on top of the slot).
        ClearHiddenGrid(bot.Inventory.Items, pocketsIdStr);

        // find existing holders. ClearHiddenGrid above would have removed
        // holders that were direct children of the hidden grid, but stale
        // holders might exist elsewhere (older save state etc).
        var existingHolder = bot.Inventory.Items.FirstOrDefault(i => i.Template == HolderTpl);
        if (existingHolder != null)
        {
            // empty the holder of any contents (mod_belt slot + descendants).
            // observed in practice: bots ended up with a random Assaulter
            // belt + magazine inside a pre-existing holder, presumably
            // placed by loot generation before our injection ran.
            ClearDescendants(bot.Inventory.Items, existingHolder.Id.ToString());
            return;
        }

        bot.Inventory.Items.Add(new Item
        {
            Id = new MongoId(),
            Template = HolderTpl,
            ParentId = pocketsIdStr,
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

    // removes every item whose ParentId chain leads to parentId. parent
    // itself is kept.
    private static void ClearDescendants(List<Item> items, string parentId)
    {
        var directChildren = items.Where(i => i.ParentId == parentId).Select(i => i.Id.ToString()).ToList();
        if (directChildren.Count == 0) return;

        var toRemove = new HashSet<string>();
        foreach (var childId in directChildren) CollectDescendants(items, childId, toRemove);
        items.RemoveAll(i => toRemove.Contains(i.Id.ToString()));
    }

    private static void CollectDescendants(List<Item> items, string parentId, HashSet<string> sink)
    {
        sink.Add(parentId);
        foreach (var child in items.Where(i => i.ParentId == parentId).ToList())
            CollectDescendants(items, child.Id.ToString(), sink);
    }
}
