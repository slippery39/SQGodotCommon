using System.Collections.Immutable;
using System.Text.Json;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Sets.Fra;

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
	public GameState Start() =>
		MagicRules.NewGame(
			Deck1.Select(FraCards.Get),
			Deck2.Select(FraCards.Get),
			Seed,
			names: (Players[0], Players[1]),
			sideboard1: Sideboard1.Select(FraCards.Get),
			sideboard2: Sideboard2.Select(FraCards.Get)
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

	/// <summary>Plays it back: the position before each move, and the last one.</summary>
	public (IReadOnlyList<GameState> Before, GameState Last) Replay()
	{
		if (Format != CurrentFormat)
			throw new MoveTokens.StaleException(
				$"a format-{Format} record; this engine reads format {CurrentFormat}"
			);
		var s = Start();
		var before = new List<GameState>(Moves.Count);
		for (var i = 0; i < Moves.Count; i++)
		{
			var move = Moves[i];
			if (MoveTokens.Offers(s) != move.Offers)
				throw new MoveTokens.StaleException(
					$"move {i + 1} ({move.Kind} {move.Said}): the position no longer offers what it did — the engine has moved"
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

	static readonly JsonSerializerOptions Json = new() { WriteIndented = true, NewLine = "\n" };

	public string ToJson() => JsonSerializer.Serialize(this, Json);

	public static GameRecord FromJson(string json) =>
		JsonSerializer.Deserialize<GameRecord>(json, Json)!;
}
