using System.Linq;
using EFT.InventoryLogic;

namespace BeltSlot.Helpers
{
    // traversal helpers for the BeltHolder, which lives in a hidden 1x1
    // grid inside every pockets template (see server-side
    // PocketsGridInjectorService) and exposes a single "mod_belt" slot.
    // stateless: equipment owner (player vs corpse) comes from the caller.
    internal static class BeltHolderHelper
    {
        // mirrors WTTPackNStrap.Models.HolderIds.*. duplicated here so the
        // client doesn't have to reference the server assembly.
        public const string HolderTpl = "6815465859b8c6ff13f94100";
        public const string BeltSlotName = "mod_belt";
        public const string HiddenGridName = "packnstrap_belt_holder_grid";

        // pockets -> hidden grid -> holder -> mod_belt. null if any step
        // is missing; callers fall back to whatever they used to do.
        public static Slot GetBeltSlot(InventoryEquipment equipment)
        {
            if (equipment == null) return null;
            var pocketsSlot = equipment.GetSlot(EquipmentSlot.Pockets);
            var pockets = pocketsSlot?.ContainedItem as PocketsItemClass;
            return GetBeltSlot(pockets);
        }

        public static Slot GetBeltSlot(PocketsItemClass pockets)
        {
            if (pockets == null) return null;

            var grids = pockets.Grids;
            if (grids == null) return null;
            foreach (var grid in grids)
            {
                if (grid == null) continue;
                if (grid.ID != HiddenGridName) continue;

                foreach (var item in grid.Items)
                {
                    if (item == null) continue;
                    // StringTemplateId compares as plain string; raw
                    // TemplateId is a MongoID struct whose operator
                    // overload is brittle across version drift.
                    if (item.StringTemplateId != HolderTpl) continue;

                    var holder = item as CompoundItem;
                    if (holder == null) return null;
                    return holder.Slots?.FirstOrDefault(s => s != null && s.ID == BeltSlotName);
                }
            }
            return null;
        }

        // walks parent chain looking for the holder template. used by
        // the bypass patches to scope "unexamined/unsearched" overrides
        // to our hierarchy only.
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
