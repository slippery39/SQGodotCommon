using System.Collections.Generic;
using System.Linq;
using MagicCore.Limited;
using MagicCore.Sets;
using MagicCore.Sets.Fra;

namespace MagicTable;

/// <summary>
/// A sealed event's deck building, plain C# (no Godot types), story LIM-1: your pool — six packs
/// opened from a seed, or your real one pasted from Arena — the deck you are building from it, free
/// basics, the suggested build — and the table setup that plays your deck against an AI's own build
/// of ITS pool. Each game's opponent is fresh, opened from that game's seed (LIM-1.7).
/// </summary>
public sealed class SealedSession
{
	public static IReadOnlyList<string> BasicNames => SealedBuilder.Basics;

	/// <summary>The sets with boosters and ratings, as the deck select offers them (MC-5.4): code and name.</summary>
	public static readonly IReadOnlyList<(string Code, string Name)> Sets =
	[
		("FRA", "Reality Fracture"),
		("TRK", "Star Trek"),
	];

	/// <summary>The set the pool is opened from: its boosters, its ratings, the printing its cards show.</summary>
	public string Set { get; }

	private readonly List<string> _deck = new();
	private readonly int[] _basics = new int[5];
	private IReadOnlyList<SealedDeck>? _best;
	private IReadOnlyList<SealedDeck>? _suggestions;
	private IReadOnlyList<SealedDeck> _pairBuilds = [];
	private readonly Dictionary<int, IReadOnlyList<SealedDeck>> _pairCache = new(); // built once a pair: measured once
	private IReadOnlyList<SealedBuilder.PairRating>? _closePairs;
	private readonly Dictionary<SealedDeck, Castability.Result> _measured =
		new(ReferenceEqualityComparer.Instance);

	/// <summary>Six packs opened from <paramref name="seed"/> — one number replays your pool.</summary>
	public SealedSession(int seed, string set = "FRA")
		: this(Boosters.SealedPool(set, seed), set: set) => Seed = seed;

	/// <summary>A pool you opened elsewhere (<see cref="ArenaDeck.ReadPool"/>); what it held that the engine hasn't is <paramref name="leftOut"/>.</summary>
	public SealedSession(
		IReadOnlyList<string> pool,
		IReadOnlyList<string>? leftOut = null,
		string set = "FRA"
	)
	{
		Pool = pool;
		LeftOut = leftOut ?? [];
		Set = set;
	}

	/// <summary>The seed your pool was opened from; null for a pasted pool.</summary>
	public int? Seed { get; }

	/// <summary>Your pool: pack by pack when opened from a seed, in the export's order when pasted.</summary>
	public IReadOnlyList<string> Pool { get; }

	/// <summary>Cards of a pasted pool the engine doesn't have — shown, never played.</summary>
	public IReadOnlyList<string> LeftOut { get; }

	/// <summary>An AI opponent: a pool opened from <paramref name="seed"/>, and one of its suggested builds at random (LIM-3).</summary>
	public static SealedDeck OpponentFor(int seed, string set = "FRA") =>
		SealedBuilder.Opponent(set, seed);

	/// <summary>The pool cards in your deck (basics added on top are <see cref="Basics"/>).</summary>
	public IReadOnlyList<string> FromPool => _deck;

	public int Basics(string basic) => _basics[BasicIndex(basic)];

	public IEnumerable<string> Deck =>
		_deck.Concat(BasicNames.SelectMany((b, i) => Enumerable.Repeat(b, _basics[i])));

	public int Count => _deck.Count + _basics.Sum();

	/// <summary>Arena's minimum; more is allowed.</summary>
	public bool Ready => Count >= SealedBuilder.DeckSize;

	/// <summary>The pool cards not in the deck — Extrapolate the Impossible's "outside the game" (LIM-1.6).</summary>
	public IReadOnlyList<string> Sideboard
	{
		get
		{
			var left = Pool.ToList();
			foreach (var card in _deck)
				left.Remove(card);
			return left;
		}
	}

	/// <summary>Unscripted cards would play silently vanilla (MagicCore/CLAUDE.md), so they stay in the pool.</summary>
	public static bool Playable(string card) => CardRegistry.IsImplemented(card);

	/// <summary>One more copy into the deck, if the sideboard still holds one and it can be played.</summary>
	public bool Add(string card)
	{
		if (!Playable(card) || !Sideboard.Contains(card))
			return false;
		_deck.Add(card);
		return true;
	}

	public bool Remove(string card) => _deck.Remove(card);

	public void SetBasics(string basic, int count) =>
		_basics[BasicIndex(basic)] = System.Math.Max(0, count);

	public void Clear()
	{
		_deck.Clear();
		System.Array.Clear(_basics);
		SuggestedIndex = null;
	}

	/// <summary>
	/// The builds suggested from your pool (LIM-3) — two colours first, then a splash and a greedy
	/// splash when the pool allows, then the builds of the close pair you showed (<see cref="ShowPair"/>);
	/// suggestions, never "the optimal deck".
	/// </summary>
	public IReadOnlyList<SealedDeck> Suggestions =>
		_suggestions ??= [
			.. Best,
			.. _pairBuilds.Where(p => !Best.Any(b => SealedBuilder.SameDeck(b, p))),
		];

	private IReadOnlyList<SealedDeck> Best =>
		_best ??= SealedBuilder.Suggestions(Pool, CardRatings.Of(Set));

	/// <summary>The other pairs within 1.5 points of the best, the 4 closest (LIM-3.6, 3.4, Shayne) — for you to judge.</summary>
	public IReadOnlyList<SealedBuilder.PairRating> ClosePairs =>
		_closePairs ??= SealedBuilder.ClosePairs(Pool, CardRatings.Of(Set));

