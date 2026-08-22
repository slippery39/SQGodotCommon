using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgGame;

public enum AiStrategyType
{
	BeamSearch,
	MultiTurnBeamSearch,
}

/// <summary>
/// Owns the GameState and drives the game loop.
/// Plain C# class — no Godot dependencies. The Godot scene owns one instance.
/// </summary>
public class MtgGameManager
{
	// Guardrails for the interactive AI path. Unlike the simulator's GameRunner, the Godot
	// loop has no outer time/action limits, so a runaway search would freeze the UI thread
	// and eventually crash. These bound a single AI turn's work.
	private static readonly TimeSpan AiMoveTimeBudget = TimeSpan.FromSeconds(2);
	private const int MaxAiActionsPerTurn = 200;

	private GameState _state;
	private readonly Random _rng = new();
#pragma warning disable CS0618
	private readonly MtgGameIds _ids;
#pragma warning restore CS0618
	private readonly ICapturingAiStrategy _aiStrategy;

	private int _aiActionsThisTurn;

	/// <summary>
	/// Set when a guardrail trips during an AI step (search threw or hit the action cap). The
	/// scene can surface this so failures are visible instead of the game silently hanging or
	/// crashing. Empty when there is no error. Cleared at the start of each
	/// <see cref="ComputeAiAction"/> / <see cref="ComputeAiChoice"/> call.
	/// </summary>
	public string LastAiError { get; private set; } = "";

	private record HistoryEntry(GameState State, string ActionDescription);

	private readonly List<HistoryEntry> _history = new();
	private readonly List<(AiDecision Decision, int HistoryIndex)> _aiDecisions = new();

	public int HumanPlayerId { get; private set; }
	public int AiPlayerId { get; private set; }

	public GameState State => _state;

	/// <summary>
	/// TEST AND DEBUG TOOLING ONLY — replaces the manager's state wholesale.
	///
	/// Gameplay must never use this; every real state change goes through SubmitAction so the
	/// engine's validation and post-processing run. It exists so a test can set up an exact board
	/// (a specific Aura in hand, a stocked graveyard) without playing twenty turns to reach it.
	/// </summary>
	public void DebugSetState(GameState state) => _state = state;

	public bool IsAiTurn => _state.TryGetGame()?.ActivePlayerId == AiPlayerId;
	public bool IsWaitingForChoice => _state.IsWaitingForChoice;

