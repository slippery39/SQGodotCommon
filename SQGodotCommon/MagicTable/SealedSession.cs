using System.Collections.Generic;
using System.Linq;
using MagicCore.Limited;
using MagicCore.Sets.Fra;

namespace MagicTable;

/// <summary>
/// A sealed event's deck building, plain C# (no Godot types), story LIM-1: your six packs and the
/// AI's, the deck you are building from your pool, free basics, the suggested build — and the
/// table setup that plays your deck against the AI's own build from ITS pool. The seed makes both
/// pools: yours from <c>seed</c>, the AI's from <c>seed + 1</c>, so one number replays the event.
/// </summary>
public sealed class SealedSession
{
	public static IReadOnlyList<string> BasicNames => SealedBuilder.Basics;

	private readonly List<string> _deck = new();
	private readonly int[] _basics = new int[5];

	public SealedSession(int seed)
	{
		Seed = seed;
		Pool = Open(seed);
		OpponentPool = Open(seed + 1);
		Opponent = SealedBuilder.Build(OpponentPool, CardRatings.Fra);
	}

	public int Seed { get; }

	/// <summary>Your pool, pack by pack.</summary>
	public IReadOnlyList<string> Pool { get; }

	public IReadOnlyList<string> OpponentPool { get; }

	/// <summary>The AI's build from its own pool — what you play against.</summary>
	public SealedDeck Opponent { get; }

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
	}

	/// <summary>Replaces the deck with the AI's build of your pool — the same builder the opponent used.</summary>
	public SealedDeck Suggest()
	{
		var built = SealedBuilder.Build(Pool, CardRatings.Fra);
		Clear();
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

	/// <summary>Your deck against the AI's build, on this event's seed.</summary>
	public TableSetup ToSetup(bool search) =>
		new(
			[.. Deck.Select(FraCards.Get)],
			[.. Opponent.Main.Select(FraCards.Get)],
			Seed,
			AiVsAi: false,
			search
		)
		{
			Back = "res://MagicTable/SealedScene.tscn",
		};

	private static int BasicIndex(string basic) =>
		BasicNames.ToList().IndexOf(basic) is var i && i >= 0
			? i
			: throw new System.ArgumentException($"{basic} is not a basic land", nameof(basic));

	private static IReadOnlyList<string> Open(int seed) =>
		[.. Boosters.Sealed(seed).SelectMany(p => p).Select(c => c.Card)];
}
