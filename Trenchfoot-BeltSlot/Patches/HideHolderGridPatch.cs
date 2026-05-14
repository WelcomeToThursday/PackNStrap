using System.Reflection;
using BeltSlot.Helpers;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace BeltSlot.Patches
{
    // ContainedGridsView allocates a GridView for every grid on the pockets
    // item, including our hidden 5th grid - it renders as an empty cell that
    // pushes the rest of the pocket layout. find the bound GridView by id
    // and disable its GameObject so layout collapses.
    public class HideHolderGridPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(ContainedGridsView),
                nameof(ContainedGridsView.Show),
                new[]
                {
                    typeof(EFT.InventoryLogic.CompoundItem),
                    typeof(ItemContextAbstractClass),
                    typeof(GridView[]),
                    typeof(SlotView[]),
                    typeof(TraderControllerClass),
                    typeof(FilterPanel),
                    typeof(ItemUiContext),
                    typeof(bool),
                });
        }

        [PatchPostfix]
        private static void Postfix(GridView[] gridViews)
        {
            if (gridViews == null) return;
            foreach (var view in gridViews)
            {
                if (view == null || view.Grid == null) continue;
                if (view.Grid.ID == BeltHolderHelper.HiddenGridName)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }
    }
}
