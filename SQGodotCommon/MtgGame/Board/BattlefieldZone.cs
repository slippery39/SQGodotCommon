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
	/// Visual scale of full cards in this zone — the graveyard viewer's, since the battlefield
	/// shows <see cref="BoardToken"/>s (<see cref="AsTokens"/>) and ignores it.
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
		// Tokens carry their own glow padding, and the row height is tight (MtgVisualDesign.md).
		margin.AddThemeConstantOverride("margin_top", AsTokens ? 0 : 12);
		margin.AddThemeConstantOverride("margin_bottom", AsTokens ? 0 : 12);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(margin);

		var scroll = new ScrollContainer();
		// Still scrolls by drag or wheel past MinFit; the bar itself read as a stray line.
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
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
		Resized += FitRow;
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
			// Scaled as a unit when the row is too full (FitRow); containers ignore Scale, so
			// it sits on a plain Control inside the slot rather than on the slot itself.
			var holder = new Control { MouseFilter = MouseFilterEnum.Pass };
			slot.AddChild(holder);

			var attached = attachments.Contains(card.Id)
				? attachments[card.Id].ToList()
				: new List<Card>();

			// Added before the host so the host draws on top of them.
			for (var i = 0; i < attached.Count; i++)
			{
				var peek = AddCard(
					holder,
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
				holder,
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

		FitRow();
	}

	/// <summary>
	/// Shrinks every token when the row is wider than the table — Magic has no board cap, and a
	/// goblin deck put ten tokens behind a scrollbar. Below <see cref="MinFit"/> the row scrolls.
	/// Runs on every resize too: the first refresh happens before layout, when the zone is 0
	/// wide, and opening the log narrows the board. Idempotent — works from the unscaled size.
	/// </summary>
	private void FitRow()
	{
		if (!AsTokens || Size.X <= 0)
			return;

		var slots = _container
			.GetChildren()
			.OfType<Control>()
			.Where(s => !s.IsQueuedForDeletion())
			.Select(s => (Slot: s, Holder: s.GetChild<Control>(0)))
			.Select(x => (x.Slot, x.Holder, Base: x.Slot.CustomMinimumSize / x.Holder.Scale.X))
			.ToList();
		var separation = _container.GetThemeConstant("separation");
		var needed = slots.Sum(x => x.Base.X) + separation * (slots.Count - 1);
		var available = Size.X - 24;
		var fit = needed <= available ? 1f : Mathf.Max(MinFit, available / needed);

		foreach (var (slot, holder, baseSize) in slots)
		{
			holder.Scale = new Vector2(fit, fit);
			slot.CustomMinimumSize = baseSize * fit;
		}
	}

	private const float MinFit = 0.55f;

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
