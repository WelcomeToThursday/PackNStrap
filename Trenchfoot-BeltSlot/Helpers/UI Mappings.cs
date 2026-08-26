using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Matchmaker;
using TMPro;
using UnityEngine;

namespace BeltSlot.Helpers
{
    internal class UI_Mappings
    {
        #region Variables
        // Menu UI components and screens
        public GameObject containerGameObject = null;
        public GameObject equipmentGameObject = null;
        public GameObject lootContainerGameObject = null;
        public GameObject lootEquipmentGameObject = null;
        public GameObject lootArmBand = null;
        public GameObject lootBeltSlot = null;
        public GameObject slotTemplate = null;
        public GameObject beltSlot = null;
        public GameObject armBandSlot = null;
        public GameObject healthParameter = null;
        public GameObject healthPanel = null;
        public GameObject insuranceScreenContainer = null;
        public GameObject insuranceScreenGearPanel = null;
        public GameObject insuranceBelt = null;
        public GameObject insuranceArmBand = null;
        public GameObject buildPanel = null;
        public GameObject buildBeltSlot = null;
        public GameObject buildArmbandSlot = null;
        public GameObject deployPanel = null;
        public GameObject deployBeltSlot = null;
        public GameObject deployArmbandSlot = null;
        public GameObject healthPanelContainer = null;
        public GameObject scavInventoryScreen = null;
        public GameObject scavInventoryContainer = null;
        public GameObject scavArmBandSlot = null;
        public GameObject scavBeltSlot = null;
        public ToggleButton toggleButton = null;
        public InventoryScreen inventoryScreen = null;
        public EquipmentBuildsScreen equipmentBuildsScreen = null;
        public MatchmakerInsuranceScreen insuranceScreen = null;
        public PreloaderUI preloaderUI = null;
        public CommonUI commonUI = null;
        public bool noActiveWindow = false;
        #endregion

        #region Game Object Mappings
        // Mappings of the health panel in the inventory screen, currently not used
        public void setHealthPanel_Mappings()
        {
            if (inventoryScreen == null)
            {
                inventoryScreen = Singleton<CommonUI>.Instance.InventoryScreen;
            }
            healthPanel = inventoryScreen.transform.Find("Items Panel/LeftSide/Left Panel/Health Panel").gameObject;
            healthParameter = inventoryScreen.transform.Find("Items Panel/LeftSide/Left Panel/Health Parameters").gameObject;
        }

        // Mappings of the build panel in the equipment builds screen
        public Slot getBuildPanel_Mappings()
        {
            if (equipmentBuildsScreen == null)
            {
                equipmentBuildsScreen = Singleton<MenuUI>.Instance.EquipmentBuildsScreen;
            }
            buildPanel = equipmentBuildsScreen.transform.Find("Panels/Gear Panel/ViewPanel").gameObject;
            buildArmbandSlot = buildPanel.transform.Find("EquipmentScrollview/Gear Panel Build/ArmBand Slot").gameObject;
            buildBeltSlot = buildPanel.transform.Find("Containers Panel/Containers Scrollview/Content/ArmBand Slot").gameObject;
            SlotView slotView = buildBeltSlot.GetComponent<SlotView>();

            setBeltSlot_Settings(buildBeltSlot);
            return slotView.Slot;
        }

        // Mappings of the equipment panel in the deploy screen
        public Slot getDeployPanel_Mappings()
        {
            if (preloaderUI == null)
            {
                preloaderUI = Singleton<PreloaderUI>.Instance;
            }
            deployPanel = preloaderUI.transform.Find("Preloader UI/UIContext/WindowsPlaceholder/PlayerEquipmentWindow/Inner/Contents").gameObject;
            deployArmbandSlot = deployPanel.transform.Find("EquipmentScrollview/Gear Panel Build/ArmBand Slot").gameObject;
            deployBeltSlot = deployPanel.transform.Find("Containers Panel/Containers Scrollview/Content/ArmBand Slot").gameObject;
            SlotView slotView = deployBeltSlot.GetComponent<SlotView>();

            setBeltSlot_Settings(deployBeltSlot);
            return slotView.Slot;
        }

        // Mappings of the insurance screen in the matchmaker
        public Slot getInsuranceScreen_Mappings()
        {
            if(insuranceScreen == null)
            {
                insuranceScreen = Singleton<MenuUI>.Instance.MatchmakerInsuranceScreen;
            }
            insuranceScreenContainer = insuranceScreen.transform.Find("ItemsPanel/Complex Loot Panel/Containers Scrollview/Content").gameObject;
            insuranceScreenGearPanel = insuranceScreenContainer.transform.Find("Gear Panel Template(Clone)").gameObject;
            insuranceArmBand = insuranceScreenGearPanel.transform.Find("ArmBand Slot").gameObject;
            insuranceBelt = insuranceScreenContainer.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = insuranceBelt.GetComponent<SlotView>();

            setBeltSlot_Settings(insuranceBelt);
            return slotView.Slot;
        }

        // Mappings of the inventory screen
        public Slot getInventoryContainer_Mappings()
        {
            if (inventoryScreen == null)
            {
                inventoryScreen = Singleton<CommonUI>.Instance.InventoryScreen;
            }
            containerGameObject = inventoryScreen.transform.Find("Items Panel/LeftSide/Containers Panel/Scrollview Parent/Containers Scrollview/Content").gameObject;
            if(countTransformChildren(containerGameObject) < 8)
            {
                Plugin.Instance.IsScav = true;
                return null;
            }
            equipmentGameObject = inventoryScreen.transform.Find("Items Panel/LeftSide/Left Panel/Gear Panel").gameObject;
            armBandSlot = equipmentGameObject.transform.Find("ArmBand Slot").gameObject;
            beltSlot = containerGameObject.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = beltSlot.GetComponent<SlotView>();

            setBeltSlot_Settings(beltSlot);
            return slotView.Slot;
        }

