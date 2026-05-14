namespace MtgGame;

public partial class MtgGameScene : Node2D
{
	private MtgGameManager _manager = null!;
	private BoardUI _boardUI = null!;

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		_boardUI = GetNode<BoardUI>("BoardUI");
		_boardUI.EndTurnPressed += OnEndTurnPressed;

		_manager.StartGame();
		Refresh();
	}

	private async void OnEndTurnPressed()
	{
		_manager.EndTurn();
		Refresh();

		while (_manager.IsAiTurn && !_manager.IsWaitingForChoice)
		{
			await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
			_manager.RunAiTurnStep();
			Refresh();
		}
	}

	private void Refresh()
	{
		_boardUI.RefreshAll(_manager.State, _manager.HumanPlayerId, _manager.AiPlayerId);
	}
}
