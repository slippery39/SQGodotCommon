using System.Collections.Immutable;
using System.Text.Json;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// A played game, kept so it replays exactly (story TBL-5): the seed, the players' names (prompts
/// carry them, so fingerprints do), both decks and sideboards by card name, and EVERY transition the
/// game took — a click, an AI move, a pass your stops made, a pass the rules forced — as a
/// <see cref="MoveTokens"/> token, with a fingerprint of what the position offered. A replay is NewGame
/// and each token in turn: no AutoPass, no stop settings to reproduce. A position that offers
/// something else, or a game that ends differently, throws <see cref="MoveTokens.StaleException"/> —
/// the engine has moved; a record never silently replays another game.
/// </summary>
public sealed record GameRecord
{
	/// <summary>Raised when the format itself changes; an older record says so instead of misreading.</summary>
	public const int CurrentFormat = 1;

	public int Format { get; init; } = CurrentFormat;
	public int Seed { get; init; }
	public ImmutableArray<string> Players { get; init; } = ["Player1", "Player2"];
	public ImmutableArray<string> Deck1 { get; init; } = [];
	public ImmutableArray<string> Deck2 { get; init; } = [];
	public ImmutableArray<string> Sideboard1 { get; init; } = [];
	public ImmutableArray<string> Sideboard2 { get; init; } = [];

	/// <summary>What made the game: which seat was human, which AI, when — free text for a reader.</summary>
	public ImmutableDictionary<string, string> About { get; init; } =
		ImmutableDictionary<string, string>.Empty;

	public ImmutableList<Move> Moves { get; init; } = [];

	/// <summary>How it ended (<see cref="MoveTokens.Ending"/>) — set when the game is over.</summary>
	public string? End { get; init; }

	/// <param name="Token">The move (<see cref="MoveTokens"/>).</param>
	/// <param name="Kind">Who made it: <c>you</c> (clicked), <c>stop</c> (your stops passed for you),
	/// <c>rule</c> (a pass was the only legal move), <c>ai</c>, <c>sandbox</c> (the playtest tool),
	/// <c>auto</c> (a stand-in played your seat).</param>
	/// <param name="By">The deciding player: 1 or 2.</param>
	/// <param name="Offers">What the position offered (<see cref="MoveTokens.Offers"/>).</param>
	/// <param name="Said">The move as the log said it; empty for passes.</param>
	public sealed record Move(string Token, string Kind, int By, string Offers, string Said = "");

	public static GameRecord New(
		int seed,
		IEnumerable<CardDefinition> deck1,
		IEnumerable<CardDefinition> deck2,
		(string Player1, string Player2) names,
		IEnumerable<CardDefinition>? sideboard1 = null,
		IEnumerable<CardDefinition>? sideboard2 = null
	) =>
		new()
		{
			Seed = seed,
			Players = [names.Player1, names.Player2],
			Deck1 = [.. deck1.Select(c => c.Data.Name)],
			Deck2 = [.. deck2.Select(c => c.Data.Name)],
			Sideboard1 = [.. (sideboard1 ?? []).Select(c => c.Data.Name)],
			Sideboard2 = [.. (sideboard2 ?? []).Select(c => c.Data.Name)],
		};

	/// <summary>The game as it began — what the first move applies to.</summary>
	// ponytail: names, not printings — a replay shows each card's first printing (MC-5.5); record the
	// set per card if a TRK game's replay must show TRK's basics.
	public GameState Start() =>
		MagicRules.NewGame(
			Deck1.Select(CardRegistry.Get),
			Deck2.Select(CardRegistry.Get),
			Seed,
			names: (Players[0], Players[1]),
			sideboard1: Sideboard1.Select(CardRegistry.Get),
			sideboard2: Sideboard2.Select(CardRegistry.Get)
		);

	/// <summary>One more move, made from <paramref name="before"/>.</summary>
	public GameRecord Add(GameState before, string token, string kind, string said = "") =>
		this with
		{
			Moves = Moves.Add(
				new Move(
					token,
					kind,
					before.DecidingPlayerId() == before.Player1Id() ? 1 : 2,
					MoveTokens.Offers(before),
					said
				)
			),
		};

