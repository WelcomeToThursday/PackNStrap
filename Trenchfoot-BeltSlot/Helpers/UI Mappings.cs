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

            // apply the "BELT" header override AFTER reading the slot - the
            // override mutates the TMP text that SlotView.Show sets back to
            // slot.ID.Localized.
            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(buildBeltSlot);
            return rebound;
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

            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(deployBeltSlot);
            return rebound;
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

            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(insuranceBelt);
            return rebound;
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
                Plugin.Instance.isSavage = true;
                return null;
            }
            equipmentGameObject = inventoryScreen.transform.Find("Items Panel/LeftSide/Left Panel/Gear Panel").gameObject;
            armBandSlot = equipmentGameObject.transform.Find("ArmBand Slot").gameObject;
            beltSlot = containerGameObject.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = beltSlot.GetComponent<SlotView>();

            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(beltSlot);
            return rebound;
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
                Plugin.Instance.complexStashPanelLoaded = false;
                return null;
            }
            lootEquipmentGameObject = lootContainerGameObject.transform.Find("Gear Panel Template(Clone)").gameObject;
            lootArmBand = lootEquipmentGameObject.transform.Find("ArmBand Slot").gameObject;
            lootBeltSlot = lootContainerGameObject.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = lootBeltSlot.GetComponent<SlotView>();

            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(lootBeltSlot);
            return rebound;
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
                Plugin.Instance.isSavage = true;
                return null;
            }
            scavArmBandSlot = scavInventoryScreen.transform.Find("Items Panel/Left Panel/Gear Panel/ArmBand Slot").gameObject;
            scavBeltSlot = scavInventoryContainer.transform.Find("ArmBand Slot").gameObject;
            SlotView slotView = scavBeltSlot.GetComponent<SlotView>();

            var rebound = SlotOrNull(slotView);
            setBeltSlot_Settings(scavBeltSlot);
            return rebound;
        }

        // BeltSlotInjectorPatch binds the SlotView to mod_belt before any
        // of these mappings run, so by the time we read slotView.Slot here
        // it's already the belt holder slot.
        private Slot SlotOrNull(SlotView slotView) => slotView?.Slot;
        #endregion

        // Count the number of child transforms in a GameObject
        public int countTransformChildren(GameObject target)
        {
            if (target == null)
            {
                if(Plugin.Instance.enableLogging)
                {
                    Plugin.Instance.Log.LogError("[Belt Slots] Target GameObject is null.");
                }
                return 0;
            }
            return target.transform.childCount;
        }

        #region Inventory Settings Methods
        // override the slot header text to "BELT".
        public void setBeltSlot_Settings(GameObject targetBelt)
        {
            if (Plugin.Instance.enableLogging)
                Plugin.Instance.Log.LogInfo($"[Belt Slots] setBeltSlot_Settings called for {targetBelt.name}");
            if (targetBelt == null) return;

            var headerPanel = targetBelt.transform.GetChild(0);
            var slotViewHeader = headerPanel.GetChild(1);
            var slotName = slotViewHeader.GetChild(2);
            slotName.GetComponent<TextMeshProUGUI>().text = "BELT";
        }

        // swaps the empty/full visual states on the belt SlotView. child
        // indices match vanilla SlotView prefab layout.
        public void toggleBeltSlotFull(bool full, GameObject target)
        {
            var slotPanel = target.transform.GetChild(1).gameObject;
            if (slotPanel.transform.childCount <= 5) return;

            slotPanel.transform.GetChild(0).gameObject.SetActive(full);   // back image
            slotPanel.transform.GetChild(1).gameObject.SetActive(full);   // background
            slotPanel.transform.GetChild(2).gameObject.SetActive(full);   // empty border
            slotPanel.transform.GetChild(3).gameObject.SetActive(!full);  // full border
            slotPanel.transform.GetChild(4).gameObject.SetActive(!full);  // slot layout
        }

        public void toggleArmBandSlotFull(bool full, GameObject target)
        {
            if (target.transform.childCount <= 8) return;

            target.transform.GetChild(4).gameObject.SetActive(full);   // back image
            target.transform.GetChild(5).gameObject.SetActive(full);   // background
            target.transform.GetChild(6).gameObject.SetActive(full);   // empty border
            target.transform.GetChild(7).gameObject.SetActive(!full);  // full border
            target.transform.GetChild(8).gameObject.SetActive(!full);  // slot layout
        }
        #endregion
    }
}
