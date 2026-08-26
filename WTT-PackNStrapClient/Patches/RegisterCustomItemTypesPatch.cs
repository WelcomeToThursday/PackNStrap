using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using PackNStrap.Core.Items;
using EFT.InventoryLogic;

namespace PackNStrap.Patches
{
    internal class RegisterCustomItemTypesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemSorter), nameof(ItemSorter.Init));
        }

        [PatchPostfix]
        public static void PatchPostfix()
        {
            RegisterCustomTypes();
        }

        private static void RegisterCustomTypes()
        {
            if (!ItemSorter._itemSuccessors.Contains(typeof(CustomContainerItemClass)))
            {
                ItemSorter._itemSuccessors.Add(typeof(CustomContainerItemClass));
            }

            if (!ItemSorter._itemSuccessors.Contains(typeof(CustomSecureContainerClass)))
            {
                ItemSorter._itemSuccessors.Add(typeof(CustomSecureContainerClass));
            }

            if (!ItemSorter._itemSuccessors.Contains(typeof(CustomBeltItemClass)))
            {
                ItemSorter._itemSuccessors.Add(typeof(CustomBeltItemClass));
            }
        }
    }
}