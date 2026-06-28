using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeltSlot.Helpers;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace BeltSlot.Patches
{
    // mounts the belt visual on PlayerBody. vanilla only mounts visuals
    // for slots in the hardcoded SlotNames array; our mod_belt slot
    // isn't there so we hand-construct an EquipmentSlotClass for it.
    //
    // NOT added to PlayerBody.SlotViews - raid-init iterates that dict
    // and crashes on synthetic keys. we keep the EquipmentSlotClass
    // alive ourselves in _liveSlots.
    //
    // ArmorVest hint matches LegArmor's working pattern; mesh placement
    // comes from the prefab's bone bindings so the hint just gates which
    // internal loader path runs.
    //
    // companion patch BeltMountSuppressOnSlotViewChangedPatch swallows
    // the post-mount OnSlotViewChanged event - that event would
    // otherwise drive a corpse-loot UI cascade.
    //
    // known limitation (same as LegArmor): the mounted visual shows on
    // third-person preview bodies but is invisible to first-person camera
    // (wrong layer + missing HotObject).
    public class PlayerBodyMountBeltPatch : ModulePatch
    {
        private const string HolderTpl = BeltHolderHelper.HolderTpl;
        private const string HolderSlotName = BeltHolderHelper.BeltSlotName;

        private static readonly Dictionary<PlayerBody, PlayerBody.EquipmentSlotClass> _liveSlots = new();

        // per-body slot-change handler so we can unsubscribe on re-Init.
        // without unsubscribe the slot accumulates a handler per Init and
        // every transition fires N times.
        private static readonly Dictionary<PlayerBody, System.Action<Item>> _slotChangeHandlers = new();

        // bodies where the next OnSlotViewChanged (fired by method_4 after
        // the async prefab load) should be swallowed - that event drives
        // UI subscribers and triggered the per-frame reflow we tried to
        // dodge. only used while the patch is enabled.
        private static readonly HashSet<PlayerBody> _suppressNextSlotViewChanged = new();

        // EquipmentSlotClass.Dispose also calls DestroyCurrentModel, which
        // returns the GameObject to the pool. we want to release the
        // binding without losing the visual, so reflect Action_0/_2 and
        // invoke them directly.
        private static readonly FieldInfo Action0Field =
            AccessTools.Field(typeof(PlayerBody.EquipmentSlotClass), "Action_0");
        private static readonly FieldInfo Action2Field =
            AccessTools.Field(typeof(PlayerBody.EquipmentSlotClass), "Action_2");

        protected override MethodBase GetTargetMethod()
        {
            // long-form Init (takes InventoryEquipment) - used by every
            // PlayerModelView context that shows equipment.
            return AccessTools.Method(
                typeof(PlayerBody),
                nameof(PlayerBody.Init),
                new[]
                {
                    typeof(GClass2197),
                    typeof(InventoryEquipment),
                    typeof(BindableStateClass<Item>),
                    typeof(int),
                    typeof(EPlayerSide),
                    typeof(string),
                    typeof(System.Collections.Generic.Dictionary<EquipmentSlot, Transform>),
                    typeof(bool),
                });
        }

        [PatchPostfix]
        private static void Postfix(PlayerBody __instance, InventoryEquipment equipment)
        {
            try
            {
                MountIfPresent(__instance, equipment);
            }
            catch (System.Exception ex)
            {
                Plugin.Instance?.Log?.LogError($"[Belt Slots] PlayerBody mount failed: {ex}");
            }
        }

        private static void MountIfPresent(PlayerBody body, InventoryEquipment equipment)
        {
            if (body == null || equipment == null) return;

            // dispose EquipmentSlotClasses for destroyed PlayerBodies.
            // multiple stale bindings firing concurrently on item moves
            // stalls inventory transactions. Init is outside the update
            // window so disposing here can't trigger the reentrancy crash.
            // Unity-destroyed objects == null via operator overload, but
            // the dict uses ReferenceEquals - explicit operator check.
            var stale = _liveSlots.Keys.Where(b => b == null).ToList();
            foreach (var b in stale)
            {
                if (_liveSlots.TryGetValue(b, out var sc))
                {
                    try { sc.Dispose(); } catch { /* best effort */ }
                }
                _liveSlots.Remove(b);
            }
            // mirror cleanup for the handler dict so dead bodies don't
            // pile up there either. handler refs become GC-eligible once
            // the slot itself goes away.
            var staleHandlers = _slotChangeHandlers.Keys.Where(b => b == null).ToList();
            foreach (var b in staleHandlers) _slotChangeHandlers.Remove(b);

            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return;

            // walk contents rather than grid name in case the holder
            // location was repaired since this body was made.
            Item holder = null;
            foreach (var child in pocketsItem.GetAllItems())
            {
                if (child.StringTemplateId == HolderTpl) { holder = child; break; }
            }
            if (holder is not CompoundItem holderCompound) return;

            var slot = holderCompound.Slots.FirstOrDefault(s => s.ID == HolderSlotName);
            if (slot == null) return;

            // HolsterPistol is the most belt-appropriate vanilla bone.
            // mesh placement comes from the prefab's bone bindings anyway.
            var bone = body.PlayerBones?.HolsterPistol;

            // re-Init fires for the same body (stash refresh after raid).
            // dispose the prior EquipmentSlotClass so its phantom GO
            // doesn't linger if the slot is now empty (belt lost on death).
            if (_liveSlots.TryGetValue(body, out var prev))
            {
                try { prev.Dispose(); } catch { /* best effort */ }
                _liveSlots.Remove(body);
            }
            // and unsubscribe any prior handler on this body so we don't
            // double-fire on subsequent slot changes.
            if (_slotChangeHandlers.TryGetValue(body, out var oldHandler))
            {
                try { slot.OnAddOrRemoveItem -= oldHandler; } catch { /* best effort */ }
                _slotChangeHandlers.Remove(body);
            }

            // single persistent handler covers every transition:
            //   empty -> filled  : mount (handles deferred-fill case)
            //   filled -> empty  : dispose so the visual disappears when
            //                      the belt is looted off a corpse
            //   filled -> filled : skipped via _liveSlots guard
            // binding was released at mount time, so Dispose's only
            // visible side-effect is DestroyCurrentModel.
            //
            // Slot fires OnAddOrRemoveItem with the AFFECTED item on both
            // add and remove (Slot.cs RemoveItemInternal passes
            // containedItem AFTER nulling ContainedItem), so the handler
            // param doesnt tell us the new state - read slot.ContainedItem.
            System.Action<Item> handler = null;
            handler = (Item _) =>
            {
                if (body == null)
                {
                    slot.OnAddOrRemoveItem -= handler;
                    _slotChangeHandlers.Remove(body);
                    return;
                }
                if (slot.ContainedItem == null)
                {
                    if (_liveSlots.TryGetValue(body, out var sc))
                    {
                        try { sc.Dispose(); } catch { /* best effort */ }
                        _liveSlots.Remove(body);
                    }
                    return;
                }
                if (_liveSlots.ContainsKey(body)) return;
                try { MountNow(body, slot, bone); }
                catch (System.Exception ex) { Plugin.Instance?.Log?.LogError($"[Belt Slots] add-handler mount failed: {ex}"); }
            };
            slot.OnAddOrRemoveItem += handler;
            _slotChangeHandlers[body] = handler;

            if (slot.ContainedItem != null)
                MountNow(body, slot, bone);
        }

        private static void MountNow(PlayerBody body, Slot slot, Transform bone)
        {
            // swallow the post-mount OnSlotViewChanged event - it drives
            // a corpse-loot UI cascade we don't want to trigger.
            _suppressNextSlotViewChanged.Add(body);

            var slotClass = new PlayerBody.EquipmentSlotClass(
                body, slot, bone, EquipmentSlot.ArmorVest, null, null, false);

            // release the binding immediately - persistent binding stalls
            // inventory transactions on slot moves (LegArmor's stash-
            // carrier fade bug).
            ReleaseBinding(slotClass, Action0Field);
            ReleaseBinding(slotClass, Action2Field);

            _liveSlots[body] = slotClass;

            Plugin.Instance?.Log?.LogInfo("[Belt Slots] mounted belt slot");
        }

        // invoke + null so Dispose doesn't re-invoke.
        private static void ReleaseBinding(PlayerBody.EquipmentSlotClass slotClass, FieldInfo field)
        {
            if (field == null) return;
            if (field.GetValue(slotClass) is System.Action unbind)
            {
                try { unbind(); } catch { /* best effort */ }
            }
            field.SetValue(slotClass, null);
        }

        // exposes the suppression set to the companion prefix patch.
        internal static bool ConsumeSuppression(PlayerBody body)
            => body != null && _suppressNextSlotViewChanged.Remove(body);
    }

    // prefix on PlayerBody.OnSlotViewChanged that drops the event for
    // bodies in PlayerBodyMountBeltPatch's "just mounted" set. one-shot
    // per add: after consuming, future calls pass through normally.
    public class BeltMountSuppressOnSlotViewChangedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(PlayerBody), nameof(PlayerBody.OnSlotViewChanged));
        }

        [PatchPrefix]
        private static bool Prefix(PlayerBody __instance)
        {
            return !PlayerBodyMountBeltPatch.ConsumeSuppression(__instance);
        }
    }
}
