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
    // ArmBand type hint passed to the visual loader because PackNStrap's
    // belt bundles were authored against the armband renderer.
    //
    // currently DISABLED in Plugin.cs - constructing the EquipmentSlotClass
    // has side effects that produce a duplicate Tactical Rig panel.
    //
    // known limitation (same as LegArmor): the mounted visual shows on
    // third-person preview bodies but is invisible to first-person camera
    // (wrong layer + missing HotObject).
    public class PlayerBodyMountBeltPatch : ModulePatch
    {
        private const string HolderTpl = BeltHolderHelper.HolderTpl;
        private const string HolderSlotName = BeltHolderHelper.BeltSlotName;

        private static readonly Dictionary<PlayerBody, PlayerBody.EquipmentSlotClass> _liveSlots = new();

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

            if (slot.ContainedItem != null)
            {
                MountNow(body, slot, bone);
                return;
            }

            // empty slot at init (common on Time Has Come where the body
            // is built before equipment resolves). subscribe and mount
            // when the item arrives.
            System.Action<Item> handler = null;
            handler = (Item item) =>
            {
                if (body == null)
                {
                    slot.OnAddOrRemoveItem -= handler;
                    return;
                }
                if (item == null) return;
                slot.OnAddOrRemoveItem -= handler;
                if (_liveSlots.ContainsKey(body)) return;
                try { MountNow(body, slot, bone); }
                catch (System.Exception ex) { Plugin.Instance?.Log?.LogError($"[Belt Slots] deferred mount failed: {ex}"); }
            };
            slot.OnAddOrRemoveItem += handler;
            Plugin.Instance?.Log?.LogInfo("[Belt Slots] holder slot empty at init; subscribed for later mount");
        }

        private static void MountNow(PlayerBody body, Slot slot, Transform bone)
        {
            var slotClass = new PlayerBody.EquipmentSlotClass(
                body, slot, bone, EquipmentSlot.ArmBand, null, null, false);

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
    }
}
