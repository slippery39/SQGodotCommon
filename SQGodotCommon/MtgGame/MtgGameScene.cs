using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using Project;

namespace MtgGame;

public partial class MtgGameScene : Node2D
{
	private MtgGameManager _manager = null!;
	private BoardUI _boardUI = null!;
	private Hand2D _hand = null!;
	private Area2D _battlefieldDropZone = null!;
	private ChoicePanel _choicePanel = null!;
	private EventLogPanel _eventLog = null!;
	private GraveyardPopup _graveyardPopup = null!;
	private CardPreviewPopup _cardPreviewPopup = null!;

	private readonly List<int> _handCardIds = new();
	private int? _selectedAttackerId;
	private bool _isGameOver;
	private bool _choicePanelShowing;

	// Debug controls
	private bool _aiPaused;
	private Label _debugStatusLabel = null!;

	// Spell targeting state
	private bool _isFlashbackTargeting;
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
		var setup = GameManager.Instance.GetService<DeckSetupData>();
		_manager = new MtgGameManager(setup);
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_choicePanel = new ChoicePanel();
		AddChild(_choicePanel);
		_choicePanel.Confirmed += OnChoiceConfirmed;

		_eventLog = new EventLogPanel();
		AddChild(_eventLog);

		_graveyardPopup = new GraveyardPopup();
		AddChild(_graveyardPopup);
		_graveyardPopup.CardClicked += OnGraveyardCardClicked;

		_cardPreviewPopup = new CardPreviewPopup();
		_cardPreviewPopup.InternalCardScene = ResourceLoader.Load<PackedScene>(
			"res://Common/Cards/2D/Card2D/internal_cardui2d_canvasgroup.tscn"
		);
		AddChild(_cardPreviewPopup);

		_boardUI.EndTurnPressed += OnEndTurnPressed;
		_boardUI.PlayerCreatureClicked += OnPlayerCreatureClicked;
		_boardUI.PlayerCreatureRightClicked += OnPlayerCreatureRightClicked;
		_boardUI.OpponentCreatureClicked += OnOpponentCreatureClicked;
		_boardUI.OpponentDirectAttacked += OnOpponentDirectAttacked;
		_boardUI.GraveyardButtonPressed += OpenGraveyardPopup;
		_boardUI.CreatureHovered += OnCreatureHovered;
		_boardUI.CreatureHoverEnded += _ => _cardPreviewPopup.HideCard();

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

			if (_manager.IsLand(cardId))
			{
				var (landSuccess, landEvents) = _manager.PlayLand(cardId);
				if (!landSuccess)
				{
					_hand.LerpCardTransform(context.CardUI2D);
					return;
				}
				_eventLog.AppendEvents(landEvents, _manager.State, _manager.HumanPlayerId);
				Refresh();
				CheckAndShowGameOver(landEvents);
				return;
			}

			if (_manager.IsNonCreaturePermanent(cardId))
			{
				var (permSuccess, permEvents) = _manager.CastPermanent(cardId);
				if (!permSuccess)
				{
					_hand.LerpCardTransform(context.CardUI2D);
					return;
				}
				_eventLog.AppendEvents(permEvents, _manager.State, _manager.HumanPlayerId);
				Refresh();
				CheckAndShowGameOver(permEvents);
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

		var debugLayer = new CanvasLayer { Layer = 5 };
		AddChild(debugLayer);
		_debugStatusLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			Visible = false,
		};
		_debugStatusLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_debugStatusLabel.OffsetTop = 8;
		_debugStatusLabel.OffsetBottom = 36;
		_debugStatusLabel.AddThemeFontSizeOverride("font_size", 16);
		debugLayer.AddChild(_debugStatusLabel);

		var startEvents = _manager.StartGame();
		_eventLog.AppendEvents(startEvents, _manager.State, _manager.HumanPlayerId);
		Refresh();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
			return;

