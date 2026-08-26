using EFT;
using EFT.InventoryLogic;
using PackNStrap.Core.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PackNStrap.Helpers;

public abstract class Common
{
    public static bool IsItemInReachableLocation(Item item, InventoryController controller)
    {
        var equipment = controller.Inventory.Equipment;
        if (equipment == null || item == null)
            return false;

        foreach (var slotId in PackNStrap.NewBindAvailableSlots)
        {
            var root = equipment.GetSlot(slotId)?.ContainedItem;
            if (root == null)
                continue;

            if (root == item)
                return true;

            var rootItems = GetTopLevelItems(root as CompoundItem);

            if (rootItems.Contains(item))
                return true;

            foreach (var child in rootItems.OfType<CompoundItem>())
            {
                if (child is Vest || child is Backpack || child is CustomBeltItemClass)
                    continue;

                if (GetTopLevelItems(child).Contains(item))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<Item> GetTopLevelItems(CompoundItem container)
    {
        return new List<CompoundItem> { container }.GetTopLevelItems();
    }
    public static List<CustomContainerItemClass> GetMagDumpPouches(InventoryEquipment equipment, bool backpackIncluded)
    {
        if (equipment == null)
        {
            Console.WriteLine("Equipment is null.");
            return null;
        }

        List<CustomContainerItemClass> magDumpPouches = new List<CustomContainerItemClass>();
        var magDumpPouchItemId = "440de5d056825485a0cf3a19";

        void FindMagDumpPouchInItem(Item item)
        {
            if (item == null) return;

            foreach (var itemInGrid in item.GetAllItems())
            {
                if (itemInGrid is CustomContainerItemClass potentialMagDumpPouch 
                    && potentialMagDumpPouch.TemplateId == magDumpPouchItemId)
                {
                    if (potentialMagDumpPouch.IsChildOf(item))
                        magDumpPouches.Add(potentialMagDumpPouch);
                }
            }
        }

        Slot tacticalVestSlot = equipment.GetSlot(EquipmentSlot.TacticalVest);
        Slot pocketsSlot = equipment.GetSlot(EquipmentSlot.Pockets);
        Slot backpackSlot = equipment.GetSlot(EquipmentSlot.Backpack);
        Slot armbandSlot = equipment.GetSlot(EquipmentSlot.ArmBand);

        FindMagDumpPouchInItem(tacticalVestSlot?.ContainedItem as Vest);
        FindMagDumpPouchInItem(pocketsSlot?.ContainedItem as Pockets);
        if (backpackIncluded)
            FindMagDumpPouchInItem(backpackSlot?.ContainedItem as Backpack);
        FindMagDumpPouchInItem(armbandSlot?.ContainedItem as CustomBeltItemClass);

        return magDumpPouches;
    }

    public static bool CanAcceptItems(Grid grid)
    {
        Player player = PackNStrap.Player;
        if (player != null && player.HandsController != null && player.HandsController?.Item != null && player.HandsController?.Item?.GetCurrentMagazine() != null)
        {
            return grid.CanAccept(player.HandsController?.Item?.GetCurrentMagazine());
        }
        return false;
    }
}