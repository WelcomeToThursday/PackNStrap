using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace BeltSlot.Helpers
{
    // sits on the injected SlotView and keeps its LayoutElement in sync
    // with the slot's filled/empty state. without this the cap set at
    // ContainersPanel.Show time goes stale: if the player drops a belt
    // into an empty slot while the panel is open, the cap stays at the
    // empty height and the SearchableSlotView's grids overflow downward
    // into the pockets row.
    public class BeltSlotLayoutCapper : MonoBehaviour
    {
        // matches BeltSlotInjector.EmptySlotPreferredHeight - duplicated
        // here so the helper has no inverse dep on the patches namespace.
        private const float EmptyHeight = 100f;

        private Slot _slot;
        private LayoutElement _le;
        private System.Action<Item> _handler;

        public void Bind(Slot slot)
        {
            if (ReferenceEquals(_slot, slot)) { Apply(slot?.ContainedItem != null); return; }
            Unbind();
            _slot = slot;
            if (_slot == null) return;
            _handler = OnItemChanged;
            _slot.OnAddOrRemoveItem += _handler;
            Apply(_slot.ContainedItem != null);
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_slot != null && _handler != null)
            {
                try { _slot.OnAddOrRemoveItem -= _handler; } catch { /* best effort */ }
            }
            _slot = null;
            _handler = null;
        }

        // Slot fires OnAddOrRemoveItem with the affected item on remove
        // too (not null), so read slot.ContainedItem for the new state.
        private void OnItemChanged(Item _) => Apply(_slot?.ContainedItem != null);

        private void Apply(bool hasItem)
        {
            if (_le == null)
            {
                _le = GetComponent<LayoutElement>();
                if (_le == null) _le = gameObject.AddComponent<LayoutElement>();
            }
            if (hasItem) { _le.preferredHeight = -1f; _le.minHeight = -1f; }
            else { _le.preferredHeight = EmptyHeight; _le.minHeight = EmptyHeight; }
        }
    }
}
