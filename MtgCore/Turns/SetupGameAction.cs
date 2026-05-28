using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Performs all pre-game setup before the first turn begins:
///   1. Shuffles both players' libraries
///   2. Draws opening hands for both players, guaranteeing OpeningHandLandCount lands
///      and OpeningHandSize - OpeningHandLandCount non-lands.
///
/// Spawned by BeginGameAction before StartTurnAction so that both players
/// have a full hand when the first turn starts.
///
/// The presentation layer never needs to know about this action — it is
/// an implementation detail of BeginGame().
/// </summary>
public record SetupGameAction : GameAction
{
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }
	public int OpeningHandSize { get; init; } = 7;

	/// <summary>
	/// Number of lands guaranteed in the opening hand. Defaults to 3.
	/// If the deck has fewer lands than this, takes all available lands.
	/// </summary>
	public int OpeningHandLandCount { get; init; } = 3;

	/// <summary>
	/// Seed used to shuffle both libraries. 0 = random (default).
	/// Player 1 uses this seed directly; Player 2 uses seed + 1.
	/// </summary>
	public int ShuffleSeed { get; init; } = 0;

	/// <summary>
	/// Seed used for in-game randomness (random discard, random targeting, etc.).
	/// 0 = unseeded (default). Written into GameState.RngSeed at game start.
	/// </summary>
	public int GameRngSeed { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState with { RngSeed = GameRngSeed };

		// Shuffle both libraries
		var rng1 = ShuffleSeed == 0 ? null : new Random(ShuffleSeed);
		var rng2 = ShuffleSeed == 0 ? null : new Random(ShuffleSeed + 1);
		state = state.ShuffleLibrary(Player1Id, rng1);
		state = state.ShuffleLibrary(Player2Id, rng2);

		// Draw opening hands for both players
		var (state1, events1) = DrawOpeningHand(state, Player1Id);
		var (state2, events2) = DrawOpeningHand(state1, Player2Id);

		return new ActionResult(state2) { Events = events1.AddRange(events2) };
	}

	private (GameState, ImmutableList<GameEvent>) DrawOpeningHand(GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var events = ImmutableList<GameEvent>.Empty;

		// Split the shuffled library into lands and non-lands, preserving shuffle order
		// within each group so selection is still random.
		var allCards = state
			.GetChildrenIds(libraryId)
			.Select(id => state.GetObject(id) as Card)
			.Where(c => c != null)
			.ToList();

		var lands = allCards.Where(c => c!.HasSubtype("Land")).ToList();
		var nonLands = allCards.Where(c => !c!.HasSubtype("Land")).ToList();

		var landsToDraw = Math.Min(OpeningHandLandCount, lands.Count);
		var nonLandsToDraw = Math.Min(OpeningHandSize - landsToDraw, nonLands.Count);

		var handCards = lands.Take(landsToDraw).Concat(nonLands.Take(nonLandsToDraw));
		foreach (var card in handCards)
		{
			state = state.MoveObject(card!.Id, handId);
			events = events.Add(new CardDrawnEvent { PlayerId = playerId, CardId = card.Id });
		}

		return (state, events);
	}
}
