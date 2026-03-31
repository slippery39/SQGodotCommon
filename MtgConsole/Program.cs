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

(state, _) = state.AddObject(bolt1, parentId: ids.Player1HandId);
(state, _) = state.AddObject(bolt2, parentId: ids.Player1HandId);
(state, _) = state.AddObject(helix, parentId: ids.Player1HandId);
(state, _) = state.AddObject(carefulStudy, parentId: ids.Player1HandId);
(state, _) = state.AddObject(tellingTime, parentId: ids.Player1HandId);

// Give Player 1 a small library for draw/reveal effects
var libraryCards = new[]
{
	"Grizzly Bears",
	"Hill Giant",
	"Shock",
	"Counterspell",
	"Dark Ritual",
	"Giant Growth",
	"Serra Angel",
	"Fireball",
};

foreach (var name in libraryCards)
{
	var card = new InstantCard
	{
		Name = name,
		ManaCost = libraryCards.ToList().IndexOf(name) + 1,
		OwnerId = ids.Player1Id,
		ControllerId = ids.Player1Id,
	};
	(state, _) = state.AddObject(card, parentId: ids.Player1LibraryId);
}

// ===== RUN =====
var loop = new ConsoleGameLoop(state, ids);
loop.Run();