	/// <summary>Back to its first <paramref name="count"/> moves — an undo.</summary>
	public GameRecord Truncate(int count) =>
		this with
		{
			Moves = Moves.Take(Math.Clamp(count, 0, Moves.Count)).ToImmutableList(),
			End = null,
		};

	public GameRecord Ended(GameState s) => this with { End = MoveTokens.Ending(s) };

	/// <summary>A record of another format is stale — said, never misread.</summary>
	public void CheckFormat()
	{
		if (Format != CurrentFormat)
			throw new MoveTokens.StaleException(
				$"a format-{Format} record; this engine reads format {CurrentFormat}"
			);
	}

	/// <summary>Plays it back: the position before each move, and the last one.</summary>
	public (IReadOnlyList<GameState> Before, GameState Last) Replay()
	{
		CheckFormat();
		GameState s;
		try
		{
			s = Start();
		}
		catch (KeyNotFoundException e) // CardRegistry.Get: a card renamed or gone since
		{
			throw new MoveTokens.StaleException($"a deck card no longer exists ({e.Message})");
		}
		var before = new List<GameState>(Moves.Count);
		for (var i = 0; i < Moves.Count; i++)
		{
			var move = Moves[i];
			while (UnaskedOrder(s, move.Offers) is { } answer)
				s = MoveTokens.Apply(s, answer);
			if (MoveTokens.Offers(s) != move.Offers)
				throw new MoveTokens.StaleException(
					$"move {i + 1} ({$"{move.Kind} {move.Said}".Trim()}): the position no longer offers what it did — the engine has moved"
				);
			before.Add(s);
			s = MoveTokens.Apply(s, move.Token);
		}
		if (End is not null && MoveTokens.Ending(s) != End)
			throw new MoveTokens.StaleException(
				"every move replays, but the game ends differently"
			);
		return (before, s);
	}

	/// <summary>
	/// A record made before TBL-9.2 never answered a surveil's "the rest on top in any order" — the engine kept
	/// their order then. Asked now and not answered by the record: the answer that keeps that order, so the record
	/// replays as it played (and its move numbers stay put). Null when the record does answer it.
	/// </summary>
	public static string? UnaskedOrder(GameState s, string recordedOffers) =>
		s.GetPendingChoice() is OrderLibraryTopChoice order
		&& MoveTokens.Offers(s) != recordedOffers
			? "o" + string.Join(",", order.Options.Select(o => o.Id))
			: null;

	/// <summary>The position before move <paramref name="at"/> (from 1, as the transcript numbers it).</summary>
	public GameState Before(int at) =>
		at >= 1 && at <= Moves.Count
			? Replay().Before[at - 1]
			: throw new ArgumentOutOfRangeException(nameof(at), $"moves are 1 to {Moves.Count}");

	/// <summary>
	/// The game as text (story TBL-5.4): <c>#57 T3 You [you] Cast Last Gasp …</c>, one line a move,
	/// then how it stands. A move's number counts from 1 — the console's <c>--at</c>, and
	/// <see cref="Before"/>'s. Passes made FOR a player (<c>rule</c>, <c>stop</c>) only with
	/// <paramref name="passes"/>. Replays first, so a stale record throws before a line is written.
	/// </summary>
	public IReadOnlyList<string> Transcript(bool passes = false)
	{
		var (before, last) = Replay();
		var lines = new List<string>();
		for (var i = 0; i < Moves.Count; i++)
		{
			var (move, s) = (Moves[i], before[i]);
			if (passes || move.Kind is not ("rule" or "stop"))
				lines.Add(
					$"#{i + 1} T{s.GetGame().TurnNumber} {Players[move.By - 1]} [{move.Kind}] {MoveTokens.Describe(s, move.Token)}"
				);
		}
		var turn = last.GetGame().TurnNumber;
		lines.Add(
			!last.IsOver() ? $"Not over: turn {turn}, {Moves.Count} moves."
			: last.WinnerId() is var winner and not 0
				? $"{last.GetPlayer(winner).Name} won on turn {turn}."
			: $"A draw on turn {turn}."
		);
		return lines;
	}

	static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NewLine = "\n" };

	public string ToJson() => JsonSerializer.Serialize(this, Json);

	public static GameRecord FromJson(string json) =>
		JsonSerializer.Deserialize<GameRecord>(json, Json)!;
}
