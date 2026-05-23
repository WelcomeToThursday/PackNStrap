using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // works around a visual bug: items dragged out of the belt into a
    // normal grid (rig/pockets) place correctly in the data and a
    // healthy ItemView gets created, but the icon doesn't render until
    // the inventory is closed and reopened. root cause was traced to
    // something downstream of method_5 (likely an async sprite-load
    // timing thing tied to the pool path the belt-source destination
    // view takes); we couldn't pin it precisely.
    //
    // brute force: on OnItemAdded(Succeed) for any destination, rebuild
    // the just-created ItemView via the same method_4 call PrepareItems
    // uses on Show. that's the in-process equivalent of close+reopen for
    // the one affected view. method_5 (called inside method_4) handles
    // the dict bookkeeping (Remove + Kill old, Add new).
    //
    // applied unconditionally rather than only on belt-source moves
    // because by Succeed time the item's CurrentAddress is the
    // destination - source isn't directly knowable. cost is one extra
    // factory call per Succeed event, which is cheap.
    public class BeltMoveRefreshPatch : ModulePatch
    {
        private static readonly FieldInfo ItemUiContextField =
            AccessTools.Field(typeof(GridView), "itemUiContext_0");
        private static readonly FieldInfo ItemViewsField =
            AccessTools.Field(typeof(GridView), "ItemViews");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GridView), nameof(GridView.OnItemAdded));
        }

        [PatchPostfix]
        private static void Postfix(GridView __instance, GEventArgs2 eventArgs)
        {
            try
            {
                if (__instance == null || eventArgs == null) return;
                if (eventArgs.Status != CommandStatus.Succeed) return;
                if (eventArgs.To?.Container != __instance.Grid) return;

                var item = eventArgs.Item;
                if (item == null) return;
                var gclass = eventArgs.To as GClass3393;
                if (gclass == null) return;

                var itemViews = ItemViewsField?.GetValue(__instance) as IDictionary<string, ItemView>;
                if (itemViews == null || !itemViews.ContainsKey(item.Id)) return;

                var uiCtx = ItemUiContextField?.GetValue(__instance) as ItemUiContext;
                if (uiCtx == null) return;

                __instance.method_4(item, gclass.LocationInGrid, uiCtx, gclass.GetOwnerOrNull());
            }
            catch (System.Exception ex)
            {
                Plugin.Instance?.Log?.LogError($"[Belt Slots] BeltMoveRefreshPatch threw: {ex}");
            }
        }
    }
}