        // Mappings of complex loot container view in the inventory screen
        public Slot getComplexLootUI_Mappings()
        {
            if (inventoryScreen == null)
            {
                inventoryScreen = Singleton<CommonUI>.Instance.InventoryScreen;
            }
            lootContainerGameObject = inventoryScreen.transform.Find("Items Panel/Stash Panel/Complex Loot Panel/Containers Scrollview/Content").gameObject;
            if(countTransformChildren(lootContainerGameObject) < 5)
            {
                Plugin.Instance.ComplexStashPanelLoaded = false;
                return null;
            }
            lootEquipmentGameObject = lootContainerGameObject.transform.Find("Gear Panel Template(Clone)").gameObject;
            lootArmBand = lootEquipmentGameObject.transform.Find("ArmBand Slot").gameObject;
            lootBeltSlot = lootContainerGameObject.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = lootBeltSlot.GetComponent<SlotView>();

            setBeltSlot_Settings(lootBeltSlot);
            return slotView.Slot;
        }

        public Slot getScavLootTransferUI_Mappings()
        {
            if (commonUI == null)
            {
                commonUI = Singleton<CommonUI>.Instance;
            }
            scavInventoryScreen = commonUI.transform.Find("Common UI/Scavenger Inventory Screen").gameObject;
            scavInventoryContainer = scavInventoryScreen.transform.Find("Items Panel/Containers Panel/Scrollview Parent/Containers Scrollview/Content").gameObject;
            if (countTransformChildren(scavInventoryContainer) < 8)
            {
                Plugin.Instance.IsScav = true;
                return null;
            }
            scavArmBandSlot = scavInventoryScreen.transform.Find("Items Panel/Left Panel/Gear Panel/ArmBand Slot").gameObject;
            scavBeltSlot = scavInventoryContainer.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = scavBeltSlot.GetComponent<SlotView>();

            setBeltSlot_Settings(scavBeltSlot);
            return slotView.Slot;
        }
        #endregion

        // Count the number of child transforms in a GameObject
        public int countTransformChildren(GameObject target)
        {
            if (target == null)
            {
                if(Plugin.Instance.EnableLogging)
                {
                    Plugin.Instance.Log.LogError("[Belt Slots] Target GameObject is null.");
                }
                return 0;
            }
            return target.transform.childCount;
        }

        #region Inventory Settings Methods
        // Set the settings of the belt slot, such as its position, name, and visibility
        public void setBeltSlot_Settings(GameObject targetBelt)
        {
            if(Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] setBeltSlot_Settings called for {targetBelt.name}");
            }

            if (targetBelt != null)
            {
                GameObject _headerPanel = targetBelt.transform.GetChild(0).gameObject; // Header panel of the belt slot
                GameObject _slotPanel = targetBelt.transform.GetChild(1).gameObject; // Slot panel of the belt slot
                GameObject _slotViewHeader = _headerPanel.transform.GetChild(1).gameObject; // Slot view header of the belt slot
                GameObject _slotName = _slotViewHeader.transform.GetChild(2).gameObject; // Slot name of the belt slot

                _slotName.GetComponent<TextMeshProUGUI>().text = "BELT"; // Set the slot name to "BELT"
            }
            else
            {
                return;
            }
        }

        public void toggleBeltSlotFull(bool showEmptyState, GameObject target)
        {
            GameObject _slotPanel = target.transform.GetChild(1).gameObject; // Slot panel of the belt slot
            if(_slotPanel.transform.childCount >5)
            {
                GameObject _backImage = _slotPanel.transform.GetChild(0).gameObject; // Back image of the belt slot
                GameObject _backGround = _slotPanel.transform.GetChild(1).gameObject; // Background of the belt slot
                GameObject _emptyBorder = _slotPanel.transform.GetChild(2).gameObject; // Empty border of the belt slot
                GameObject _fullBorder = _slotPanel.transform.GetChild(3).gameObject; // Full border of the belt slot
                GameObject _slotLayout = _slotPanel.transform.GetChild(4).gameObject; // Slot layout of the belt slot
                    
                _backImage.SetActive(showEmptyState); // Show the back image
                _backGround.SetActive(showEmptyState); // Show the background
                _emptyBorder.SetActive(showEmptyState); // Show the empty border
                _fullBorder.SetActive(!showEmptyState); // Hide the full border
                _slotLayout.SetActive(!showEmptyState); // Hide the slot layout
            }    
        }

        public void toggleArmBandSlotFull(bool showEmptyState, GameObject target)
        {
            GameObject _slotPanel = target.transform.GetChild(1).gameObject; // Slot panel of the armband slot
            if (target.transform.childCount > 8)
            {
                GameObject _backImage = target.transform.GetChild(4).gameObject; // Back image of the armband slot
                GameObject _backGround = target.transform.GetChild(5).gameObject; // Background of the armband slot
                GameObject _emptyBorder = target.transform.GetChild(6).gameObject; // Empty border of the armband slot
                GameObject _fullBorder = target.transform.GetChild(7).gameObject; // Full border of the armband slot
                GameObject _slotLayout = target.transform.GetChild(8).gameObject; // Slot layout of the armband slot
                    
                _backImage.SetActive(showEmptyState); // Show the back image
                _backGround.SetActive(showEmptyState); // Show the background
                _emptyBorder.SetActive(showEmptyState); // Show the empty border
                _fullBorder.SetActive(!showEmptyState); // Hide the full border
                _slotLayout.SetActive(!showEmptyState); // Hide the slot layout
            }
        }
        #endregion
    }
}
