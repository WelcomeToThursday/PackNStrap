using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Screens;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace BeltSlot.Patches
{
    public class ContainersPanelPatch : ModulePatch
    {
        private static FieldInfo? defaultSlotTemplate;

        protected override MethodBase GetTargetMethod()
        {
            defaultSlotTemplate = AccessTools.Field(typeof(ContainersPanel), "_defaultSlotTemplate");
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.InstantiateSlotView));
        }

        [PatchPrefix]
        static bool Prefix(ContainersPanel __instance, EquipmentSlot slotName, ref SlotView __result)
        {
            try
            {
                if (Plugin.Instance.EnableLogging)
                {
                    Plugin.Instance.Log.LogInfo($"[Belt Slots] ContainersPanelPatch.PreFix called");
                }

                if (slotName == EquipmentSlot.ArmBand)
                {
                    SlotView template = defaultSlotTemplate.GetValue(__instance) as SlotView;
                    if (template != null)
                    {
                        __result = UnityEngine.Object.Instantiate<SlotView>(template);

                        if (Plugin.Instance.EnableLogging)
                        {
                            Plugin.Instance.Log.LogInfo($"[Belt Slots] default template for armband");
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] Exception: {ex}");
            }

            return true;
        }
    }

    public class ContainersPanelPatch2 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }
        [PatchPostfix]
        static void Postfix()
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ContainersPanelPatch2.Postfix called");
            }
            //Plugin.Instance.armbandSlot = Plugin.Instance.inventoryEquipment.GetSlot(EquipmentSlot.ArmBand);

            Plugin.Instance.IsScav = false;
            Plugin.Instance.SetPlayerArmbandSlotOnOpen();
        }
    }

    public class MainMenuShowOperationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MainMenuShowOperation), nameof(MainMenuShowOperation.method_48));
        }
        [PatchPostfix]
        static void Postfix()
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] MainMenuShowOperationPatch.Postfix called");
            }
            Plugin.Instance.SetInsuranceArmbandSlot();
        }
    }

    public class ComplexStashPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ComplexStashPanel), nameof(ComplexStashPanel.Show));
        }

        [PatchPostfix]
        static void Postfix(ComplexStashPanel __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ComplexStashPanelPatch.Postfix called");
            }
            Plugin.Instance.ComplexStashPanelLoaded = true;
            Plugin.Instance.SetLootArmbandSlotOnOpen();
        }
    }

    public class ComplexStashPanelPatch2 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ComplexStashPanel), nameof(ComplexStashPanel.Close));
        }
        [PatchPostfix]
        static void Postfix(ComplexStashPanel __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ComplexStashPanelPatch2.Postfix called");
            }
            Plugin.Instance.ComplexStashPanelLoaded = false;
        }
    }

    public class ItemViewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemView), nameof(ItemView.Update));
        }
        [PatchPostfix]
        static void Postfix(ItemView __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ItemViewPatch.Postfix called");
            }
            Plugin.Instance.UpdateLootArmBandSlot();
            Plugin.Instance.UpdatePlayerArmBandSlot();
            Plugin.Instance.UpdateScavInventoryArmbandSlot();
        }
    }
}
