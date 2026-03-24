using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Builds a fresh GameState with all players and zones correctly wired up.
///
/// Zone ownership:
///   Per-player: Hand, Library, Graveyard, Battlefield, Exile
///   Shared (child of MtgGame): Stack only
///
/// After calling Create(), no cards are present — decks are loaded separately.
/// </summary>
public static class MtgGameFactory
{
	public static (GameState State, MtgGameIds Ids) Create()
	{
		var state = new GameState();

		// Root game object
		var (s1, game) = state.AddObject(new MtgGame { Name = "Game" });

		// Shared zone — Stack only
		var (s2, stack) = s1.AddObject(
			new Zone
			{
				Name = "Stack",
				ZoneType = ZoneType.Stack,
				IsPublic = true,
			},
			parentId: game.Id
		);

		// Player 1 and their zones
		var (s3, player1) = s2.AddObject(
			new MtgPlayer { Name = "Player 1", Life = 20 },
			parentId: game.Id
		);
		var (s4, p1Hand) = s3.AddObject(
			new Zone
			{
				Name = "Player 1 Hand",
				ZoneType = ZoneType.Hand,
				IsPublic = false,
			},
			parentId: player1.Id
		);
		var (s5, p1Library) = s4.AddObject(
			new Zone
			{
				Name = "Player 1 Library",
				ZoneType = ZoneType.Library,
				IsPublic = false,
			},
			parentId: player1.Id
		);
		var (s6, p1Graveyard) = s5.AddObject(
			new Zone
			{
				Name = "Player 1 Graveyard",
				ZoneType = ZoneType.Graveyard,
				IsPublic = true,
			},
			parentId: player1.Id
		);
		var (s7, p1Battlefield) = s6.AddObject(
			new Zone
			{
				Name = "Player 1 Battlefield",
				ZoneType = ZoneType.Battlefield,
				IsPublic = true,
			},
			parentId: player1.Id
		);
		var (s8, p1Exile) = s7.AddObject(
			new Zone
			{
				Name = "Player 1 Exile",
				ZoneType = ZoneType.Exile,
				IsPublic = true,
			},
			parentId: player1.Id
		);

		// Player 2 and their zones
		var (s9, player2) = s8.AddObject(
			new MtgPlayer { Name = "Player 2", Life = 20 },
			parentId: game.Id
		);
		var (s10, p2Hand) = s9.AddObject(
			new Zone
			{
				Name = "Player 2 Hand",
				ZoneType = ZoneType.Hand,
				IsPublic = false,
			},
			parentId: player2.Id
		);
		var (s11, p2Library) = s10.AddObject(
			new Zone
			{
				Name = "Player 2 Library",
				ZoneType = ZoneType.Library,
				IsPublic = false,
			},
			parentId: player2.Id
		);
		var (s12, p2Graveyard) = s11.AddObject(
			new Zone
			{
				Name = "Player 2 Graveyard",
				ZoneType = ZoneType.Graveyard,
				IsPublic = true,
			},
			parentId: player2.Id
		);
		var (s13, p2Battlefield) = s12.AddObject(
			new Zone
			{
				Name = "Player 2 Battlefield",
				ZoneType = ZoneType.Battlefield,
				IsPublic = true,
			},
			parentId: player2.Id
		);
		var (s14, p2Exile) = s13.AddObject(
			new Zone
			{
				Name = "Player 2 Exile",
				ZoneType = ZoneType.Exile,
				IsPublic = true,
			},
			parentId: player2.Id
		);

		var ids = new MtgGameIds(
			GameId: game.Id,
			StackId: stack.Id,
			Player1Id: player1.Id,
			Player1HandId: p1Hand.Id,
			Player1LibraryId: p1Library.Id,
			Player1GraveyardId: p1Graveyard.Id,
			Player1BattlefieldId: p1Battlefield.Id,
			Player1ExileId: p1Exile.Id,
			Player2Id: player2.Id,
			Player2HandId: p2Hand.Id,
			Player2LibraryId: p2Library.Id,
			Player2GraveyardId: p2Graveyard.Id,
			Player2BattlefieldId: p2Battlefield.Id,
			Player2ExileId: p2Exile.Id
		);

		return (s14, ids);
	}
}

/// <summary>
/// Holds all the well-known IDs for a freshly created game.
/// Passed around so callers don't need to query for these fixed objects.
/// </summary>
public record MtgGameIds(
	int GameId,
	int StackId,
	int Player1Id,
	int Player1HandId,
	int Player1LibraryId,
	int Player1GraveyardId,
	int Player1BattlefieldId,
	int Player1ExileId,
	int Player2Id,
	int Player2HandId,
	int Player2LibraryId,
	int Player2GraveyardId,
	int Player2BattlefieldId,
	int Player2ExileId
);
