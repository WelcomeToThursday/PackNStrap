using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Screens;
using UnityEngine;

using BeltSlot.Helpers;
using BeltSlot.Patches;

namespace BeltSlot;

[BepInPlugin("com.trenchfoot.beltslot", "Trenchfoot-BeltSlot", "2.0.4")]
[BepInDependency("com.wtt.packnstrap")]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly EquipmentSlot[] SlotsAbovePockets =
    {
        EquipmentSlot.TacticalVest,
        EquipmentSlot.ArmBand,
        EquipmentSlot.Pockets,
        EquipmentSlot.Backpack,
        EquipmentSlot.SecuredContainer,
        EquipmentSlot.Dogtag
    };

    private static readonly EquipmentSlot[] SlotsBelowPockets =
    {
        EquipmentSlot.TacticalVest,
        EquipmentSlot.Pockets,
        EquipmentSlot.ArmBand,
        EquipmentSlot.Backpack,
        EquipmentSlot.SecuredContainer,
        EquipmentSlot.Dogtag
    };

    internal static Plugin Instance { get; private set; } = null!;
    internal static UI_Mappings UiMappings { get; private set; } = null!;
    internal ManualLogSource Log { get; private set; } = null!;

    public bool EnableLogging { get; set; }
    public bool IconToggle { get; set; } = true;

    public bool InventoryScreenLoaded { get; set; }
    public bool ComplexStashPanelLoaded { get; set; }
    public bool IsScav { get; set; }

    public Slot? PlayerArmbandSlot { get; private set; }
    public Slot? LootArmbandSlot { get; private set; }

    public InventoryEquipment InventoryEquipment;
    public InventoryScreen InventoryScreen;

    private string? _playerArmbandItemId;
    private string? _lootArmbandItemId;
    private string? _scavTransferArmbandItemId;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Settings.Init(Config);
        UiMappings = new UI_Mappings();
        ConfigureEquipmentSlotOrder();
        EnablePatches();
    }

    private void EnablePatches()
    {
        new ContainersPanelPatch().Enable();
        new ContainersPanelPatch2().Enable();
        new ComplexStashPanelPatch().Enable();
        new ComplexStashPanelPatch2().Enable();
        new MainMenuShowOperationPatch().Enable();
        new ItemUiContextPatch().Enable();
        new EquipmentBuildsScreenPatch().Enable();
        new InventoryEquipmentPatch().Enable();
        new InventoryScreenPatch().Enable();
        new ItemViewPatch().Enable();
        new GetPrioritizedContainersPackNStrapPatch().Enable();
    }

    private void ConfigureEquipmentSlotOrder()
    {
        var slots = Settings.BeltSlotLocation.Value switch
        {
            BeltSlotLocationOption.AbovePockets => SlotsAbovePockets,
            BeltSlotLocationOption.BelowPockets => SlotsBelowPockets,
            _ => SlotsAbovePockets
        };

        var field = typeof(ContainersPanel).GetField(
            "_slotNames",
            BindingFlags.Static | BindingFlags.NonPublic);

        if (field == null)
        {
            Log.LogError("Could not find ContainersPanel._slotNames. Belt slot ordering was not applied.");
            return;
        }

        field.SetValue(null, slots);
    }

    public void UpdatePlayerArmBandSlot()
    {
        if (!IsGameReady() || IsScav || !IsInventoryScreenOpen() || PlayerArmbandSlot == null)
        {
            return;
        }

        RefreshIfChanged(
            PlayerArmbandSlot,
            UiMappings.armBandSlot,
            UiMappings.beltSlot,
            ref _playerArmbandItemId);
    }

    public void UpdateLootArmBandSlot()
    {
        if (!IsGameReady()
            || !InGameStatus.InRaid
            || !ComplexStashPanelLoaded
            || !IsInventoryScreenOpen()
            || LootArmbandSlot == null)
        {
            return;
        }

        RefreshIfChanged(
            LootArmbandSlot,
            UiMappings.lootArmBand,
            UiMappings.lootBeltSlot,
            ref _lootArmbandItemId);
    }

    public void UpdateScavInventoryArmbandSlot()
    {
        if (IsScav || !IsGameReady() || !IsScavengerTransferScreenOpen())
        {
            return;
        }

        var slot = UiMappings.getScavLootTransferUI_Mappings();
        if (slot == null)
        {
            IsScav = true;
            return;
        }

        RefreshIfChanged(
            slot,
            UiMappings.scavArmBandSlot,
            UiMappings.scavBeltSlot,
            ref _scavTransferArmbandItemId);
    }

    public void SetPlayerArmbandSlotOnOpen()
    {
        if (IsScav || !IsInventoryScreenOpen())
        {
            return;
        }

        var slot = UiMappings.getInventoryContainer_Mappings();
        if (slot == null)
        {
            IsScav = true;
            return;
        }

        PlayerArmbandSlot = slot;
        RefreshAndTrack(slot, UiMappings.armBandSlot, UiMappings.beltSlot, ref _playerArmbandItemId);
    }

    public void SetLootArmbandSlotOnOpen()
    {
        if (!IsInventoryScreenOpen() || !ComplexStashPanelLoaded)
        {
            return;
        }

        var slot = UiMappings.getComplexLootUI_Mappings();
        if (slot == null)
        {
            ComplexStashPanelLoaded = false;
            return;
        }

        LootArmbandSlot = slot;
        RefreshAndTrack(slot, UiMappings.lootArmBand, UiMappings.lootBeltSlot, ref _lootArmbandItemId);
    }

    public void SetInsuranceArmbandSlot()
    {
        RefreshStaticScreen(
            UiMappings.getInsuranceScreen_Mappings(),
            UiMappings.insuranceArmBand,
            UiMappings.insuranceBelt);
    }

    public void SetBuildsArmbandSlot()
    {
        RefreshStaticScreen(
            UiMappings.getBuildPanel_Mappings(),
            UiMappings.buildArmbandSlot,
            UiMappings.buildBeltSlot);
    }

    public void SetDeployArmbandSlot()
    {
        if (IsScav)
        {
            return;
        }

        var slot = UiMappings.getDeployPanel_Mappings();
        if (slot == null)
        {
            IsScav = true;
            return;
        }

        RefreshStaticScreen(slot, UiMappings.deployArmbandSlot, UiMappings.deployBeltSlot);
    }

    private void RefreshStaticScreen(Slot? slot, GameObject armbandUi, GameObject beltUi)
    {
        if (!TryGetContainedItem(slot, out _))
        {
            return;
        }

        RefreshBeltSlot(slot!, armbandUi, beltUi);
    }

    private void RefreshAndTrack(
        Slot slot,
        GameObject armbandUi,
        GameObject beltUi,
        ref string? trackedItemId)
    {
        if (!TryGetContainedItem(slot, out var item))
        {
            trackedItemId = null;
            RefreshEmptySlot(armbandUi, beltUi);
            return;
        }

        RefreshBeltSlot(slot, armbandUi, beltUi);
        trackedItemId = item.Id;
    }

    private void RefreshIfChanged(
        Slot slot,
        GameObject armbandUi,
        GameObject beltUi,
        ref string? trackedItemId)
    {
        if (!TryGetContainedItem(slot, out var item))
        {
            if (trackedItemId != null)
            {
                trackedItemId = null;
                DebugLog("Armband slot was emptied.");
            }

            RefreshEmptySlot(armbandUi, beltUi);
            return;
        }

        // Reapply every ItemView.Update in case EFT rebuilds/resets the SlotView state.
        RefreshBeltSlot(slot, armbandUi, beltUi);

        if (trackedItemId == item.Id)
        {
            return;
        }

        trackedItemId = item.Id;
        DebugLog($"Armband item changed: {item.Id}; IsContainer={item.IsContainer}");
    }

    private void RefreshBeltSlot(Slot slot, GameObject targetArmband, GameObject targetBelt)
    {
        var item = slot.ContainedItem;
        var isBelt = item != null && item.IsContainer;

        // true = empty/hidden-item visual
        // false = full/shown-item visual

        if (isBelt)
        {
            // Container in armband slot: display it as the belt.
            UiMappings.toggleArmBandSlotFull(showEmptyState: true, targetArmband);
            UiMappings.toggleBeltSlotFull(showEmptyState: false, targetBelt);
            return;
        }

        // Normal non-container armband: display it as the armband.
        UiMappings.toggleArmBandSlotFull(showEmptyState: false, targetArmband);
        UiMappings.toggleBeltSlotFull(showEmptyState: true, targetBelt);
    }
    private void RefreshEmptySlot(GameObject targetArmband, GameObject targetBelt)
    {
        // No item in either representation.
        UiMappings.toggleArmBandSlotFull(showEmptyState: true, targetArmband);
        UiMappings.toggleBeltSlotFull(showEmptyState: true, targetBelt);
    }
    private bool IsGameReady()
    {
        return Singleton<CommonUI>.Instantiated
            && Singleton<PreloaderUI>.Instantiated
            && InventoryScreenLoaded;
    }

    private static bool IsInventoryScreenOpen()
    {
        return Singleton<CommonUI>.Instantiated
            && Singleton<CommonUI>.Instance.InventoryScreen.isActiveAndEnabled;
    }

    private static bool IsScavengerTransferScreenOpen()
    {
        if (!Singleton<CommonUI>.Instantiated)
        {
            return false;
        }

        var screen = Singleton<CommonUI>.Instance.ScavengerInventoryScreen;
        return screen.isActiveAndEnabled
            && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "EmptyScene"
            && EftScreenManager.Instance.CurrentScreenController.ScreenType == EEftScreenType.ScavInventory;
    }

    private static bool TryGetContainedItem(Slot? slot, out Item item)
    {
        item = null!;

        if (slot == null || slot.Items.IsNullOrEmpty() || slot.ContainedItem == null)
        {
            return false;
        }

        item = slot.ContainedItem;
        return true;
    }

    private void DebugLog(string message)
    {
        if (EnableLogging)
        {
            Log.LogInfo(message);
        }
    }
}