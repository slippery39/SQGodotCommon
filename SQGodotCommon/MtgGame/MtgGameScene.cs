using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class MtgGameScene : Node2D
{
	private MtgGameManager _manager = null!;
	private BoardUI _boardUI = null!;
	private Hand2D _hand = null!;
	private Area2D _battlefieldDropZone = null!;

	private readonly List<int> _handCardIds = new();
	private int? _selectedAttackerId;
	private bool _isGameOver;

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_boardUI.EndTurnPressed += OnEndTurnPressed;
		_boardUI.PlayerCreatureClicked += OnPlayerCreatureClicked;
		_boardUI.OpponentCreatureClicked += OnOpponentCreatureClicked;
		_boardUI.OpponentDirectAttacked += OnOpponentDirectAttacked;

		_hand.IsDragSuccess = context => context.SelectedAreas.Contains(_battlefieldDropZone);

		_hand.OnDragSuccess = context =>
		{
			if (!int.TryParse(context.CardUI2D.Id, out var cardId))
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			var (success, events) = _manager.CastCreature(cardId);
			if (!success)
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			Refresh();
			CheckAndShowGameOver(events);
		};

		_manager.StartGame();
		Refresh();
	}

	private async void OnEndTurnPressed()
	{
		if (_isGameOver)
			return;

		_selectedAttackerId = null;
		var events = _manager.EndTurn();
		Refresh();
		if (CheckAndShowGameOver(events))
			return;

		while (_manager.IsAiTurn && !_manager.IsWaitingForChoice && !_isGameOver)
		{
			await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
			events = _manager.RunAiTurnStep();
			Refresh();
			if (CheckAndShowGameOver(events))
				return;
		}
	}

	private void OnPlayerCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		_selectedAttackerId = _selectedAttackerId == cardId ? null : cardId;
		Refresh();
	}

	private void OnOpponentCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn || !_selectedAttackerId.HasValue)
			return;

		TryAttack(_selectedAttackerId.Value, cardId);
	}

	private void OnOpponentDirectAttacked()
	{
		if (_isGameOver || _manager.IsAiTurn || !_selectedAttackerId.HasValue)
			return;

		TryAttack(_selectedAttackerId.Value, _manager.AiPlayerId);
	}

	private void TryAttack(int attackerId, int targetId)
	{
		var (success, events) = _manager.Attack(attackerId, targetId);
		if (!success)
			return;

		_selectedAttackerId = null;
		Refresh();
		CheckAndShowGameOver(events);
	}

	private bool CheckAndShowGameOver(ImmutableList<GameEvent> events)
	{
		var gameOver = events.OfType<GameOverEvent>().FirstOrDefault();
		if (gameOver == null)
			return false;

		_isGameOver = true;
		Refresh();
		ShowGameOverOverlay(gameOver);
		return true;
	}

	private void ShowGameOverOverlay(GameOverEvent e)
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);

		var bg = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Pass;
		layer.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 32);
		center.AddChild(vbox);

		var resultLabel = new Label();
		resultLabel.Text =
			e.WinnerPlayerId == _manager.HumanPlayerId ? "You Win!"
			: e.WinnerPlayerId == -1 ? "Draw!"
			: "You Lose!";
		resultLabel.HorizontalAlignment = HorizontalAlignment.Center;
		resultLabel.AddThemeFontSizeOverride("font_size", 72);
		vbox.AddChild(resultLabel);

		var restartBtn = new Button { Text = "Play Again" };
		restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
		vbox.AddChild(restartBtn);
	}

	private void Refresh()
	{
		_boardUI.RefreshAll(
			_manager.State,
			_manager.HumanPlayerId,
			_manager.AiPlayerId,
			_selectedAttackerId
		);
		SyncHand();
	}

	private void SyncHand()
	{
		var state = _manager.State;
		var humanHandId = state.GetWellKnownId(MtgObjectKeys.Player1Hand);
		var handCards = state.GetCardsInZone(humanHandId).ToList();
		var currentCardIds = handCards.Select(c => c.Id).ToHashSet();

		var toRemove = _handCardIds.Where(id => !currentCardIds.Contains(id)).ToList();
		foreach (var id in toRemove)
		{
			_hand.DiscardCard(id.ToString());
			_handCardIds.Remove(id);
		}

		var existingIds = _handCardIds.ToHashSet();
		foreach (var card in handCards.Where(c => !existingIds.Contains(c.Id)))
		{
			var cardUI = _hand.DrawCard();
			cardUI.Id = card.Id.ToString();
			_handCardIds.Add(card.Id);
		}

		var uiCards = _hand.GetCards();
		if (uiCards.Count == 0)
			return;

		var cardLookup = handCards.ToDictionary(c => c.Id);
		var details = uiCards
			.Select(ui =>
				int.TryParse(ui.Id, out var id) && cardLookup.TryGetValue(id, out var card)
					? MtgCardMapper.ToDetails(card, state, _manager.HumanPlayerId)
					: new InternalCardUI2D.Details()
			)
			.ToList();

		_hand.SetCardsDetails(details);
	}
}
