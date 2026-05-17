using System.Linq;
using System.Reflection;
using BeltSlot.Helpers;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using TMPro;
using UnityEngine;

namespace BeltSlot.Patches
{
    // mirrors LegArmor's EquipmentTabShowPatch but for ContainersPanel.
    // postfix on ContainersPanel.Show clones a SlotView from the panel's
    // default template, parents it next to the pockets row inside the
    // _slotViewsContainer, calls Show with BeltHolder.mod_belt, and
    // SetSiblingIndex-es it above or below pockets per the config.
    //
    // chose this site because:
    //   - postfixes on ContainersPanel.Show have always worked (the legacy
    //     PackNStrap code already used one); only prefixes broke the render
    //   - the same patch handles every screen that uses ContainersPanel:
    //     player inventory, corpse loot (ComplexStashPanel), insurance,
    //     deploy preview, equipment builds
    //   - no need to touch ContainersPanel.equipmentSlot_0 - vanilla slot
    //     iteration is left alone
    public static class BeltSlotInjector
    {
        // initial name we set on the cloned GameObject before SlotView.Show
        // runs. note Show overwrites base.name to "{slot.ID} Slot" - so
        // identification across re-Shows uses sv.Slot.ID instead (see
        // FindOrCreate). this constant just affects the first-frame name
        // before Show runs.
        private const string InjectedName = "BeltSlotView";

        // reflection handles for ContainersPanel private fields.
        private static readonly FieldInfo SlotViewsContainerField =
            AccessTools.Field(typeof(ContainersPanel), "_slotViewsContainer");
        private static readonly FieldInfo DefaultSlotTemplateField =
            AccessTools.Field(typeof(ContainersPanel), "_defaultSlotTemplate");

        // call from ContainersPanelPatch2's postfix. all the args mirror the
        // ones passed to ContainersPanel.Show so we can hand them to SlotView.Show.
        public static void Inject(
            ContainersPanel panel,
            ItemContextAbstractClass parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid)
        {
            try
            {
                if (panel == null || equipment == null) return;

                var beltSlot = BeltHolderHelper.GetBeltSlot(equipment);
                if (beltSlot == null) return; // no holder for this equipment - skip silently

                var container = SlotViewsContainerField.GetValue(panel) as Transform;
                var template = DefaultSlotTemplateField.GetValue(panel) as SlotView;
                if (container == null || template == null) return;

                var slotView = FindOrCreate(container, template);
                if (slotView == null) return;

                // bind to the belt holder slot. Show handles the rest (header,
                // item view, owner registration, search button if searchable).
                slotView.Show(beltSlot, parentContext, inventoryController, ItemUiContext.Instance, skills, insurance, !inRaid);

                // override the header text - SlotView.Show + SlotViewHeader.Show
                // both wrote "MOD_BELT" (the slot ID localized). vanilla PackNStrap
                // did the same override after binding; we keep it.
                SetHeaderText(slotView, "BELT");

                // position above/below pockets per the user's config.
                PositionRelativeToPockets(slotView.transform, container, panel);

                // corpse-loot view only: apply user-tunable spacer + offset.
                // player's own inventory uses the simpler SetSiblingIndex
                // placement above and doesn't need fine-tuning. detect via
                // ReferenceEquals because corpse loot passes the BOT's
                // equipment, not the player controller's.
                var isOwnView = ReferenceEquals(equipment, inventoryController?.Inventory?.Equipment);
                if (!isOwnView)
                {
                    ApplyCorpseLayoutTuning(slotView.transform);
                    ApplyPocketsOffset(container);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Instance?.Log?.LogError($"[Belt Slots] BeltSlotInjector.Inject threw: {ex}");
            }
        }

        // find an existing clone (created on a previous Show) or instantiate
        // a fresh one. idempotent so re-Shows don't accumulate ghosts.
        //
        // matches by sv.Slot.ID == "mod_belt" rather than GameObject name -
        // SlotView.Show renames the gameObject to "{slot.ID} Slot" right
        // after we Show() it, so a name-based check would miss the existing
        // clone on the second call and we'd stack a new one every time.
        private static SlotView FindOrCreate(Transform container, SlotView template)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var sv = container.GetChild(i).GetComponent<SlotView>();
                if (sv?.Slot?.ID == BeltHolderHelper.BeltSlotName) return sv;
            }

            var clone = Object.Instantiate(template.gameObject, container, false);
            clone.name = InjectedName;
            return clone.GetComponent<SlotView>();
        }

        // ContainersPanel arranges its children via a vertical layout group
        // where lower sibling index = higher on screen. find the Pockets
        // SlotView by ID and place ours at the right index relative to it.
        //
        // SetAsLastSibling first so the subsequent SetSiblingIndex always
        // moves the belt "from later -> earlier" through the list. without
        // this, on re-Shows the existing belt is at a lower index than
        // Pockets, our pocketsIdx is calculated BEFORE the belt is moved
        // out of the way, and SetSiblingIndex(pocketsIdx) lands belt AFTER
        // pockets's (now-shifted-up) position. result: belt below pockets
        // every time after the first open. same trick LegArmor's spacer
        // injection uses.
        private static void PositionRelativeToPockets(Transform beltTransform, Transform container, ContainersPanel panel)
        {
            // park belt at the end first so all the other slots have stable
            // indices we can reason about.
            beltTransform.SetAsLastSibling();

            int pocketsIdx = -1;
            for (int i = 0; i < container.childCount; i++)
            {
                var c = container.GetChild(i);
                if (c == beltTransform) continue;
                var sv = c.GetComponent<SlotView>();
                if (sv?.Slot?.ID == "Pockets")
                {
                    pocketsIdx = i;
                    break;
                }
            }
            if (pocketsIdx < 0) return; // no pockets in this panel, just leave belt at end

            // AbovePockets => belt at pockets' current index (pockets shifts down).
            // BelowPockets => belt at pockets' index + 1 (right after pockets).
            var targetIdx = Settings.BeltSlotLocation.Value == BeltSlotLocationOption.AbovePockets
                ? pocketsIdx
                : pocketsIdx + 1;

            beltTransform.SetSiblingIndex(targetIdx);
        }

