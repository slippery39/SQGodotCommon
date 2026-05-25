using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgConsole;
using MtgCore;

// ===== SETUP GAME =====
var (state, ids) = MtgGameFactory.Create();

// ===== PLAYER 1 LIBRARY (20 cards — BeginGame will shuffle and deal 7) =====
var p1Cards = new (string Name, int Cost, bool IsCreature, int Power, int Toughness)[]
{
	("Lightning Bolt", 1, false, 0, 0),
	("Lightning Bolt", 1, false, 0, 0),
	("Lightning Helix", 2, false, 0, 0),
	("Careful Study", 1, false, 0, 0),
	("Telling Time", 2, false, 0, 0),
	("Dark Confidant", 2, true, 2, 1),
	("Grizzly Bears", 2, true, 2, 2),
	("Hill Giant", 3, true, 3, 4),
	("Llanowar Elves", 1, true, 1, 1),
	("Serra Angel", 5, true, 4, 4),
	("Siege Rhino", 4, true, 4, 5),
	("Goblin Raider", 1, true, 2, 1),
	("Centaur Courser", 3, true, 3, 3),
	("Wind Drake", 3, true, 2, 2),
	("Iron Golem", 4, true, 4, 4),
	("Runeclaw Bear", 2, true, 2, 2),
	("Elvish Warrior", 2, true, 2, 3),
	("Jackal Pup", 1, true, 2, 1),
	("Bladetusk Boar", 4, true, 3, 3),
	("Kalonian Tusker", 3, true, 3, 3),
};

foreach (var (name, cost, isCreature, power, toughness) in p1Cards)
{
	var components = isCreature
		? ImmutableArray.Create<GameComponent>(
			new CreatureComponent { Power = power, Toughness = toughness }
		)
		: ImmutableArray.Create<GameComponent>(
			new SpellComponent { Effects = ImmutableList<CardEffect>.Empty }
		);

	// Use CardLibrary for cards that have proper effect definitions
	Card card = name switch
	{
		"Lightning Bolt" => CardLibrary.LightningBolt() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		},
		"Lightning Helix" => CardLibrary.LightningHelix() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		},
		"Careful Study" => CardLibrary.CarefulStudy() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		},
		"Telling Time" => CardLibrary.TellingTime() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		},
		"Dark Confidant" => CardLibrary.DarkConfidant() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		},
		_ => new Card
		{
			Name = name,
			ManaCost = cost,
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
			Components = components,
		},
	};

	(state, _) = state.AddObject(card, parentId: ids.Player1LibraryId);
}

// ===== PLAYER 1 BATTLEFIELD (pre-placed, no summoning sickness) =====
var p1Confidant = CardLibrary.DarkConfidant() with
{
	OwnerId = ids.Player1Id,
	ControllerId = ids.Player1Id,
	Components = ImmutableArray.Create<GameComponent>(
		new CreatureComponent
		{
			Power = 2,
			Toughness = 1,
			HasSummoningSickness = false,
		}
	),
};
(state, _) = state.AddObject(p1Confidant, parentId: ids.Player1BattlefieldId);

// ===== PLAYER 2 LIBRARY (20 cards — BeginGame will shuffle and deal 7) =====
var p2Cards = new (string Name, int Cost, int Power, int Toughness)[]
{
	("Goblin Guide", 1, 2, 2),
	("Grizzly Bears", 2, 2, 2),
	("Craw Wurm", 6, 6, 4),
	("Wall of Stone", 3, 0, 8),
	("Hill Giant", 3, 3, 4),
	("Iron Golem", 4, 4, 4),
	("Serra Angel", 5, 4, 4),
	("Centaur Courser", 3, 3, 3),
	("Jackal Pup", 1, 2, 1),
	("Wind Drake", 3, 2, 2),
	("Goblin Raider", 1, 2, 1),
	("Elvish Warrior", 2, 2, 3),
	("Runeclaw Bear", 2, 2, 2),
	("Bladetusk Boar", 4, 3, 3),
	("Kalonian Tusker", 3, 3, 3),
	("Savannah Lions", 1, 2, 1),
	("Raging Goblin", 1, 1, 1),
	("Llanowar Elves", 1, 1, 1),
	("Mahamoti Djinn", 6, 5, 6),
	("Ancient Ooze", 7, 6, 6),
};

foreach (var (name, cost, power, toughness) in p2Cards)
{
	var card = new Card
	{
		Name = name,
		ManaCost = cost,
		OwnerId = ids.Player2Id,
		ControllerId = ids.Player2Id,
		Components = ImmutableArray.Create<GameComponent>(
			new CreatureComponent { Power = power, Toughness = toughness }
		),
	};
	(state, _) = state.AddObject(card, parentId: ids.Player2LibraryId);
}

// ===== PLAYER 2 BATTLEFIELD (pre-placed, no summoning sickness) =====
var goblinGuide = new Card
{
	Name = "Goblin Guide",
	ManaCost = 1,
	OwnerId = ids.Player2Id,
	ControllerId = ids.Player2Id,
	Components = ImmutableArray.Create<GameComponent>(
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
	Components = ImmutableArray.Create<GameComponent>(
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
