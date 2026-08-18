using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// MTG-specific query helpers on GameState.
/// These are read-only — no state mutations live here.
/// </summary>
public static class MtgGameStateExtensions
{
	// ===== PLAYER QUERIES =====

	public static MtgPlayer GetPlayer(this GameState state, int playerId) =>
		(MtgPlayer)state.GetObject(playerId);

	// ===== GAME / TURN STATE QUERIES =====

	/// <summary>
	/// Returns the MtgGame root object via a direct registry lookup. O(1).
	/// </summary>
	public static MtgGame GetGame(this GameState state) =>
		(MtgGame)state.GetObject(state.GetWellKnownId(MtgObjectKeys.Game));

	/// <summary>
	/// Returns the MtgGame root object via a known ID. Use when the caller already
	/// holds the game ID (e.g. CheckStateBasedEffectsAction).
	/// </summary>
	public static MtgGame GetGame(this GameState state, int gameId) =>
		(MtgGame)state.GetObject(gameId);

	/// <summary>
	/// Returns the MtgGame root object, or null if the registry is not yet populated.
	/// Prefer GetGame() in all normal gameplay code.
	/// </summary>
	public static MtgGame? TryGetGame(this GameState state) =>
		state.WellKnownIds.TryGetValue(MtgObjectKeys.Game, out var id)
			? (MtgGame)state.GetObject(id)
			: null;

	/// <summary>
	/// Returns the ID of the player whose turn it currently is.
	/// </summary>
	public static int GetActivePlayerId(this GameState state) => state.GetGame().ActivePlayerId;

	/// <summary>
	/// Returns the opponent's player ID given the active player ID and both player IDs.
	/// </summary>
	public static int GetOpponentId(
		this GameState state,
		int activePlayerId,
		int player1Id,
		int player2Id
	) => activePlayerId == player1Id ? player2Id : player1Id;

	// ===== DECK OPERATIONS =====

	/// <summary>
	/// Shuffles a player's library by randomly reordering the cards within it.
	/// Pure helper — no action, no events. Use this for game setup only.
	/// If a shuffle effect needs to occur during gameplay, implement it as a GameAction instead.
	/// </summary>
	public static GameState ShuffleLibrary(this GameState state, int playerId, Random? rng = null)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var cardIds = state.GetChildrenIds(libraryId).ToList();

		rng ??= new Random();
		for (int i = cardIds.Count - 1; i > 0; i--)
		{
			var j = rng.Next(i + 1);
			(cardIds[i], cardIds[j]) = (cardIds[j], cardIds[i]);
		}

		// Rebuild the parent-to-children mapping in the new order
		// by moving each card to the zone in shuffled order
		// We do this by removing all cards and re-adding in new order
		var tempState = state;
		foreach (var cardId in cardIds)
			tempState = tempState.MoveObject(cardId, 0); // detach from library

		foreach (var cardId in cardIds)
			tempState = tempState.MoveObject(cardId, libraryId); // re-attach in shuffled order

