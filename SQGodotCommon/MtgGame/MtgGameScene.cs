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
	private ChoicePanel _choicePanel = null!;

	private readonly List<int> _handCardIds = new();
	private int? _selectedAttackerId;
	private bool _isGameOver;
	private bool _choicePanelShowing;

	private int? _targetingSpellCardId;
	private ImmutableDictionary<int, ImmutableList<int>> _pendingTargetIds = ImmutableDictionary<
		int,
		ImmutableList<int>
	>.Empty;
	private int _currentEffectIndex;
	private HashSet<int> _currentValidTargetIds = new();

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_choicePanel = new ChoicePanel();
		AddChild(_choicePanel);
		_choicePanel.Confirmed += OnChoiceConfirmed;

		_boardUI.EndTurnPressed += OnEndTurnPressed;
		_boardUI.PlayerCreatureClicked += OnPlayerCreatureClicked;
		_boardUI.OpponentCreatureClicked += OnOpponentCreatureClicked;
		_boardUI.OpponentDirectAttacked += OnOpponentDirectAttacked;

		_hand.IsDragSuccess = context =>
			!_manager.IsAiTurn
			&& !_isGameOver
			&& !_targetingSpellCardId.HasValue
			&& !_choicePanelShowing
			&& context.SelectedAreas.Contains(_battlefieldDropZone);

		_hand.OnDragSuccess = context =>
		{
			if (!int.TryParse(context.CardUI2D.Id, out var cardId))
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			if (_manager.IsSpell(cardId))
			{
				_hand.LerpCardTransform(context.CardUI2D);
				if (!_manager.SpellNeedsTargets(cardId))
				{
					var (success, events) = _manager.CastSpell(
						cardId,
						ImmutableDictionary<int, ImmutableList<int>>.Empty
					);
					if (!success)
						return;
					Refresh();
					CheckAndShowGameOver(events);
				}
				else
				{
					EnterTargetingMode(cardId);
				}
				return;
			}

			var (creatureSuccess, creatureEvents) = _manager.CastCreature(cardId);
			if (!creatureSuccess)
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			Refresh();
			CheckAndShowGameOver(creatureEvents);
		};

		_manager.StartGame();
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (
			@event is InputEventKey key
			&& key.Pressed
			&& !key.Echo
			&& key.Keycode == Key.Escape
			&& _targetingSpellCardId.HasValue
		)
		{
			ExitTargetingMode();
		}
	}

	// ===== TARGETING STATE MACHINE =====

	private void EnterTargetingMode(int cardId)
	{
		_targetingSpellCardId = cardId;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentEffectIndex = _manager.GetNextEffectNeedingTarget(cardId, -1);
		_currentValidTargetIds = new HashSet<int>(
			_manager.GetSpellValidTargets(cardId, _currentEffectIndex)
		);
		Refresh();
	}

	private void ExitTargetingMode()
	{
		_targetingSpellCardId = null;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentEffectIndex = 0;
		_currentValidTargetIds = new HashSet<int>();
		Refresh();
	}

	private void OnTargetSelected(int targetId)
	{
		if (!_targetingSpellCardId.HasValue)
			return;
		if (!_currentValidTargetIds.Contains(targetId))
			return;

		var cardId = _targetingSpellCardId.Value;
		_pendingTargetIds = _pendingTargetIds.Add(
			_currentEffectIndex,
			ImmutableList.Create(targetId)
		);

		var nextEffect = _manager.GetNextEffectNeedingTarget(cardId, _currentEffectIndex);
		if (nextEffect == -1)
		{
			var targetIds = _pendingTargetIds;
			ExitTargetingMode();
			var (success, events) = _manager.CastSpell(cardId, targetIds);
			if (!success)
				return;
			Refresh();
			CheckAndShowGameOver(events);
		}
		else
		{
			_currentEffectIndex = nextEffect;
			_currentValidTargetIds = new HashSet<int>(
				_manager.GetSpellValidTargets(cardId, _currentEffectIndex)
			);
			Refresh();
		}
	}

	// ===== CHOICE HANDLING =====

	private void OnChoiceConfirmed(ImmutableList<int> selectedIds)
	{
		_choicePanelShowing = false;
		var events = _manager.ResolveChoice(selectedIds);
		Refresh();
		CheckAndShowGameOver(events);
	}

	// ===== TURN / ACTION HANDLERS =====

	private async void OnEndTurnPressed()
	{
		if (_isGameOver)
			return;

		if (_targetingSpellCardId.HasValue)
		{
			ExitTargetingMode();
			return;
		}

		_selectedAttackerId = null;
		var events = _manager.EndTurn();
		Refresh();
		if (CheckAndShowGameOver(events))
			return;

		while (_manager.IsAiTurn && !_isGameOver)
		{
			await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);

			ImmutableList<GameEvent> stepEvents;
			if (_manager.IsWaitingForChoice)
				stepEvents = _manager.ResolveAiChoice();
			else
				stepEvents = _manager.RunAiTurnStep();

			Refresh();
			if (CheckAndShowGameOver(stepEvents))
				return;
		}
	}

	private void OnPlayerCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		if (_targetingSpellCardId.HasValue)
		{
			OnTargetSelected(cardId);
			return;
		}

		_selectedAttackerId = _selectedAttackerId == cardId ? null : cardId;
		Refresh();
	}

	private void OnOpponentCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		if (_targetingSpellCardId.HasValue)
		{
			OnTargetSelected(cardId);
			return;
		}

		if (!_selectedAttackerId.HasValue)
			return;

		TryAttack(_selectedAttackerId.Value, cardId);
	}

	private void OnOpponentDirectAttacked()
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		if (_targetingSpellCardId.HasValue)
		{
			OnTargetSelected(_manager.AiPlayerId);
			return;
		}

		if (!_selectedAttackerId.HasValue)
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

	// ===== GAME OVER =====

	private bool CheckAndShowGameOver(ImmutableList<GameEvent> events)
	{
		var gameOver = events.OfType<GameOverEvent>().FirstOrDefault();
		if (gameOver == null)
			return false;

		_isGameOver = true;
		Refresh();

		var lostEvent = events.OfType<PlayerLostEvent>().FirstOrDefault();
		if (lostEvent != null)
		{
			_boardUI.FlashLoss(lostEvent.PlayerId, _manager.HumanPlayerId);
			var timer = GetTree().CreateTimer(0.55f);
			timer.Timeout += () => CreateGameOverUI(gameOver);
		}
		else
		{
			CreateGameOverUI(gameOver);
		}

		return true;
	}

	private void CreateGameOverUI(GameOverEvent e)
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

	// ===== REFRESH =====

	private void Refresh()
	{
		_boardUI.RefreshAll(
			_manager.State,
			_manager.HumanPlayerId,
			_manager.AiPlayerId,
			_selectedAttackerId,
			_targetingSpellCardId.HasValue ? _currentValidTargetIds : null
		);
		_hand.Modulate = _manager.IsAiTurn ? new Color(0.5f, 0.5f, 0.5f, 0.7f) : Colors.White;

		var shouldShowChoice = !_manager.IsAiTurn && !_isGameOver && _manager.IsWaitingForChoice;
		if (shouldShowChoice && !_choicePanelShowing)
		{
			var choice = _manager.GetPendingChoice();
			var options = _manager.GetPendingChoiceOptions();
			_choicePanel.ShowChoice(choice.Prompt, options, choice.MinChoices, choice.MaxChoices);
			_choicePanelShowing = true;
		}
		else if (!shouldShowChoice && _choicePanelShowing)
		{
			_choicePanel.Hide();
			_choicePanelShowing = false;
		}

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
		var libraryPos = _boardUI.GetPlayerLibraryPosition();
		foreach (var card in handCards.Where(c => !existingIds.Contains(c.Id)))
		{
			var cardUI = _hand.DrawCard(libraryPos);
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
