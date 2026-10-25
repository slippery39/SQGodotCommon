using System.Collections.Generic;
using System.Linq;
using MagicCore.Limited;
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

	private readonly List<string> _deck = new();
	private readonly int[] _basics = new int[5];
	private IReadOnlyList<SealedDeck>? _suggestions;
	private double[]? _onTime;

	/// <summary>Six packs opened from <paramref name="seed"/> — one number replays your pool.</summary>
	public SealedSession(int seed)
		: this(Open(seed)) => Seed = seed;

	/// <summary>A pool you opened elsewhere (<see cref="ArenaDeck.ReadPool"/>); what it held that the engine hasn't is <paramref name="leftOut"/>.</summary>
	public SealedSession(IReadOnlyList<string> pool, IReadOnlyList<string>? leftOut = null)
	{
		Pool = pool;
		LeftOut = leftOut ?? [];
	}

	/// <summary>The seed your pool was opened from; null for a pasted pool.</summary>
	public int? Seed { get; }

	/// <summary>Your pool: pack by pack when opened from a seed, in the export's order when pasted.</summary>
	public IReadOnlyList<string> Pool { get; }

	/// <summary>Cards of a pasted pool the engine doesn't have — shown, never played.</summary>
	public IReadOnlyList<string> LeftOut { get; }

	/// <summary>An AI opponent: a pool opened from <paramref name="seed"/>, and one of its suggested builds at random (LIM-3).</summary>
	public static SealedDeck OpponentFor(int seed) => SealedBuilder.Opponent(seed);

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
	public static bool Playable(string card) => FraCards.IsImplemented(card);

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
	/// splash when the pool allows; suggestions, never "the optimal deck".
	/// </summary>
	public IReadOnlyList<SealedDeck> Suggestions =>
		_suggestions ??= SealedBuilder.Suggestions(Pool, CardRatings.Fra);

	/// <summary>The label of the suggestion the deck started from — kept through your edits; null once cleared.</summary>
	public string? Suggested => SuggestedIndex is { } i ? Suggestions[i].Label : null;

	/// <summary>Which of <see cref="Suggestions"/> the deck started from; null once cleared.</summary>
	public int? SuggestedIndex { get; private set; }

	/// <summary>
	/// How often suggestion <paramref name="which"/>'s spells are castable on time, by simulated draws
	/// (LIM-3.3 — shown, never used to choose). Measured once for all of them, ~0.1 s each.
	/// </summary>
	public double OnTime(int which) =>
		(_onTime ??= [.. Suggestions.Select(d => Castability.OnTime(d))])[which];

	/// <summary>"castable on time 79% (two colours 85%)" — the two-colour build is the one to beat.</summary>
	public string OnTimeText(int which) =>
		$"castable on time {OnTime(which):P0}"
		+ (which == 0 ? "" : $" (two colours {OnTime(0):P0})");

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
	/// — which also seeds the game, so the table's seed replays the opponent too.
	/// </summary>
	public TableSetup ToSetup(bool search, int gameSeed)
	{
		var opponent = OpponentFor(gameSeed);
		return new(
			[.. Deck.Select(FraCards.Get)],
			[.. opponent.Main.Select(FraCards.Get)],
			gameSeed,
			AiVsAi: false,
			search
		)
		{
			Back = "res://MagicTable/SealedScene.tscn",
			// outside the game: each pool's unplayed cards — every one playable, Extrapolate's options
			MySideboard = [.. Sideboard.Where(Playable).Select(FraCards.Get)],
			TheirSideboard = [.. opponent.Sideboard.Where(Playable).Select(FraCards.Get)],
		};
	}

	private static int BasicIndex(string basic) =>
		BasicNames.ToList().IndexOf(basic) is var i && i >= 0
			? i
			: throw new System.ArgumentException($"{basic} is not a basic land", nameof(basic));

	private static IReadOnlyList<string> Open(int seed) => Boosters.SealedPool(seed);
}