		return tempState;
	}

	// ===== GAME ACTIONS =====

	/// <summary>
	/// Begins the game. Shuffles both libraries, deals opening hands, then kicks off
	/// the first player's turn. Presentation layers call this once at startup and
	/// respond to the events — no knowledge of BeginGameAction or SetupGameAction required.
	/// </summary>
	public static (GameState State, ImmutableList<GameEvent> Events) BeginGame(
		this GameState state,
		int gameId,
		int player1Id,
		int player2Id,
		int shuffleSeed = 0,
		int gameRngSeed = 0
	)
	{
		return state
			.AddAction(
				new BeginGameAction
				{
					GameId = gameId,
					Player1Id = player1Id,
					Player2Id = player2Id,
					ShuffleSeed = shuffleSeed,
					GameRngSeed = gameRngSeed,
				}
			)
			.ProcessAllActions();
	}

	// ===== ZONE QUERIES =====

	public static Zone GetZone(this GameState state, int zoneId) => (Zone)state.GetObject(zoneId);

	/// <summary>
	/// Returns the ID of the given zone type owned by the given player. O(1) via registry.
	/// Valid for: Hand, Library, Graveyard, Battlefield, Exile.
	/// </summary>
	public static int GetPlayerZoneId(this GameState state, int playerId, ZoneType zoneType)
	{
		var isP1 = state.GetWellKnownId(MtgObjectKeys.Player1) == playerId;
		var key = (isP1, zoneType) switch
		{
			(true, ZoneType.Hand) => MtgObjectKeys.Player1Hand,
			(true, ZoneType.Library) => MtgObjectKeys.Player1Library,
			(true, ZoneType.Graveyard) => MtgObjectKeys.Player1Graveyard,
			(true, ZoneType.Battlefield) => MtgObjectKeys.Player1Battlefield,
			(true, ZoneType.Exile) => MtgObjectKeys.Player1Exile,
			(false, ZoneType.Hand) => MtgObjectKeys.Player2Hand,
			(false, ZoneType.Library) => MtgObjectKeys.Player2Library,
			(false, ZoneType.Graveyard) => MtgObjectKeys.Player2Graveyard,
			(false, ZoneType.Battlefield) => MtgObjectKeys.Player2Battlefield,
			(false, ZoneType.Exile) => MtgObjectKeys.Player2Exile,
			_ => throw new InvalidOperationException($"No well-known key for zone {zoneType}"),
		};
		return state.GetWellKnownId(key);
	}

	/// <summary>
	/// Returns the Zone of the given type owned by the given player. O(1) via registry.
	/// Valid for: Hand, Library, Graveyard, Battlefield, Exile.
	/// </summary>
	public static Zone GetPlayerZone(this GameState state, int playerId, ZoneType zoneType) =>
		state.GetZone(state.GetPlayerZoneId(playerId, zoneType));

	/// <summary>
	/// Returns the Stack zone via a direct registry lookup. O(1).
	/// </summary>
	public static Zone GetStack(this GameState state) =>
		state.GetZone(state.GetWellKnownId(MtgObjectKeys.Stack));

	public static int GetStackId(this GameState state) => state.GetWellKnownId(MtgObjectKeys.Stack);

	// ===== CARD QUERIES =====

	/// <summary>
	/// Returns all cards currently in the given zone.
	/// </summary>
	public static IEnumerable<Card> GetCardsInZone(this GameState state, int zoneId)
	{
		foreach (var id in state.GetChildrenIds(zoneId))
			if (state.GetObject(id) is Card card)
				yield return card;
	}

	/// <summary>
	/// Returns the zone ID that currently contains this card.
	/// </summary>
	public static int GetCardZoneId(this GameState state, int cardId) =>
		state.GetParent(cardId)
		?? throw new InvalidOperationException($"Card {cardId} has no parent zone");

	/// <summary>
	/// Returns the zone that currently contains this card.
	/// </summary>
	public static Zone GetCardZone(this GameState state, int cardId) =>
		state.GetZone(state.GetCardZoneId(cardId));

	/// <summary>
	/// True if the casting player may cast/play this card from where it currently sits: their
	/// hand, or their exile zone if it carries ExiledPlayableComponent (impulse draw — "exile the
	/// top card of your library, you may play it this turn").
	///
	/// The single entry point for all three cast actions' "is this card available to you" check,
	/// so impulse draw did not need a bespoke copy of that logic in each one.
	/// </summary>
	public static bool IsInCastableZone(this GameState state, int cardId, int castingPlayerId)
	{
		var zoneId = state.GetCardZoneId(cardId);
		if (zoneId == state.GetPlayerZoneId(castingPlayerId, ZoneType.Hand))
			return true;

		if (zoneId != state.GetPlayerZoneId(castingPlayerId, ZoneType.Exile))
			return false;

		return ((Card)state.GetObject(cardId)).HasComponent<ExiledPlayableComponent>();
	}
}