	public MtgGameManager(
		DeckSetupData setup,
		AiStrategyType strategyType = AiStrategyType.MultiTurnBeamSearch
	)
	{
#pragma warning disable CS0618
		(_state, _ids) = MtgGameFactory.Create();
		_aiStrategy = strategyType switch
		{
			AiStrategyType.MultiTurnBeamSearch => new MultiTurnBeamSearchAiStrategy(
				_ids,
				currentTurnDepth: 3,
				lookaheadTurns: 2,
				captureDecisions: true,
				moveTimeBudget: AiMoveTimeBudget
			),
			_ => new BeamSearchAiStrategy(_ids, maxDepth: 3, captureDecisions: true),
		};
#pragma warning restore CS0618
		HumanPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player1);
		AiPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player2);
		PopulateDecks(setup);
	}

	public ImmutableList<GameEvent> StartGame()
	{
		(_state, var events) = _state.BeginGame(
			_state.GetWellKnownId(MtgObjectKeys.Game),
			HumanPlayerId,
			AiPlayerId
		);
		_history.Add(new HistoryEntry(_state, "Game Start"));
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) SubmitAction(GameAction action)
	{
		var preActionState = _state;
		var (newState, success) = _state.TryAddAction(action);
		if (!success)
			return (false, ImmutableList<GameEvent>.Empty);
		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;
		_history.Add(new HistoryEntry(_state, ActionDescriber.Describe(action, preActionState)));
		return (true, events.AddRange(DrainAiOwnedChoices()));
	}

	/// <summary>
	/// Answers, as the AI, any pending choice that belongs to the AI — then keeps going, because
	/// one resolution can immediately pause on the next.
	///
	/// Necessary the moment choices became owner-gated. The AI only ever answered choices from
	/// inside its own turn loop, which was sound while the UI handed the human everything that
	/// appeared on the human's turn. Now that the human is correctly shown only their own
	/// choices, an AI-owned one raised during the human's turn — an opponent's end-step trigger,
	/// a trigger off a creature you killed — would sit unanswered on the action stack and wedge
	/// the game with no panel and no way to proceed.
	///
	/// Called from the two funnels every human-driven state change passes through, so no caller
	/// can forget it.
	/// </summary>
	private ImmutableList<GameEvent> DrainAiOwnedChoices()
	{
		var events = ImmutableList<GameEvent>.Empty;

		// Bounded rather than while(true): a choice whose resolution re-raises an identical
		// choice would otherwise hang the UI thread outright. Matches MaxAiStepsPerTurn's intent.
		for (var guard = 0; guard < 64; guard++)
		{
			if (!_state.IsWaitingForChoice || IsHumanChoice)
				return events;

			events = events.AddRange(ApplyAiChoice(ComputeAiChoice()));
		}

		LastAiError = "AI choice resolution did not settle after 64 steps.";
		return events;
	}

	/// <summary>
	/// Attacker deduplication is applied only for the AI. It collapses strategically-identical
	/// attackers to one representative action, which keeps the AI's search from blowing up on a
	/// board full of identical tokens — but for the human it means the second copy of a creature
	/// generates no attack action at all and is simply unclickable.
	/// </summary>
	public List<GameAction> GetLegalActions(int playerId)
	{
		return MtgActionGenerator.GetLegalActions(
			_state,
			playerId,
			deduplicateAttackers: playerId != HumanPlayerId
		);
	}

	public ImmutableList<GameEvent> EndTurn()
	{
		var (_, events) = SubmitAction(
			new EndTurnAction
			{
				GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
				Player1Id = HumanPlayerId,
				Player2Id = AiPlayerId,
			}
		);
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastCreature(int cardId)
	{
		// XValue for the {X} Hydras, on the same "spend what you have" rule the X spells use.
		// Without it a human casting Primordial Hydra gets a 0/0 that dies on arrival while the
		// AI casts it correctly — the "the AI can do it and I can't" signature.
		return SubmitAction(
			new CastCreatureAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				XValue = MaxAffordableX(cardId),
			}
		);
	}

	public (bool Success, ImmutableList<GameEvent> Events) Attack(int attackerId, int targetId)
	{
		return SubmitAction(
			new AttackAction
			{
				AttackerId = attackerId,
				TargetId = targetId,
				AttackingPlayerId = HumanPlayerId,
			}
		);
	}

	/// <summary>
	/// Every target this attacker may legally attack — opponent creatures plus the opponent
	/// themselves. Decided by asking <see cref="AttackAction"/> itself rather than by
	/// re-implementing the rules here, so the highlight cannot drift out of step with Taunt,
	/// Flying and Reach. Empty means the creature cannot attack at all.
	/// </summary>
	public List<int> GetLegalAttackTargets(int attackerId)
	{
		var opponentBattlefieldId = _state.GetPlayerZoneId(AiPlayerId, ZoneType.Battlefield);

		return _state
			.GetCardsInZone(opponentBattlefieldId)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Select(c => c.Id)
			.Append(AiPlayerId)
			.Where(targetId =>
				new AttackAction
				{
					AttackerId = attackerId,
					TargetId = targetId,
					AttackingPlayerId = HumanPlayerId,
				}
					.ValidateAdd(_state)
					.IsValid
			)
			.ToList();
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastSpell(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds
	)
	{
		return CastSpell(cardId, targetIds, ImmutableDictionary<int, ImmutableList<int>>.Empty);
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastSpell(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds,
		ImmutableDictionary<int, ImmutableList<int>> additionalCostPayments
	)
	{
		return SubmitAction(
			new CastSpellAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				TargetIds = targetIds,
				AdditionalCostPayments = additionalCostPayments,
				XValue = MaxAffordableX(cardId),
			}
		);
	}

	/// <summary>
	/// The largest X the human can pay for, or 0 for a card with no {X} in its cost.
	///
	/// X was previously never set at all from the UI, so every X spell resolved at X = 0 —
	/// Mind Spring was a two-mana "draw zero cards". There is no X prompt in the interface, so
	/// rather than build one this spends whatever mana is available, which is both what the
	/// player expects from a card that says "draw X" and almost always the right choice.
	///
	/// The AI is unaffected: MtgActionGenerator enumerates one action per affordable X, so it
	/// still chooses.
	/// </summary>
	private int MaxAffordableX(int cardId)
	{
		if (_state.GetObject(cardId) is not Card card)
			return 0;
		if (!card.HasComponent<XCostComponent>())
			return 0;

		var available = _state.GetPlayer(HumanPlayerId).CurrentMana;

		var x = 0;
		while (_state.ComputeEffectiveCost(card, HumanPlayerId, x + 1) <= available)
			x++;

		return x;
	}

	public bool HasFlashback(int cardId)
	{
		if (!_state.HasObject(cardId))
			return false;
		var card = _state.GetObject(cardId) as Card;
		return card?.HasComponent<FlashbackComponent>() == true;
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastFromGraveyard(
		int cardId,
		ImmutableDictionary<int, ImmutableList<int>> targetIds
	)
	{
		return SubmitAction(
			new CastFromGraveyardAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				TargetIds = targetIds,
				// Flashback can cost more than mana — Despoiler of Souls exiles two creature
				// cards from your graveyard. Leaving this empty made the action fail validation
				// every time, so the card was simply uncastable for a human.
				AdditionalCostPayments = PaymentsFor<CastFromGraveyardAction>(cardId),
			}
		);
	}

	/// <summary>
	/// The additional-cost payments MtgActionGenerator already worked out for this card.
	///
	/// Taken from the generator rather than recomputed so the UI cannot disagree with the engine
	/// about what a cost is or how it is paid. Empty when the card is not currently offered,
	/// which lets the action fail its own validation with a real reason rather than this method
	/// inventing one.
	/// </summary>
	private ImmutableDictionary<int, ImmutableList<int>> PaymentsFor<T>(int cardId)
		where T : GameAction
	{
		foreach (var action in MtgActionGenerator.GetLegalActions(_state, HumanPlayerId, false))
		{
			if (action is not T)
				continue;

			switch (action)
			{
				case CastFromGraveyardAction g when g.CardId == cardId:
					return g.AdditionalCostPayments;
				case CastPermanentAction p when p.CardId == cardId:
					return p.AdditionalCostPayments;
			}
		}

		return ImmutableDictionary<int, ImmutableList<int>>.Empty;
	}

	public bool IsSpell(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.GetComponent<SpellComponent>() != null;
	}

	public bool IsLand(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.HasSubtype("Land") ?? false;
	}

	public (bool Success, ImmutableList<GameEvent> Events) PlayLand(int cardId)
	{
		return SubmitAction(
			new PlayLandAction { CardId = cardId, CastingPlayerId = HumanPlayerId }
		);
	}

	public bool IsNonCreaturePermanent(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card != null
			&& card.HasComponent<PermanentComponent>()
			&& !card.HasComponent<CreatureComponent>();
	}

	/// <summary>
	/// An Aura picks what it enchants as it is cast, exactly as a targeted spell does. Without a
	/// target CastPermanentAction refuses to validate, so a human could not play ANY Aura — the
	/// AI could, because it goes through MtgActionGenerator, which offers one action per legal
	/// target. That asymmetry is the tell for this whole class of bug: the UI re-derived action
	/// construction instead of asking the generator.
	/// </summary>
	public bool PermanentNeedsTarget(int cardId) =>
		(_state.GetObject(cardId) as Card)?.HasComponent<AuraTargetComponent>() == true;

	public List<int> GetPermanentValidTargets(int cardId)
	{
		var aura = (_state.GetObject(cardId) as Card)?.GetComponent<AuraTargetComponent>();
		if (aura == null)
			return [];

		return aura
			.Targeting.GetValidTargets(
				new TargetingContext
				{
					GameState = _state,
					SourceCardId = cardId,
					CastingPlayerId = HumanPlayerId,
				}
			)
			.ToList();
	}

	public (bool Success, ImmutableList<GameEvent> Events) CastPermanent(
		int cardId,
		int targetId = 0
	)
	{
		return SubmitAction(
			new CastPermanentAction
			{
				CardId = cardId,
				CastingPlayerId = HumanPlayerId,
				TargetIds =
					targetId == 0 ? ImmutableList<int>.Empty : ImmutableList.Create(targetId),
				AdditionalCostPayments = PaymentsFor<CastPermanentAction>(cardId),
			}
		);
	}

	public bool SpellNeedsTargets(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		return spell?.Effects.Any(e => e.TargetingStrategy.RequiresUserSelection) ?? false;
	}

	public List<int> GetSpellValidTargets(int cardId, int effectIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		if (spell == null || effectIndex < 0 || effectIndex >= spell.Effects.Count)
			return new List<int>();

		var context = new TargetingContext
		{
			GameState = _state,
			SourceCardId = cardId,
			CastingPlayerId = HumanPlayerId,
		};
		return spell.Effects[effectIndex].TargetingStrategy.GetValidTargets(context).ToList();
	}

	public int GetNextEffectNeedingTarget(int cardId, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var spell = card?.GetComponent<SpellComponent>();
		if (spell == null)
			return -1;
		for (int i = afterIndex + 1; i < spell.Effects.Count; i++)
		{
			if (spell.Effects[i].TargetingStrategy.RequiresUserSelection)
				return i;
		}
		return -1;
	}

	// ===== ADDITIONAL COST HELPERS =====

	public bool SpellHasAdditionalCostSelection(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.AdditionalCastCosts.Any(c => c.RequiresSelection) ?? false;
	}

	public List<int> GetAdditionalCostValidPayments(int cardId, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null || costIndex < 0 || costIndex >= card.AdditionalCastCosts.Count)
			return new List<int>();
		return card.AdditionalCastCosts[costIndex]
			.GetValidPayments(_state, HumanPlayerId, cardId)
			.ToList();
	}

	public string GetAdditionalCostDescription(int cardId, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null || costIndex < 0 || costIndex >= card.AdditionalCastCosts.Count)
			return "";
		return card.AdditionalCastCosts[costIndex].Describe();
	}

	/// <summary>
	/// How many cards this cost needs selected — 2 for "exile two cards from your graveyard".
	///
	/// The UI collected exactly one payment per cost and then moved on, while Validate demands the
	/// exact count, so any cost with a Count above 1 was unpayable by a human and the activation
	/// failed silently. The AI was unaffected: it goes through MtgActionGenerator, which reads
	/// RequiredPaymentCount. That asymmetry is the "the AI can do it and I can't" signature.
	/// </summary>
	public int GetAdditionalCostRequiredPayments(int cardId, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null || costIndex < 0 || costIndex >= card.AdditionalCastCosts.Count)
			return 1;
		return card.AdditionalCastCosts[costIndex].RequiredPaymentCount;
	}

	public int GetNextAdditionalCostNeedingSelection(int cardId, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		if (card == null)
			return -1;
		for (int i = afterIndex + 1; i < card.AdditionalCastCosts.Count; i++)
			if (card.AdditionalCastCosts[i].RequiresSelection)
				return i;
		return -1;
	}

	// ===== ACTIVATED ABILITY HELPERS =====

	public List<(int Index, string Name, int ManaCost)> GetLegalAbilities(int cardId)
	{
		// Must go through the wrapper, not the generator directly: the generator defaults to
		// deduplicating, which is an AI-only optimisation. It reads nothing but
		// ActivateAbilityActions today, so the default was harmless — but a human path silently
		// running with AI dedup on is a trap waiting for the day dedup covers abilities too,
		// and the symptom would be a creature the player simply cannot activate.
		var legalActions = GetLegalActions(HumanPlayerId);
		var result = new List<(int, string, int)>();
		var card = _state.GetObject(cardId) as Card;
		if (card == null)
			return result;
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		foreach (var action in legalActions.OfType<ActivateAbilityAction>())
		{
			if (action.CardId != cardId)
				continue;
			if (action.AbilityIndex >= abilities.Count)
				continue;
			var ability = abilities[action.AbilityIndex];
			result.Add((action.AbilityIndex, ability.Name, ability.ManaCost));
		}
		return result;
	}

	public bool AbilityNeedsTarget(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return false;
		return abilities[abilityIndex].TargetedEffect?.TargetingStrategy.RequiresUserSelection
			== true;
	}

	public List<int> GetAbilityValidTargets(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return new List<int>();
		var context = new TargetingContext
		{
			GameState = _state,
			SourceCardId = cardId,
			CastingPlayerId = HumanPlayerId,
		};
		return abilities[abilityIndex]
				.TargetedEffect?.TargetingStrategy.GetValidTargets(context)
				.ToList() ?? new List<int>();
	}

	public bool AbilityHasAdditionalCostSelection(int cardId, int abilityIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return false;
		return abilities[abilityIndex].AdditionalCosts.Any(c => c.RequiresSelection);
	}

	public List<int> GetAbilityAdditionalCostValidPayments(
		int cardId,
		int abilityIndex,
		int costIndex
	)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return new List<int>();
		var costs = abilities[abilityIndex].AdditionalCosts;
		if (costIndex < 0 || costIndex >= costs.Count)
			return new List<int>();
		return costs[costIndex].GetValidPayments(_state, HumanPlayerId, cardId).ToList();
	}

	public string GetAbilityAdditionalCostDescription(int cardId, int abilityIndex, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return "";
		var costs = abilities[abilityIndex].AdditionalCosts;
		return costIndex >= 0 && costIndex < costs.Count ? costs[costIndex].Describe() : "";
	}

	/// <summary>Ability counterpart of <see cref="GetAdditionalCostRequiredPayments"/>.</summary>
	public int GetAbilityAdditionalCostRequiredPayments(int cardId, int abilityIndex, int costIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return 1;
		var costs = abilities[abilityIndex].AdditionalCosts;
		return costIndex >= 0 && costIndex < costs.Count
			? costs[costIndex].RequiredPaymentCount
			: 1;
	}

	public int GetNextAbilityCostNeedingSelection(int cardId, int abilityIndex, int afterIndex)
	{
		var card = _state.GetObject(cardId) as Card;
		var abilities = card?.GetComponents<ActivatedAbilityComponent>().ToList();
		if (abilities == null || abilityIndex >= abilities.Count)
			return -1;
		var costs = abilities[abilityIndex].AdditionalCosts;
		for (int i = afterIndex + 1; i < costs.Count; i++)
			if (costs[i].RequiresSelection)
				return i;
		return -1;
	}

	public (bool Success, ImmutableList<GameEvent> Events) ActivateAbility(
		int cardId,
		int abilityIndex,
		ImmutableList<int> targetIds,
		ImmutableDictionary<int, ImmutableList<int>> additionalCostPayments
	)
	{
		return SubmitAction(
			new ActivateAbilityAction
			{
				CardId = cardId,
				ActivatingPlayerId = HumanPlayerId,
				AbilityIndex = abilityIndex,
				TargetIds = targetIds,
				AdditionalCostPayments = additionalCostPayments,
			}
		);
	}

	public ChoiceAction GetPendingChoice() => _state.GetPendingChoice();

	/// <summary>
	/// True when the pending choice is the HUMAN's to answer.
	///
	/// The scene used to ask "is it the AI's turn?" instead, which is a different question and
	/// gave the wrong answer in both directions: an opponent's trigger firing during your turn
	/// put their discard and their scry in front of you, and your own trigger firing during
	/// their turn was answered silently by the AI.
	///
	/// A choice whose owner is unknown (0) falls back to the active player, so it always has
	/// someone to answer it rather than blocking the stack.
	/// </summary>
	public bool IsHumanChoice
	{
		get
		{
			if (!_state.IsWaitingForChoice)
				return false;

			var decider = _state.GetPendingChoiceDecidingPlayerId();
			return decider == 0 ? !IsAiTurn : decider == HumanPlayerId;
		}
	}

	public ImmutableList<ChoiceOption> GetPendingChoiceOptions()
	{
		if (!_state.IsWaitingForChoice)
			return ImmutableList<ChoiceOption>.Empty;
		if (_state.ActionStack.Peek() is not PipelineAction pipeline)
			return ImmutableList<ChoiceOption>.Empty;
		if (pipeline.CurrentStep is not ChoiceAction choice)
			return ImmutableList<ChoiceOption>.Empty;
		return choice.GetOptions(_state, pipeline.PipelineContext);
	}

	public ImmutableList<GameEvent> ResolveChoice(ImmutableList<int> selectedIds)
	{
		var (newState, events) = _state.ResolveChoice(selectedIds);
		_state = newState;
		_history.Add(new HistoryEntry(_state, "Choice resolved"));
		return events;
	}

	/// <summary>
	/// The human answering their own choice. Separate from <see cref="ResolveChoice"/> because
	/// that one is also the AI's path via <see cref="ApplyAiChoice"/> — draining there would
	/// recurse. Resolving the human's choice can immediately raise an AI-owned one (their trigger
	/// was waiting behind yours), so this drains afterwards.
	/// </summary>
	public ImmutableList<GameEvent> ResolveHumanChoice(ImmutableList<int> selectedIds) =>
		ResolveChoice(selectedIds).AddRange(DrainAiOwnedChoices());

	// ===== AI STEP (compute / apply split for off-main-thread execution) =====
	//
	// Compute* methods are pure: they read the immutable GameState and run the search but do
	// NOT mutate manager state, so the Godot scene can run them on a background thread to keep
	// the UI responsive during a heavy AI turn. Apply* methods mutate state/history and must
	// run on the main thread. LastAiError is written by Compute* and read after the await.

	/// <summary>
	/// Outcome of <see cref="ComputeAiAction"/>: the action to apply, and whether it came from
	/// a real search decision (true) or a forced end-of-turn fallback (false).
	/// </summary>
	public readonly record struct AiActionPlan(GameAction Action, bool IsDecision);

	/// <summary>
	/// Picks the AI's next action (or a forced EndTurn). Pure compute — safe to run on a
	/// background thread. Honors the per-turn action cap and wraps the search in try/catch,
	/// setting <see cref="LastAiError"/> when a guardrail trips. Apply the result with
	/// <see cref="ApplyAiAction"/> on the main thread.
	/// </summary>
	public AiActionPlan ComputeAiAction()
	{
		LastAiError = "";

		var actions = GetLegalActions(AiPlayerId);

		// No legal actions, or the AI has taken an implausible number of actions this turn —
		// end the turn rather than risk an unbounded loop.
		if (actions.Count == 0 || _aiActionsThisTurn >= MaxAiActionsPerTurn)
		{
			if (_aiActionsThisTurn >= MaxAiActionsPerTurn)
				LastAiError =
					$"AI exceeded {MaxAiActionsPerTurn} actions in one turn — forcing end of turn.";
			return new AiActionPlan(BuildAiEndTurnAction(), IsDecision: false);
		}

		try
		{
#pragma warning disable CS0618
			var next = _aiStrategy.SelectAction(_state, _ids, AiPlayerId);
#pragma warning restore CS0618
			return new AiActionPlan(next, IsDecision: true);
		}
		catch (Exception ex)
		{
			// A runaway or failed search must not crash the session. End the AI turn so play
			// can continue, and surface the error for display.
			LastAiError = $"AI move selection failed: {ex.Message}";
			return new AiActionPlan(BuildAiEndTurnAction(), IsDecision: false);
		}
	}

	/// <summary>
	/// Applies a plan from <see cref="ComputeAiAction"/>: records the AI decision, updates the
	/// per-turn action counter, and submits the action. Mutates state — main thread only.
	/// </summary>
	public ImmutableList<GameEvent> ApplyAiAction(AiActionPlan plan)
	{
		if (plan.IsDecision && _aiStrategy.LastDecision != null)
			_aiDecisions.Add((_aiStrategy.LastDecision, _history.Count));

		if (plan.Action is EndTurnAction)
			_aiActionsThisTurn = 0;
		else
			_aiActionsThisTurn++;

		var (_, events) = SubmitAction(plan.Action);
		return events;
	}

	/// <summary>
	/// Picks the AI's selection for the pending choice. Pure compute — safe to run on a
	/// background thread. Apply with <see cref="ApplyAiChoice"/> on the main thread.
	/// </summary>
	public ImmutableList<int> ComputeAiChoice()
	{
		LastAiError = "";
		if (!IsWaitingForChoice)
			return ImmutableList<int>.Empty;

		var choice = _state.GetPendingChoice()!;
		try
		{
			return _aiStrategy.ResolveChoice(_state, choice, AiPlayerId);
		}
		catch (Exception ex)
		{
			// Fall back to the minimum legal selection so the game can proceed.
			LastAiError = $"AI choice resolution failed: {ex.Message}";
			return choice
				.Options.Take(Math.Max(choice.MinChoices, 0))
				.Select(o => o.Id)
				.ToImmutableList();
		}
	}

	/// <summary>
	/// Applies an AI choice selection: resolves the choice and records the decision.
	/// Mutates state — main thread only.
	/// </summary>
	public ImmutableList<GameEvent> ApplyAiChoice(ImmutableList<int> selected)
	{
		if (!IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;
		var historyIndex = _history.Count;
		var events = ResolveChoice(selected);
		if (_aiStrategy.LastDecision != null)
			_aiDecisions.Add((_aiStrategy.LastDecision, historyIndex));
		return events;
	}

	/// <summary>
	/// Hands the turn back to the human after the AI turn failed or stopped progressing.
	/// Clears any pending choice first — an unresolved ChoiceAction blocks the action stack, so
	/// ending the turn on top of one would leave the game permanently stuck.
	/// Best-effort by design: it must not throw, because its callers are error handlers.
	/// </summary>
	public void ForceEndAiTurn()
	{
		try
		{
			while (_state.IsWaitingForChoice)
			{
				var choice = _state.GetPendingChoice()!;
				var minimum = Math.Min(choice.MinChoices, choice.Options.Count);
				var (newState, _) = _state.ResolveChoice(
					choice.Options.Take(minimum).Select(o => o.Id).ToImmutableList()
				);
				_state = newState;
			}

			if (IsAiTurn)
				SubmitAction(BuildAiEndTurnAction());

			_aiActionsThisTurn = 0;
			_history.Add(new HistoryEntry(_state, "Forced end of AI turn"));
		}
		catch (Exception ex)
		{
			LastAiError = $"Forced end of AI turn failed: {ex.Message}";
		}
	}

	private EndTurnAction BuildAiEndTurnAction() =>
		new()
		{
			GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
			Player1Id = HumanPlayerId,
			Player2Id = AiPlayerId,
		};

	// ===== DEBUG / REPLAY =====

	public IReadOnlyList<(int Index, string Description)> GetHistorySummary() =>
		_history.Select((e, i) => (i, e.ActionDescription)).ToList();

	/// <summary>
	/// Restores the game to a previous state in the history and trims everything after it.
	/// </summary>
	public void RewindTo(int historyIndex)
	{
		if (historyIndex < 0 || historyIndex >= _history.Count)
			return;
		_state = _history[historyIndex].State;
		_history.RemoveRange(historyIndex + 1, _history.Count - historyIndex - 1);
		_aiDecisions.RemoveAll(d => d.HistoryIndex > historyIndex);
	}

	/// <summary>
	/// Builds and returns a JSON debug snapshot of the full game history and AI decisions.
	/// </summary>
	/// <param name="error">
	/// Crash context, when this snapshot is a crash dump. The full history is already included,
	/// so a dump carries every state up to the failure, not just the broken one.
	/// </param>
	public string ExportDebugSnapshot(string? error = null)
	{
		var historyForBuilder = _history.Select(e => (e.State, e.ActionDescription)).ToList();
		return DebugSnapshotBuilder.BuildJson(historyForBuilder, _aiDecisions, error);
	}

	// ===== DECK SETUP =====

	private void PopulateDecks(DeckSetupData setup)
	{
		AddCardsToLibrary(
			HumanPlayerId,
			MtgObjectKeys.Player1Library,
			ResolveCards(setup.PlayerDeck, HumanPlayerId)
		);
		AddCardsToLibrary(
			AiPlayerId,
			MtgObjectKeys.Player2Library,
			ResolveCards(setup.OpponentDeck, AiPlayerId)
		);
	}

	private IReadOnlyList<Card> ResolveCards(DeckChoice choice, int ownerId) =>
		choice switch
		{
			DeckChoice.Premade p => DeckRegistry.Build(p.Name, ownerId),
			DeckChoice.RandomPremade => DeckRegistry.Build(
				DeckRegistry.All[_rng.Next(DeckRegistry.All.Count)].Name,
				ownerId
			),
			DeckChoice.Randomized => CardPool.BuildRandomDeck(
				ownerId,
				[.. CardPool.All, .. CardLibrary.All]
			),
			DeckChoice.Drafted d => Draft.BuildDeck(d.Pool, ownerId),
			_ => throw new ArgumentOutOfRangeException(nameof(choice)),
		};

	private void AddCardsToLibrary(int playerId, string libraryKey, IReadOnlyList<Card> cards)
	{
		var libraryId = _state.GetWellKnownId(libraryKey);
		foreach (var card in cards)
		{
			var stamped = card with { OwnerId = playerId, ControllerId = playerId };
			(_state, _) = _state.AddObject(stamped, parentId: libraryId);
		}
	}
}
