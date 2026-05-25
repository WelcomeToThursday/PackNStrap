using BeltSlot.Helpers;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Core.Items;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BeltSlot.Patches
{
    // ground-loot priority list. fork of the upstream PackNStrap patch
    // that sources the belt from BeltHolder.mod_belt instead of the
    // legacy ArmBand slot. falls back to ArmBand for old-style loadouts.
    public class GetPrioritizedContainersPackNStrapPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GClass3372), nameof(GClass3372.GetPrioritizedContainersForLoot));
        }

        [PatchPrefix]
        public static bool Prefix(InventoryEquipment equipment, Item item, ref IEnumerable<EFT.InventoryLogic.IContainer> __result)
        {
            var vest = equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem as VestItemClass;
            var backpack = equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem as BackpackItemClass;
            var pockets = equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem as PocketsItemClass;
            var secured = equipment.GetSlot(EquipmentSlot.SecuredContainer).ContainedItem as MobContainerItemClass;

            // belt = our mod_belt first, ArmBand fallback for legacy.
            var beltSlot = BeltHolderHelper.GetBeltSlot(equipment);
            var beltItem = beltSlot?.ContainedItem;
            CustomBeltItemClass customBelt = beltItem as CustomBeltItemClass;
            VestItemClass tacticalBelt = beltItem as VestItemClass;
            if (customBelt == null && tacticalBelt == null)
            {
                var legacyArmBand = equipment.GetSlot(EquipmentSlot.ArmBand)?.ContainedItem;
                customBelt = legacyArmBand as CustomBeltItemClass;
                tacticalBelt = legacyArmBand as VestItemClass;
            }

            var rigGrids = vest?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();
            var backpackGrids = backpack?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();
            var pocketGrids = pockets?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();
            var securedGrids = secured?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();
            var customBeltGrids = customBelt?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();
            var tacticalBeltGrids = tacticalBelt?.Containers ?? Enumerable.Empty<EFT.InventoryLogic.IContainer>();

            if (item is MagazineItemClass)
            {
                __result = rigGrids.Concat(customBeltGrids).Concat(tacticalBeltGrids).Concat(pocketGrids).Concat(backpackGrids).Concat(securedGrids);
                return false;
            }
            if (item is AmmoItemClass)
            {
                __result = customBeltGrids.Concat(tacticalBeltGrids).Concat(rigGrids).Concat(pocketGrids).Concat(backpackGrids).Concat(securedGrids);
                return false;
            }
            if (item is MoneyItemClass)
            {
                __result = securedGrids.Concat(backpackGrids).Concat(rigGrids).Concat(customBeltGrids).Concat(tacticalBeltGrids).Concat(pocketGrids);
                return false;
            }
            if (item is ThrowWeapItemClass)
            {
                __result = pocketGrids.Concat(customBeltGrids).Concat(tacticalBeltGrids).Concat(rigGrids).Concat(backpackGrids).Concat(securedGrids);
                return false;
            }
            __result = backpackGrids.Concat(rigGrids).Concat(customBeltGrids).Concat(tacticalBeltGrids).Concat(pocketGrids).Concat(securedGrids);
            return false;
        }
    }
}
