using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using KinCore;
using Godot;
using ImmutableGameObjects;

namespace KinGame;

/// <summary>
/// **A card rack, with no game behind it.** Every card that is hard to draw, side by side, at the
/// size the hand actually shows them, plus one blown up.
///
/// This exists because the loop for judging a card used to be: change a colour, build, launch the
/// board, wait for a battle to deal a hand, and hope the card you changed was in it. Half the pool
/// never appeared at all — a Rite, a two-digit statline, a card whose text runs to three lines. The
/// front end's own rule is that a size is verified by looking at a capture, and that rule is only
/// affordable if looking is cheap.
///
/// **It draws through <see cref="KinCardFace"/>, never its own copy of the look.** The whole point
/// is that what is on this screen is what the hand will show; a preview with its own styling is a
/// second opinion, and the one you are not looking at is the one that rots.
///
/// Run it:
/// <code>
/// godot-mono --path SQGodotCommon --position 1920,0 --resolution 1600x900 \
///   --write-movie shots/cards.png --fixed-fps 10 --quit-after 20 KinGame/kin_card_preview.tscn
/// </code>
/// </summary>
public partial class KinCardPreview : Node2D
{
	/// <summary>
	/// The cases worth looking at, chosen to BREAK the layout rather than to flatter it.
	///
	/// A preview of five comfortable cards proves nothing. These are the widest statline in the
	/// game, the longest rules text, a Rite (no stat badge at all), a two-line effect, and the
	/// Companion — each one a thing that has already come out wrong at least once.
	/// </summary>
	private static IEnumerable<KinCard> Cases()
	{
		// The reward pool too, not just the shared one: the cards with EFFECTS are the act's own,
		// and they are exactly the cards whose text is hard to fit and whose keywords matter.
		var pool = StarterContent
			.SharedPool.Concat(StarterContent.NewRun().Deck)
			.Concat(StarterContent.RewardPool(KinTheme.LongEmergency))
			.ToList();

		// Named rather than taken by index: the pool is reordered every balance pass, and a preview
		// that silently starts showing five different cards is a preview nobody trusts.
		string[] wanted =
		[
			"Drone Swarm", // the LONGEST authored effect text — three lines, and the box must hold it
			"Salvage Rig", // a unit with a doom-triggered effect — the keyword panel's real case
			"Long Watcher", // 12/20 — the widest statline the badge must hold
			"Scavenger", // a vanilla body: flavour is all it has to say
			"Breaching Charge", // a Rite: no stat badge, and its text IS the card
			"Bulwark", // 2/10 — the statline that used to render as 2/1
			"Lantern Bearer", // a 0-cost, and a drawing with a gold accent
		];

		foreach (var name in wanted)
		{
			var found = pool.FirstOrDefault(c => c.Name == name);

			// A renamed card must be LOUD here, not silently absent — an empty slot in a preview
			// reads as a rendering bug and sends you looking in the wrong file.
			if (found is null)
				GD.PushWarning($"KinCardPreview: no card named '{name}' in the pool any more.");
			else
				yield return found.ToKinCard();
		}
	}

	public override void _Ready()
	{
		var canvas = GetViewportRect().Size;

		var ground = new ColorRect { Color = KinPalette.Navy };
		ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		// **Layer -1, or the ground covers the cards.** A CanvasLayer defaults to 1 and the cards are
		// plain Node2D children of the root on layer 0, so the "background" drew over all five of
		// them and the screen came back empty but for the caption. The board does not hit this
		// because everything it draws lives inside one layer.
		var layer = new CanvasLayer { Layer = -1 };
		AddChild(layer);
		layer.AddChild(ground);

		var cards = Cases().ToList();

		layer.AddChild(
			Caption(
				$"CARD PREVIEW — {cards.Count} cards, hand scale {KinCardFace.CardScale:0.00} above, "
					+ "full size below",
				new Vector2(40, 24)
			)
		);

		// The top row is the size a player actually sees in the fan. Judge legibility HERE — a card
		// that only reads at full size is a card that does not read.
		var spacing = canvas.X / (cards.Count + 1);
		for (var i = 0; i < cards.Count; i++)
			Place(cards[i], new Vector2(spacing * (i + 1), 300), KinCardFace.CardScale);

		// One card at full size, for the art and the frame. This is also what a hover-enlarge will
		// look like, so it is worth having on screen beside the small one.
		Place(cards[0], new Vector2(canvas.X * 0.24f, 760), 1.6f);
		Place(cards[2], new Vector2(canvas.X * 0.52f, 760), 1.6f);

		// The keyword panel, beside them. It normally appears on hover, and hover cannot be
		// captured — so the only way it ever gets looked at is here.
		//
		// Its OWN layer, not the background one: `layer` is at -1 so the ground does not cover the
		// cards, and anything else put in it is behind the ground too.
		var front = new CanvasLayer();
		AddChild(front);

		var inspector = new KinCardInspector(front, new Vector2(1250, 430));
		inspector.Describe(cards[0]);
	}

	private void Place(KinCard card, Vector2 position, float scale)
	{
		var ui = GD.Load<PackedScene>("res://Common/Cards/2D/Card2D/card_2d_canvasgroup.tscn")
			.Instantiate<CardUI2D>();

		AddChild(ui);
		ui.Position = position;

		// AFTER AddChild, and in this order. `Style` reaches into the card's own tree by node name,
		// so the scene has to be built first; `ApplyTo` then fills the textures and text.
		KinCardFace.Style(ui);
		ui.ApplyTo(KinCardFace.For(card));
		KinCardFace.ApplyStats(ui, card);

		// The scene already carries a 0.7 of its own — this multiplies it, exactly as the hand does.
		ui.Scale *= scale;
	}

	private static Label Caption(string text, Vector2 at)
	{
		var label = KinPalette.Text(text, 24, KinPalette.Bone, HorizontalAlignment.Left);
		label.Position = at;
		return label;
	}
}
