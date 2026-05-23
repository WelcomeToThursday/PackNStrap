using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // appends belt grids to the unloaded-object priority list so
    // swapped-out magazines (or any unloaded item) can auto-place into
    // the belt instead of dropping.
    //
    // WTT-PackNStrapClient already has a skip-original prefix on this
    // method that builds its own list pointing at the legacy ArmBand
    // belt path (empty under our refactor). postfixes still run after
    // skip-original, so we tack on top of whatever the prefix produced.
    public class BeltUnloadDestinationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
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

            var beltGrids = belt.Grids;
            if (beltGrids == null || beltGrids.Length == 0) return;

            // append, don't prepend: pockets/rig should be preferred
            // landing spots over belt.
            __result = __result.Concat(beltGrids);
        }
    }
}
