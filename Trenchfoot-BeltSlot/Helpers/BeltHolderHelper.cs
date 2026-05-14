using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;

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

        // SlotView stores its controllers/skills/etc in protected fields.
        // we read them via reflection on rebind because we need to call
        // Show() again with the new slot but the same surrounding context
        // (parent item context, controller, ui context, skills, insurance).
        // cached FieldInfos so we pay the reflection lookup once.
        private static readonly FieldInfo _itemControllerField = AccessTools.Field(typeof(SlotView), "ItemController");
        private static readonly FieldInfo _itemUiContextField = AccessTools.Field(typeof(SlotView), "ItemUiContext");
        private static readonly FieldInfo _skillsField = AccessTools.Field(typeof(SlotView), "Skills");
        private static readonly FieldInfo _insuranceField = AccessTools.Field(typeof(SlotView), "InsuranceCompany");

        // re-bind a SlotView from whatever slot it was originally Show()n
        // with (the equipment's ArmBand slot, in PackNStrap's existing
        // approach) to the BeltHolder's mod_belt slot owned by the same
        // equipment. returns the new bound slot, or null if no holder was
        // found (caller leaves the SlotView bound to armband as a fallback,
        // matching legacy behavior).
        //
        // figures out which equipment to traverse from the SlotView's
        // currently-bound slot - slot.ParentItem is the equipment item,
        // which lives in the corpse's inventory if this SlotView is in a
        // ComplexStashPanel for a looted bot, or the player's if it's in
        // their inventory. so a single helper handles every screen.
        public static Slot RebindToBeltHolder(SlotView slotView)
        {
            if (slotView == null || slotView.Slot == null) return null;

            // figure out the equipment item the bound slot belongs to.
            // CompoundItem exposes Slots; equipment is always a CompoundItem.
            var equipmentItem = slotView.Slot.ParentItem as CompoundItem;
            if (equipmentItem == null) return null;

            // find pockets on the equipment, then the belt slot within
            // pockets' hidden grid -> holder -> mod_belt.
            Slot pocketsSlot = null;
            if (equipmentItem.Slots != null)
            {
                foreach (var s in equipmentItem.Slots)
                {
                    if (s != null && s.ID == "Pockets") { pocketsSlot = s; break; }
                }
            }
            var beltSlot = GetBeltSlot(pocketsSlot?.ContainedItem as PocketsItemClass);
            if (beltSlot == null) return null;

            // already pointing at the belt slot (e.g. patch re-fired on the
            // same SlotView). nothing to do.
            if (ReferenceEquals(slotView.Slot, beltSlot)) return beltSlot;

            // snapshot the protected fields BEFORE Close() nulls them out.
            var parentContext = slotView.ParentItemContext;
            var itemController = _itemControllerField?.GetValue(slotView) as TraderControllerClass;
            var itemUiContext = _itemUiContextField?.GetValue(slotView) as ItemUiContext;
            var skills = _skillsField?.GetValue(slotView) as SkillManager;
            var insurance = _insuranceField?.GetValue(slotView) as InsuranceCompanyClass;

            // fall back to the singleton if reflection missed (shouldnt happen
            // but the cost of a null deref is way higher than a singleton ref).
            if (itemUiContext == null) itemUiContext = ItemUiContext.Instance;

            // Close unregisters from the old item owner & clears event subs;
            // Show re-runs the full setup against the new slot.
            try
            {
                slotView.Close();
                slotView.Show(beltSlot, parentContext, itemController, itemUiContext, skills, insurance, true);
            }
            catch (Exception ex)
            {
                // bail rather than crash UI - the SlotView may be in an
                // unexpected state if we got here at a weird time.
                Plugin.Instance?.Log?.LogError($"[Belt Slots] RebindToBeltHolder failed: {ex}");
                return null;
            }
            return beltSlot;
        }
    }
}
