using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // makes BELT items bindable to quick-use hotkeys (1-0) just like
    // pocket/rig items. previous attempt postfixed
    // InventoryController.IsAtBindablePlace and re-implemented the entire
    // accept-or-reject expression ourselves. that broke when other mods
    // (eg user's ModularMedpouches) patched IsAtBindablePlace to also
    // accept medkit-CONTAINERS as bindable - our type filter only knew
    // about vanilla types so it rejected mod-extended types like
    // SimpleContainerItemClass-medkits even in the belt.
    //
    // new approach: relax the SPATIAL check only, by targeting the
    // closure lambda Class2402.method_1 that vanilla's IsAtBindablePlace
    // uses to compare each FastAccessSlots entry against the item's
    // parentSlot. when this lambda returns false but parentSlot is our
    // mod_belt slot, we flip the result to true.
    //
    // effect: vanilla's IsAtBindablePlace then evaluates as if belt was
    // a fast-access slot. it runs vanilla's type filter, vital-parts
    // check, examined check - and any postfixes from other mods see
    // vanilla's "real" answer (including their own type extensions)
    // without us having to know about them. ModularMedpouches' postfix
    // for example sees vanilla returned true for the spatial part and
    // its own logic for medkit-containers takes over.
    //
    // why Class2402.method_1 is safe to patch: it's a private nested
    // class created inline inside IsAtBindablePlace solely for this
    // check. it isn't called from anywhere else in EFT - i grep-checked.
    // worst case for an over-broad patch: we'd accept items in belt as
    // bindable - which is precisely what we want.
    public class BeltBindablePlacePatch : ModulePatch
    {
        private static FieldInfo _parentSlotField;
        private static FieldInfo _controllerField;

        protected override MethodBase GetTargetMethod()
        {
            // resolve the nested closure class. EFT obfuscates names but
            // the nested-type name is stable per-version. if EFT renames
            // Class2402 on update we'll need to bump this.
            var inner = typeof(InventoryController).GetNestedType("Class2402", BindingFlags.Public | BindingFlags.NonPublic);
            if (inner == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] InventoryController+Class2402 nested type not found; belt bind disabled");
                return null;
            }

            // cache field handles for the postfix. parentSlot holds the
            // slot the item's container chain landed on; inventoryController_0
            // gives us the controller to walk equipment -> belt from.
            _parentSlotField = inner.GetField("parentSlot", BindingFlags.Public | BindingFlags.Instance);
            _controllerField = inner.GetField("inventoryController_0", BindingFlags.Public | BindingFlags.Instance);
            if (_parentSlotField == null || _controllerField == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] Class2402 fields not found; belt bind disabled");
                return null;
            }

            return AccessTools.Method(inner, "method_1");
        }

        // signature mirrors Class2402.method_1: takes a Slot, returns bool.
        // __instance is the Class2402 closure (typed as object since we
        // can't reference the obfuscated nested type directly without it
        // bleeding through our codegen).
        [PatchPostfix]
        private static void Postfix(Slot slot, object __instance, ref bool __result)
        {
            // vanilla already matched the item to one of FastAccessSlots
            // (Pockets/TacticalVest); leave that decision alone.
            if (__result) return;
            if (__instance == null) return;

            var parentSlot = _parentSlotField.GetValue(__instance) as Slot;
            if (parentSlot == null) return;

            var controller = _controllerField.GetValue(__instance) as InventoryController;
            if (controller?.Inventory?.Equipment == null) return;

            // resolve our belt slot for this controller's equipment. lookup
            // is the standard pockets -> hidden grid -> holder -> mod_belt
            // walk. fires twice per IsAtBindablePlace call (once per
            // FastAccessSlot iteration); not free but not hot-path either.
            var beltSlot = BeltHolderHelper.GetBeltSlot(controller.Inventory.Equipment);
            if (beltSlot == null) return;

            // item lives in the belt? then for the purposes of vanilla's
            // FastAccessSlots.Any(slot => slot == parentSlot) we say yes.
            // we always flip true (not gated on which FastAccessSlot we're
            // currently iterating) because the outer Any() short-circuits
            // on first true - so the first iteration that hits this returns.
            if (parentSlot == beltSlot)
            {
                __result = true;
            }
        }
    }
}
