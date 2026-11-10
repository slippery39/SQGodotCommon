using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Effects;

namespace MagicTable;

/// <summary>
/// Arena's "click the spell, then click its targets": a play's legal moves differ by what they
/// pick — their targets, then the card a cost takes (what to sacrifice, discard, behold, exile, tap) —
/// and are narrowed one pick at a time by clicking objects on the table. EVERY pick is clicked,
/// even when only one is legal (Shayne, 2026-10-09: a spell casting itself was a surprise; Arena and
/// MTGO ask). Plain C#; the moves come from LegalActions — nothing here decides what is legal.
/// </summary>
public static class Targeting
{
	/// <summary>
	/// A move's picks in click order: what the prompt asks for, and for a cost's pick how a way
	/// that takes it is named ("sacrificing" — the same word Describe.Action uses); null for a target.
	/// </summary>
	public static IReadOnlyList<(int Id, string What, string? Cost)> PicksOf(GameAction move)
	{
		IEnumerable<(int, string, string?)> Cost(int id, string verb, string ing) =>
			id == 0 ? [] : [(id, $"what to {verb}", ing)];
		IEnumerable<(int, string, string?)> Targets(IEnumerable<int> ids) =>
			ids.Select((id, i) => (id, $"target {i + 1}", (string?)null));
		return move switch
		{
			CastSpellAction c =>
			[
				.. Targets(c.Targets),
				.. Cost(c.SacrificeId, "sacrifice", "sacrificing"),
				.. Cost(c.DiscardId, "discard", "discarding"),
				.. Cost(c.BeholdId, "behold", "beholding"),
				.. Cost(c.ReturnId, "return", "returning"),
			],
			ActivateAbilityAction a =>
			[
				.. Targets(a.Targets),
				.. Cost(a.SacrificeId, "sacrifice", "sacrificing"),
				.. Cost(a.DiscardId, "discard", "discarding"),
				.. Cost(a.ExileId, "exile", "exiling"),
				.. Cost(a.TapId, "tap", "tapping"),
				.. Cost(a.SecondTapId, "tap", "tapping"),
				.. a.TapIds.SelectMany(id => Cost(id, "tap", "tapping")), // a set, last: clicked in any order (SetRest)
			],
			ActivateLoyaltyAction l => [.. Targets(l.Targets)],
			_ => [],
		};
	}

	/// <summary>
	/// A card's moves as plays: the same spell or ability with the same modes is one play whatever it
	/// picks and whatever its X — X is chosen on a spinner (<see cref="Xs"/>, TBL-9.1: one button per X
	/// was "terrible past a few"). Each play is offered once; its picks are clicked afterwards.
	/// </summary>
	public static List<List<GameAction>> Plays(GameState s, IEnumerable<GameAction> moves) =>
		moves
			.GroupBy(m => $"{Label(s, WithX(m, 0))}|{(m as ActivateAbilityAction)?.AbilityIndex}")
			.Select(g => g.ToList())
			.ToList();

	/// <summary>
	/// Arena's mode picker: every mode of a modal spell (every combination, for "choose two"), each
	/// with the legal casts that choose it. A mode that can't be chosen now has none — the table
	/// greys it, saying why — so a spell with one legal mode still asks (Shayne, 2026-10-09: Surgical
	/// Precision cast its only legal mode by itself). Null when the card's casts aren't modal.
	/// </summary>
	public static List<(string Label, List<GameAction> Moves)>? Modes(
		GameState s,
		int cardId,
		IReadOnlyList<GameAction> moves
	)
	{
		var casts = moves.OfType<CastSpellAction>().ToList();
		if (
			casts.Count == 0
			|| s.GetCard(cardId).GetComponent<SpellEffectsComponent>()
				is not { Modes.IsEmpty: false } spell
		)
			return null;
		return spell
			.Choose.Choices(s, s.GetCard(cardId), spell.Modes)
			.Select(combo =>
			{
				var text = Describe.Modes(spell.Modes, combo);
				var legal = casts.Where(c => c.Modes.Order().SequenceEqual(combo)).ToList();
				// "No legal targets" only when that is surely why: one mode, and it targets.
				var why =
					combo is [var only] && !spell.Modes[only].Targets.IsEmpty
						? "no legal targets"
						: "not possible now";
				if (legal.Count == 0)
					return ($"{text}  ({why})", new List<GameAction>());
				// X is chosen with the mode, on a spinner (Xs), before any target is clicked.
				return (text, legal.Cast<GameAction>().ToList());
			})
			.ToList();
	}

	/// <summary>
	/// The next pick to click, from <paramref name="from"/> on. -1 when none is left, or when the
	/// candidates pick different numbers of things ("sacrifice a creature or pay {2}") and the
	/// table lists them instead.
	/// </summary>
	public static int NextIndex(IReadOnlyList<GameAction> candidates, int from)
	{
		var lengths = candidates.Select(c => PicksOf(c).Count).Distinct().ToList();
		return lengths is [var length] && from < length ? from : -1;
	}

