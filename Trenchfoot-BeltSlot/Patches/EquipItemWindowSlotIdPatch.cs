using System.Reflection;
using BeltSlot.Helpers;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace BeltSlot.Patches
{
    // EquipItemWindow.Show parses slot.ID through Enum.TryParse<EquipmentSlot>
    // and *throws* if it doesnt match a vanilla enum value. our belt slot
    // is "mod_belt" - not a real EquipmentSlot - so the click on the BELT
    // header surfaces an empty Available pane (and a throw under the hood).
    //
    // fix mirrors LegArmor's EquipItemWindowSlotIdPatch: swap the slot's
    // ID to "ArmBand" for the duration of Show, restore in postfix. safe
    // because the parsed enum only special-cases Dogtag (different UI) and
    // FirstPrimary/SecondPrimary (different layout); ArmBand hits neither.
    // the actual available-items list comes from the slot's filter via
    // method_4 -> InteractionsHandlerClass.GetItemsForAddress, not the enum.
    public class EquipItemWindowSlotIdPatch : ModulePatch
    {
        private const string OurSlotId = BeltHolderHelper.BeltSlotName; // "mod_belt"
        private const string SubstituteId = "ArmBand";

        // cached - this fires on every BELT header click.
        private static readonly FieldInfo SlotIdBackingField =
            AccessTools.Field(typeof(Slot), "<ID>k__BackingField");

        protected override MethodBase GetTargetMethod()
        {
            // Show is overloaded; pin to the (Slot, InventoryController,
            // ISession, SkillManager, Vector3) overload that the slot header
            // click invokes via ItemUiContext.ShowEquipItemWindow.
            return AccessTools.Method(
                typeof(EquipItemWindow),
                nameof(EquipItemWindow.Show),
                new[]
                {
                    typeof(Slot),
                    typeof(InventoryController),
                    typeof(ISession),
                    typeof(SkillManager),
                    typeof(Vector3),
                });
        }

        [PatchPrefix]
        private static void Prefix(Slot slot, out string __state)
        {
            __state = null;
            if (slot == null || slot.ID != OurSlotId) return;
            if (SlotIdBackingField == null)
            {
                Plugin.Instance?.Log?.LogError("[Belt Slots] could not find Slot.ID backing field; equip window will throw");
                return;
            }
            __state = (string)SlotIdBackingField.GetValue(slot);
            SlotIdBackingField.SetValue(slot, SubstituteId);
        }

        [PatchPostfix]
        private static void Postfix(Slot slot, string __state)
        {
            if (__state == null) return;
            SlotIdBackingField?.SetValue(slot, __state);
        }
    }
}
