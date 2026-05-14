using System;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class BoardUI : Control
{
	public event Action? EndTurnPressed;
	public event Action<int>? PlayerCreatureClicked;
	public event Action<int>? OpponentCreatureClicked;
	public event Action? OpponentDirectAttacked;

	private Label _turnLabel = null!;
	private PlayerPanel _opponentPanel = null!;
	private PlayerPanel _playerPanel = null!;
	private BattlefieldZone _opponentBattlefield = null!;
	private BattlefieldZone _playerBattlefield = null!;
	private Button _endTurnButton = null!;

	public override void _Ready()
	{
		_turnLabel = GetNode<Label>("MainColumn/TurnLabel");
		_opponentPanel = GetNode<PlayerPanel>("MainColumn/OpponentPanel");
		_opponentBattlefield = GetNode<BattlefieldZone>("MainColumn/OpponentBattlefield");
		_playerBattlefield = GetNode<BattlefieldZone>("MainColumn/PlayerBattlefield");
		_playerPanel = GetNode<PlayerPanel>("MainColumn/PlayerPanel");
		_endTurnButton = GetNode<Button>("MainColumn/EndTurnButton");

		_endTurnButton.Pressed += () => EndTurnPressed?.Invoke();
		_playerBattlefield.CardClicked += id => PlayerCreatureClicked?.Invoke(id);
		_opponentBattlefield.CardClicked += id => OpponentCreatureClicked?.Invoke(id);
		_opponentPanel.Clicked += () => OpponentDirectAttacked?.Invoke();
	}

	public Vector2 GetPlayerLibraryPosition() =>
		_playerPanel.GlobalPosition
		+ new Vector2(_playerPanel.Size.X - 40f, _playerPanel.Size.Y * 0.5f);

	public void FlashLoss(int losingPlayerId, int humanPlayerId)
	{
		var panel = losingPlayerId == humanPlayerId ? _playerPanel : _opponentPanel;
		var tween = panel.CreateTween();
		tween.TweenProperty(panel, "modulate", new Color(1f, 0.15f, 0.15f, 1f), 0.15f);
		tween.TweenProperty(panel, "modulate", Colors.White, 0.35f);
	}

	public void RefreshAll(
		GameState state,
		int humanPlayerId,
		int aiPlayerId,
		int? selectedAttackerId = null
	)
	{
		var game = state.GetGame();
		var human = state.GetPlayer(humanPlayerId);
		var ai = state.GetPlayer(aiPlayerId);

		var humanLibraryId = state.GetWellKnownId(MtgObjectKeys.Player1Library);
		var aiLibraryId = state.GetWellKnownId(MtgObjectKeys.Player2Library);
		var humanBattlefieldId = state.GetWellKnownId(MtgObjectKeys.Player1Battlefield);
		var aiBattlefieldId = state.GetWellKnownId(MtgObjectKeys.Player2Battlefield);

		_playerPanel.Refresh("You", human, state.GetCardsInZone(humanLibraryId).Count());
		_opponentPanel.Refresh("Opponent", ai, state.GetCardsInZone(aiLibraryId).Count());
		_playerBattlefield.Refresh(
			state.GetCardsInZone(humanBattlefieldId),
			state,
			selectedAttackerId
		);
		_opponentBattlefield.Refresh(state.GetCardsInZone(aiBattlefieldId), state);

		var isHumanTurn = game.ActivePlayerId == humanPlayerId;
		_playerPanel.SetActive(isHumanTurn);
		_opponentPanel.SetActive(!isHumanTurn);
		_turnLabel.Text =
			$"Turn {game.TurnNumber} — {(isHumanTurn ? "Your turn" : "Opponent's turn")}";
		_endTurnButton.Disabled = !isHumanTurn;
	}
}
