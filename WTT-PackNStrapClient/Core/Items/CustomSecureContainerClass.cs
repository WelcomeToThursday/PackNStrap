using EFT;
using EFT.InventoryLogic;
using PackNStrap.Core.Templates;
using System.Collections.Generic;
using WTTClientCommonLib.Attributes;
namespace PackNStrap.Core.Items;

[CustomParent("68154651f849fb4e7d816738", typeof(CustomSecureContainerClass), typeof(CustomContainerTemplateClass))]

public class CustomSecureContainerClass : SearchableItem
{
    public CustomSecureContainerClass(string id, CustomContainerTemplateClass template)
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