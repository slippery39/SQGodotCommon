using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class BattlefieldZone : PanelContainer
{
	private HBoxContainer _container = null!;

	public event Action<int>? CardClicked;
	public event Action<int>? CardRightClicked;

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

	public void Refresh(
		IEnumerable<Card> cards,
		GameState state,
		int? selectedId = null,
		IEnumerable<int> targetHighlightIds = null,
		IEnumerable<int> additionalCostHighlightIds = null
	)
	{
		foreach (var child in _container.GetChildren())
			child.QueueFree();

		foreach (var card in cards)
			_container.AddChild(
				CreateCreaturePlaceholder(
					card,
					state,
					selectedId,
					targetHighlightIds,
					additionalCostHighlightIds
				)
			);
	}

	private PanelContainer CreateCreaturePlaceholder(
		Card card,
		GameState state,
		int? selectedId,
		IEnumerable<int> targetHighlightIds,
		IEnumerable<int> additionalCostHighlightIds
	)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(110, 150);

		var creature = card.GetComponent<CreatureComponent>();

		// Color priority: orange (cost selection) > yellow (target) > green (selected) > gray (sick) > white
		if (additionalCostHighlightIds != null && additionalCostHighlightIds.Contains(card.Id))
			panel.Modulate = new Color(1f, 0.65f, 0.1f, 1f);
		else if (targetHighlightIds != null && targetHighlightIds.Contains(card.Id))
			panel.Modulate = new Color(1f, 1f, 0.3f, 1f);
		else if (card.Id == selectedId)
			panel.Modulate = new Color(0.4f, 1f, 0.4f, 1f);
		else if (creature is { HasSummoningSickness: true })
			panel.Modulate = new Color(0.6f, 0.6f, 0.6f, 1f);

		var cardId = card.Id;
		panel.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.Left)
					CardClicked?.Invoke(cardId);
				else if (mb.ButtonIndex == MouseButton.Right)
					CardRightClicked?.Invoke(cardId);
			}
		};

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

		if (creature != null)
		{
			var effectiveStats = state.GetEffectiveStats(card.Id);
			var statsText = $"{effectiveStats.Power}/{effectiveStats.Toughness}";
			if (creature.Damage > 0)
				statsText += $" -{creature.Damage}";

			var stats = new Label { Text = statsText };
			stats.HorizontalAlignment = HorizontalAlignment.Right;
			vbox.AddChild(stats);
		}

		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities.Any(a => !a.HasActivated))
		{
			var abilityLabel = new Label { Text = "[A]" };
			abilityLabel.AddThemeFontSizeOverride("font_size", 11);
			abilityLabel.Modulate = new Color(0.6f, 0.9f, 1f, 1f);
			vbox.AddChild(abilityLabel);
		}

		return panel;
	}
}
