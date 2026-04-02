using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgConsole;
using MtgCore;

// ===== SETUP GAME =====
var (state, ids) = MtgGameFactory.Create();

// Wire up the post-action processor for state-based effects
state = state with
{
	PostActionProcessor = new CheckStateBasedEffectsAction
	{
		Player1Id = ids.Player1Id,
		Player2Id = ids.Player2Id,
	},
};

// ===== PLAYER 1 HAND =====
var bolt1 = CardLibrary.LightningBolt() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};
var bolt2 = CardLibrary.LightningBolt() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};
var helix = CardLibrary.LightningHelix() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};
var carefulStudy = CardLibrary.CarefulStudy() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};
var tellingTime = CardLibrary.TellingTime() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};
var darkConfidantInHand = CardLibrary.DarkConfidant() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
};

(state, _) = state.AddObject(bolt1, parentId: ids.Player1HandId);
(state, _) = state.AddObject(bolt2, parentId: ids.Player1HandId);
(state, _) = state.AddObject(helix, parentId: ids.Player1HandId);
(state, _) = state.AddObject(carefulStudy, parentId: ids.Player1HandId);
(state, _) = state.AddObject(tellingTime, parentId: ids.Player1HandId);
(state, _) = state.AddObject(darkConfidantInHand, parentId: ids.Player1HandId);

// ===== PLAYER 1 LIBRARY =====
var p1Creatures = new[]
{
	("Grizzly Bears", 2, 2, 2),
	("Hill Giant", 3, 3, 4),
	("Llanowar Elves", 1, 1, 1),
	("Serra Angel", 5, 4, 4),
	("Siege Rhino", 4, 4, 5),
};
foreach (var (name, cost, power, toughness) in p1Creatures)
{
	var card = new Card
	{
		Name = name,
		ManaCost = cost,
		OwnerId = ids.Player1Id,
		ControllerId = ids.Player1Id,
		Components = ImmutableList.Create<GameComponent>(
			new CreatureComponent { Power = power, Toughness = toughness }
		),
	};
	(state, _) = state.AddObject(card, parentId: ids.Player1LibraryId);
}

// ===== PLAYER 1 BATTLEFIELD =====
var p1Confidant = CardLibrary.DarkConfidant() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
	Components = ImmutableList.Create<GameComponent>(
		new CreatureComponent
		{
			Power = 2,
			Toughness = 1,
			HasSummoningSickness = false,
		}
	),
};
(state, _) = state.AddObject(p1Confidant, parentId: ids.Player1BattlefieldId);

// ===== PLAYER 2 HAND =====
var p2Bolt = CardLibrary.LightningBolt() with
{
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
};
var p2Helix = CardLibrary.LightningHelix() with
{
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
};
var p2Confidant = CardLibrary.DarkConfidant() with
{
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
};

(state, _) = state.AddObject(p2Bolt, parentId: ids.Player2HandId);
(state, _) = state.AddObject(p2Helix, parentId: ids.Player2HandId);
(state, _) = state.AddObject(p2Confidant, parentId: ids.Player2HandId);

// ===== PLAYER 2 LIBRARY =====
var p2Creatures = new[]
{
	("Goblin Guide", 1, 2, 2),
	("Grizzly Bears", 2, 2, 2),
	("Craw Wurm", 6, 6, 4),
	("Wall of Stone", 3, 0, 8),
};
foreach (var (name, cost, power, toughness) in p2Creatures)
{
	var card = new Card
	{
		Name = name,
		ManaCost = cost,
		OwnerId = ids.Player2Id,
		ControllerId = ids.Player2Id,
		Components = ImmutableList.Create<GameComponent>(
			new CreatureComponent { Power = power, Toughness = toughness }
		),
	};
	(state, _) = state.AddObject(card, parentId: ids.Player2LibraryId);
}

// ===== PLAYER 2 BATTLEFIELD =====
var goblinGuide = new Card
{
	Name = "Goblin Guide",
	ManaCost = 1,
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
	Components = ImmutableList.Create<GameComponent>(
		new CreatureComponent
		{
			Power = 2,
			Toughness = 2,
			HasSummoningSickness = false,
		}
	),
};
var grizzlyBears = new Card
{
	Name = "Grizzly Bears",
	ManaCost = 2,
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
	Components = ImmutableList.Create<GameComponent>(
		new CreatureComponent
		{
			Power = 2,
			Toughness = 2,
			HasSummoningSickness = false,
		}
	),
};
(state, _) = state.AddObject(goblinGuide, parentId: ids.Player2BattlefieldId);
(state, _) = state.AddObject(grizzlyBears, parentId: ids.Player2BattlefieldId);

// ===== MODE SELECTION =====
Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║           MTG SANDBOX                    ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine("  Select mode:");
Console.WriteLine("    [1] vs AI");
Console.WriteLine("    [2] Hotseat");
Console.Write("  > ");

var modeInput = Console.ReadLine()?.Trim() ?? "";
var mode = modeInput == "2" ? GameMode.Hotseat : GameMode.Ai;

// ===== RUN =====
var loop = new ConsoleGameLoop(state, ids, mode);
loop.Run();
