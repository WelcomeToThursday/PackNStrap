using Diz.LanguageExtensions;
using Diz.Utils;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using PackNStrap.Core.Items;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PackNStrap.Patches;

internal class UnloadWeaponPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.UnloadWeapon));
    }

    [PatchPrefix]
    public static bool UnloadWeaponPrefix(ItemUiContext __instance, ref Weapon weapon, ref Task __result)
    {
        if (!InGameStatus.InRaid)
        {
            return true;
        }
        
        #if DEBUG
        Console.WriteLine($"Starting CustomUnloadWeapon for weapon: {weapon.TemplateId}");
        #endif
        try
        {
            __result = CustomUnloadWeapon(__instance, weapon);
            return false;
        }
        catch (Exception ex) 
        {
            Console.WriteLine(ex.ToString());
            return true;
        }
    }

    private static async Task CustomUnloadWeapon(ItemUiContext __instance, Weapon weapon)
    {
        ItemController _itemController = (ItemController)
            AccessTools.Field(typeof(ItemUiContext),
                    "_itemController")
                .GetValue(__instance);
        CompoundItem[] _rightPanelItem = (CompoundItem[])
            AccessTools.Field(typeof(ItemUiContext),
                    "_rightPanelItem")
                .GetValue(__instance);
        if (!weapon.IsUnderBarrelDeviceActive)
        {
            Magazine currentMagazine = weapon.GetCurrentMagazine();
            if (currentMagazine != null)
            {
                if (!__instance.TryExamineMalfunction(weapon))
                {
                    var inventoryEquipment = (InventoryEquipment)
                        AccessTools.Field(typeof(ItemUiContext),
                                "_equipment")
                            .GetValue(__instance);
                    bool flag;
                    if (!(flag = inventoryEquipment.Contains(currentMagazine)) && _rightPanelItem == null)
                    {
                        UnityEngine.Debug.LogError("Something went wrong. Right panel is null while mag is not from equipment.");
                    }
                    else
                    {
                        IEnumerable<CompoundItem> enumerable;
                        if (_rightPanelItem != null)
                        {
                            enumerable = (flag ? inventoryEquipment.ToEnumerable().Concat(_rightPanelItem) : _rightPanelItem.Concat(inventoryEquipment.ToEnumerable()));
                        }
                        else
                        {
                            IEnumerable<CompoundItem> enumerable2 = inventoryEquipment.ToEnumerable();
                            enumerable = enumerable2;
                        }
                        
#if DEBUG
                        Console.WriteLine("[BEFORE] Original containers:");
                        LogContainers(enumerable);
#endif

                        List<CustomContainerItemClass> magDumpPouches = Common.GetMagDumpPouches(inventoryEquipment, false);
                
#if DEBUG
                        Console.WriteLine($"Found {magDumpPouches?.Count ?? 0} MagDumpPouches");
#endif
                        IEnumerable<CompoundItem> enumerable3;
                        if (magDumpPouches != null)
                        {
                            enumerable3 = magDumpPouches
                                .Concat(enumerable);
                        }
                        else
                        {
                            enumerable3 = enumerable;
                        }

#if DEBUG
                        Console.WriteLine("[AFTER] Final search order:");
                        LogContainers(enumerable3);
#endif
                        OperationResult<IItemOperationResult> gstruct = ItemManipulator.QuickFindAppropriatePlace(currentMagazine, _itemController, enumerable3, ItemManipulator.EMoveItemOrder.PrioritizeTargetsOrder, true);
                        bool flag2;
                        if (flag2 = gstruct.Succeeded)
                        {
                            flag2 = (await ItemUiContext.RunWithSound(_itemController, currentMagazine, gstruct)).Succeed;
                        }
                        if (!flag2)
                        {
                            if (!InGameStatus.InRaid)
                            {
                                NotificationManager.DisplayWarningNotification("Can't find a place for item".Localized());
                            }
                            else if (_itemController.CanThrow(currentMagazine))
                            {
                                _itemController.ThrowItem(currentMagazine, true);
                            }
                        }
                    }
                }
            }
            
        }
    }
    private static void LogContainers(IEnumerable<CompoundItem> containers)
    {
        if (containers == null)
        {
            Console.WriteLine("No containers available");
            return;
        }

        foreach (var container in containers)
        {
            Console.WriteLine($"- Container: {container.Name} ({container.Id})");
        }
    }
}