	/// <summary>
	/// Moves that can't be told apart by clicking, as a list to choose from. One play cast different
	/// ways ("sacrifice a creature or pay {3}") is one entry per WAY, never per combination — listing
	/// every target × sacrifice once grew a button list off the screen; the chosen way's picks are
	/// then clicked. Moves that differ in any other way are listed one by one, in full.
	/// </summary>
	public static List<(string Label, List<GameAction> Moves)> Ways(
		GameState s,
		List<GameAction> candidates
	)
	{
		static List<string> Costs(GameAction m) =>
			PicksOf(m).Select(p => p.Cost).OfType<string>().Distinct().ToList(); // a set of taps is one "tapping"
		var ways = candidates
			.GroupBy(c => string.Join(",", PicksOf(c).Select(p => p.What)))
			.Select(g => g.ToList())
			.ToList();
		if (ways.Count < 2 || !ways.Any(w => Costs(w[0]).Count > 0))
			return candidates
				.Select(c => (Describe.Action(s, c), new List<GameAction> { c }))
				.ToList();
		return ways.Select(way =>
				Costs(way[0]) is { Count: > 0 } costs
					? ($"{Label(s, way[0])}, {string.Join(" and ", costs)}", way)
					: ($"{Label(s, way[0])}, paying instead", way)
			)
			.ToList();
	}

	/// <summary>The objects that may be clicked for pick <paramref name="index"/> — in a set of taps, any not clicked yet.</summary>
	public static IReadOnlyList<int> Options(IReadOnlyList<GameAction> candidates, int index) =>
		candidates.SelectMany(c => SetRest(c, index) ?? [PicksOf(c)[index].Id]).Distinct().ToList();

	/// <summary>
	/// The candidates that pick <paramref name="id"/> at <paramref name="index"/>. A set of taps
	/// (Collation Trimatrix) has no order: a candidate with the id anywhere in the rest of its set
	/// stays, the id swapped into this place — so the next pick reads the set's unclicked rest.
	/// </summary>
	public static List<GameAction> Narrow(IEnumerable<GameAction> candidates, int index, int id) =>
		candidates
			.Select(c =>
				SetRest(c, index) is { } rest
					? rest.Contains(id)
						? Swapped((ActivateAbilityAction)c, index, id)
						: null
					: PicksOf(c)[index].Id == id
						? c
						: null
			)
			.OfType<GameAction>()
			.ToList();

	/// <summary>A tap set's picks from <paramref name="index"/> on, or null when that pick isn't in one.</summary>
	static IReadOnlyList<int>? SetRest(GameAction move, int index) =>
		move is ActivateAbilityAction { TapIds.Length: > 0 } a
		&& index - (PicksOf(move).Count - a.TapIds.Length) is >= 0 and var at
			? a.TapIds[at..]
			: null;

	static ActivateAbilityAction Swapped(ActivateAbilityAction a, int index, int id)
	{
		var ids = a.TapIds.ToArray();
		var at = index - (PicksOf(a).Count - ids.Length);
		var from = Array.IndexOf(ids, id);
		(ids[at], ids[from]) = (ids[from], ids[at]);
		return a with { TapIds = [.. ids] };
	}

	/// <summary>The X values a play can be made with, lowest first; more than one asks for X before any pick.</summary>
	public static List<int> Xs(IEnumerable<GameAction> play) =>
		play.Select(XOf).OfType<int>().Distinct().Order().ToList();

	/// <summary>The play's moves made with this X — the nearest legal one when it isn't (a typed 7 of 0–5).</summary>
	public static List<GameAction> WithChosenX(List<GameAction> play, int x)
	{
		var nearest = Xs(play).MinBy(legal => Math.Abs(legal - x));
		return play.Where(m => XOf(m) == nearest).ToList();
	}

	/// <summary>A move's X: a spell's {X}, a planeswalker's −X; null for anything without one.</summary>
	static int? XOf(GameAction m) =>
		m switch
		{
			CastSpellAction c => c.X,
			ActivateLoyaltyAction l => l.X,
			_ => null,
		};

	/// <summary>The same move with another X — the label of a play whatever its X.</summary>
	public static GameAction WithX(GameAction m, int x) =>
		m switch
		{
			CastSpellAction c => c with { X = x },
			ActivateLoyaltyAction l => l with { X = x },
			_ => m,
		};

	/// <summary>What pick <paramref name="index"/> is, for the prompt: "target 1", "what to sacrifice".</summary>
	public static string What(GameAction move, int index) => PicksOf(move)[index].What;

	/// <summary>A play's label, its picks left out — they are clicked, not listed.</summary>
	public static string Label(GameState s, GameAction move) =>
		Describe.Action(s, WithoutPicks(move));

	private static GameAction WithoutPicks(GameAction move) =>
		move switch
		{
			CastSpellAction c => c with
			{
				Targets = [],
				SacrificeId = 0,
				DiscardId = 0,
				BeholdId = 0,
				ReturnId = 0,
			},
			ActivateAbilityAction a => a with
			{
				Targets = [],
				SacrificeId = 0,
				DiscardId = 0,
				ExileId = 0,
				TapId = 0,
				SecondTapId = 0,
				TapIds = [],
			},
			ActivateLoyaltyAction l => l with { Targets = [] },
			_ => move,
		};
}
