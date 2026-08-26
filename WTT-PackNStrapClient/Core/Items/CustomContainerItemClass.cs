using EFT;
using EFT.InventoryLogic;
using PackNStrap.Core.Templates;
using System.Collections.Generic;
using WTTClientCommonLib.Attributes;
namespace PackNStrap.Core.Items;

[CustomParent("680fd1dae5044e670a092e16", typeof(CustomContainerItemClass), typeof(CustomContainerTemplateClass))]

public class CustomContainerItemClass : SearchableItem
{
	public CustomContainerItemClass(string id, CustomContainerTemplateClass template)
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
			foreach (var button in base.ItemInteractionButtons)
			{
				yield return button;
			}
			
			yield return EItemInfoButton.Open;

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
