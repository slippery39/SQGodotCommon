using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using Godot;
using KinCore;

namespace KinGame;

/// <summary>
/// What happened between battles, and the only place the player is shown the bargain the whole
/// design rests on.
///
/// **The apocalypses ARE the power curve** — there is no separate progression system. Eating a doom
/// rewrites your deck and marks your companion; dodging one leaves you clean, unmarked and no
/// stronger. Until this screen existed the game never said so: a battle ended and stopped, so the
/// trade was real in `KinCore` and invisible to anyone playing.
///
/// It reports a DIFF of the deck the run actually had against the deck it actually has. It does not
/// describe what a scenario does — that would be a second account of the rules, and it would drift.
/// </summary>
public sealed class KinIntermission
{
	private readonly ColorRect _root;
	private readonly Label _title;
	private readonly Label _body;
	private readonly Button _continue;
	private readonly Label _coming;

	/// <summary>
	/// The offered cards.
	///
	/// A `CardUI2D` is a Node2D, and a Control container will not lay a Node2D out — it leaves it at
	/// the origin. So the row is positioned by hand, and lives beside the panel rather than in it.
	/// </summary>
	private readonly Node2D _offers;

	private readonly Action<RunCard> _onTake;
	private readonly Action<CompanionUpgrade> _onUpgrade;

	/// <summary>The three companion upgrades, when the floor offers any. Controls, so the panel lays them out.</summary>
	private readonly HBoxContainer _upgradeRow;
	private readonly Label _upgradeHeading;

	/// <summary>Where the DESCEND button sits, in canvas pixels from the top.</summary>
	private const int ButtonY = 900;

	/// <summary>
	/// How large a reward card is drawn. **Bigger than in hand**, because this is the one moment in
	/// a run where a card is the entire decision and there is nothing else on screen to read.
	/// </summary>
	private const float OfferScale = 1.05f;

