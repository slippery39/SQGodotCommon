using System.Collections.Generic;
using System.Linq;
using MagicCore.Limited;

namespace MagicTable;

/// <summary>
/// An FRA Quick Draft on the table, plain C# (no Godot types), story LIM-2.3: you in seat 0 and 7 bots
/// (<see cref="Draft"/>), then your 42 picks to the Sealed screen's deck builder (Shayne, 2026-10-29),
/// then a run as Arena's Quick Draft runs — until <see cref="WinsToEnd"/> wins or
/// <see cref="LossesToEnd"/> losses — against the bots' own decks, one after another.
/// </summary>
public sealed class DraftSession
{
	public const int WinsToEnd = 7;
	public const int LossesToEnd = 3;

	private SealedSession? _builder;
	private readonly Dictionary<int, SealedDeck> _botDecks = new();

	public DraftSession(int seed)
	{
		Seed = seed;
		State = Draft.Fra(seed);
		PackSize = State.Seats[0].Pack.Count;
	}

	/// <summary>The seed the packs were opened from — one number replays the draft.</summary>
	public int Seed { get; }

	public DraftState State { get; private set; }

	public int PackSize { get; }

	public bool IsComplete => State.IsComplete;

	/// <summary>The pack in front of you.</summary>
	public IReadOnlyList<string> Pack => State.Seats[0].Pack;

	/// <summary>Your picks, in order.</summary>
	public IReadOnlyList<string> Picks => State.Seats[0].Picks;

	/// <summary>"Pack 2 · Pick 5" — the pick you are making now.</summary>
	public string Where =>
		$"Pack {State.Round + 1} · Pick {Picks.Count - State.Round * PackSize + 1}";

	/// <summary>You take card <paramref name="index"/> of your pack; every bot picks at the same time.</summary>
	public void Pick(int index)
	{
		if (IsComplete || index < 0 || index >= Pack.Count)
			return;
		State = Draft.Next(State, CardRatings.Fra, seat0: index);
	}

	/// <summary>Your picks as a pool for the Sealed screen's deck builder — made once, so your deck survives each game.</summary>
	public SealedSession Builder =>
		_builder ??= IsComplete
			? new SealedSession(Picks)
			: throw new System.InvalidOperationException("the draft isn't over");

	public int Wins { get; private set; }

	public int Losses { get; private set; }

	public bool RunOver => Wins >= WinsToEnd || Losses >= LossesToEnd;

	/// <summary>The seat you face next (1–7): each bot in turn, round again after the seventh.</summary>
	public int NextBot => (Wins + Losses) % (State.Seats.Count - 1) + 1;

	/// <summary>
	/// A bot's deck: the sealed builder's best two-colour build of its picks — what an AI opponent's
	/// sealed deck is too. ponytail: always the first suggestion, never the splash an AI sealed
	/// opponent sometimes plays (LIM-3); vary it if every bot's deck feels the same.
	/// </summary>
	public SealedDeck BotDeck(int seat) =>
		_botDecks.TryGetValue(seat, out var deck)
			? deck
			: _botDecks[seat] = SealedBuilder.Suggestions(State.Seats[seat].Picks, CardRatings.Fra)[
				0
			];

	/// <summary>A game of the run is over; once the run is over nothing more counts.</summary>
	public void Record(bool won)
	{
		if (RunOver)
			return;
		if (won)
			Wins++;
		else
			Losses++;
	}

	/// <summary>"3–1 · next: seat 5" — the run so far; "Run over: 7–2" at the end.</summary>
	public string RunText =>
		RunOver ? $"Run over: {Wins}–{Losses}" : $"{Wins}–{Losses} · next: seat {NextBot + 1}";

	/// <summary>Your deck against the next bot's, from <paramref name="gameSeed"/>; the table reports who won.</summary>
	public TableSetup ToSetup(bool search, int gameSeed) =>
		Builder.ToSetup(search, gameSeed, BotDeck(NextBot)) with
		{
			Finished = Record,
		};
}
