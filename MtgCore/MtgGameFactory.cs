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
/// Player 1 is always the first active player.
/// </summary>
public static class MtgGameFactory
{
	public static (GameState State, MtgGameIds Ids) Create()
	{
		var state = new GameState();

		var startingLife = 20;

		// Root game object — ActivePlayerId is set below once we know Player 1's ID
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
			// StartingLife is set alongside Life so the two cannot silently diverge — abilities
			// gated on "N more than your starting life total" read it.
			new MtgPlayer
			{
				Name = "Player 1",
				Life = startingLife,
				StartingLife = startingLife,
			},
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
			new MtgPlayer
			{
				Name = "Player 2",
				Life = startingLife,
				StartingLife = startingLife,
			},
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

		// Stamp Player 1 as the first active player
		var gameWithTurnState = (MtgGame)s14.GetObject(game.Id);
		var s15 = s14.UpdateObject(game.Id, gameWithTurnState with { ActivePlayerId = player1.Id });

		// Register the post-action processor now that both player IDs are known
		var s16 = s15 with
		{
			PostActionProcessor = new CheckStateBasedEffectsAction
			{
				GameId = game.Id,
				Player1Id = player1.Id,
				Player2Id = player2.Id,
				Player1BattlefieldId = p1Battlefield.Id,
				Player2BattlefieldId = p2Battlefield.Id,
				Player1GraveyardId = p1Graveyard.Id,
				Player2GraveyardId = p2Graveyard.Id,
			},
		};

		// Register all well-known object IDs for O(1) access throughout the game
		var s17 = s16.RegisterWellKnownId(MtgObjectKeys.Game, game.Id)
			.RegisterWellKnownId(MtgObjectKeys.Stack, stack.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1, player1.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1Hand, p1Hand.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1Library, p1Library.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1Graveyard, p1Graveyard.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1Battlefield, p1Battlefield.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player1Exile, p1Exile.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2, player2.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2Hand, p2Hand.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2Library, p2Library.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2Graveyard, p2Graveyard.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2Battlefield, p2Battlefield.Id)
			.RegisterWellKnownId(MtgObjectKeys.Player2Exile, p2Exile.Id);

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

		return (s17, ids);
	}

	/// <summary>
	/// Creates a game state suitable for unit testing.
	/// Both players are given a large mana pool so tests don't need to
	/// worry about mana unless they are specifically testing mana behaviour.
	/// Use Create() directly for mana-specific tests.
	///
	/// Decking is also disabled, for the same reason and with the same trade-off: a hand-built
	/// test leaves both libraries empty, and with decking live that alone decides games — the AI
	/// correctly plays EndTurn to deck the opponent, and correctly refuses to cast draw spells.
	/// See MtgGame.DeckingLossEnabled. Use Create() plus a real library to test decking itself.
	/// </summary>
	public static (GameState State, MtgGameIds Ids) CreateForTesting()
	{
		var (state, ids) = Create();

		var p1 = state.GetPlayer(ids.Player1Id);
		var p2 = state.GetPlayer(ids.Player2Id);

		state = state.UpdateObject(ids.Player1Id, p1 with { CurrentMana = 99, MaxMana = 99 });
		state = state.UpdateObject(ids.Player2Id, p2 with { CurrentMana = 99, MaxMana = 99 });

		return (state.WithoutDeckingLoss(), ids);
	}

	/// <summary>
	/// Turns off the decking loss for a state built with Create(). For tests that need real
	/// mana rules — where CreateForTesting's 99 mana would defeat the point — but still build
	/// their board by hand and so have empty libraries.
	/// </summary>
	public static GameState WithoutDeckingLoss(this GameState state)
	{
		var game = state.TryGetGame();
		return game == null
			? state
			: state.UpdateObject(game.Id, game with { DeckingLossEnabled = false });
	}
}

/// <summary>
/// Holds all the well-known IDs for a freshly created game.
/// Passed around so callers don't need to query for these fixed objects.
/// </summary>
[Obsolete("Use GameState.GetWellKnownId directly instead of passing around this struct")]
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