	/// <summary>Which of <see cref="ClosePairs"/> has its builds among the suggestions; null for none.</summary>
	public int? ShownPair { get; private set; }

	/// <summary>Adds close pair <paramref name="which"/>'s builds to the suggestions, in place of the last one's.</summary>
	public void ShowPair(int which)
	{
		if (which == ShownPair)
			return;
		if (SuggestedIndex >= Best.Count)
			SuggestedIndex = null; // its build is going; the deck stays as it is
		ShownPair = which;
		if (!_pairCache.TryGetValue(which, out var builds))
			_pairCache[which] = builds = SealedBuilder.ForPair(
				Pool,
				CardRatings.Of(Set),
				ClosePairs[which].Pair
			);
		_pairBuilds = builds;
		_suggestions = null;
	}

	/// <summary>
	/// The console command that compares every build offered for this pool by AI-vs-AI sims (LIM-3.4,
	/// Shayne), and what it will play: its seed, or <paramref name="poolFile"/> for a pasted pool.
	/// </summary>
	public (string Command, string Note) SimCommand(string? poolFile)
	{
		var pool =
			Seed is { } seed ? $"seed:{seed}"
			: poolFile is not null ? $"\"{poolFile}\""
			: throw new System.InvalidOperationException("a pasted pool needs its file");
		var builds = SealedBuilder.AllBuilds(Pool, CardRatings.Of(Set)).Count;
		const int opponents = SealedBuildMatch.DefaultOpponents;
		return (
			$"dotnet run -c Release --project MagicConsole -- --compare={pool}",
			$"{builds} builds x {opponents} AI opponents, ~{SealedBuildMatch.EstimatedMinutes(builds, opponents):0} min on this machine."
		);
	}

	/// <summary>"White/Black 58.4% · 16 at 1–3" — a close pair's rating a card and its early curve.</summary>
	public static string PairText(SealedBuilder.PairRating p) =>
		$"{p.Pair[0]}/{p.Pair[1]} {p.Rating:P1} · {p.Cheap} at 1–3";

	/// <summary>The label of the suggestion the deck started from — kept through your edits; null once cleared.</summary>
	public string? Suggested => SuggestedIndex is { } i ? Suggestions[i].Label : null;

	/// <summary>Which of <see cref="Suggestions"/> the deck started from; null once cleared.</summary>
	public int? SuggestedIndex { get; private set; }

	/// <summary>
	/// How often suggestion <paramref name="which"/>'s spells are castable on time, and how often it
	/// landcycles early, by simulated draws (LIM-3.3, LIM-3.7 — shown, never used to choose). Measured
	/// once for all of them, ~0.1 s each.
	/// </summary>
	public Castability.Result Measured(int which)
	{
		var deck = Suggestions[which];
		if (!_measured.TryGetValue(deck, out var result))
			_measured[deck] = result = Castability.Measure(deck);
		return result;
	}

	/// <summary>Below this the text leaves the cycles out — it would read "0% of games".</summary>
	public const double CyclesShownFrom = 0.005;

	/// <summary>
	/// "castable on time 79% (Blue/Black 85%) · 16 spells at 1–3 mana · cycles in turns 1–4 in 23% of
	/// games" — the best pair's two colours is the one to beat; the cycles only when any.
	/// </summary>
	public string OnTimeText(int which) =>
		$"castable on time {Measured(which).OnTime:P0}"
		+ (which == 0 ? "" : $" ({Suggestions[0].Button} {Measured(0).OnTime:P0})")
		+ $" · {SealedBuilder.CheapSpells(Suggestions[which].Main)} spells at 1–3 mana"
		+ (
			Measured(which).CyclesEarly is var c && c >= CyclesShownFrom
				? $" · cycles in turns 1–{Castability.EarlyTurns} in {c:P0} of games"
				: ""
		);

	/// <summary>Replaces the deck with one of <see cref="Suggestions"/> — the builder the opponents use too.</summary>
	public SealedDeck Suggest(int which = 0)
	{
		var built = Suggestions[which];
		Clear();
		SuggestedIndex = which;
		foreach (var card in built.Main)
		{
			var basic = BasicNames.ToList().IndexOf(card);
			if (basic >= 0)
				_basics[basic]++;
			else
				_deck.Add(card);
		}
		return built;
	}

	/// <summary>
	/// Your deck against a fresh AI opponent (<see cref="OpponentFor"/>), both from <paramref name="gameSeed"/>
	/// — which also seeds the game, so the table's seed replays the opponent too — or against
	/// <paramref name="opponent"/> (a draft's bot, LIM-2.3).
	/// </summary>
	public TableSetup ToSetup(bool search, int gameSeed, SealedDeck? opponent = null)
	{
		opponent ??= OpponentFor(gameSeed, Set);
		return new(
			[.. Deck.Select(n => CardRegistry.Get(n, Set))],
			[.. opponent.Main.Select(n => CardRegistry.Get(n, Set))],
			gameSeed,
			AiVsAi: false,
			search
		)
		{
			Back = "res://MagicTable/SealedScene.tscn",
			// outside the game: each pool's unplayed cards — every one playable, Extrapolate's options
			MySideboard = [.. Sideboard.Where(Playable).Select(n => CardRegistry.Get(n, Set))],
			TheirSideboard =
			[
				.. opponent.Sideboard.Where(Playable).Select(n => CardRegistry.Get(n, Set)),
			],
		};
	}

	private static int BasicIndex(string basic) =>
		BasicNames.ToList().IndexOf(basic) is var i && i >= 0
			? i
			: throw new System.ArgumentException($"{basic} is not a basic land", nameof(basic));
}
