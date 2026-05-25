using BeltSlot.Helpers;
using BeltSlot.Patches;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Screens;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeltSlot
{
    [BepInPlugin("com.trenchfoot.beltslot", "Trenchfoot-BeltSlot", "2.1.0")]
    [BepInDependency("com.SPT.core", "4.0.4")]
    [BepInDependency("com.wtt.packnstrap", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        #region Variables
        public bool enableLogging = false;
        public bool packNStrapInstalled;
        public bool iconToggle = true;
        public bool inventoryScreenLoaded = false;
        public bool complexStashPanelLoaded = false;
        public bool isSavage = false;
        private string playerID = "0000000000";
        public string? itemId = "0000000000";
        public string? raidItemId = "0000000000";
        public Item? itemToTest;
        public Item? raidItemToTest;
        public Slot playerArmbandSlot;
        public Slot lootArmbandSlot;
        public InventoryEquipment inventoryEquipment;
        public InventoryScreen inventoryScreen;
        public CommonUI commonUI;
        public CurrentScreenSingletonClass currentScreenSingletonClass = null;
        internal static Plugin Instance { get; set; }
        internal ManualLogSource Log { get; set; }
        private static UI_Mappings uiMappings;
        internal static UI_Mappings UiMappings { get => uiMappings; set => uiMappings = value; }
        #endregion

        #region Test Methods
        // Check if the inventory screen is focused, if so, return true
        private bool isInventoryScreenFocus()
        {
            var inventoryScreen = Singleton<CommonUI>.Instance.InventoryScreen;
            if (inventoryScreen.isActiveAndEnabled)
            {
                if (enableLogging)
                {
                    Log.LogInfo("Inventory screen is focused.");
                }
                return true;
            }
            return false;
        }
        // Checks if the scav loot transfer screen is open
        private bool isScavengerInventoryScreenFocus()
        {
            var scavInventoryScreen = Singleton<CommonUI>.Instance.ScavengerInventoryScreen;
            if (scavInventoryScreen.isActiveAndEnabled)
            {
                if ((getCurrentScene() == "EmptyScene"))
                {
                    if ((getCurrentScreen() == EEftScreenType.ScavInventory))
                    {
                        return true;
                    }
                    return false;
                }
                return false;
            }
            return false;
        }
        // Test the ArmBand slot, if it has an item, return true, otherwise return false
        private bool TestSlotHasItem(Slot _slot)
        {
            // null-guard added because ItemViewPatch fires every frame and
            // can race ahead of SetLootArmbandSlotOnOpen on the corpse view
            // open/close lifecycle - was spamming NREs and lagging the UI.
            if (_slot == null) return false;
            if (inventoryEquipment != null)
            {
                Slot slot = _slot;
                if (slot.Items.IsNullOrEmpty())
                {
                    itemId = "0000000000"; // Reset the item ID
                    if (enableLogging)
                    {
                        Log.LogInfo("No item in ArmBand slot");
                    }
                    return false;
                }
                else
                {
                    Item item = slot.ContainedItem;
                    itemId = item.Id;
                    if (enableLogging)
                    {
                        Log.LogInfo("Item in ArmBand slot: " + item.Id);
                    }
                    return true;
                }
            }
            Log.LogError("InventoryEquipment is null, cannot check ArmBand slot");
            return false;
        }
        // Test the ArmBand slot, if it has a compound item, return true, otherwise return false
        private bool TestItemIsCompound(Slot _slot)
        {
            if (_slot == null || _slot.ContainedItem == null) return false;
            if (inventoryEquipment != null)
            {
                //Slot slot = inventoryEquipment.GetSlot(EquipmentSlot.ArmBand);
                Slot slot = _slot;
                Item item = slot.ContainedItem;
                if (!item.IsContainer)
                {
                    if (enableLogging)
                    {
                        Log.LogInfo("Item in ArmBand slot is not a compound item: " + item.Id);
                    }
                    return false;
                }
                else
                {
                    if (enableLogging)
                    {
                        Log.LogInfo("Item in ArmBand slot is a compound item: " + item.Id);
                    }
                    return true;
                }
            }
            return false;
        }
        // Test the item in the ArmBand slot, if it has changed, return true, otherwise return false
        private bool TestItemChanged(String? item, Slot _slot)
        {
            if (_slot == null || _slot.ParentItem == null || _slot.ContainedItem == null) return false;
            string? itemTest = item;
            Slot slot = _slot;
            string owner1 = playerID; // Player ID
            string owner2 = slot.ParentItem.Id; // Check if initiator is corpse or player

            if(enableLogging)
            {
                Log.LogInfo("Owner1: " + owner1);
                Log.LogInfo("Owner2: " + owner2);
            }
            if(owner1 == owner2)
            {
                if (itemTest != slot.ContainedItem.Id)
                {
                    if (enableLogging)
                    {
                        Log.LogInfo("Item in ArmBand slot has changed, updating itemToTest");
                    }
                    itemToTest = slot.ContainedItem;
                    itemId = slot.ContainedItem.Id;
                    return true;
                }
                return false;
            }
            else
            {
                if (itemTest != slot.ContainedItem.Id)
                    {
                    if (enableLogging)
                    {
                        Log.LogInfo("Item in ArmBand slot has changed, updating itemToTest");
                    }
                    raidItemToTest = slot.ContainedItem;
                    raidItemId = slot.ContainedItem.Id;
                    return true;
                }
                return false;
            }
        }
        // Check if the current scene is a raid scene, if so, return true, otherwise return false
        private bool testInRaidScene()
        {
            string _currentScene = getCurrentScene();
            switch (_currentScene)
            {
                case "Factory_Rework_Day_Scripts":
                case "Factory_Rework_Night_Scripts":
                case "Sandbox_Scripts":
                case "City_Scripts":
                case "Shopping_Mall_Scripts":
                case "custom_Scripts":
                case "woods_Scripts":
                case "Reserve_Base_Scripts":
                case "Lighthouse_Scripts":
                case "shoreline_scripts":
                case "Laboratory_Scripts":
                    return true;
                default: 
                    return false;
            }
        }
        // Check if the game is ready to be played, if so, return true, otherwise return false
        private bool testGameReady()
        {
            if (!Singleton<CommonUI>.Instantiated)
            {
                return false;
            }
            if (!Singleton<PreloaderUI>.Instantiated)
            {
                return false;
            }
            if (!inventoryScreenLoaded)
            {
                return false;
            }
            return true;
        }
        // Check current scene
        private string getCurrentScene()
        {
            string _currentScene = SceneManager.GetActiveScene().name;
            if (_currentScene == null || _currentScene == string.Empty)
            {
                if (enableLogging)
                {
                    Log.LogInfo("Current scene is null or empty");
                }
                return "Unknown";
            }

            if (enableLogging)
            {
                Log.LogInfo($"Current scene: {_currentScene}");
            }

            return _currentScene;
        }
        // Check current screen type, kept for testing purposes
        private EEftScreenType getCurrentScreen()
        {
            if (currentScreenSingletonClass == null)
            {
                currentScreenSingletonClass = CurrentScreenSingletonClass.Instance;
            }

            EEftScreenType _eScreenType = currentScreenSingletonClass.CurrentScreenController.ScreenType;

            if (_eScreenType == null)
            {
                if (enableLogging)
                {
                    Log.LogInfo("Current screen is null, return none");
                }
                return EEftScreenType.None;
            }

            if (enableLogging)
            {
                Log.LogInfo($"Current screen type: {_eScreenType}");
            }
            return _eScreenType;
        }
        #endregion

        #region Belt Settings
        // layout is applied per-panel by BeltSlotInjector now. left as a
        // no-op so legacy callers don't break.
        void SetEquipmentSlots() { }
        #endregion

        private void Awake()
        {
            packNStrapInstalled = Chainloader.PluginInfos.Keys.Contains("com.wtt.packnstrap");
            var legArmorInstalled = Chainloader.PluginInfos.ContainsKey("com.manimal.legarmor");
            Settings.Init(Config, legArmorInstalled);
            Instance = this;
            Log = Logger;
            UiMappings = new UI_Mappings();
            if (legArmorInstalled)
                Log.LogInfo("[Belt Slots] LegArmor detected; using leg-armor-aware layout values");

            SetEquipmentSlots();

            // legacy cloned-armband UI patches removed - they walked to an
            // "ArmBand Slot" GameObject that no longer exists under the
            // BeltHolder pattern. BeltSlotInjectorPatch handles everything
            // they used to.

            // SlotView injection + holder hierarchy bypass patches. the
            // bypass patches make corpse-loot work without the user
            // having to search pockets first.
            new HideHolderGridPatch().Enable();
            new EquipItemWindowSlotIdPatch().Enable();
            new BeltSlotInjectorPatch().Enable();
            new BeltHolderCanModifyItemPatch().Enable();
            new BeltHolderCanPlaceInPatch().Enable();
            new BeltHolderIsSearchedPatch().Enable();
            new BeltHolderExaminedPatch().Enable();
            new BeltHolderSlotGatePatch().Enable();
            new BeltHolderIsItemKnownPatch().Enable();

            // disabled: constructing PlayerBody.EquipmentSlotClass produces
            // a duplicate Tactical Rig panel via side effects we haven't
            // untangled. keep the patch around in case we ever fix that.
            // new PlayerBodyMountBeltPatch().Enable();

            // reload-from-belt: mags, ammo, and throwables all go through
            // caller-method transpilers (the closed-generic postfix
            // approach corrupts HarmonyX dispatch across other closed
            // instantiations of the open method - see BeltCallSiteTranspilers).
            BeltCallSiteTranspilers.Apply();

            // unload destination + quick-use binding.
            new BeltUnloadDestinationPatch().Enable();
            new BeltBindablePlacePatch().Enable();
            // alt-click quick-equip routing into mod_belt.
            new BeltFindSlotPatch().Enable();

            // workaround for the belt-source-move invisible-icon bug.
            new BeltMoveRefreshPatch().Enable();

            // pick the right PackNStrap-aware variant of the
            // GetPrioritizedContainers patch.
            if (packNStrapInstalled)
            {
                new GetPrioritizedContainersPatch().Disable();
                new GetPrioritizedContainersPackNStrapPatch().Enable();
            }
            else
            {
                new GetPrioritizedContainersPackNStrapPatch().Disable();
                new GetPrioritizedContainersPatch().Enable();
            }
        }
        
        #region Update Methods
        // Updates the armband and belt slots dynamically when inventory is open and not in raid
        public void UpdatePlayerArmBandSlot()
        {
            if (!testGameReady())
            {
                return;
            }
            if (isInventoryScreenFocus())
            {
                // Test if the player is a scav, if so, return
                if (isSavage)
                {
                    return;
                }
                Slot _slot = playerArmbandSlot;
                if (TestSlotHasItem(_slot))
                {
                    RefreshBeltSlot(_slot, uiMappings.armBandSlot, uiMappings.beltSlot);
                    if (TestItemChanged(itemId, _slot))
                    {
                        Log.LogInfo("Item in ArmBand slot has changed, updating belt slot");
                        RefreshBeltSlot(_slot, uiMappings.armBandSlot, uiMappings.beltSlot);
                    }
                    return;
                }
                return;
            }
            return;
        }

        // Updates the corpse armband and belt slots dynamically when looting in raid
        public void UpdateLootArmBandSlot()
        {
            if (!testGameReady())
            {
                return;
            }
            if (!testInRaidScene())
            {
                return;
            }
            if (!complexStashPanelLoaded)
            {
                return;
            }
            if (isInventoryScreenFocus())
            {
                Slot _slot = lootArmbandSlot;
                if (TestSlotHasItem(_slot))
                {
                    RefreshBeltSlot(_slot, uiMappings.lootArmBand, uiMappings.lootBeltSlot);
                    if (TestItemChanged(raidItemId, _slot))
                    {
                        RefreshBeltSlot(_slot, uiMappings.lootArmBand, uiMappings.lootBeltSlot);
                    }
                    return;
                }
                return;
            }
            return;
        }

        // Updates the scav loot transfer screen
        public void UpdateScavInventoryArmbandSlot()
        {
            // Dynamically checks if the player has a scav that can have an armband and belt slot,
            // and updates the loot transfer screen accordingly
            if (isSavage)
            {
                return;
            }
            if (!testGameReady())
            {
                return;
            }
            if (isScavengerInventoryScreenFocus())
            {
                Slot _slot = UiMappings.getScavLootTransferUI_Mappings();
                if (_slot == null)
                {
                    isSavage = true;
                    return;
                }
                if (TestSlotHasItem(_slot))
                {
                    RefreshBeltSlot(_slot, uiMappings.scavArmBandSlot, uiMappings.scavBeltSlot);
                    if (TestItemChanged(itemId, _slot))
                    {
                        Log.LogInfo("Item in ArmBand slot has changed, updating belt slot");
                        RefreshBeltSlot(_slot, uiMappings.scavArmBandSlot, uiMappings.scavBeltSlot);
                    }
                    return;
                }
                return;
            }
            return;
        }
        #endregion

        #region Inventory Setting Methods
        // Sets the armband and belt slots when the inventory is opened
        public void SetPlayerArmbandSlotOnOpen()
        {
            if (isInventoryScreenFocus())
            {
                // Test if the player is a scav, if so, return
                if (isSavage)
                {
                    return;
                }
                Slot _slot = UiMappings.getInventoryContainer_Mappings();
                if (_slot == null)
                {
                    isSavage = true;
                    return;
                }
                playerID = _slot.ParentItem.Id;
                playerArmbandSlot = _slot;
                if (TestSlotHasItem(_slot))
                {
                    RefreshBeltSlot(_slot, uiMappings.armBandSlot, uiMappings.beltSlot);
                    itemToTest = _slot.ContainedItem;
                    itemId = _slot.ContainedItem.Id;
                }
                else
                {                     
                    itemToTest = null; // Reset the item ID
                    itemId = "0000000000"; // Reset the item ID
                }
            }
        }

        // Sets the corpse armband and belt slots when the looting screen is opened
        public void SetLootArmbandSlotOnOpen()
        {
            if (isInventoryScreenFocus())
            {
                if(!complexStashPanelLoaded)
                {
                    return;
                }
                Slot _slot = UiMappings.getComplexLootUI_Mappings();
                if (_slot == null)
                {
                    complexStashPanelLoaded = false;
                    return;
                }
                lootArmbandSlot = _slot;
                if (TestSlotHasItem(_slot))
                {
                    RefreshBeltSlot(_slot, uiMappings.lootArmBand, uiMappings.lootBeltSlot);
                    raidItemToTest = _slot.ContainedItem;
                    raidItemId = _slot.ContainedItem.Id;
                }
                else
                {
                    raidItemToTest = null; // Reset the item ID
                    raidItemId = "0000000000"; // Reset the item ID
                }
            }
        }

        // Sets the armband and belt slots when the insurance screen is opened
        public void SetInsuranceArmbandSlot()
        {
            Slot _slot = UiMappings.getInsuranceScreen_Mappings();
            if (!TestSlotHasItem(_slot))
            {
                return;
            }
            RefreshBeltSlot(_slot, uiMappings.insuranceArmBand, uiMappings.insuranceBelt);
        }

        // Sets the armband and belt slots when the builds screen is opened
        public void SetBuildsArmbandSlot()
        {
            Slot _slot = UiMappings.getBuildPanel_Mappings();
            if (!TestSlotHasItem(_slot))
            {
                return;
            }
            RefreshBeltSlot(_slot, uiMappings.buildArmbandSlot, uiMappings.buildBeltSlot);
        }

        // Sets the armband and belt slots when the time has come screen is opened
        public void SetDeployArmbandSlot()
        {
            // Test if the player is a scav, if so, return
            if (isSavage)
            {
                return;
            }
            Slot _slot = UiMappings.getDeployPanel_Mappings();
            if (_slot == null)
            {
                isSavage = true;
                return;
            }
            if(!TestSlotHasItem(_slot))
            {
                return;
            }
            // Add check for scav
            RefreshBeltSlot(_slot, uiMappings.deployArmbandSlot, uiMappings.deployBeltSlot);
        }
        #endregion

        // Refreshes the belt slot's visual state.
        //
        // pre-refactor: the BELT slot was a clone of the ArmBand slot bound
        // to equipment.GetSlot(ArmBand). only one of armband/belt could be
        // worn at a time so this method toggled BOTH visuals - showing the
        // belt look when a compound (belt) sat in the armband slot, and
        // showing an armband look otherwise.
        //
        // post-refactor: the BELT slot is now bound to BeltHolder.mod_belt
        // via UI_Mappings.RebindOrFallback, which is independent of the
        // real ArmBand slot. so:
        //   - the actual armband slot (in the Gear Panel) renders itself
        //     and we never touch it (targetArm is left alone).
        //   - the belt slot only ever contains belts (the holder filter
        //     enforces it), so we always force it into "belt mode".
        //
        // targetArm stays in the signature so the call sites in Plugin.cs
        // dont change shape; just unused now.
        private void RefreshBeltSlot(Slot _slot, GameObject targetArm, GameObject targetBelt)
        {
            UiMappings.toggleBeltSlotFull(!iconToggle, targetBelt);
        }
    }
}
