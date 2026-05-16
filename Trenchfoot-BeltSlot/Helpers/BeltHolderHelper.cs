using System.Linq;
using EFT.InventoryLogic;

namespace BeltSlot.Helpers
{
    // shared traversal helpers for the BeltHolder. the holder lives in a
    // hidden 1x1 grid inside every pockets template (see server-side
    // PocketsGridInjectorService). it has a single Slot named "mod_belt"
    // that the BELT SlotView in the containers panel binds to.
    //
    // these helpers are deliberately stateless - the equipment owner
    // (player vs corpse) is whoever owns the InventoryEquipment we get
    // passed. that way the same code reads the right belt slot whether
    // we're rendering player inventory, an open corpse, an insurance
    // build, or the deploy preview.
    internal static class BeltHolderHelper
    {
        // matches WTTPackNStrap.Models.HolderIds.* . repeated here as
        // string literals so the client doesnt have to reference the
        // server assembly.
        public const string HolderTpl = "6815465859b8c6ff13f94100";
        public const string BeltSlotName = "mod_belt";
        public const string HiddenGridName = "packnstrap_belt_holder_grid";

        // walk pockets -> hidden grid -> holder -> mod_belt. returns null if
        // anything along the path is missing - caller falls back to whatever
        // it used to do (usually leaves the armband slot alone).
        public static Slot GetBeltSlot(InventoryEquipment equipment)
        {
            if (equipment == null) return null;
            var pocketsSlot = equipment.GetSlot(EquipmentSlot.Pockets);
            var pockets = pocketsSlot?.ContainedItem as PocketsItemClass;
            return GetBeltSlot(pockets);
        }

        // same traversal starting from the pockets item itself. used when
        // we already have the pockets in hand (e.g. via a parent walk from
        // a hovered item).
        public static Slot GetBeltSlot(PocketsItemClass pockets)
        {
            if (pockets == null) return null;

            // Grids is the array of StackSlot-equivalents inside pockets;
            // each has an .ID that matches the grid name we injected.
            var grids = pockets.Grids;
            if (grids == null) return null;
            foreach (var grid in grids)
            {
                if (grid == null) continue;
                if (grid.ID != HiddenGridName) continue;

                foreach (var item in grid.Items)
                {
                    if (item == null) continue;
                    // use StringTemplateId so the compare is a normal string
                    // compare; Item.TemplateId is a MongoID (struct) and the
                    // operator overload is brittle across version drift.
                    if (item.StringTemplateId != HolderTpl) continue;

                    // CompoundItem exposes Slots; find mod_belt by id.
                    var holder = item as CompoundItem;
                    if (holder == null) return null;
                    return holder.Slots?.FirstOrDefault(s => s != null && s.ID == BeltSlotName);
                }
            }
            return null;
        }

        // walks an item or its address chain looking for the holder tpl.
        // used by the bypass patches that need to know "does this item
        // belong to our system?" (so we can clear unexamined/unsearched
        // gates only for our hierarchy).
        public static bool BelongsToBeltHolder(Item item)
        {
            var current = item;
            for (int i = 0; i < 8 && current != null; i++)
            {
                if (current.StringTemplateId == HolderTpl) return true;
                var addr = current.CurrentAddress;
                var parent = addr?.Container?.ParentItem;
                if (parent == null || parent == current) break;
                current = parent;
            }
            return false;
        }

        public static bool AddressContainsHolder(ItemAddress addr)
        {
            var container = addr?.Container;
            for (int i = 0; i < 8 && container != null; i++)
            {
                var parent = container.ParentItem;
                if (parent == null) break;
                if (parent.StringTemplateId == HolderTpl) return true;
                container = parent.CurrentAddress?.Container;
            }
            return false;
        }

    }
}
