using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // makes belt items bindable to quick-use hotkeys (1-0). targets the
    // inner closure lambda InventoryController+Class2402.method_1, which
    // vanilla's IsAtBindablePlace uses to compare each FastAccessSlots
    // entry against the item's parentSlot. when the comparison says false
    // and parentSlot is our mod_belt, we flip to true.
    //
    // patching the lambda (rather than IsAtBindablePlace itself) means
    // vanilla's full type-filter/vital-parts/examined chain runs against
    // a "yes it's in fast-access" result, so any third-party postfix on
    // IsAtBindablePlace (e.g. ModularMedpouches' container-type extension)
    // sees a clean vanilla pass and layers its own logic on top.
    public class BeltBindablePlacePatch : ModulePatch
    {
        private static FieldInfo _parentSlotField;
        private static FieldInfo _controllerField;

        protected override MethodBase GetTargetMethod()
        {
            var inner = typeof(InventoryController).GetNestedType("Class2402", BindingFlags.Public | BindingFlags.NonPublic);
            if (inner == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] InventoryController+Class2402 nested type not found; belt bind disabled");
                return null;
            }

            _parentSlotField = inner.GetField("parentSlot", BindingFlags.Public | BindingFlags.Instance);
            _controllerField = inner.GetField("inventoryController_0", BindingFlags.Public | BindingFlags.Instance);
            if (_parentSlotField == null || _controllerField == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] Class2402 fields not found; belt bind disabled");
                return null;
            }

            return AccessTools.Method(inner, "method_1");
        }

        [PatchPostfix]
        private static void Postfix(Slot slot, object __instance, ref bool __result)
        {
            if (__result) return;
            if (__instance == null) return;

            var parentSlot = _parentSlotField.GetValue(__instance) as Slot;
            if (parentSlot == null) return;

            var controller = _controllerField.GetValue(__instance) as InventoryController;
            if (controller?.Inventory?.Equipment == null) return;

            var beltSlot = BeltHolderHelper.GetBeltSlot(controller.Inventory.Equipment);
            if (beltSlot == null) return;

            // outer Any() short-circuits, so flipping once on parentSlot
            // match is enough regardless of which FastAccessSlot we're on.
            if (parentSlot == beltSlot)
            {
                __result = true;
            }
        }
    }
}
