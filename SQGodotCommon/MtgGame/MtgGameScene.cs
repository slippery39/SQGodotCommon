using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class MtgGameScene : Node2D
{
	private MtgGameManager _manager = null!;

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		var events = _manager.StartGame();

		PrintGameState();
		GD.Print($"Game started with {events.Count} opening events.");
	}

	private void PrintGameState()
	{
		var state = _manager.State;
		var game = state.GetGame();
		var human = state.GetPlayer(_manager.HumanPlayerId);
		var ai = state.GetPlayer(_manager.AiPlayerId);

		var humanHandId = state.GetWellKnownId(MtgObjectKeys.Player1Hand);
		var aiHandId = state.GetWellKnownId(MtgObjectKeys.Player2Hand);
		var humanLibraryId = state.GetWellKnownId(MtgObjectKeys.Player1Library);
		var aiLibraryId = state.GetWellKnownId(MtgObjectKeys.Player2Library);

		var humanHandCount = state.GetCardsInZone(humanHandId).Count();
		var aiHandCount = state.GetCardsInZone(aiHandId).Count();
		var humanLibraryCount = state.GetCardsInZone(humanLibraryId).Count();
		var aiLibraryCount = state.GetCardsInZone(aiLibraryId).Count();

		GD.Print(
			$"Turn {game.TurnNumber} | Active: {(game.ActivePlayerId == _manager.HumanPlayerId ? "Human" : "AI")}"
		);
		GD.Print(
			$"  Human — Life: {human.Life}, Mana: {human.CurrentMana}/{human.MaxMana}, Hand: {humanHandCount}, Library: {humanLibraryCount}"
		);
		GD.Print(
			$"  AI    — Life: {ai.Life}, Mana: {ai.CurrentMana}/{ai.MaxMana}, Hand: {aiHandCount}, Library: {aiLibraryCount}"
		);
	}
}