        // corpse-loot tuning. injects a spacer GameObject before the belt
        // slot (preferredHeight = config.BeltSpacerHeight) and attaches a
        // BeltSlotOffsetter watchdog component (re-applies config.BeltSlotOffsetY
        // on top of VLG's natural position each frame).
        //
        // both adjustments are reflow-aware - if the panel re-runs its
        // layout for any reason (item search reveal, drag/drop, etc.), the
        // spacer + watchdog combo keeps the belt where the user wants it.
        private static void ApplyCorpseLayoutTuning(Transform beltTransform)
        {
            var beltRt = beltTransform as RectTransform;
            if (beltRt == null) return;

            // spacer right before the belt slot - height controlled by config.
            // skipped entirely if InjectBeltSpacer is off (lets the user A/B
            // the layout with vs without a spacer GameObject at all). if a
            // spacer exists from a previous Show, destroy it so toggling
            // off takes effect immediately.
            if (Settings.InjectBeltSpacer)
            {
                EnsureSpacerBefore(beltRt, Mathf.Max(0f, Settings.BeltSpacerHeight));
            }
            else
            {
                DestroySpacerBefore(beltRt);
            }

            // watchdog re-applies the fine-tune Y offset every LateUpdate.
            // idempotent: GetComponent reuses the existing one if present.
            var offsetter = beltRt.GetComponent<BeltSlotOffsetter>();
            if (offsetter == null) offsetter = beltRt.gameObject.AddComponent<BeltSlotOffsetter>();
            offsetter.OffsetFn = () => Settings.BeltSlotOffsetY;
        }

        // attach the offsetter watchdog to the Pockets SlotView in corpse
        // loot views so config.PocketsSlotOffsetY tweaks live. Belt mod
        // owns this offset across both standalone and LegArmor-installed
        // scenarios; LegArmor's WithBeltSlot.PocketsSlot is zeroed when
        // Belt is detected so they don't stack.
        private static void ApplyPocketsOffset(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var c = container.GetChild(i);
                var sv = c.GetComponent<SlotView>();
                if (sv?.Slot?.ID != "Pockets") continue;

                var rt = c as RectTransform;
                if (rt == null) return;

                var offsetter = rt.GetComponent<BeltSlotOffsetter>();
                if (offsetter == null) offsetter = rt.gameObject.AddComponent<BeltSlotOffsetter>();
                offsetter.OffsetFn = () => Settings.PocketsSlotOffsetY;
                return;
            }
        }

        // destroy any existing spacer in the same parent. used when the
        // user disables InjectBeltSpacer at runtime so the layout reverts
        // to "no spacer at all" on the next panel Show.
        private static void DestroySpacerBefore(RectTransform slotRt)
        {
            var parent = slotRt.parent;
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == SpacerName)
                {
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                    return;
                }
            }
        }

        // creates (or reuses) a named GameObject before slotRt in its parent's
        // sibling order with a LayoutElement at the given preferred height.
        // height = 0 effectively zeroes the offset (LayoutElement contributes
        // nothing). copied from LegArmor's EnsureSpacerBefore.
        private const string SpacerName = "BeltSlotSpacer";
        private static void EnsureSpacerBefore(RectTransform slotRt, float height)
        {
            var parent = slotRt.parent;
            if (parent == null) return;

            Transform spacer = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == SpacerName) { spacer = c; break; }
            }

            if (spacer == null)
            {
                var go = new GameObject(SpacerName, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
                go.transform.SetParent(parent, false);
                spacer = go.transform;
            }

            // park spacer at end first so SetSiblingIndex always moves it
            // "from later -> earlier" - otherwise if spacer is already right
            // before the slot, SetSiblingIndex(slotIdx) swaps them and the
            // spacer lands after the slot.
            spacer.SetAsLastSibling();
            spacer.SetSiblingIndex(slotRt.GetSiblingIndex());

            var le = spacer.GetComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0;
        }

        // walks the header tree the same way the legacy setBeltSlot_Settings
        // did (child(0)=headerPanel, child(1)=slotViewHeader, child(2)=text).
        // if the path doesnt match (different prefab layout), this is a no-op.
        private static void SetHeaderText(SlotView slotView, string text)
        {
            try
            {
                var t = slotView.transform;
                if (t.childCount < 1) return;
                var headerPanel = t.GetChild(0);
                if (headerPanel.childCount < 2) return;
                var slotViewHeader = headerPanel.GetChild(1);
                if (slotViewHeader.childCount < 3) return;
                var slotName = slotViewHeader.GetChild(2);
                // use TMP_Text (base class) to avoid taking a UnityEngine.UI
                // assembly dep just for the UGUI subclass. the runtime
                // component is still TextMeshProUGUI; we just cast up.
                var tmp = slotName.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
            catch { /* best effort */ }
        }
    }

    // postfix that triggers the injection. swaps the existing
    // ContainersPanelPatch2's no-op postfix for actual belt setup.
    public class BeltSlotInjectorPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }

        [PatchPostfix]
        private static void Postfix(
            ContainersPanel __instance,
            ItemContextAbstractClass parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid)
        {
            BeltSlotInjector.Inject(__instance, parentContext, equipment, inventoryController, skills, insurance, inRaid);
        }
    }
}
