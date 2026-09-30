using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using Godot;
using ImmutableGameObjects;
using KinCore;

namespace KinGame;

/// <summary>
/// The hand, drawn with the shared `Hand2D` / `CardUI2D` fan from `Common/Cards/2D`. That code is
/// game-agnostic — roughly 1600 lines of drag, hover and fan with no MTG in it — so DOOMJAM gets
/// the whole interaction for the cost of filling in a `Details` per card.
///
/// **Presentation only.** It never decides whether a play is legal: it hands a card id and a lane
/// to the board, and the board asks the engine. What comes back is the engine's own refusal text.
/// </summary>
public sealed class KinHandView
{
	/// <summary>Roughly how tall the scaled fan is, so the board can reserve room for it.</summary>
	public const int BandHeight = 360;

	private readonly Hand2D _hand;

	/// <summary>Which lane a drop point lands in, or null if it missed every lane.</summary>
	private readonly Func<Vector2, int?> _laneAt;

	/// <summary>
	/// Plays the card into a lane, or with no lane at all for something that is not a body.
	/// Returns null when it worked, or the engine's reason when it did not.
	/// </summary>
	private readonly Func<int, int?, string> _tryPlay;

	private readonly Action<string> _report;

	/// <summary>The top of the hand's band: a card released below it went back to the hand.</summary>
	private readonly float _bandTop;

	public KinHandView(
		Node parent,
		Vector2 position,
		Func<Vector2, int?> laneAt,
		Func<int, int?, string> tryPlay,
		Action<string> report
	)
	{
		_laneAt = laneAt;
		_tryPlay = tryPlay;
		_report = report;
		_bandTop = position.Y - BandHeight / 2f;

		// The scene, not `new Hand2D()` — Hand.tscn already carries the position curves, the card
		// scene and the LeftMostPoint / RightMostPoint anchors that Hand2D looks up by name.
		_hand = GD.Load<PackedScene>("res://Common/Cards/2D/Hand2D/Hand.tscn")
			.Instantiate<Hand2D>();
		_hand.Position = position;

		// A Card2D is around 500px tall in the 1920x1080 base canvas — sized for a screen that is
		// all hand. Here the hand is one band of six, so it is scaled to fit rather than the board
		// being shrunk around it.
		_hand.Scale = new Vector2(KinCardFace.CardScale, KinCardFace.CardScale);

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

	/// <summary>
	/// Shows or hides the whole fan.
	///
	/// **The intermission needs this.** Its dim overlay is a Control, and Hand2D gives every card a
	/// ZIndex of its own — which beats tree order — so the hand drew straight through the dimmed
	/// screen and over the reward cards. Hiding it is also the right reading: between floors the
	/// hand is not yours to play, and a hand you cannot use should not be on screen looking like
	/// one you can.
	/// </summary>
	public void SetVisible(bool visible) => _hand.Visible = visible;

	private void OnCardDropped(Hand2D.DragEndContext context)
	{
		var card = context.CardUI2D;

		if (!int.TryParse(card.Id, out var cardId))
		{
			_hand.LerpCardTransform(card);
			return;
		}

		// **Released over the hand: it went back.** Not a play, and not a refusal to report — a card
		// that needs no target would otherwise play the moment it was let go of in the fan.
		if (context.DragEndPoint.Y > _bandTop)
		{
			_hand.LerpCardTransform(card);
			return;
		}

		// A lane is only meaningful for a body. A rite has no position, so it plays from wherever it
		// was dropped — the board decides, because the board is the thing that knows.
		var refusal = _tryPlay(cardId, _laneAt(context.DragEndPoint));
		if (refusal is null)
		{
			// It played. The board re-renders, and Sync takes the card out of the fan — UNLESS the same
			// card is back in hand (returned or redrawn), when Sync keeps it and it froze where it was
			// dropped. Found in play with Feint drawing itself. Slide it home.
			if (_hand.GetCards().Contains(card))
				_hand.LerpCardTransform(card);
			return;
		}

		// **Never swallow a refused drag.** A card that silently slides back tells the player
		// nothing, and a click that does nothing is the worst bug a card game front end can have.
		_hand.LerpCardTransform(card);
		_report(refusal);
	}

	/// <summary>
	/// Brings the fan into line with the Hand zone. Cards are matched by GameState id, so replaying
	/// the same hand does not rebuild every card and restart its tween.
	/// </summary>
	public void Sync(
		IReadOnlyList<KinCard> cards,
		int energy,
		Func<KinCard, InternalCardUI2D.Details> face = null
	)
	{
		face ??= KinCardFace.For;
		var wanted = cards.ToDictionary(c => c.Id.ToString());

		foreach (var ui in _hand.GetCards().ToList())
			if (ui.Id is null || !wanted.ContainsKey(ui.Id))
				_hand.DiscardCard(ui.Id ?? "");

		var present = _hand.GetCards().Select(c => c.Id).ToHashSet();
		foreach (var card in cards)
			if (!present.Contains(card.Id.ToString()))
			{
				// **From below the fan, not from the far left.** The parameterless `DrawCard` starts a
				// card at global x=0 — the screen's left edge — so every draw flew in sideways
				// across the whole board. It was invisible for three sessions because nothing ever
				// captured a turn in flight; it only showed up once the screenshot loop could end
				// a turn by itself. Cards now rise into the hand from under the bottom edge.
				var ui = _hand.DrawCard(_hand.GlobalPosition + new Vector2(0, 340));
				ui.Id = card.Id.ToString();
				KinCardFace.Style(ui);
			}

		// SetCardsDetails applies positionally, so the list has to be ordered the way the fan
		// currently holds its cards rather than the way the zone holds them.
		var inFanOrder = _hand.GetCards();
		_hand.SetCardsDetails(inFanOrder.Select(ui => face(wanted[ui.Id])).ToList());

		// AFTER the details: power lives on a node the shared `Details` does not know about, and a
		// card recycled into a new hand would otherwise keep the last card's number.
		foreach (var ui in inFanOrder)
			KinCardFace.ApplyStats(ui, wanted[ui.Id]);

		// Affordability is shown by dimming rather than by hiding: an unaffordable card is still
		// information — it is what you are playing around this turn.
		foreach (var ui in inFanOrder)
			ui.Modulate = wanted[ui.Id].Cost <= energy ? Colors.White : new Color(1, 1, 1, 0.45f);
	}
}
