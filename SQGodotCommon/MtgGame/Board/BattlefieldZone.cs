using System.Collections.Generic;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class BattlefieldZone : PanelContainer
{
	private HBoxContainer _container = null!;

	public override void _Ready()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(margin);

		_container = new HBoxContainer();
		_container.AddThemeConstantOverride("separation", 12);
		margin.AddChild(_container);
	}

	public void Refresh(IEnumerable<Card> cards)
	{
		foreach (var child in _container.GetChildren())
			child.QueueFree();

		foreach (var card in cards)
			_container.AddChild(CreateCreaturePlaceholder(card));
	}

	private static PanelContainer CreateCreaturePlaceholder(Card card)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(110, 150);

		var marginInner = new MarginContainer();
		marginInner.AddThemeConstantOverride("margin_left", 6);
		marginInner.AddThemeConstantOverride("margin_right", 6);
		marginInner.AddThemeConstantOverride("margin_top", 6);
		marginInner.AddThemeConstantOverride("margin_bottom", 6);
		panel.AddChild(marginInner);

		var vbox = new VBoxContainer();
		marginInner.AddChild(vbox);

		var nameLabel = new Label { Text = card.Name };
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddChild(nameLabel);

		var creature = card.GetComponent<CreatureComponent>();
		if (creature != null)
		{
			var stats = new Label { Text = $"{creature.Power}/{creature.Toughness}" };
			stats.HorizontalAlignment = HorizontalAlignment.Right;
			vbox.AddChild(stats);
		}

		return panel;
	}
}
