using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using DoomCore;
using Godot;
using ImmutableGameObjects;

namespace DoomGame;

/// <summary>
/// The hand, drawn with the shared `Hand2D` / `CardUI2D` fan from `Common/Cards/2D`. That code is
/// game-agnostic — roughly 1600 lines of drag, hover and fan with no MTG in it — so DOOMJAM gets
/// the whole interaction for the cost of filling in a `Details` per card.
///
/// **Presentation only.** It never decides whether a play is legal: it hands a card id and a lane
/// to the board, and the board asks the engine. What comes back is the engine's own refusal text.
/// </summary>
public sealed class DoomHandView
{
	/// <summary>How much a Card2D is shrunk to sit in one band of the board rather than fill a screen.</summary>
	public const float CardScale = 0.75f;

	/// <summary>Roughly how tall the scaled fan is, so the board can reserve room for it.</summary>
	public const int BandHeight = 380;

	private readonly Hand2D _hand;

	/// <summary>Which lane a drop point lands in, or null if it missed every lane.</summary>
	private readonly Func<Vector2, int?> _laneAt;

	/// <summary>Plays the card. Returns null when it worked, or the engine's reason when it did not.</summary>
	private readonly Func<int, int, string> _tryPlay;

	private readonly Action<string> _report;

	public DoomHandView(
		Node parent,
		Vector2 position,
		Func<Vector2, int?> laneAt,
		Func<int, int, string> tryPlay,
		Action<string> report
	)
	{
		_laneAt = laneAt;
		_tryPlay = tryPlay;
		_report = report;

		// The scene, not `new Hand2D()` — Hand.tscn already carries the position curves, the card
		// scene and the LeftMostPoint / RightMostPoint anchors that Hand2D looks up by name.
		_hand = GD.Load<PackedScene>("res://Common/Cards/2D/Hand2D/Hand.tscn")
			.Instantiate<Hand2D>();
		_hand.Position = position;

		// A Card2D is around 500px tall in the 1920x1080 base canvas — sized for a screen that is
		// all hand. Here the hand is one band of six, so it is scaled to fit rather than the board
		// being shrunk around it.
		_hand.Scale = new Vector2(CardScale, CardScale);

		// Spacing is in the hand's own space, so it has to out-pace the scale or the fan closes up
		// and the names disappear under the next card.
		_hand.CardSizeX = 290;

		parent.AddChild(_hand);

		// Hand.tscn spans its fan between these two anchors, and at +/-400 five DOOMJAM cards sit
		// closer together than they are wide, so each one buries the next one's name. The board is
		// 1920 across; spend more of it.
		_hand.GetNode<Node2D>("LeftMostPoint").Position = new Vector2(-700, 38);
		_hand.GetNode<Node2D>("RightMostPoint").Position = new Vector2(700, 27);

		// AFTER AddChild: Hand2D._Ready assigns its own handler to CardDragEnd, so setting this
		// earlier would be overwritten. Taking CardDragEnd rather than the
		// IsDragSuccess/OnDragSuccess pair is deliberate — the pair would make us resolve the lane
		// twice and would leave nowhere to surface WHY a play was refused.
		_hand.CardDragEnd = OnCardDropped;
	}

	private void OnCardDropped(Hand2D.DragEndContext context)
	{
		var card = context.CardUI2D;

		if (!int.TryParse(card.Id, out var cardId))
		{
			_hand.LerpCardTransform(card);
			return;
		}

		var lane = _laneAt(context.DragEndPoint);
		if (lane is null)
		{
			_hand.LerpCardTransform(card);
			_report("drop a card on one of your lanes");
			return;
		}

		var refusal = _tryPlay(cardId, lane.Value);
		if (refusal is null)
			return; // It played. The board re-renders, and Sync takes the card out of the fan.

		// **Never swallow a refused drag.** A card that silently slides back tells the player
		// nothing, and a click that does nothing is the worst bug a card game front end can have.
		_hand.LerpCardTransform(card);
		_report(refusal);
	}

