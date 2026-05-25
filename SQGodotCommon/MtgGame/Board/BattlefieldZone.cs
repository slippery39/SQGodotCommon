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
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(margin);

		_container = new HBoxContainer();
		_container.AddThemeConstantOverride("separation", 12);
		_container.MouseFilter = Control.MouseFilterEnum.Ignore;
		margin.AddChild(_container);
	}

	public void Refresh(
		IEnumerable<Card> cards,
		GameState state,
		int? selectedId = null,
		IEnumerable<int> targetHighlightIds = null,
		IEnumerable<int> additionalCostHighlightIds = null,
		IEnumerable<int> flashbackHighlightIds = null
	)
	{
		foreach (var child in _container.GetChildren())
			child.QueueFree();

		foreach (var card in cards)
			_container.AddChild(
				CreateMiniCard(
					card,
					state,
					selectedId,
					targetHighlightIds,
					additionalCostHighlightIds,
					flashbackHighlightIds
				)
			);
	}

	private PanelContainer CreateMiniCard(
		Card card,
		GameState state,
		int? selectedId,
		IEnumerable<int> targetHighlightIds,
		IEnumerable<int> additionalCostHighlightIds,
		IEnumerable<int> flashbackHighlightIds
	)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(130, 186);
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.MiniCardStyle());

		var creature = card.GetComponent<CreatureComponent>();

		// Color priority: orange (cost) > yellow (target) > green (selected) > grey (sick) > default
		if (additionalCostHighlightIds != null && additionalCostHighlightIds.Contains(card.Id))
			panel.Modulate = new Color(1f, 0.65f, 0.1f, 1f);
		else if (targetHighlightIds != null && targetHighlightIds.Contains(card.Id))
			panel.Modulate = new Color(1f, 1f, 0.3f, 1f);
		else if (card.Id == selectedId)
			panel.Modulate = new Color(0.4f, 1f, 0.4f, 1f);
		else if (creature is { HasSummoningSickness: true })
			panel.Modulate = new Color(0.6f, 0.6f, 0.6f, 1f);
		else if (flashbackHighlightIds != null && flashbackHighlightIds.Contains(card.Id))
			panel.Modulate = new Color(0.8f, 0.5f, 1f, 1f);

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

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 6);
		margin.AddThemeConstantOverride("margin_right", 6);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		margin.AddChild(vbox);

		// Card name
		var nameLabel = new Label { Text = card.Name };
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.70f, 1f));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(nameLabel);

		// Art placeholder
		var artRect = new ColorRect();
		artRect.Color = MtgUiStyles.ArtBlock;
		artRect.CustomMinimumSize = new Vector2(0, 50);
		artRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		artRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(artRect);

		// Mana cost
		var manaLabel = new Label { Text = $"Mana: {card.ManaCost}" };
		manaLabel.AddThemeFontSizeOverride("font_size", 16);
		manaLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.85f, 1f));
		manaLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(manaLabel);

		// Keyword pills
		if (creature != null)
			AddKeywordPills(vbox, creature);

		var spacer = new Control();
		spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		spacer.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(spacer);

		// Activated ability indicator
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		if (
			abilities.Any(a =>
				a.MaxActivationsPerTurn == 0 || a.ActivationCount < a.MaxActivationsPerTurn
			)
		)
		{
			var abilityLabel = new Label { Text = "[A]" };
			abilityLabel.AddThemeFontSizeOverride("font_size", 16);
			abilityLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.85f, 1f, 1f));
			abilityLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			vbox.AddChild(abilityLabel);
		}

		// Power / toughness
		if (creature != null)
		{
			var stats = state.GetEffectiveStats(card.Id);
			var statsText = $"{stats.Power}/{stats.Toughness}";
			if (creature.Damage > 0)
				statsText += $" -{creature.Damage}";

			var statsLabel = new Label { Text = statsText };
			statsLabel.AddThemeFontSizeOverride("font_size", 20);
			statsLabel.HorizontalAlignment = HorizontalAlignment.Right;
			statsLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.75f, 1f));
			statsLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			vbox.AddChild(statsLabel);
		}

		panel.TooltipText = MtgCardMapper.GetRulesText(card);

		return panel;
	}

	private static void AddKeywordPills(VBoxContainer vbox, CreatureComponent creature)
	{
		var pills = new List<string>();
		if (creature.HasFlying)
			pills.Add("FLY");
		if (creature.HasHaste)
			pills.Add("HASTE");
		if (creature.HasDoubleStrike)
			pills.Add("DBLSTK");
		if (creature.HasTaunt)
			pills.Add("TAUNT");
		if (creature.HasReach)
			pills.Add("REACH");
		if (creature.HasLifelink)
			pills.Add("LINK");
		if (creature.HasTrample)
			pills.Add("TRAMPLE");

		if (pills.Count == 0)
			return;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 3);
		row.MouseFilter = Control.MouseFilterEnum.Ignore;

		foreach (var pill in pills)
		{
			var label = new Label { Text = pill };
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f, 1f));
			label.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(label);
		}

		vbox.AddChild(row);
	}
}
