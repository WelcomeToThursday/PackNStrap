using EFT;
using EFT.InventoryLogic;
using PackNStrap.Core.Templates;
using System.Collections.Generic;
using WTTClientCommonLib.Attributes;
namespace PackNStrap.Core.Items;

[CustomParent("6815465859b8c6ff13f94026", typeof(CustomBeltItemClass), typeof(CustomContainerTemplateClass))]
public class CustomBeltItemClass : SearchableItem
{
    public CustomBeltItemClass(string id, CustomContainerTemplateClass template)
        : base(id, template)
    {
        if (!string.IsNullOrEmpty(template.LayoutName))
        {
            Components.Add(new GridLayoutComponent(this, template));
        }
        Components.Add(Tag = new TagComponent(this));
    }

    public override IEnumerable<EItemInfoButton> ItemInteractionButtons
    {
        get
        {
            // Yield base buttons first
            foreach (var button in base.ItemInteractionButtons)
            {
                yield return button;
            }

            // Add container-specific buttons
            yield return EItemInfoButton.Tag;
            if (!string.IsNullOrEmpty(Tag.Name))
            {
                yield return EItemInfoButton.ResetTag;
            }
        }
    }
    [ComponentAttribute]
    public readonly TagComponent Tag;
}