	public KinIntermission(
		CanvasLayer parent,
		Action onContinue,
		Action<RunCard> onTake,
		Action<CompanionUpgrade> onUpgrade
	)
	{
		// **A ColorRect, not a PanelContainer.** A Container OVERRIDES its children's anchors and
		// positions on every layout pass — so the panel stretched to the full screen and the DESCEND
		// button, anchored near the bottom, was dragged up behind the reward cards. Both looked like
		// placement bugs and were one: the wrong node type at the root.
		_root = new ColorRect { Visible = false, Color = new Color(0, 0, 0, 0.82f) };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		// Text at the top, the decision in the middle, the way out at the bottom.
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(KinPalette.Slate, KinPalette.Gold, 3)
		);
		card.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		card.OffsetLeft = 420;
		card.OffsetRight = -420;
		card.OffsetTop = 36;
		card.OffsetBottom = 36; // Grown by the content's minimum size, not fixed here.
		_root.AddChild(card);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 14);
		card.AddChild(rows);

		_onTake = onTake;
		_onUpgrade = onUpgrade;

		_title = KinPalette.Text("", 42, KinPalette.Bone);
		_body = KinPalette.Text("", 20, KinPalette.Bone);
		rows.AddChild(_title);
		rows.AddChild(_body);

		// **WRAP, or the panel leaves the screen.** A Control's size is at least its content's
		// minimum size, and an unwrapped Label's minimum width is its whole single line — so the
		// panel's anchors were only ever a suggestion. On floor 19 the companion's name ran to a
		// dozen marks and pushed the panel off the right-hand edge, taking the title with it.
		//
		// With wrapping on, the minimum width collapses and the anchors decide the width, which is
		// what they were always meant to do. Nothing here may be allowed to set its own width.
		Wrap(_body);

		// **The next apocalypse, BEFORE the reward is chosen.** Free tension at zero cost: it turns
		// picking a card into a decision about the fight you are walking into rather than a shopping
		// trip. KinJam.md has wanted this since the run structure was written.
		// **The companion upgrade, ABOVE the cards.** It is the run's power curve and the card is
		// the run's texture; putting the smaller decision first would bury the larger one. Unlike
		// the cards these are Controls, so the panel lays them out and they need no hand-placing.
		_upgradeHeading = KinPalette.Text("", 24, KinPalette.Gold);
		_upgradeHeading.Visible = false;
		rows.AddChild(_upgradeHeading);

		_upgradeRow = new HBoxContainer { Visible = false };
		_upgradeRow.AddThemeConstantOverride("separation", 12);
		rows.AddChild(_upgradeRow);

		_coming = KinPalette.Text("", 22, KinPalette.Red);
		rows.AddChild(_coming);
		Wrap(_coming);
		Wrap(_title);

		// **Outside the panel**, anchored near the bottom, so it sits BELOW the cards rather than
		// behind them. It is the way past the decision, so it goes after it.
		_continue = new Button { Text = "DESCEND", CustomMinimumSize = new Vector2(420, 66) };
		_continue.AddThemeFontSizeOverride("font_size", 24);
		_continue.Pressed += onContinue;
		_continue.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_continue.Position = new Vector2(-210, ButtonY);
		_root.AddChild(_continue);

		parent.AddChild(_root);

		// AFTER the panel, so the cards draw over the dimmed background rather than under it.
		_offers = new Node2D { Visible = false };
		parent.AddChild(_offers);
	}

	/// <summary>
	/// Lets a label wrap, and stops it demanding a width of its own.
	///
	/// `CustomMinimumSize.X = 1` is the part that matters: autowrap alone still reports a minimum
	/// width, and a container will honour it. Told it may be one pixel wide, the label wraps to
	/// whatever the panel's anchors give it instead.
	/// </summary>
	private static void Wrap(Label label)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(1, 0);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
	}

	/// <summary>Whether this screen is up. The board must not treat a click on it as a lane click.</summary>
	public bool IsShowing => _root.Visible;

	public void Hide()
	{
		_root.Visible = false;
		_offers.Visible = false;
	}

	/// <summary>Throws the offered cards away. They are rebuilt per floor and never reused.</summary>
	private void ClearOffers()
	{
		foreach (var child in _offers.GetChildren())
		{
			_offers.RemoveChild(child);
			child.QueueFree();
		}

		_offers.Visible = false;
	}

	/// <summary>
	/// A floor cleared. Says what changed in the deck, and where the run stands now.
	/// </summary>
	public void ShowFloorCleared(Run before, Run after)
	{
		_title.Text = $"FLOOR {before.Floor} CLEARED";

		// **COMPUTED from the two decks, never narrated.** Nothing rewrites the deck mid-battle
		// today, so this is usually empty — and it stays because the moment something does, this
		// screen reports it without being taught how.
		var lines = new List<string>(DeckChanges(before, after));

		lines.Add("");
		lines.Add(
			$"Life  {after.Life} / {after.MaxLife}        Deck  {after.Deck.Count} cards"
				+ $"        {after.Companion.Name}  {after.Companion.Power}/{after.Companion.Toughness}"
		);

		_body.Text = string.Join("\n", lines);
		_continue.Text = "DESCEND WITH NOTHING";
		_continue.Visible = true;
		_root.Visible = true;
	}

	/// <summary>
	/// A rest floor. No fight, no reward, no decision — you heal and you walk on.
	///
	/// It reads the healed run rather than computing the heal, the same rule every other panel
	/// follows: `Run.Rest` did the arithmetic and this says what happened.
	/// </summary>
	public void ShowRest(Run before, Run after)
	{
		_title.Text = $"FLOOR {before.Floor} — NOTHING HERE";

		_body.Text = string.Join(
			"\n",
			"Quiet, for once. Long enough to bind what is bleeding.",
			"",
			$"Life  {before.Life}  ->  {after.Life} / {after.MaxLife}",
			$"Deck  {after.Deck.Count} cards",
			$"Companion  {after.Companion.Name}  ({after.Companion.Power}/{after.Companion.Toughness})"
		);

		_coming.Text = "";
		ClearOffers();

		_continue.Text = "WALK ON";
		_continue.Visible = true;
		_root.Visible = true;
	}

	/// <summary>
	/// Offers three cards, and names the apocalypse waiting below before you choose.
	///
	/// **Skipping is a real option**, which is why the continue button says so out loud instead of
	/// being hidden: you draw five a turn from a deck that never shrinks, so a card you will not
	/// play is a card crowding out one you would. Taking nothing is sometimes correct, and a reward
	/// screen that cannot be declined is not a decision.
	/// </summary>
	/// <summary>
	/// The companion upgrades for this floor, or nothing on a floor that offers none.
	///
	/// **Taking one disables the row rather than closing the screen.** The card choice below is a
	/// separate decision and either may be skipped, so neither may advance the floor on its own —
	/// DESCEND is still the only way down.
	/// </summary>
	public void OfferUpgrades(IEnumerable<CompanionUpgrade> upgrades)
	{
		foreach (var child in _upgradeRow.GetChildren())
		{
			_upgradeRow.RemoveChild(child);
			child.QueueFree();
		}

		var offered = upgrades.ToList();
		_upgradeRow.Visible = offered.Count > 0;
		_upgradeHeading.Visible = offered.Count > 0;
		_upgradeHeading.Text = "YOUR COMPANION GROWS — TAKE ONE";

		foreach (var upgrade in offered)
			_upgradeRow.AddChild(UpgradeButton(upgrade));
	}

	private Button UpgradeButton(CompanionUpgrade upgrade)
	{
		var button = new Button
		{
			Text = $"{upgrade.Name.ToUpperInvariant()}\n{upgrade.Text}",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 76),
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeStyleboxOverride(
			"normal",
			KinPalette.Box(KinPalette.Navy, KinPalette.Bone, 2)
		);
		button.AddThemeStyleboxOverride(
			"hover",
			KinPalette.Box(KinPalette.Navy, KinPalette.Gold, 3)
		);

		button.Pressed += () =>
		{
			_onUpgrade(upgrade);

			// One per floor. Hiding the row rather than disabling the buttons, because a row of
			// greyed choices reads as a screen that is still asking something.
			_upgradeRow.Visible = false;
			_upgradeHeading.Text = $"{upgrade.Name.ToUpperInvariant()} TAKEN";
		};

		return button;
	}

	public void OfferRewards(IEnumerable<RunCard> cards, int nextFloor)
	{
		_coming.Text = $"Floor {nextFloor} below";

		ClearOffers();

		// **Real cards, not buttons describing cards.** A reward used to be a rectangle reading
		// "Scrapper / cost 1 / 10 / 4", which asked the player to picture the card they were being
		// offered — and made the one screen where a card IS the whole decision the one screen that
		// would not show you one. The genre settled this years ago: the offer is the card, at full
		// size, and taking it is clicking it.
		//
		// Drawn through KinCardFace, like the hand and the preview, so a reward can never look like
		// a different card from the one that joins the deck.
		var offered = cards.ToList();

		for (var i = 0; i < offered.Count; i++)
		{
			var shown = offered[i].ToKinCard();

			var ui = GD.Load<PackedScene>("res://Common/Cards/2D/Card2D/card_2d_canvasgroup.tscn")
				.Instantiate<CardUI2D>();

			_offers.AddChild(ui);

			// AFTER AddChild, and in this order: Style reaches into the card's own tree by node
			// name, so the scene has to be built first.
			KinCardFace.Style(ui);
			ui.ApplyTo(KinCardFace.For(shown));
			KinCardFace.ApplyStats(ui, shown);

			ui.Position = OfferPosition(i, offered.Count);
			ui.Scale *= OfferScale;

			// Above everything the board draws. Hand2D hands out per-card ZIndex and z beats tree
			// order, so without this a reward could sit under a card from the battle just won.
			ui.ZIndex = 400 + i;

			// **Dragging OFF, or clicking cannot work at all.** CardUI2D raises Clicked only while
			// it is the hovered card, and beginning a drag clears the hovered card on the same
			// press — so a card that can be dragged can never be clicked. There is nowhere to drag
			// a reward to anyway.
			ui.DragEnabled = () => false;

			// **And the hover LIFT off, which is not the same thing.** CardUI2D.StartHover moves the
			// hovered card to `viewportHeight - cardHeight/2` — it is written for a hand along the
			// bottom of the screen, where lifting a card clear of the edge is exactly right. A
			// reward sits in the middle of the screen, so hovering one yanked it down over the
			// DESCEND button.
			//
			// `IsPosLerping` is the shared card's own early-out in StartHover, so this suppresses
			// the lift while leaving CardUIManager free to mark the card as hovered — which is what
			// Clicked depends on. Nothing ever clears it here, which is the point.
			ui.IsPosLerping = true;

			// The card is already at full size, so the hover cue is an outline rather than a lift.
			// Without one nothing on this screen looks clickable, which KinUI.md forbids.
			if (ui.FindChild("HoverArea", true, false) is Area2D area)
			{
				area.MouseEntered += ui.Highlight;
				area.MouseExited += ui.NoHighlight;
			}

			// First press, finger or mouse — see KinCardTap. This screen used to need two taps on
			// a phone because `Clicked` waits for a hover.
			var taken = offered[i];
			KinCardTap.OnFirstPress(ui, () => _onTake(taken));
		}

		_offers.Visible = true;
	}

	/// <summary>
	/// Where the nth of several offered cards sits, in canvas coordinates.
	///
	/// Spread about the centre rather than packed from one side, so two offers and four offers both
	/// look deliberate. The pool hands out three today; nothing in the content promises that forever.
	/// </summary>
	private static Vector2 OfferPosition(int index, int count)
	{
		const float spacing = 340f;
		const float centreX = 960f;
		const float centreY = 660f;

		return new Vector2(centreX + ((index - ((count - 1) / 2f)) * spacing), centreY);
	}

	public void ShowRunOver(Run run)
	{
		_title.Text = run.IsDead ? "YOU DIED" : "THE ACT IS OVER";
		_body.Text =
			$"{run.OverReason}\n\nYou reached floor {run.Floor} of {Run.ActLength}.\n"
			+ $"Your companion came out as {run.Companion.Name}.";

		// Nowhere to descend to, and nothing to pick. A button that did nothing would be worse than
		// no button.
		_continue.Visible = false;
		_coming.Text = "";
		ClearOffers();
		_root.Visible = true;
	}

	/// <summary>
	/// The deck before against the deck after, matched on RunCardId — the one identity that survives
	/// a battle. Counts are computed from the real decks, never narrated from the scenario.
	/// </summary>
	private static IEnumerable<string> DeckChanges(Run before, Run after)
	{
		var was = before.Deck.ToDictionary(c => c.RunCardId);
		var now = after.Deck.ToDictionary(c => c.RunCardId);

		var gained = now.Values.Where(c => !was.ContainsKey(c.RunCardId)).ToList();
		var lost = was.Values.Where(c => !now.ContainsKey(c.RunCardId)).ToList();
		var changed = was
			.Values.Where(c => now.TryGetValue(c.RunCardId, out var a) && a != c)
			.ToList();

		if (gained.Count == 0 && lost.Count == 0 && changed.Count == 0)
		{
			yield return "Your deck is exactly as you brought it.";
			yield break;
		}

		yield return "";

		foreach (var group in gained.GroupBy(c => c.Name))
			yield return $"+  {group.Count()}x {group.Key} joined the deck";

		foreach (var group in lost.GroupBy(c => c.Name))
			yield return $"-  {group.Count()}x {group.Key} LOST";

		foreach (var group in changed.GroupBy(c => c.Name))
			yield return $"~  {group.Count()}x {group.Key} rewritten";
	}
}
