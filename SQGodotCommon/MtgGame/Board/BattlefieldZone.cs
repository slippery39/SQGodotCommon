using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class BattlefieldZone : PanelContainer
{
	[Export]
	public PackedScene BoardCardScene { get; set; }

	/// <summary>
	/// Visual scale of the cards in this zone. The graveyard raises it further, since a card there
	/// has to be read rather than recognised.
	///
	/// ponytail: 0.68 is set against the height budget, not chosen for looks. MainColumn gets
	/// 0.73 of a 1080 viewport (788px), and since the player panels moved into the left rail the
	/// only fixed chrome left in the column is the top bar and the End Turn row (~75px), leaving
	/// ~713px for both rows. A row costs the card's height plus 28px of margin and border, and
	/// CustomMinimumSize is a hard floor — past ~0.74 the rows push the End Turn button off the
	/// bottom instead of shrinking. Recompute this if anything else moves back into the column.
	/// </summary>
	[Export]
	public float CardScale { get; set; } = 0.68f;

	private HBoxContainer _container = null!;

	public event Action<int>? CardClicked;
	public event Action<int>? CardRightClicked;
	public event Action<int>? CardHovered;
	public event Action<int>? CardHoverEnded;

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

		var scroll = new ScrollContainer();
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(scroll);

		_container = new HBoxContainer();
		_container.AddThemeConstantOverride("separation", 12);
		_container.MouseFilter = Control.MouseFilterEnum.Ignore;
		scroll.AddChild(_container);
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

		BoardCardScene ??= ResourceLoader.Load<PackedScene>("res://MtgGame/Board/board_card.tscn");
		if (BoardCardScene == null)
		{
			GD.PushWarning($"{Name}: BoardCardScene is not set and could not be loaded.");
			return;
		}

		foreach (var card in cards)
		{
			var boardCard = BoardCardScene.Instantiate<BoardCard>();
			// Set before entering the tree: BoardCard applies it in _Ready.
			boardCard.CardScale = CardScale;
			_container.AddChild(boardCard);
			boardCard.Clicked += id => CardClicked?.Invoke(id);
			boardCard.RightClicked += id => CardRightClicked?.Invoke(id);
			boardCard.Hovered += id => CardHovered?.Invoke(id);
			boardCard.HoverEnded += id => CardHoverEnded?.Invoke(id);
			boardCard.Refresh(
				card,
				state,
				ComputeHighlight(
					card,
					selectedId,
					targetHighlightIds,
					additionalCostHighlightIds,
					flashbackHighlightIds
				)
			);
		}
	}

	private static BoardCardHighlight ComputeHighlight(
		Card card,
		int? selectedId,
		IEnumerable<int> targetHighlightIds,
		IEnumerable<int> additionalCostHighlightIds,
		IEnumerable<int> flashbackHighlightIds
	)
	{
		var creature = card.GetComponent<CreatureComponent>();

		if (additionalCostHighlightIds?.Contains(card.Id) == true)
			return BoardCardHighlight.AdditionalCost;
		if (targetHighlightIds?.Contains(card.Id) == true)
			return BoardCardHighlight.Target;
		if (card.Id == selectedId)
			return BoardCardHighlight.Selected;
		if (creature is { HasSummoningSickness: true })
			return BoardCardHighlight.SummoningSick;
		if (flashbackHighlightIds?.Contains(card.Id) == true)
			return BoardCardHighlight.Flashback;

		return BoardCardHighlight.None;
	}
}
