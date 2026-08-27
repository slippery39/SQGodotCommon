using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using MtgSimulator.Scenarios;
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
	private AiInspectorPanel _aiInspector = null!;
	private GraveyardPopup _graveyardPopup = null!;
	private CardPreviewPopup _cardPreviewPopup = null!;

	/// Comfortably above the 0.42 board-card scale — a preview that is not clearly bigger
	/// than the card it previews is worse than none.
	private const float PreviewCardScale = 1.3f;

	/// Ceiling on AI steps in one turn before the turn is force-ended and the state dumped.
	/// MtgGameManager caps the AI's own actions; this catches the case where the loop itself
	/// stops making progress — a choice that never resolves, or a turn that never flips.
	private const int MaxAiStepsPerTurn = 300;

	private readonly List<int> _handCardIds = new();
	private int? _selectedAttackerId;
	private bool _isGameOver;
	private bool _choicePanelShowing;

	/// <summary>
	/// Set while the choice panel is offering which ABILITY to activate, rather than resolving a
	/// pending ChoiceAction from the game. The two share one panel and must not share a handler.
	/// </summary>
	private int? _abilityChoiceCardId;

	/// The graveyard popup is shared between both players' graveyards; this says whose is on
	/// screen, so a click in the opponent's cannot start a flashback cast that would never validate.
	private bool _viewingOpponentGraveyard;

	// Debug controls
	private bool _aiPaused;
	private Label _debugStatusLabel = null!;

	/// Steps taken in the AI turn currently in progress. A field rather than a local in
	/// RunAiTurn because that loop can be interrupted by a human-owned choice and resumed, and
	/// the hang guard has to budget the whole turn rather than restarting on every resume.
	private int _aiSteps;

	/// Tells the player what the game is waiting for. Every interaction mode that consumes the
	/// next click must set it, or that mode is unexplained.
	private PanelContainer _promptBanner = null!;
	private Label _promptLabel = null!;

	// Spell targeting state
	private bool _isFlashbackTargeting;

	/// An Aura being cast from hand. Shares the spell targeting state machine, but has exactly one
	/// target and no effect index, so the "next effect needing a target" walk is skipped.
	private bool _isAuraTargeting;
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

	/// <summary>
	/// Sandbox card values for the AI's discard/scry/tutor choices.
	///
	/// Read through Godot's FileAccess because System.IO cannot see a res:// path in an exported
	/// build — the same reason DraftScene loads the draft model this way. **Regenerating
	/// sim_results/card_values_csc.json does NOT update this file**; copy it across, or the game
	/// keeps playing against stale values. Null degrades to the pre-feature AI.
	/// </summary>
	private static CardValueTable? LoadCardValues()
	{
		const string path = "res://MtgGame/Assets/card_values_csc.json";
		if (!FileAccess.FileExists(path))
		{
			GD.Print($"[MtgGameScene] no card values at {path} — AI choices run unassisted");
			return null;
		}
		return CardValueTable.FromJson(FileAccess.GetFileAsString(path));
	}

	public override void _Ready()
	{
		var setup = GameManager.Instance.GetService<DeckSetupData>();
		_manager = new MtgGameManager(setup, cardValues: LoadCardValues());
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_choicePanel = new ChoicePanel();
		AddChild(_choicePanel);
		_choicePanel.Confirmed += OnChoiceConfirmed;

		_eventLog = new EventLogPanel();
		AddChild(_eventLog);

		_aiInspector = new AiInspectorPanel();
		AddChild(_aiInspector);

		_graveyardPopup = new GraveyardPopup();
		AddChild(_graveyardPopup);
		_graveyardPopup.CardClicked += OnGraveyardCardClicked;

		_cardPreviewPopup = new CardPreviewPopup();
		_cardPreviewPopup.InternalCardScene = ResourceLoader.Load<PackedScene>(
			"res://Common/Cards/2D/Card2D/internal_cardui2d_canvasgroup.tscn"
		);
		// The battlefield is capped at 0.45 by the height budget (see BattlefieldZone.CardScale),
		// so the board card is for recognising a card and this preview is for reading it. It has
		// to be a lot bigger than 0.45 to earn that job. Matches the draft screen.
		_cardPreviewPopup.PreviewScale = PreviewCardScale;
		AddChild(_cardPreviewPopup);

		_boardUI.EndTurnPressed += OnEndTurnPressed;
		_boardUI.PlayerCreatureClicked += OnPlayerCreatureClicked;
		_boardUI.PlayerCreatureRightClicked += OnPlayerCreatureRightClicked;
		_boardUI.OpponentCreatureClicked += OnOpponentCreatureClicked;
		_boardUI.OpponentDirectAttacked += OnOpponentDirectAttacked;
		_boardUI.GraveyardButtonPressed += OpenGraveyardPopup;
		_boardUI.OpponentGraveyardButtonPressed += OpenOpponentGraveyardPopup;
		_boardUI.LogTogglePressed += OnLogTogglePressed;
		_boardUI.CreatureHovered += OnCreatureHovered;
		_boardUI.CreatureHoverEnded += _ => _cardPreviewPopup.HideCard();

		// The log starts closed, so the board takes the full width until it is opened.
		_boardUI.SetBoardWidth(logOpen: false);
		_boardUI.SetLogToggleText(_eventLog.ToggleText);
		// Deferred so the containers have resolved their sizes before the drop zone is measured.
		CallDeferred(nameof(SyncBattlefieldDropZone));

		// Dragging and clicking are mutually exclusive on the same card — picking one up clears the
		// hover the click handler tests. So the hand stops being draggable whenever it is a
		// selection surface, which is the same condition a successful drag already required.
		_hand.DragEnabled = () => !IsWaitingForSelection();

		_hand.IsDragSuccess = context =>
			!IsWaitingForSelection() && context.SelectedAreas.Contains(_battlefieldDropZone);

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
				// An Aura chooses what it enchants as it is cast, so it needs the same targeting
				// prompt a spell gets. Without this branch it was cast with no target, failed
				// validation, and silently sprang back to hand.
				if (_manager.PermanentNeedsTarget(cardId))
				{
					_hand.LerpCardTransform(context.CardUI2D);
					EnterAuraTargetingMode(cardId);
					return;
				}

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

		_hand.CardClicked += OnHandCardClicked;

		var debugLayer = new CanvasLayer { Layer = 5 };
		AddChild(debugLayer);
		BuildPromptBanner(debugLayer);
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

		// Last-resort net: anything that escapes the handlers above still gets the game state
		// written out before the process dies. Godot does not surface a managed stack on its
		// way down, so without this a crash leaves nothing to work from.
		System.AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

		var startEvents = _manager.StartGame();
		_eventLog.AppendEvents(startEvents, _manager.State, _manager.HumanPlayerId);
		Refresh();
	}

	public override void _ExitTree()
	{
		System.AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
		System.Threading.Tasks.TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
	}

	private void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
	{
		WriteSnapshot("crash", $"Unhandled exception: {e.ExceptionObject}");
	}

	private void OnUnobservedTaskException(
		object? sender,
		System.Threading.Tasks.UnobservedTaskExceptionEventArgs e
	)
	{
		WriteSnapshot("crash", $"Unobserved task exception: {e.Exception}");
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
		else if (key.Keycode == Key.F7)
		{
			SaveScenario();
		}
		else if (key.Keycode == Key.F6)
		{
			// Pull on open as well as on every AI step, so toggling it on mid-turn shows the
			// decision already made rather than staying blank until the AI moves again.
			_aiInspector.Toggle();
			_aiInspector.Show(_manager.LastAiDecision);
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

		// ACCUMULATE, then advance. A cost states how many cards it wants — "exile two cards from
		// your graveyard" is one cost needing two selections — and this used to record a single
		// payment and move straight on. Validate demands the exact count, so any cost above 1 was
		// unpayable by a human and the activation just failed with no message: Grim Lavamancer's
		// ability could not be used at all, while the AI played it fine because MtgActionGenerator
		// reads RequiredPaymentCount. Same rule as everywhere else — ask the engine, do not
		// re-derive it here.
		var already = _pendingCostPayments.TryGetValue(_currentCostIndex, out var existing)
			? existing
			: ImmutableList<int>.Empty;
		var collectedForThisCost = already.Add(permanentId);
		_pendingCostPayments = _pendingCostPayments.SetItem(
			_currentCostIndex,
			collectedForThisCost
		);

		var requiredForThisCost = isAbility
			? _manager.GetAbilityAdditionalCostRequiredPayments(
				cardId,
				abilityIndex,
				_currentCostIndex
			)
			: _manager.GetAdditionalCostRequiredPayments(cardId, _currentCostIndex);

		if (collectedForThisCost.Count < requiredForThisCost)
		{
			// Same cost, fewer choices — a card already picked must not be pickable twice.
			_currentCostValidPaymentIds.Remove(permanentId);
			Refresh();
			return;
		}

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

	// ===== PROMPT BANNER =====

	private void BuildPromptBanner(CanvasLayer layer)
	{
		_promptBanner = new PanelContainer { Visible = false };
		_promptBanner.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		_promptBanner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		_promptBanner.GrowHorizontal = Control.GrowDirection.Both;
		_promptBanner.OffsetTop = 44;
		_promptBanner.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(_promptBanner);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		_promptBanner.AddChild(margin);

		_promptLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_promptLabel.AddThemeFontSizeOverride("font_size", 18);
		_promptLabel.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		_promptLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		margin.AddChild(_promptLabel);
	}

	/// True while the game is waiting on the player to pick something (a target, a cost payment,
	/// a choice) or is not theirs to act in at all. In those states the hand is for clicking.
	private bool IsWaitingForSelection() =>
		_manager.IsAiTurn
		|| _isGameOver
		|| _targetingSpellCardId.HasValue
		|| _additionalCostCardId.HasValue
		|| _activatingAbilityCardId.HasValue
		|| _choicePanelShowing;

	private void UpdatePromptBanner()
	{
		var text = BuildPromptText();
		_promptLabel.Text = text;
		// The choice panel is modal and carries its own prompt — two prompts at once is noise.
		_promptBanner.Visible = text.Length > 0 && !_choicePanelShowing;
	}

	/// <summary>
	/// One sentence naming what the next click does, for every mode that consumes a click.
	/// Empty means the player is free to act however they like.
	/// </summary>
	private string BuildPromptText()
	{
		if (_isGameOver)
			return "";

		if (_manager.IsAiTurn)
			return "Opponent is thinking…";

		if (_additionalCostCardId.HasValue)
		{
			var name = CardName(_additionalCostCardId.Value);
			var cost = _additionalCostIsAbility
				? _manager.GetAbilityAdditionalCostDescription(
					_additionalCostCardId.Value,
					_additionalCostAbilityIndex,
					_currentCostIndex
				)
				: _manager.GetAdditionalCostDescription(
					_additionalCostCardId.Value,
					_currentCostIndex
				);
			// A multi-card cost has to say how many are still wanted, or the player clicks once,
			// sees nothing happen, and concludes the card is broken.
			var required = _additionalCostIsAbility
				? _manager.GetAbilityAdditionalCostRequiredPayments(
					_additionalCostCardId.Value,
					_additionalCostAbilityIndex,
					_currentCostIndex
				)
				: _manager.GetAdditionalCostRequiredPayments(
					_additionalCostCardId.Value,
					_currentCostIndex
				);
			var picked = _pendingCostPayments.TryGetValue(_currentCostIndex, out var sofar)
				? sofar.Count
				: 0;
			var remaining = required > 1 ? $" ({required - picked} more)" : "";

			return $"{name} — {cost}{remaining}{WhereHint(_currentCostValidPaymentIds)}"
				+ "   (Esc to cancel)";
		}

		if (_activatingAbilityCardId.HasValue)
			return $"{CardName(_activatingAbilityCardId.Value)} — choose a target"
				+ $"{WhereHint(_currentValidTargetIds)}   (Esc to cancel)";

		if (_targetingSpellCardId.HasValue)
			return $"{CardName(_targetingSpellCardId.Value)} — choose a target"
				+ $"{WhereHint(_currentValidTargetIds)}   (Esc to cancel)";

		if (_selectedAttackerId.HasValue)
		{
			var name = CardName(_selectedAttackerId.Value);
			var targets = _manager.GetLegalAttackTargets(_selectedAttackerId.Value);
			if (targets.Count == 0)
				return $"{name} has no legal attack — click it again to deselect";
			// Not being allowed to go to the face is the whole tell that Taunt is in play, and it
			// is the one thing a player will not work out from the highlights alone.
			return targets.Contains(_manager.AiPlayerId)
				? $"{name} is attacking — click a highlighted target"
				: $"{name} is attacking — Taunt or Flying forces it onto a highlighted creature";
		}

		return "";
	}

	/// GetObject throws on an unknown id, and the banner is rebuilt on every Refresh — including
	/// the one right after a card the player had selected left play. Check before looking up.
	private string CardName(int cardId) =>
		_manager.State.HasObject(cardId) && _manager.State.GetObject(cardId) is Card card
			? card.Name
			: "Card";

	/// <summary>
	/// Names the zone the player should be looking in. Derived from where the legal choices
	/// actually are rather than declared per card, so it cannot drift from what is clickable.
	/// </summary>
	private string WhereHint(IReadOnlyCollection<int> ids)
	{
		if (ids.Count == 0)
			return " — no legal choices";

		var state = _manager.State;
		var handId = state.GetWellKnownId(MtgObjectKeys.Player1Hand);
		var graveyardId = state.GetWellKnownId(MtgObjectKeys.Player1Graveyard);

		if (ids.All(id => state.HasObject(id) && state.GetCardZoneId(id) == handId))
			return " — click a card in your hand";
		if (ids.All(id => state.HasObject(id) && state.GetCardZoneId(id) == graveyardId))
			return " — click a card in your graveyard";
		return "";
	}

	// ===== SPELL TARGETING STATE MACHINE =====

	private void EnterTargetingMode(int cardId)
	{
		EnterTargetingMode(cardId, ImmutableDictionary<int, ImmutableList<int>>.Empty);
	}

	/// <summary>
	/// Targeting for an Aura. Its legal targets come from AuraTargetComponent rather than from a
	/// spell effect, so the valid-target set is seeded from the manager's aura lookup instead of
	/// GetSpellValidTargets — which reads a SpellComponent an Aura does not have and would return
	/// nothing, leaving every creature unhighlighted and unclickable.
	/// </summary>
	private void EnterAuraTargetingMode(int cardId)
	{
		_isAuraTargeting = true;
		_targetingSpellCardId = cardId;
		_pendingTargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_pendingAdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		_currentEffectIndex = 0;
		_currentValidTargetIds = new HashSet<int>(_manager.GetPermanentValidTargets(cardId));
		Refresh();
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
		_isAuraTargeting = false;
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
		_viewingOpponentGraveyard = false;
		_graveyardPopup.ShowGraveyard(cards, state, flashbackIds, targetIds);
	}

	/// <summary>
	/// The opponent's graveyard, for reading only. Nothing in it is castable or targetable by the
	/// player, so it shows no flashback glow and its clicks are ignored — see
	/// <see cref="OnGraveyardCardClicked"/>.
	/// </summary>
	private void OpenOpponentGraveyardPopup()
	{
		var state = _manager.State;
		var graveyardId = state.GetWellKnownId(MtgObjectKeys.Player2Graveyard);
		_viewingOpponentGraveyard = true;
		_graveyardPopup.ShowGraveyard(
			state.GetCardsInZone(graveyardId).ToList(),
			state,
			flashbackIds: System.Array.Empty<int>(),
			title: "Opponent's Graveyard"
		);
	}

	/// <summary>
	/// A click on a card in hand. Only meaningful while something is asking the player to pick
	/// one — outside those modes the hand is drag-to-play and a click does nothing.
	/// </summary>
	private void OnHandCardClicked(CardUI2D cardUI)
	{
		if (_manager.IsAiTurn || _isGameOver || _choicePanelShowing)
			return;
		if (!int.TryParse(cardUI.Id, out var cardId))
			return;

		if (_additionalCostCardId.HasValue)
			OnCostPaymentSelected(cardId);
		else if (_activatingAbilityCardId.HasValue)
			OnAbilityTargetSelected(cardId);
		else if (_targetingSpellCardId.HasValue)
			OnTargetSelected(cardId);
	}

	private void OnGraveyardCardClicked(int cardId)
	{
		if (_manager.IsAiTurn || _isGameOver || _viewingOpponentGraveyard)
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

		// An Aura has exactly one target and no spell effects to walk, so it completes here
		// rather than asking which effect still needs one — that walk reads a SpellComponent an
		// Aura does not have and would report "none left" only by accident.
		if (_isAuraTargeting)
		{
			ExitTargetingMode();
			var (auraSuccess, auraEvents) = _manager.CastPermanent(cardId, targetId);
			if (!auraSuccess)
				return;
			_eventLog.AppendEvents(auraEvents, _manager.State, _manager.HumanPlayerId);
			Refresh();
			CheckAndShowGameOver(auraEvents);
			return;
		}

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

	private async void OnChoiceConfirmed(ImmutableList<int> selectedIds)
	{
		// The panel serves two different jobs. An ability choice is a purely local decision that
		// has not touched the game yet, so it must not be routed into ResolveChoice — there is no
		// pending ChoiceAction for it to resolve, and doing so would drop the pick silently.
		if (_abilityChoiceCardId.HasValue)
		{
			var cardId = _abilityChoiceCardId.Value;
			_abilityChoiceCardId = null;
			_choicePanelShowing = false;
			_choicePanel.Hide();

			if (!selectedIds.IsEmpty)
				BeginAbilityActivation(cardId, selectedIds[0]);
			else
				Refresh();
			return;
		}

		_choicePanelShowing = false;
		var events = _manager.ResolveHumanChoice(selectedIds);
		_eventLog.AppendEvents(events, _manager.State, _manager.HumanPlayerId);
		Refresh();
		if (CheckAndShowGameOver(events))
			return;

		// That choice may have been the one that stopped the opponent's turn mid-flight — your
		// death trigger firing off their removal spell, say. RunAiTurn returned when it hit it,
		// so pick the turn back up now that it is answered.
		if (_manager.IsAiTurn)
			await RunAiTurn();
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

		_aiSteps = 0;
		await RunAiTurn();
	}

	/// <summary>
	/// Drives the AI's turn until it ends — or until it reaches a choice that belongs to the
	/// HUMAN, at which point it returns and <see cref="OnChoiceConfirmed"/> calls it again.
	///
	/// Re-entrant for exactly that reason. A triggered ability of yours can fire on the
	/// opponent's turn (a death trigger that scries, most obviously), and answering it is yours
	/// to do — this loop used to take any pending choice regardless of owner, so the AI silently
	/// answered yours. The step cap lives in a field rather than a local so a turn interrupted by
	/// three of your choices still gets ONE budget, not three.
	/// </summary>
	private async Task RunAiTurn()
	{
		while (_manager.IsAiTurn && !_isGameOver)
		{
			// Checked before the delay so the panel is not left sitting behind a dead wait. The
			// panel itself is already up: Refresh gates on ownership and ran before we got here.
			if (_manager.IsWaitingForChoice && _manager.IsHumanChoice)
				return;

			await ToSignal(
				GetTree().CreateTimer(_aiPaused ? 0.1f : 0.8f),
				SceneTreeTimer.SignalName.Timeout
			);

			if (_aiPaused)
				continue;

			if (++_aiSteps > MaxAiStepsPerTurn)
			{
				var path = WriteSnapshot(
					"hang",
					$"AI turn exceeded {MaxAiStepsPerTurn} steps without ending its turn."
				);
				ShowDebugToast(
					$"AI turn did not finish after {MaxAiStepsPerTurn} steps — forcing end of turn."
						+ (path == null ? "" : $"\nState saved to {path}")
				);
				_manager.ForceEndAiTurn();
				Refresh();
				break;
			}

			// Run the (potentially expensive) AI search on a background thread so the UI stays
			// responsive during a heavy opponent turn. The manager's Compute* methods are pure
			// reads of the immutable GameState; only the Apply* calls mutate state, and those
			// run here on the main thread after the await resumes (Godot marshals the
			// continuation back to the main thread via its SynchronizationContext).
			ImmutableList<GameEvent> stepEvents;
			try
			{
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
			}
			catch (System.Exception ex)
			{
				ReportCrash("AI turn", ex);
				_manager.ForceEndAiTurn();
				Refresh();
				break;
			}

			if (!string.IsNullOrEmpty(_manager.LastAiError))
				ShowDebugToast(_manager.LastAiError);

			_eventLog.AppendEvents(stepEvents, _manager.State, _manager.HumanPlayerId);
			// Pushed every step whether or not the panel is open — it early-outs when the decision
			// has not changed, and pausing (Space) then opening it should show the step you paused
			// on rather than nothing.
			_aiInspector.Show(_manager.LastAiDecision);
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

		// More than one ability means the player has a decision to make. Taking [0] silently
		// picked for them — a planeswalker with three loyalty abilities could only ever use its
		// first, which is how Jace Beleren shipped with two unreachable abilities.
		if (legalAbilities.Count > 1)
		{
			_abilityChoiceCardId = cardId;
			_choicePanel.ShowChoice(
				"Choose an ability",
				legalAbilities
					.Select(a => new ChoiceOption { Id = a.Index, DisplayText = a.Name })
					.ToImmutableList(),
				minChoices: 1,
				maxChoices: 1
			);
			_choicePanelShowing = true;
			Refresh();
			return;
		}

		BeginAbilityActivation(cardId, legalAbilities[0].Index);
	}

	/// <summary>
	/// Runs the activation flow for one chosen ability: additional-cost selection, then target
	/// selection, then activation. Split out of the click handler so the ability chooser can
	/// re-enter it once the player has picked.
	/// </summary>
	private void BeginAbilityActivation(int cardId, int abilityIndex)
	{
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

	private void OnLogTogglePressed()
	{
		_eventLog.Toggle();
		_boardUI.SetBoardWidth(_eventLog.IsOpen);
		_boardUI.SetLogToggleText(_eventLog.ToggleText);
		CallDeferred(nameof(SyncBattlefieldDropZone));
	}

	/// <summary>
	/// Matches the hand-card drop target to wherever the player's battlefield actually is. The
	/// zone used to be a fixed Area2D authored against one layout; the board now changes width
	/// when the log opens, so a hardcoded rect would silently stop lining up.
	/// </summary>
	private void SyncBattlefieldDropZone()
	{
		var rect = _boardUI.GetPlayerBattlefieldRect();
		if (rect.Size.X <= 0 || rect.Size.Y <= 0)
			return;

		_battlefieldDropZone.Position = rect.Position + rect.Size / 2f;
		if (_battlefieldDropZone.GetChildOrNull<CollisionShape2D>(0)?.Shape is RectangleShape2D box)
			box.Size = rect.Size;
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
			TypeLine = MtgCardMapper.GetTypeLine(card),
			PowerToughness = MtgCardMapper.GetPowerToughness(card, _manager.State),
			RulesText = MtgCardMapper.GetRulesText(card, _manager.State),
			ArtworkTexture = CardArtLoader.Load(card.Name),
			FrameColor = MtgCardTheme.FrameColor(card),
			NamePlateColor = MtgCardTheme.NamePlateColor(card),
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
		var path = WriteSnapshot("debug", error: null);
		if (path != null)
			DisplayServer.ClipboardSet(path);
		ShowDebugToast(path == null ? "Snapshot failed" : $"Saved (path copied): {path}");
	}

	/// <summary>
	/// Writes a debug snapshot and returns its absolute path, or null if writing failed.
	/// Never throws — it is called from crash handlers, where a second failure would replace
	/// the diagnosis with a mystery.
	/// </summary>
	/// <summary>
	/// Saves the current position as a scenario the standalone viewer can load.
	///
	/// Deliberately no naming dialog — a position worth keeping is usually noticed mid-turn, and
	/// anything that interrupts to ask for a name is a thing you stop doing. Rename the file
	/// afterwards; the viewer lists them by filename.
	///
	/// The AI's player id is saved as the mover because the scenarios worth keeping are the ones
	/// where the AI did something inexplicable.
	/// </summary>
	private void SaveScenario()
	{
		try
		{
			var name = $"scenario_{(long)Time.GetUnixTimeFromSystem()}";
			var scenario = Scenario.Capture(
				_manager.State,
				_manager.AiPlayerId,
				name,
				$"Captured from a live game on turn {_manager.State.TryGetGame()?.TurnNumber ?? 0}."
			);

			using var da = DirAccess.Open("user://");
			da?.MakeDir("scenarios");
			var path = $"user://scenarios/{name}.json";
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
			if (file == null)
			{
				ShowDebugToast("Could not open the scenarios folder for writing.");
				return;
			}
			file.StoreString(scenario.ToJson());

			// The console reads scenarios/ relative to the SHELL's working directory while Godot
			// writes to user://, so the two do not meet on their own — same trap as the draft
			// model asset. The absolute path is printed so the copy is one command.
			ShowDebugToast(
				$"Scenario saved to {ProjectSettings.GlobalizePath(path)}\n"
					+ "Copy it into scenarios/ at the repo root for console mode 5."
			);
		}
		catch (System.Exception ex)
		{
			GD.PushError($"Failed to save scenario: {ex}");
			ShowDebugToast($"Scenario save failed: {ex.Message}");
		}
	}

	private string? WriteSnapshot(string prefix, string? error)
	{
		try
		{
			var json = _manager.ExportDebugSnapshot(error);
			using var da = DirAccess.Open("user://");
			da?.MakeDir("debug_snapshots");
			var timestamp = (long)Time.GetUnixTimeFromSystem();
			var path = $"user://debug_snapshots/{prefix}_{timestamp}.json";
			using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
			if (file == null)
				return null;
			file.StoreString(json);
			return ProjectSettings.GlobalizePath(path);
		}
		catch (System.Exception ex)
		{
			GD.PushError($"Failed to write debug snapshot: {ex}");
			return null;
		}
	}

	/// <summary>
	/// Single funnel for anything that kills a turn. Dumps the game state next to the exception
	/// so a crash report is reproducible instead of anecdotal, and leaves the message on screen.
	/// </summary>
	private void ReportCrash(string context, System.Exception ex)
	{
		GD.PushError($"[{context}] {ex}");
		var path = WriteSnapshot("crash", $"{context}: {ex}");
		ShowDebugToast(
			path == null
				? $"CRASH in {context}: {ex.Message} (snapshot failed)"
				: $"CRASH in {context}: {ex.Message}\nState saved to {path}"
		);
	}

	private void UpdateDebugStatus()
	{
		if (_aiPaused)
		{
			_debugStatusLabel.Text =
				"AI Paused  (Space = resume  |  Ctrl+Z = rewind  |  F5 = export  |  F6 = inspector  |  F7 = save scenario)";
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
		// A selected attacker highlights what it may legally hit, for the same reason a spell
		// highlights its targets: Taunt and Flying are invisible restrictions otherwise, and the
		// only feedback on an illegal attack was the click doing nothing.
		IEnumerable<int> highlightIds =
			isTargeting ? _currentValidTargetIds
			: _selectedAttackerId.HasValue
				? _manager.GetLegalAttackTargets(_selectedAttackerId.Value)
			: null;

		_boardUI.RefreshAll(
			_manager.State,
			_manager.HumanPlayerId,
			_manager.AiPlayerId,
			_selectedAttackerId,
			targetHighlightIds: highlightIds,
			additionalCostHighlightIds: _additionalCostCardId.HasValue
				? _currentCostValidPaymentIds
				: null
		);
		_hand.Modulate = Colors.White;

		// Central refresh rather than per-append: every path that logs an event ends here, so the
		// unread badge cannot go stale without the rest of the board going stale too.
		_boardUI.SetLogToggleText(_eventLog.ToggleText);

		// An ability choice owns the panel until the player answers it. Without this guard the
		// next Refresh sees no pending game choice and hides the panel out from under them.
		// Gate on WHOSE choice it is, not whose turn it is — see MtgGameManager.IsHumanChoice.
		var shouldShowChoice = !_isGameOver && _manager.IsHumanChoice;
		if (_abilityChoiceCardId.HasValue)
		{
			// Leave it alone.
		}
		else if (shouldShowChoice && !_choicePanelShowing)
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
		UpdatePromptBanner();
	}

	private void SyncHand()
	{
		var state = _manager.State;
		var humanHandId = state.GetWellKnownId(MtgObjectKeys.Player1Hand);
		var handCards = state.GetCardsInZone(humanHandId).ToList();

		// "You may play lands from the top of your library" (Radha) is shown AS A HAND CARD rather
		// than as a new board slot. The hand is already the surface for "cards you can play" — it
		// has the drawing, the details, the click routing and the highlight tinting — and the
		// engine treats the card identically, because IsInCastableZone accepts library-top and all
		// four play actions consult it. A dedicated slot would be a second copy of all of that.
		//
		// Without this the AI plays lands off the top and the human cannot see the card at all.
		var libraryTop = _manager.GetPlayableLibraryTopCard();
		if (libraryTop != null)
			handCards.Add(libraryTop);

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
					? MarkIfFromLibraryTop(
						MtgCardMapper.ToDetails(card, state, _manager.HumanPlayerId),
						id,
						libraryTop
					)
					: new InternalCardUI2D.Details()
			)
			.ToList();

		_hand.SetCardsDetails(details);
		HighlightSelectableHandCards(uiCards);
	}

	/// <summary>
	/// Says so on the card when one of the "hand" cards is actually the top of your library.
	///
	/// Sharing the hand surface is what makes the feature cheap, but a card that is not in your
	/// hand sitting silently among cards that are is worse than no display at all — the player
	/// would think they had drawn it, and wonder why it vanished when they drew for turn.
	/// </summary>
	private static InternalCardUI2D.Details MarkIfFromLibraryTop(
		InternalCardUI2D.Details details,
		int cardId,
		Card? libraryTop
	)
	{
		if (libraryTop == null || cardId != libraryTop.Id)
			return details;

		details.RulesText = string.IsNullOrWhiteSpace(details.RulesText)
			? "(Top of your library)"
			: $"(Top of your library)\n{details.RulesText}";

		return details;
	}

	/// <summary>
	/// Tints the hand cards the current mode will accept, using the same colours the battlefield
	/// uses for the same meanings (yellow = target, orange = cost payment).
	/// </summary>
	private void HighlightSelectableHandCards(List<CardUI2D> uiCards)
	{
		var isCostMode = _additionalCostCardId.HasValue;
		var selectable =
			isCostMode ? _currentCostValidPaymentIds
			: _targetingSpellCardId.HasValue || _activatingAbilityCardId.HasValue
				? _currentValidTargetIds
			: null;

		var tint = isCostMode ? new Color(1f, 0.65f, 0.1f, 1f) : new Color(1f, 1f, 0.3f, 1f);

		foreach (var ui in uiCards)
		{
			var isSelectable =
				selectable != null && int.TryParse(ui.Id, out var id) && selectable.Contains(id);
			ui.Modulate = isSelectable ? tint : Colors.White;
		}
	}
}
