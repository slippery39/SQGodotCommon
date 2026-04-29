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
	/// Returns the MtgGame root object, which holds global turn state.
	/// </summary>
	public static MtgGame GetGame(this GameState state, int gameId) =>
		(MtgGame)state.GetObject(gameId);

	/// <summary>
	/// Finds the MtgGame object without knowing its ID. There is exactly one per game state.
	/// Used by actions that need global turn state but don't carry GameId.
	/// </summary>
	public static MtgGame? TryGetGame(this GameState state) =>
		state.IdToGameObjectMap.Values.OfType<MtgGame>().FirstOrDefault();

	/// <summary>
	/// Returns the ID of the player whose turn it currently is.
	/// </summary>
	public static int GetActivePlayerId(this GameState state, int gameId) =>
		state.GetGame(gameId).ActivePlayerId;

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
	public static GameState ShuffleLibrary(this GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		var cardIds = state.GetChildrenIds(libraryId).ToList();

		var rng = new Random();
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
		int player2Id
	)
	{
		return state
			.AddAction(
				new BeginGameAction
				{
					GameId = gameId,
					Player1Id = player1Id,
					Player2Id = player2Id,
				}
			)
			.ProcessAllActions();
	}

	// ===== ZONE QUERIES =====

	public static Zone GetZone(this GameState state, int zoneId) => (Zone)state.GetObject(zoneId);

	/// <summary>
	/// Returns the Zone of the given type owned by the given player.
	/// Valid for: Hand, Library, Graveyard, Battlefield, Exile.
	/// </summary>
	public static Zone GetPlayerZone(this GameState state, int playerId, ZoneType zoneType)
	{
		return state.GetChildren(playerId).OfType<Zone>().First(z => z.ZoneType == zoneType);
	}

	public static int GetPlayerZoneId(this GameState state, int playerId, ZoneType zoneType) =>
		state.GetPlayerZone(playerId, zoneType).Id;

	/// <summary>
	/// Returns the Stack zone. There is exactly one Stack zone per game state.
	/// </summary>
	public static Zone GetStack(this GameState state) =>
		state.IdToGameObjectMap.Values.OfType<Zone>().First(z => z.ZoneType == ZoneType.Stack);

	public static int GetStackId(this GameState state) => state.GetStack().Id;

	// ===== CARD QUERIES =====

	/// <summary>
	/// Returns all cards currently in the given zone.
	/// </summary>
	public static IEnumerable<Card> GetCardsInZone(this GameState state, int zoneId) =>
		state.GetChildren(zoneId).OfType<Card>();

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
}
