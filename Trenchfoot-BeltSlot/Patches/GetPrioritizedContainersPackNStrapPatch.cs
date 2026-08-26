using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Core.Items;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BeltSlot.Patches
{
    // Creates the prioritized destination list used by loot/inventory actions.
    public class GetPrioritizedContainersPackNStrapPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryEquipmentExtension),
                nameof(InventoryEquipmentExtension.GetPrioritizedContainersForLoot));
        }

        [PatchPrefix]
        public static bool Prefix(
            InventoryEquipment equipment,
            Item item,
            ref IEnumerable<IContainer> __result)
        {
            IEnumerable<IContainer> vestContainers =
                (equipment.GetSlot(EquipmentSlot.TacticalVest)?.ContainedItem as Vest)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            IEnumerable<IContainer> backpackContainers =
                (equipment.GetSlot(EquipmentSlot.Backpack)?.ContainedItem as Backpack)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            IEnumerable<IContainer> pocketContainers =
                (equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as Pockets)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            IEnumerable<IContainer> secureContainerContainers =
                (equipment.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem as MobContainer)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            Slot armBandSlot = equipment.GetSlot(EquipmentSlot.ArmBand);

            IEnumerable<IContainer> customBeltContainers =
                (armBandSlot?.ContainedItem as CustomBeltItemClass)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            IEnumerable<IContainer> tacticalBeltContainers =
                (armBandSlot?.ContainedItem as Vest)
                ?.Containers
                ?? Enumerable.Empty<IContainer>();

            if (item is Ammo or Magazine)
            {
                __result = vestContainers
                    .Concat(customBeltContainers)
                    .Concat(tacticalBeltContainers)
                    .Concat(pocketContainers)
                    .Concat(backpackContainers)
                    .Concat(secureContainerContainers);

                return false;
            }

            if (item is Money)
            {
                __result = secureContainerContainers
                    .Concat(backpackContainers)
                    .Concat(vestContainers)
                    .Concat(customBeltContainers)
                    .Concat(tacticalBeltContainers)
                    .Concat(pocketContainers);

                return false;
            }

            if (item is ThrowWeap)
            {
                __result = pocketContainers
                    .Concat(vestContainers)
                    .Concat(customBeltContainers)
                    .Concat(tacticalBeltContainers)
                    .Concat(backpackContainers)
                    .Concat(secureContainerContainers);

                return false;
            }

            __result = backpackContainers
                .Concat(vestContainers)
                .Concat(customBeltContainers)
                .Concat(tacticalBeltContainers)
                .Concat(pocketContainers)
                .Concat(secureContainerContainers);

            return false;
        }
    }
}