		if (key.Keycode == Key.Escape)
		{
			if (_additionalCostCardId.HasValue)
				ExitAdditionalCostMode();
			else if (_activatingAbilityCardId.HasValue)
				ExitAbilityTargetingMode();
			else if (_targetingSpellCardId.HasValue)
				ExitTargetingMode();
		}
		else if (key.Keycode == Key.Space)
		{
			ToggleAiPause();
		}
		else if (key.Keycode == Key.F5)
		{
			ExportAndSaveSnapshot();
		}
		else if (key.Keycode == Key.Z && key.CtrlPressed)
		{
			RewindOnce();
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

		var state = _manager.State;
		var graveyardId = state.GetWellKnownId(MtgObjectKeys.Player1Graveyard);
		var graveyardIds = new HashSet<int>(state.GetCardsInZone(graveyardId).Select(c => c.Id));
		if (_currentValidTargetIds.Any(id => graveyardIds.Contains(id)))
			OpenGraveyardPopup();

		Refresh();
	}

	private void ExitTargetingMode()
	{
		_isFlashbackTargeting = false;
		_targetingSpellCardId = null;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_pendingAdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentEffectIndex = 0;
		_currentValidTargetIds = new HashSet<int>();
		Refresh();
	}

	private void OpenGraveyardPopup()
	{
		var state = _manager.State;
		var graveyardId = state.GetWellKnownId(MtgObjectKeys.Player1Graveyard);
		var cards = state.GetCardsInZone(graveyardId).ToList();
		var flashbackIds = cards.Where(c => c.HasComponent<FlashbackComponent>()).Select(c => c.Id);
		var targetIds = _targetingSpellCardId.HasValue ? _currentValidTargetIds : null;
		_graveyardPopup.ShowGraveyard(cards, state, flashbackIds, targetIds);
	}

	private void OnGraveyardCardClicked(int cardId)
	{
		if (_manager.IsAiTurn || _isGameOver)
			return;

		if (_targetingSpellCardId.HasValue)
		{
			OnTargetSelected(cardId);
			return;
		}

		if (_additionalCostCardId.HasValue || _activatingAbilityCardId.HasValue)
			return;
		if (!_manager.HasFlashback(cardId))
			return;

		if (_manager.SpellNeedsTargets(cardId))
		{
			_isFlashbackTargeting = true;
			EnterTargetingMode(cardId);
		}
		else
		{
			var (success, events) = _manager.CastFromGraveyard(
				cardId,
				ImmutableDictionary<int, ImmutableList<int>>.Empty
			);
			if (!success)
				return;
			_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
			Refresh();
			CheckAndShowGameOver(events);
		}
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
			var isFlashback = _isFlashbackTargeting;
			ExitTargetingMode();
			var (success, events) = isFlashback
				? _manager.CastFromGraveyard(cardId, targetIds)
				: _manager.CastSpell(cardId, targetIds, costPayments);
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
			await ToSignal(
				GetTree().CreateTimer(_aiPaused ? 0.1f : 0.8f),
				SceneTreeTimer.SignalName.Timeout
			);

			if (_aiPaused)
				continue;

			// Run the (potentially expensive) AI search on a background thread so the UI stays
			// responsive during a heavy opponent turn. The manager's Compute* methods are pure
			// reads of the immutable GameState; only the Apply* calls mutate state, and those
			// run here on the main thread after the await resumes (Godot marshals the
			// continuation back to the main thread via its SynchronizationContext).
			ImmutableList<GameEvent> stepEvents;
			if (_manager.IsWaitingForChoice)
			{
				var selected = await Task.Run(() => _manager.ComputeAiChoice());
				stepEvents = _manager.ApplyAiChoice(selected);
			}
			else
			{
				var plan = await Task.Run(() => _manager.ComputeAiAction());
				stepEvents = _manager.ApplyAiAction(plan);
			}

			if (!string.IsNullOrEmpty(_manager.LastAiError))
				ShowDebugToast(_manager.LastAiError);

			_eventLog.AppendEvents(stepEvents, _manager.State, _manager.HumanPlayerId);
			Refresh();
			if (CheckAndShowGameOver(stepEvents))
				return;
		}

		_aiPaused = false;
		UpdateDebugStatus();
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

