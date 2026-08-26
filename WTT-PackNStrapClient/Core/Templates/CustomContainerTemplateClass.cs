using EFT.InventoryLogic;
using WTTClientCommonLib.Attributes;

namespace PackNStrap.Core.Templates;

[CustomParent("680fce2ec7b9b222270f074c", null, typeof(CustomContainerTemplateClass))]
public class CustomContainerTemplateClass : SearchableItemTemplate, IGridLayoutComponentTemplate
{
    string IGridLayoutComponentTemplate.LayoutName => LayoutName;
    public string LayoutName;
}