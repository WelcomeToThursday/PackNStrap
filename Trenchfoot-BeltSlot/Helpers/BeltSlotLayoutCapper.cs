using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace BeltSlot.Helpers
{
    // sits on the injected SlotView. ONE job: shrink the slot when
    // empty so it doesnt take a full row of empty space.
    //
    // CRITICAL: never reuse or destroy any pre-existing LayoutElement
    // on the SlotView - the template prefab may have its own that
    // vanilla SlotView sizing depends on. we add OUR OWN
    // LayoutElement with higher layoutPriority, and toggle its
    // preferredHeight between EmptyHeight (override) and -1 (defer to
    // the template's). previous attempts that called GetComponent or
    // DestroyImmediate clobbered the template's element and left the
    // slot stuck at empty-cap height after the first empty<->filled
    // cycle.
    //
    // polls in LateUpdate because drag-drop between two belt slots
    // (player <-> corpse) doesnt reliably fire OnAddOrRemoveItem on
    // both sides. only re-applies on state flips - no per-frame churn.
    public class BeltSlotLayoutCapper : MonoBehaviour
    {
        private const float EmptyHeight = 100f;

        private Slot _slot;
        private LayoutElement _ownLe;
        private System.Action<Item> _handler;
        private bool _lastHasItem;
        private bool _everApplied;

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

        private void OnDestroy()
        {
            Unbind();
            // clean up our own LayoutElement so we dont leak it onto
            // the SlotView if the capper is removed mid-session.
            if (_ownLe != null) { try { DestroyImmediate(_ownLe); } catch { /* best effort */ } }
            _ownLe = null;
        }

        private void Unbind()
        {
            if (_slot != null && _handler != null)
            {
                try { _slot.OnAddOrRemoveItem -= _handler; } catch { /* best effort */ }
            }
            _slot = null;
            _handler = null;
        }

        private void OnItemChanged(Item _) => Apply(_slot?.ContainedItem != null);

        // backstop for drag-drop transitions that fire on one side of
        // the transfer but not the other. only re-Apply on state flip.
        private void LateUpdate()
        {
            if (_slot == null) return;
            var hasItem = _slot.ContainedItem != null;
            if (hasItem != _lastHasItem || !_everApplied) Apply(hasItem);
        }

        private void Apply(bool hasItem)
        {
            if (_ownLe == null)
            {
                _ownLe = gameObject.AddComponent<LayoutElement>();
                // priority 2 beats the template's default-priority
                // LayoutElement (if any) when we set concrete values,
                // and falls back to it when we set -1.
                _ownLe.layoutPriority = 2;
            }
            if (hasItem)
            {
                // -1 = no opinion; VLG will use the template's
                // LayoutElement (or natural RectTransform size) which
                // is correct for the loaded belt's grids.
                _ownLe.preferredHeight = -1f;
                _ownLe.minHeight = -1f;
            }
            else
            {
                _ownLe.preferredHeight = EmptyHeight;
                _ownLe.minHeight = EmptyHeight;
            }
            _lastHasItem = hasItem;
            _everApplied = true;
        }
    }
}