	/// <summary>
	/// Brings the fan into line with the Hand zone. Cards are matched by GameState id, so replaying
	/// the same hand does not rebuild every card and restart its tween.
	/// </summary>
	public void Sync(IReadOnlyList<DoomCard> cards, int energy)
	{
		var wanted = cards.ToDictionary(c => c.Id.ToString());

		foreach (var ui in _hand.GetCards().ToList())
			if (ui.Id is null || !wanted.ContainsKey(ui.Id))
				_hand.DiscardCard(ui.Id ?? "");

		var present = _hand.GetCards().Select(c => c.Id).ToHashSet();
		foreach (var card in cards)
			if (!present.Contains(card.Id.ToString()))
			{
				var ui = _hand.DrawCard();
				ui.Id = card.Id.ToString();
				Flatten(ui);
			}

		// SetCardsDetails applies positionally, so the list has to be ordered the way the fan
		// currently holds its cards rather than the way the zone holds them.
		var inFanOrder = _hand.GetCards();
		_hand.SetCardsDetails(inFanOrder.Select(ui => DetailsFor(wanted[ui.Id])).ToList());

		// Affordability is shown by dimming rather than by hiding: an unaffordable card is still
		// information — it is what you are playing around this turn.
		foreach (var ui in inFanOrder)
			ui.Modulate = wanted[ui.Id].Cost <= energy ? Colors.White : new Color(1, 1, 1, 0.45f);
	}

	/// <summary>
	/// The two pieces of the shared card that `Details` does not reach.
	///
	/// Both are plain nodes inside the card scene rather than swappable textures, so they are found
	/// by name and restyled once, when the card is created. Reaching into another scene's tree is
	/// not free — if either node is renamed this silently stops working, which is why it degrades to
	/// doing nothing rather than throwing.
	/// </summary>
	private static void Flatten(CardUI2D ui)
	{
		// The "Unit" type band: a dark stripe straight across the card face. The reference card has
		// no such band, and the type line is already implied by the stat badge.
		if (ui.FindChild("TypeLineBand", true, false) is ColorRect band)
			band.Color = Colors.Transparent;

		if (ui.FindChild("PowerToughnessBadge", true, false) is Sprite2D badge)
			badge.Texture = DoomArt.StatBadge;
	}

	private static InternalCardUI2D.Details DetailsFor(DoomCard card)
	{
		var unit = card.GetComponent<UnitComponent>();

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name,
			ManaCost = card.Cost.ToString(),
			// Blank: the reference card has no type line, and "Unit" floating across the face says
			// nothing a stat badge does not already say. A Rite has no badge, which is the tell.
			TypeLine = "",
			PowerToughness = unit is null ? "" : $"{unit.Power}/{unit.Toughness}",
			RulesText = card.Tags.IsEmpty ? "" : string.Join(", ", card.Tags),

			// Every part of the shared card swapped for a flat one. The interaction is untouched —
			// only the pixels change. See DoomArt.
			MainFrameTexture = DoomArt.Frame,
			NameFrameTexture = DoomArt.NamePlate,
			ManaCostFrameTexture = DoomArt.CostBadge,

			// The artwork covers the upper half and the rules plate the lower, so giving both the
			// same block is what makes the card read as ONE flat colour rather than two stacked
			// panels. The reference card is a single solid shape; this is how you get it out of a
			// frame built for Magic.
			ArtworkTexture = DoomArt.ArtBlock(DoomArt.ColourFor(card.Name)),
			RulesTextFrameTexture = DoomArt.RulesBlock(DoomArt.ColourFor(card.Name)),

			NameColor = DoomPalette.Bone,
			ManaCostColor = DoomPalette.Bone,
			RulesTextColor = DoomPalette.Bone,

			// The border is painted into the frame texture, so the shader outline would only
			// double it.
			OutlineThickness = 0f,

			// Explicitly OFF. The scene sets enable_holographic false, but UpdateHolographicShader
			// rewrites the shader from the C# field at _Ready, so the scene's value does not
			// survive. Left unset it laid a rainbow-noise wash over every card — invisible on
			// saturated colours, and unmistakable on the flat mid-tones this design uses.
			Holographic = false,
			HolographicIntensity = 0f,
		};
	}
}
