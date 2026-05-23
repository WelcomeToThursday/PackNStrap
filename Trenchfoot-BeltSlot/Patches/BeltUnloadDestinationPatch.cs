using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // postfix on GClass3372.GetPrioritizedGridsForUnloadedObject that
    // appends the BELT's grids to the priority list, so a magazine
    // swapped out by reload (or any unloaded item) can be auto-placed
    // back into the belt instead of being dropped.
    //
    // WTT-PackNStrapClient already has a prefix on this method that
    // returns false (skip original) and computes its own priority list.
    // its list references the legacy ArmBand-slot belt path which is
    // empty under our refactor (belts live in BeltHolder.mod_belt now).
    // postfixes still run after a skip-original prefix, so we tack belt
    // grids onto whatever the prefix produced.
    //
    // ordering: belt grids appended at the END of the priority list so
    // EFT fills pockets/rig first, then falls back to belt if those are
    // full. MagDumpPouch-only result (when present) stays intact and
    // belt is added as a fallback.
    public class BeltUnloadDestinationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // GClass3372 is the same target the existing PackNStrap patch
            // uses; matching by name avoids hardcoding the obfuscated id.
            var t = AccessTools.TypeByName("GClass3372");
            if (t == null)
            {
                Plugin.Instance?.Log?.LogWarning("[Belt Slots] GClass3372 not found; unloaded-item belt routing disabled");
                return null;
            }
            return AccessTools.Method(t, "GetPrioritizedGridsForUnloadedObject");
        }

        [PatchPostfix]
        private static void Postfix(InventoryEquipment equipment, ref IEnumerable<StashGridClass> __result)
        {
            if (equipment == null || __result == null) return;

            var beltSlot = BeltHolderHelper.GetBeltSlot(equipment);
            var belt = beltSlot?.ContainedItem as CompoundItem;
            if (belt == null) return;

            // belt's Grids array. CompoundItem exposes Grids directly
            // (StashGridClass[] field) - equivalent to what PackNStrap
            // does for CustomBeltItemClass.Grids in its own prefix.
            var beltGrids = belt.Grids;
            if (beltGrids == null || beltGrids.Length == 0) return;

            // append rather than prepend: EFT walks the priority list in
            // order and the user's pockets/rig should be preferred over
            // belt for "where did my swapped mag go".
            __result = __result.Concat(beltGrids);
        }
    }
}
