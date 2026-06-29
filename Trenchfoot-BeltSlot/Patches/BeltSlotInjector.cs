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
    // injects the BELT SlotView into ContainersPanel. mirrors LegArmor's
    // EquipmentTab approach: clone the panel's default SlotView template,
    // bind to BeltHolder.mod_belt, position relative to pockets per
    // config. one entry point covers every screen that uses
    // ContainersPanel (inventory, corpse loot, insurance, deploy, builds).
    public static class BeltSlotInjector
    {
        // initial GameObject name before SlotView.Show renames it to
        // "{slot.ID} Slot". identification across re-Shows uses
        // sv.Slot.ID, not the name.
        private const string InjectedName = "BeltSlotView";
        private const string SpacerName = "BeltSlotSpacer";
        private const string PocketsSpacerName = "BeltPocketsSpacer";

        private static readonly FieldInfo SlotViewsContainerField =
            AccessTools.Field(typeof(ContainersPanel), "_slotViewsContainer");
        private static readonly FieldInfo DefaultSlotTemplateField =
            AccessTools.Field(typeof(ContainersPanel), "_defaultSlotTemplate");

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
                if (beltSlot == null) return;

                var container = SlotViewsContainerField.GetValue(panel) as Transform;
                var template = DefaultSlotTemplateField.GetValue(panel) as SlotView;
                if (container == null || template == null) return;

                var slotView = FindOrCreate(container, template);
                if (slotView == null) return;

                slotView.Show(beltSlot, parentContext, inventoryController, ItemUiContext.Instance, skills, insurance, !inRaid);
                SetHeaderText(slotView, "BELT");
                PositionRelativeToPockets(slotView.transform, container);
                // capper removed - couldnt find a LayoutElement
                // configuration that shrunk the empty slot without
                // breaking vanilla's filled-state sizing on subsequent
                // empty<->filled cycles. accepting the empty-slot gap
                // as the price of stable filled-state layout.
                // StripCapperRemnants clears any LayoutElement left
                // over from prior dll versions.
                StripCapperRemnants(slotView);

                // corpse loot only: apply user spacer + offset. detect via
                // reference compare since corpse loot passes the bot's
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

        // idempotent: match by Slot.ID, not GameObject name (SlotView.Show
        // renames the GO after we Show() it).
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

        // SetAsLastSibling first so SetSiblingIndex always moves the belt
        // "later -> earlier" through the VLG. without it, re-Shows put
        // the belt below pockets every time after the first open.
        private static void PositionRelativeToPockets(Transform beltTransform, Transform container)
        {
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
            if (pocketsIdx < 0) return;

            var targetIdx = Settings.BeltSlotLocation.Value == BeltSlotLocationOption.AbovePockets
                ? pocketsIdx
                : pocketsIdx + 1;

            beltTransform.SetSiblingIndex(targetIdx);
        }

        // spacer + per-frame offsetter so corpse-loot layout survives
        // re-flows (search reveal, drag/drop) without re-injecting.
        private static void ApplyCorpseLayoutTuning(Transform beltTransform)
        {
            var beltRt = beltTransform as RectTransform;
            if (beltRt == null) return;

            if (Settings.InjectBeltSpacer)
                EnsureSpacerBefore(beltRt, Mathf.Max(0f, Settings.BeltSpacerHeight));
            else
                DestroySpacerBefore(beltRt);

            var offsetter = beltRt.GetComponent<BeltSlotOffsetter>();
            if (offsetter == null) offsetter = beltRt.gameObject.AddComponent<BeltSlotOffsetter>();
            offsetter.OffsetFn = () => Settings.BeltSlotOffsetY;
        }

        // height cap for an empty belt slot. without it the
        // SearchableSlotView's child panels (_gridsContainer,
        // _specSlotsPanel) keep their layout space and leave a big gap
        // between belt and pockets.
        private const float EmptySlotPreferredHeight = 100f;

        // attaches a live capper. ApplyEmptyHeightCap used to fire once
        // per ContainersPanel.Show, so if the slot filled mid-session the
        // cap stayed at the empty height and the grids overflowed into
        // pockets. capper subscribes to OnAddOrRemoveItem and updates.
        // cleans up the capper and any LayoutElement we added to the
        // SlotView in prior sessions. NOT a no-op: stale state from
        // previous dll versions would otherwise persist across the
        // session that drops the capper. only removes the capper's
        // own LayoutElements - leaves any vanilla template
        // LayoutElement alone.
        private static void StripCapperRemnants(SlotView slotView)
        {
            var capper = slotView.GetComponent<BeltSlotLayoutCapper>();
            if (capper != null) UnityEngine.Object.DestroyImmediate(capper);
            // walk all LayoutElements in case prior versions left an
            // extra one with custom priority. only kill the highest-
            // priority one (ours), spare priority<=1 (templates).
            var elements = slotView.GetComponents<UnityEngine.UI.LayoutElement>();
            foreach (var le in elements)
            {
                if (le != null && le.layoutPriority >= 2)
                    UnityEngine.Object.DestroyImmediate(le);
            }
        }

        // pockets gets a spacer (sibling LayoutElement) instead of a
        // runtime offsetter. spacer is VLG-native and survives reflows;
        // offsetter compounded during the search-reveal animation and
        // sent pockets off-screen.
        private static void ApplyPocketsOffset(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var c = container.GetChild(i);
                var sv = c.GetComponent<SlotView>();
                if (sv?.Slot?.ID != "Pockets") continue;

                var rt = c as RectTransform;
                if (rt == null) return;

                var offsetVal = Mathf.Max(0f, Settings.PocketsSlotOffsetY);
                if (offsetVal > 0f)
                    EnsureNamedSpacerBefore(rt, offsetVal, PocketsSpacerName);
                else
                    DestroyNamedSpacerBefore(rt, PocketsSpacerName);

                // tear down any leftover runtime offsetter from old versions.
                var legacyOffsetter = rt.GetComponent<BeltSlotOffsetter>();
                if (legacyOffsetter != null) UnityEngine.Object.Destroy(legacyOffsetter);
                return;
            }
        }

        private static void DestroySpacerBefore(RectTransform slotRt)
            => DestroyNamedSpacerBefore(slotRt, SpacerName);

        private static void EnsureSpacerBefore(RectTransform slotRt, float height)
            => EnsureNamedSpacerBefore(slotRt, height, SpacerName);

        private static void DestroyNamedSpacerBefore(RectTransform slotRt, string spacerName)
        {
            var parent = slotRt.parent;
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == spacerName)
                {
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                    return;
                }
            }
        }

        private static void EnsureNamedSpacerBefore(RectTransform slotRt, float height, string spacerName)
        {
            var parent = slotRt.parent;
            if (parent == null) return;

            Transform spacer = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == spacerName) { spacer = c; break; }
            }

            if (spacer == null)
            {
                var go = new GameObject(spacerName, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
                go.transform.SetParent(parent, false);
                spacer = go.transform;
            }

            // same SetAsLastSibling trick as the belt position: ensures
            // SetSiblingIndex always moves later -> earlier.
            spacer.SetAsLastSibling();
            spacer.SetSiblingIndex(slotRt.GetSiblingIndex());

            var le = spacer.GetComponent<UnityEngine.UI.LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0;
        }

        // path is headerPanel(0) -> slotViewHeader(1) -> text(2).
        // TMP_Text base avoids a UnityEngine.UI assembly dep.
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
                var tmp = slotName.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
            catch { /* best effort */ }
        }
    }

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
