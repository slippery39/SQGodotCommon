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
	private EventLogPanel _eventLog = null!;

	private readonly List<int> _handCardIds = new();
	private int? _selectedAttackerId;
	private bool _isGameOver;
	private bool _choicePanelShowing;

	// Spell targeting state
	private int? _targetingSpellCardId;
	private ImmutableDictionary<int, ImmutableList<int>> _pendingTargetIds = ImmutableDictionary<
		int,
		ImmutableList<int>
	>.Empty;
	private int _currentEffectIndex;
	private HashSet<int> _currentValidTargetIds = new();
	private ImmutableDictionary<int, ImmutableList<int>> _pendingAdditionalCostPayments =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	// Additional cost selection state (for both spells and abilities)
	private int? _additionalCostCardId;
	private bool _additionalCostIsAbility;
	private int _additionalCostAbilityIndex;
	private int _currentCostIndex;
	private ImmutableDictionary<int, ImmutableList<int>> _pendingCostPayments = ImmutableDictionary<
		int,
		ImmutableList<int>
	>.Empty;
	private HashSet<int> _currentCostValidPaymentIds = new();

	// Activated ability targeting state
	private int? _activatingAbilityCardId;
	private int _activatingAbilityIndex;
	private ImmutableDictionary<int, ImmutableList<int>> _abilityCollectedCostPayments =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_choicePanel = new ChoicePanel();
		AddChild(_choicePanel);
		_choicePanel.Confirmed += OnChoiceConfirmed;

		_eventLog = new EventLogPanel();
		AddChild(_eventLog);

		_boardUI.EndTurnPressed += OnEndTurnPressed;
		_boardUI.PlayerCreatureClicked += OnPlayerCreatureClicked;
		_boardUI.PlayerCreatureRightClicked += OnPlayerCreatureRightClicked;
		_boardUI.OpponentCreatureClicked += OnOpponentCreatureClicked;
		_boardUI.OpponentDirectAttacked += OnOpponentDirectAttacked;

		_hand.IsDragSuccess = context =>
			!_manager.IsAiTurn
			&& !_isGameOver
			&& !_targetingSpellCardId.HasValue
			&& !_additionalCostCardId.HasValue
			&& !_activatingAbilityCardId.HasValue
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
				if (_manager.SpellHasAdditionalCostSelection(cardId))
				{
					EnterAdditionalCostMode(cardId, isAbility: false, abilityIndex: -1);
				}
				else if (!_manager.SpellNeedsTargets(cardId))
				{
					var (success, events) = _manager.CastSpell(
						cardId,
						ImmutableDictionary<int, ImmutableList<int>>.Empty
					);
					if (!success)
						return;
					_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
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

			_eventLog.AppendEvents(creatureEvents, _manager.State, _manager.HumanPlayerId);
			Refresh();
			CheckAndShowGameOver(creatureEvents);
		};

		var startEvents = _manager.StartGame();
		_eventLog.AppendEvents(startEvents, _manager.State, _manager.HumanPlayerId);
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			if (_additionalCostCardId.HasValue)
				ExitAdditionalCostMode();
			else if (_activatingAbilityCardId.HasValue)
				ExitAbilityTargetingMode();
			else if (_targetingSpellCardId.HasValue)
				ExitTargetingMode();
		}
	}

	// ===== ADDITIONAL COST STATE MACHINE =====

	private void EnterAdditionalCostMode(int cardId, bool isAbility, int abilityIndex)
	{
		_additionalCostCardId = cardId;
		_additionalCostIsAbility = isAbility;
		_additionalCostAbilityIndex = abilityIndex;
		_currentCostIndex = isAbility
			? _manager.GetNextAbilityCostNeedingSelection(cardId, abilityIndex, -1)
			: _manager.GetNextAdditionalCostNeedingSelection(cardId, -1);
		_pendingCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentCostValidPaymentIds = new HashSet<int>(
			isAbility
				? _manager.GetAbilityAdditionalCostValidPayments(
					cardId,
					abilityIndex,
					_currentCostIndex
				)
				: _manager.GetAdditionalCostValidPayments(cardId, _currentCostIndex)
		);
		Refresh();
	}

	private void ExitAdditionalCostMode()
	{
		_additionalCostCardId = null;
		_additionalCostIsAbility = false;
		_additionalCostAbilityIndex = -1;
		_currentCostIndex = 0;
		_pendingCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentCostValidPaymentIds = new HashSet<int>();
		Refresh();
	}

	private void OnCostPaymentSelected(int permanentId)
	{
		if (!_additionalCostCardId.HasValue)
			return;
		if (!_currentCostValidPaymentIds.Contains(permanentId))
			return;

		var cardId = _additionalCostCardId.Value;
		var isAbility = _additionalCostIsAbility;
		var abilityIndex = _additionalCostAbilityIndex;

		_pendingCostPayments = _pendingCostPayments.Add(
			_currentCostIndex,
			ImmutableList.Create(permanentId)
		);

		int nextCost = isAbility
			? _manager.GetNextAbilityCostNeedingSelection(cardId, abilityIndex, _currentCostIndex)
			: _manager.GetNextAdditionalCostNeedingSelection(cardId, _currentCostIndex);

		if (nextCost >= 0)
		{
			_currentCostIndex = nextCost;
			_currentCostValidPaymentIds = new HashSet<int>(
				isAbility
					? _manager.GetAbilityAdditionalCostValidPayments(cardId, abilityIndex, nextCost)
					: _manager.GetAdditionalCostValidPayments(cardId, nextCost)
			);
			Refresh();
			return;
		}

		// All costs collected — clear cost mode and proceed
		var collectedPayments = _pendingCostPayments;
		_additionalCostCardId = null;
		_pendingCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentCostValidPaymentIds = new HashSet<int>();

		if (isAbility)
		{
			if (_manager.AbilityNeedsTarget(cardId, abilityIndex))
			{
				_activatingAbilityCardId = cardId;
				_activatingAbilityIndex = abilityIndex;
				_abilityCollectedCostPayments = collectedPayments;
				_currentValidTargetIds = new HashSet<int>(
					_manager.GetAbilityValidTargets(cardId, abilityIndex)
				);
			}
			else
			{
				var (_, events) = _manager.ActivateAbility(
					cardId,
					abilityIndex,
					ImmutableList<int>.Empty,
					collectedPayments
				);
				_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
				CheckAndShowGameOver(events);
			}
		}
		else
		{
			if (_manager.SpellNeedsTargets(cardId))
			{
				EnterTargetingMode(cardId, collectedPayments);
			}
			else
			{
				var (_, events) = _manager.CastSpell(
					cardId,
					ImmutableDictionary<int, ImmutableList<int>>.Empty,
					collectedPayments
				);
				_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
				CheckAndShowGameOver(events);
			}
		}
		Refresh();
	}

	// ===== SPELL TARGETING STATE MACHINE =====

	private void EnterTargetingMode(int cardId)
	{
		EnterTargetingMode(cardId, ImmutableDictionary<int, ImmutableList<int>>.Empty);
	}

	private void EnterTargetingMode(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> collectedCostPayments
	)
	{
		_targetingSpellCardId = cardId;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_pendingAdditionalCostPayments = collectedCostPayments;
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
		_pendingAdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
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
			var costPayments = _pendingAdditionalCostPayments;
			ExitTargetingMode();
			var (success, events) = _manager.CastSpell(cardId, targetIds, costPayments);
			if (!success)
				return;
			_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
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

	// ===== ABILITY TARGETING STATE MACHINE =====

	private void ExitAbilityTargetingMode()
	{
		_activatingAbilityCardId = null;
		_activatingAbilityIndex = 0;
		_abilityCollectedCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentValidTargetIds = new HashSet<int>();
		Refresh();
	}

	private void OnAbilityTargetSelected(int targetId)
	{
		if (!_activatingAbilityCardId.HasValue)
			return;
		if (!_currentValidTargetIds.Contains(targetId))
			return;

		var cardId = _activatingAbilityCardId.Value;
		var abilityIndex = _activatingAbilityIndex;
		var costPayments = _abilityCollectedCostPayments;
		ExitAbilityTargetingMode();

		var (_, events) = _manager.ActivateAbility(
			cardId,
			abilityIndex,
			ImmutableList.Create(targetId),
			costPayments
		);
		_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
		Refresh();
		CheckAndShowGameOver(events);
	}

	// ===== CHOICE HANDLING =====

	private void OnChoiceConfirmed(ImmutableList<int> selectedIds)
	{
		_choicePanelShowing = false;
		var events = _manager.ResolveChoice(selectedIds);
		_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
		Refresh();
		CheckAndShowGameOver(events);
	}

	// ===== TURN / ACTION HANDLERS =====

	private async void OnEndTurnPressed()
	{
		if (_isGameOver)
			return;

		if (_additionalCostCardId.HasValue)
		{
			ExitAdditionalCostMode();
			return;
		}

		if (_activatingAbilityCardId.HasValue)
		{
			ExitAbilityTargetingMode();
			return;
		}

		if (_targetingSpellCardId.HasValue)
		{
			ExitTargetingMode();
			return;
		}

		_selectedAttackerId = null;
		var events = _manager.EndTurn();
		_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
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

			_eventLog.AppendEvents(stepEvents, _manager.State, _manager.HumanPlayerId);
			Refresh();
			if (CheckAndShowGameOver(stepEvents))
				return;
		}
	}

	private void OnPlayerCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		if (_additionalCostCardId.HasValue)
		{
			OnCostPaymentSelected(cardId);
			return;
		}

		if (_activatingAbilityCardId.HasValue)
		{
			OnAbilityTargetSelected(cardId);
			return;
		}

		if (_targetingSpellCardId.HasValue)
		{
			OnTargetSelected(cardId);
			return;
		}

		_selectedAttackerId = _selectedAttackerId == cardId ? null : cardId;
		Refresh();
	}

	private void OnPlayerCreatureRightClicked(int cardId)
	{
		if (
			_isGameOver
			|| _manager.IsAiTurn
			|| _additionalCostCardId.HasValue
			|| _targetingSpellCardId.HasValue
			|| _activatingAbilityCardId.HasValue
			|| _choicePanelShowing
		)
			return;

		var legalAbilities = _manager.GetLegalAbilities(cardId);
		if (legalAbilities.Count == 0)
			return;

		var (abilityIndex, _, _) = legalAbilities[0];

		if (_manager.AbilityHasAdditionalCostSelection(cardId, abilityIndex))
		{
			EnterAdditionalCostMode(cardId, isAbility: true, abilityIndex);
		}
		else if (_manager.AbilityNeedsTarget(cardId, abilityIndex))
		{
			_activatingAbilityCardId = cardId;
			_activatingAbilityIndex = abilityIndex;
			_abilityCollectedCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
			_currentValidTargetIds = new HashSet<int>(
				_manager.GetAbilityValidTargets(cardId, abilityIndex)
			);
			Refresh();
		}
		else
		{
			var (_, events) = _manager.ActivateAbility(
				cardId,
				abilityIndex,
				ImmutableList<int>.Empty,
				ImmutableDictionary<int, ImmutableList<int>>.Empty
			);
			_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
			Refresh();
			CheckAndShowGameOver(events);
		}
	}

	private void OnOpponentCreatureClicked(int cardId)
	{
		if (_isGameOver || _manager.IsAiTurn)
			return;

		if (_activatingAbilityCardId.HasValue)
		{
			OnAbilityTargetSelected(cardId);
			return;
		}

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

		if (_activatingAbilityCardId.HasValue)
		{
			OnAbilityTargetSelected(_manager.AiPlayerId);
			return;
		}

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
		_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
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
		restartBtn.Pressed += () =>
		{
			layer.QueueFree();
			ResetGame();
		};
		vbox.AddChild(restartBtn);
	}

	private void ResetGame()
	{
		// Clear hand visuals
		foreach (var id in _handCardIds.ToList())
			_hand.DiscardCard(id.ToString());
		_handCardIds.Clear();

		// Reset all scene state
		_selectedAttackerId = null;
		_isGameOver = false;
		_choicePanelShowing = false;
		_targetingSpellCardId = null;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_pendingAdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentEffectIndex = 0;
		_currentValidTargetIds = new HashSet<int>();
		_additionalCostCardId = null;
		_additionalCostIsAbility = false;
		_additionalCostAbilityIndex = -1;
		_currentCostIndex = 0;
		_pendingCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentCostValidPaymentIds = new HashSet<int>();
		_activatingAbilityCardId = null;
		_activatingAbilityIndex = 0;
		_abilityCollectedCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;

		_choicePanel.Hide();
		_eventLog.Clear();

		_manager = new MtgGameManager();
		var startEvents = _manager.StartGame();
		_eventLog.AppendEvents(startEvents, _manager.State, _manager.HumanPlayerId);
		Refresh();
	}

	// ===== REFRESH =====

	private void Refresh()
	{
		var isTargeting = _targetingSpellCardId.HasValue || _activatingAbilityCardId.HasValue;
		_boardUI.RefreshAll(
			_manager.State,
			_manager.HumanPlayerId,
			_manager.AiPlayerId,
			_selectedAttackerId,
			targetHighlightIds: isTargeting ? _currentValidTargetIds : null,
			additionalCostHighlightIds: _additionalCostCardId.HasValue
				? _currentCostValidPaymentIds
				: null
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
