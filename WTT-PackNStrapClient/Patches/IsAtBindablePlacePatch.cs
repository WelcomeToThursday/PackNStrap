using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection; 

namespace PackNStrap.Patches
{
    public class IsAtBindablePlacePatch : ModulePatch
    {
        private static readonly HashSet<MongoID> Items =
        [
            new("62178c4d4ecf221597654e3d"), // Red Flare
            new("624c0b3340357b5f566e8766"), // Yellow Flare
            new("6217726288ed9f0845317459"), // Green Flare
            new("62178be9d0050232da3485d9"), // White Flare
        ];

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryController),
                nameof(InventoryController.IsAtBindablePlace)
            );
        }

        [PatchPostfix]
        private static void Postfix(InventoryController __instance, ref bool __result, Item item)
        {
            if (item is CompoundItem compoundItem && compoundItem.MissingVitalParts.Any())
            {
                __result = false;
                return;
            }

            if (!__instance.Examined(item))
            {
                __result = false;
                return;
            }

            if (!IsValidItemForBinding(item))
            {
                __result = false;
                return;
            }

            __result = Common.IsItemInReachableLocation(item, __instance);
        }

        private static bool IsValidItemForBinding(Item item)
        {
            return item is Weapon
                || item is ThrowWeap
                || item is Meds
                || item is Food
                || item is Compass
                || item is PortableRangeFinder
                || item is RadioTransmitter
                || item.GetItemComponent<KnifeComponent>() != null
                || Items.Contains(item.TemplateId);
        }
    }
}