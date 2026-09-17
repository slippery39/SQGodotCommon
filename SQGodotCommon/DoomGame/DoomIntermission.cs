using System;
using System.Collections.Generic;
using System.Linq;
using DoomCore;
using Godot;

namespace DoomGame;

/// <summary>
/// What happened between battles, and the only place the player is shown the bargain the whole
/// design rests on.
///
/// **The apocalypses ARE the power curve** — there is no separate progression system. Eating a doom
/// rewrites your deck and marks your companion; dodging one leaves you clean, unmarked and no
/// stronger. Until this screen existed the game never said so: a battle ended and stopped, so the
/// trade was real in `DoomCore` and invisible to anyone playing.
///
/// It reports a DIFF of the deck the run actually had against the deck it actually has. It does not
/// describe what a scenario does — that would be a second account of the rules, and it would drift.
/// </summary>
public sealed class DoomIntermission
{
	private readonly PanelContainer _root;
	private readonly Label _title;
	private readonly Label _body;
	private readonly Button _continue;
	private readonly Label _coming;
	private readonly HBoxContainer _offers;
	private readonly Action<RunCard> _onTake;

	public DoomIntermission(CanvasLayer parent, Action onContinue, Action<RunCard> onTake)
	{
		_root = new PanelContainer { Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddThemeStyleboxOverride("panel", DoomPalette.Box(new Color(0, 0, 0, 0.82f)));

		var centre = new CenterContainer();
		_root.AddChild(centre);

		var card = new PanelContainer();
		card.AddThemeStyleboxOverride(
			"panel",
			DoomPalette.Box(DoomPalette.Slate, DoomPalette.Gold, 3)
		);
		centre.AddChild(card);

		var rows = new VBoxContainer { CustomMinimumSize = new Vector2(760, 0) };
		rows.AddThemeConstantOverride("separation", 20);
		card.AddChild(rows);

		_onTake = onTake;

		_title = DoomPalette.Text("", 42, DoomPalette.Bone);
		_body = DoomPalette.Text("", 20, DoomPalette.Bone);
		rows.AddChild(_title);
		rows.AddChild(_body);

		// **The next apocalypse, BEFORE the reward is chosen.** Free tension at zero cost: it turns
		// picking a card into a decision about the fight you are walking into rather than a shopping
		// trip. DoomJam.md has wanted this since the run structure was written.
		_coming = DoomPalette.Text("", 22, DoomPalette.Red);
		rows.AddChild(_coming);

		_offers = new HBoxContainer();
		_offers.AddThemeConstantOverride("separation", 14);
		_offers.Alignment = BoxContainer.AlignmentMode.Center;
		rows.AddChild(_offers);

		_continue = new Button { Text = "DESCEND", CustomMinimumSize = new Vector2(0, 66) };
		_continue.AddThemeFontSizeOverride("font_size", 24);
		_continue.Pressed += onContinue;
		rows.AddChild(_continue);

		parent.AddChild(_root);
	}

	public void Hide() => _root.Visible = false;

	/// <summary>
	/// A floor cleared. Says what the apocalypses took and gave, and what the companion carries now.
	/// </summary>
	public void ShowFloorCleared(Run before, Run after, int doomsFired)
	{
		_title.Text = $"FLOOR {before.Floor} CLEARED";

		var lines = new List<string>
		{
			doomsFired == 0
				? "You got out before it landed. Nothing was rewritten - and nothing was gained."
			: doomsFired == 1 ? "One apocalypse survived."
			: $"{doomsFired} apocalypses survived.",
			"",
		};

		lines.AddRange(DeckChanges(before, after));

		lines.Add("");
		lines.Add($"Life  {after.Life} / {after.MaxLife}        Deck  {after.Deck.Count} cards");
		lines.Add(
			$"Companion  {after.Companion.FullName}  ({after.Companion.Power}/{after.Companion.Toughness})"
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
			$"Companion  {after.Companion.FullName}  ({after.Companion.Power}/{after.Companion.Toughness})"
		);

		_coming.Text = "";
		foreach (var child in _offers.GetChildren())
			child.QueueFree();

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
	public void OfferRewards(IEnumerable<RunCard> cards, DoomScenario next, int nextFloor)
	{
		_coming.Text =
			$"Floor {nextFloor} below:  {DoomPalette.Caps(next.ToString())}  —  "
			+ StarterContent.DescriptionFor(next);

		foreach (var child in _offers.GetChildren())
		{
			_offers.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var card in cards)
		{
			var offer = new Button
			{
				Text = $"{card.Name}\ncost {card.Cost}\n{card.Power} / {card.Toughness}",
				CustomMinimumSize = new Vector2(210, 118),
			};
			offer.AddThemeFontSizeOverride("font_size", 18);

			var taken = card;
			offer.Pressed += () => _onTake(taken);
			_offers.AddChild(offer);
		}

		_offers.Visible = true;
	}

	public void ShowRunOver(Run run)
	{
		_title.Text = run.IsDead ? "YOU DIED" : "THE ACT IS OVER";
		_body.Text =
			$"{run.OverReason}\n\nYou reached floor {run.Floor} of {Run.ActLength}.\n"
			+ $"Your companion came out as {run.Companion.FullName}.";

		// Nowhere to descend to, and nothing to pick. A button that did nothing would be worse than
		// no button.
		_continue.Visible = false;
		_coming.Text = "";
		_offers.Visible = false;
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

		foreach (var group in gained.GroupBy(c => c.Name))
			yield return $"+  {group.Count()}x {group.Key} joined the deck";

		foreach (var group in lost.GroupBy(c => c.Name))
			yield return $"-  {group.Count()}x {group.Key} LOST";

		foreach (var group in changed.GroupBy(c => c.Name))
			yield return $"~  {group.Count()}x {group.Key} rewritten";
	}
}
