using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgConsole;
using MtgCore;

// ===== SETUP GAME =====
var (state, ids) = MtgGameFactory.Create();

// Give Player 1 a hand of cards to test with
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

// Give Player 1 a library of properly-typed cards for draw effects
var libraryCreatures = new[]
{
	("Grizzly Bears", 2, 2, 2),
	("Hill Giant", 3, 3, 4),
	("Llanowar Elves", 1, 1, 1),
	("Serra Angel", 5, 4, 4),
	("Siege Rhino", 4, 4, 5),
};

foreach (var (name, cost, power, toughness) in libraryCreatures)
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

var librarySpells = new[] { ("Shock", 1), ("Counterspell", 2), ("Dark Ritual", 1) };

foreach (var (name, cost) in librarySpells)
{
	var card = new Card
	{
		Name = name,
		ManaCost = cost,
		OwnerId = ids.Player1Id,
		ControllerId = ids.Player1Id,
		Components = ImmutableList.Create<GameComponent>(
			new SpellComponent { Effects = ImmutableList<CardEffect>.Empty }
		),
	};
	(state, _) = state.AddObject(card, parentId: ids.Player1LibraryId);
}

// Give Player 1 a creature on the battlefield (no summoning sickness — pre-placed)
var darkConfidantOnField = CardLibrary.DarkConfidant() with
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
(state, _) = state.AddObject(darkConfidantOnField, parentId: ids.Player1BattlefieldId);

// Give Player 2 two creatures on the battlefield
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

// ===== RUN =====
var loop = new ConsoleGameLoop(state, ids);
loop.Run();