	private void OnCreatureHovered(int cardId)
	{
		var card = _manager.State.GetObject(cardId) as Card;
		if (card == null)
			return;
		var details = new InternalCardUI2D.Details
		{
			CardName = card.Name,
			ManaCost = card.ManaCost.ToString(),
			RulesText = MtgCardMapper.GetRulesText(card),
			ArtworkTexture = CardArtLoader.Load(card.Name),
		};
		_cardPreviewPopup.ShowCard(details, GetViewport().GetMousePosition());
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

	// ===== DEBUG CONTROLS =====

	private void ToggleAiPause()
	{
		_aiPaused = !_aiPaused;
		UpdateDebugStatus();
	}

	private void RewindOnce()
	{
		if (_manager.IsAiTurn && !_aiPaused)
			return;
		var history = _manager.GetHistorySummary();
		if (history.Count <= 1)
			return;
		_manager.RewindTo(history.Count - 2);
		Refresh();
		ShowDebugToast($"Rewound to: {history[history.Count - 2].Description}");
	}

	private void ExportAndSaveSnapshot()
	{
		var json = _manager.ExportDebugSnapshot();
		using var da = DirAccess.Open("user://");
		da?.MakeDir("debug_snapshots");
		var timestamp = (long)Time.GetUnixTimeFromSystem();
		var path = $"user://debug_snapshots/debug_{timestamp}.json";
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		file?.StoreString(json);
		ShowDebugToast($"Saved: {ProjectSettings.GlobalizePath(path)}");
	}

	private void UpdateDebugStatus()
	{
		if (_aiPaused)
		{
			_debugStatusLabel.Text =
				"AI Paused  (Space = resume  |  Ctrl+Z = rewind  |  F5 = export)";
			_debugStatusLabel.Visible = true;
		}
		else
		{
			_debugStatusLabel.Visible = false;
		}
	}

	private async void ShowDebugToast(string message)
	{
		_debugStatusLabel.Text = message;
		_debugStatusLabel.Visible = true;
		await ToSignal(GetTree().CreateTimer(4.0f), SceneTreeTimer.SignalName.Timeout);
		UpdateDebugStatus();
	}

	// ===== GAME OVER =====

	private bool CheckAndShowGameOver(ImmutableList<GameEvent> events)
	{
		var gameOver = events.OfType<GameOverEvent>().FirstOrDefault();
		if (gameOver == null)
			return false;

		// Callers guard on _isGameOver inconsistently, and several of them can deliver the same
		// batch of events. Stacking a second game-over overlay was merely ugly; double-reporting
		// a draft tournament result would silently corrupt the standings.
		if (_isGameOver)
			return true;

		_isGameOver = true;
		Refresh();
		ReportTournamentResult(gameOver);

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

		vbox.AddChild(BuildPostGameButton());
	}

	/// <summary>
	/// Reports the human's result when this game is one round of a draft tournament. Called
	/// exactly once, from the guarded branch above — the standings have no way to detect a
	/// duplicate.
	/// </summary>
	private void ReportTournamentResult(GameOverEvent e)
	{
		if (!GameManager.Instance.HasService<DraftTournament>())
			return;

		var tournament = GameManager.Instance.GetService<DraftTournament>();
		tournament.RecordHumanResult(
			tournament.Round,
			humanWon: e.WinnerPlayerId == _manager.HumanPlayerId,
			isDraw: e.WinnerPlayerId == -1
		);
	}

	/// A tournament game returns to the standings; a one-off game goes back to deck selection.
	private static Button BuildPostGameButton()
	{
		if (GameManager.Instance.HasService<DraftTournament>())
		{
			var standingsBtn = new Button { Text = "Back to Standings" };
			standingsBtn.Pressed += () =>
				GameManager.Instance.ChangeScene("res://MtgGame/Draft/TournamentScene.tscn");
			return standingsBtn;
		}

		var restartBtn = new Button { Text = "Play Again" };
		restartBtn.Pressed += () =>
			GameManager.Instance.ChangeScene("res://MtgGame/DeckSelect/DeckSelectScene.tscn");
		return restartBtn;
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
		_hand.Modulate = Colors.White;

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
