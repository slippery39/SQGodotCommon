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

	/// <summary>
	/// The battlefield shows permanents as compact <see cref="BoardToken"/>s; off (the graveyard
	/// viewer) it shows full cards, to be READ. There is no land row: a played land becomes
	/// permanent mana and goes to exile (PlayLandAction), so lands are never on the battlefield.
	/// </summary>
	[Export]
	public bool AsTokens { get; set; }

	/// The opponent's row sits at the BOTTOM of its zone, so both sides face each other across the
	/// centre line rather than one hugging the top of the screen.
	[Export]
	public bool HugBottom { get; set; }

	/// Room around a token for its glow, which a ScrollContainer would otherwise clip.
	private const float GlowPad = 24f;

	private HBoxContainer _container = null!;

	public event Action<int>? CardClicked;
	public event Action<int>? CardRightClicked;
	public event Action<int>? CardHovered;
	public event Action<int>? CardHoverEnded;

	public override void _Ready()
	{
		// Open table: the backdrop is the battlefield (MtgVisualDesign.md §6).
		AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

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
		_container.AddThemeConstantOverride("separation", AsTokens ? -8 : 12);
		_container.MouseFilter = Control.MouseFilterEnum.Ignore;
		_container.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		// A ScrollContainer only stretches its child vertically when the child asks to; without
		// this the row is its minimum height and HugBottom has nothing to align within.
		_container.SizeFlagsVertical = SizeFlags.ExpandFill;
		_container.Alignment = AsTokens
			? BoxContainer.AlignmentMode.Center
			: BoxContainer.AlignmentMode.Begin;
		scroll.AddChild(_container);
	}

	public void Refresh(
		IEnumerable<Card> cards,
		GameState state,
		int? selectedId = null,
		IEnumerable<int> targetHighlightIds = null,
		IEnumerable<int> additionalCostHighlightIds = null,
		IEnumerable<int> flashbackHighlightIds = null,
		bool canAttack = false
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

		var attachments = AttachmentsByHost(state);

		foreach (var card in cards)
		{
			// An Aura or Equipment is rendered tucked behind whatever it is attached to, which
			// may be a creature in the OTHER row — an Aura stays on its controller's battlefield
			// however hostile it is. Skipped here so it is not also drawn loose in this one.
			if (HostId(card) != 0)
				continue;

			var slot = new Control
			{
				MouseFilter = MouseFilterEnum.Pass,
				SizeFlagsVertical = HugBottom ? SizeFlags.ShrinkEnd : SizeFlags.ShrinkBegin,
			};
			_container.AddChild(slot);

			var attached = attachments.Contains(card.Id)
				? attachments[card.Id].ToList()
				: new List<Card>();

			// Added before the host so the host draws on top of them.
			for (var i = 0; i < attached.Count; i++)
			{
				var peek = AddCard(
					slot,
					attached[i],
					state,
					selectedId,
					targetHighlightIds,
					null,
					null,
					canAttack: false
				);
				peek.Position = Inset + new Vector2(1, 1) * AttachmentPeek * (i + 1);
			}

			var host = AddCard(
				slot,
				card,
				state,
				selectedId,
				targetHighlightIds,
				additionalCostHighlightIds,
				flashbackHighlightIds,
				canAttack
			);
			host.Position = Inset;

			// CustomMinimumSize is final once the node has entered the tree, so the slot can be
			// sized from the card rather than from a second copy of the authored dimensions.
			slot.CustomMinimumSize =
				host.CustomMinimumSize
				+ Inset * 2
				+ new Vector2(1, 1) * AttachmentPeek * attached.Count;
		}
	}

	/// Pixels an attachment sticks out from behind the permanent it is attached to.
	private const float AttachmentPeek = 12f;

	private Vector2 Inset => AsTokens ? new Vector2(GlowPad, GlowPad) : Vector2.Zero;

	private Control AddCard(
		Control parent,
		Card card,
		GameState state,
		int? selectedId,
		IEnumerable<int> targetHighlightIds,
		IEnumerable<int> additionalCostHighlightIds,
		IEnumerable<int> flashbackHighlightIds,
		bool canAttack
	)
	{
		var highlight = ComputeHighlight(
			card,
			selectedId,
			targetHighlightIds,
			additionalCostHighlightIds,
			flashbackHighlightIds
		);
		if (AsTokens)
		{
			var token = new BoardToken();
			parent.AddChild(token);
			token.Clicked += id => CardClicked?.Invoke(id);
			token.RightClicked += id => CardRightClicked?.Invoke(id);
			token.Hovered += id => CardHovered?.Invoke(id);
			token.HoverEnded += id => CardHoverEnded?.Invoke(id);
			token.Refresh(card, state, highlight, canAttack);
			return token;
		}

		var boardCard = BoardCardScene.Instantiate<BoardCard>();
		// Set before entering the tree: BoardCard applies it in _Ready.
		boardCard.CardScale = CardScale;
		parent.AddChild(boardCard);
		boardCard.Clicked += id => CardClicked?.Invoke(id);
		boardCard.RightClicked += id => CardRightClicked?.Invoke(id);
		boardCard.Hovered += id => CardHovered?.Invoke(id);
		boardCard.HoverEnded += id => CardHoverEnded?.Invoke(id);
		boardCard.Refresh(card, state, highlight);
		return boardCard;
	}

	/// <summary>
	/// Every attached Aura and Equipment in play, keyed by what it is attached to. Both
	/// battlefields are scanned because an Aura on an opposing creature is controlled by the
	/// player who cast it and so lives in the other zone.
	/// </summary>
	private static ILookup<int, Card> AttachmentsByHost(GameState state) =>
		new[] { MtgObjectKeys.Player1Battlefield, MtgObjectKeys.Player2Battlefield }
			.Select(state.GetWellKnownId)
			.Where(id => id != 0)
			.SelectMany(state.GetCardsInZone)
			.Where(c => HostId(c) != 0)
			.ToLookup(HostId);

	private static int HostId(Card card) =>
		card.GetComponent<EquipmentComponent>()?.EquippedToCardId ?? 0;

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
		// A tapped creature cannot attack, and before this it looked exactly like a ready one.
		if (creature is { IsExhausted: true })
			return BoardCardHighlight.Exhausted;
		if (flashbackHighlightIds?.Contains(card.Id) == true)
			return BoardCardHighlight.Flashback;

		return BoardCardHighlight.None;
	}
}
