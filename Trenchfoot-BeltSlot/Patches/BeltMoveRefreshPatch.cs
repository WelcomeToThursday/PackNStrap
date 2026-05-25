using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // workaround for a visual bug where items dragged out of the belt
    // into a normal grid place correctly in the data and even get a
    // healthy destination ItemView, but the icon doesn't render until
    // the inventory is closed and reopened. root cause is somewhere in
    // the rendering pipeline downstream of method_5 - couldn't pin it.
    //
    // brute force: on OnItemAdded(Succeed) for any destination, rebuild
    // the just-created ItemView via the same method_4 call PrepareItems
    // uses on Show. in-process equivalent of close+reopen for one view.
    // applied unconditionally because by Succeed time the item's
    // CurrentAddress is already the destination so we can't cheaply
    // detect "source was belt" - cost is one extra factory call per add.
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
