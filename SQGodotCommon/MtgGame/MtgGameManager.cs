using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgGame;

/// <summary>
/// Owns the GameState and drives the game loop.
/// Plain C# class — no Godot dependencies. The Godot scene owns one instance.
/// </summary>
public class MtgGameManager
{
	private GameState _state;
	private readonly Random _rng = new();
#pragma warning disable CS0618
	private readonly MtgGameIds _ids;
	private readonly BeamSearchAiStrategy _aiStrategy;
#pragma warning restore CS0618

	public int HumanPlayerId { get; private set; }
	public int AiPlayerId { get; private set; }

	public GameState State => _state;
	public bool IsAiTurn => _state.TryGetGame()?.ActivePlayerId == AiPlayerId;
	public bool IsWaitingForChoice => _state.IsWaitingForChoice;

	public MtgGameManager()
	{
#pragma warning disable CS0618
		(_state, _ids) = MtgGameFactory.Create();
		_aiStrategy = new BeamSearchAiStrategy(_ids, maxDepth: 3);
#pragma warning restore CS0618
		HumanPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player1);
		AiPlayerId = _state.GetWellKnownId(MtgObjectKeys.Player2);
		PopulateDecks();
	}

	public ImmutableList<GameEvent> StartGame()
	{
		(_state, var events) = _state.BeginGame(
			_state.GetWellKnownId(MtgObjectKeys.Game),
			HumanPlayerId,
			AiPlayerId
		);
		return events;
	}

	public (bool Success, ImmutableList<GameEvent> Events) SubmitAction(GameAction action)
	{
		var (newState, success) = _state.TryAddAction(action);
		if (!success)
			return (false, ImmutableList<GameEvent>.Empty);
		var (finalState, events) = newState.ProcessAllActions();
		_state = finalState;
		return (true, events);
	}

	public List<GameAction> GetLegalActions(int playerId)
	{
		return MtgActionGenerator.GetLegalActions(_state, playerId);
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
		return SubmitAction(
			new CastCreatureAction { CardId = cardId, CastingPlayerId = HumanPlayerId }
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
			}
		);
	}

	public bool IsSpell(int cardId)
	{
		var card = _state.GetObject(cardId) as Card;
		return card?.GetComponent<SpellComponent>() != null;
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
		var legalActions = MtgActionGenerator.GetLegalActions(_state, HumanPlayerId);
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
		return abilities[abilityIndex].Effect.TargetingStrategy.RequiresUserSelection;
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
		return abilities[abilityIndex].Effect.TargetingStrategy.GetValidTargets(context).ToList();
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
		return events;
	}

	public ImmutableList<GameEvent> ResolveAiChoice()
	{
		if (!IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;
		var options = GetPendingChoiceOptions();
		var choice = _state.GetPendingChoice()!;
		var count = Math.Min(choice.MinChoices, options.Count);
		var selected = options
			.OrderBy(_ => _rng.Next())
			.Take(count)
			.Select(o => o.Id)
			.ToImmutableList();
		return ResolveChoice(selected);
	}

	/// <summary>
	/// Executes one AI action using BeamSearch, or ends the turn if no legal actions remain.
	/// Call repeatedly with a visual delay between calls until IsAiTurn is false.
	/// </summary>
	public ImmutableList<GameEvent> RunAiTurnStep()
	{
		if (!IsAiTurn || IsWaitingForChoice)
			return ImmutableList<GameEvent>.Empty;

		var actions = GetLegalActions(AiPlayerId);
		GameAction next;
		if (actions.Count == 0)
		{
			next = new EndTurnAction
			{
				GameId = _state.GetWellKnownId(MtgObjectKeys.Game),
				Player1Id = HumanPlayerId,
				Player2Id = AiPlayerId,
			};
		}
		else
		{
#pragma warning disable CS0618
			next = _aiStrategy.SelectAction(_state, _ids, AiPlayerId);
#pragma warning restore CS0618
		}

		var (_, events) = SubmitAction(next);
		return events;
	}

	// ===== DECK SETUP =====

	private void PopulateDecks()
	{
		AddCardsToLibrary(HumanPlayerId, MtgObjectKeys.Player1Library, HumanDeck);
		AddCardsToLibrary(AiPlayerId, MtgObjectKeys.Player2Library, AiDeck);
	}

	private void AddCardsToLibrary(int playerId, string libraryKey, Func<Card>[] factories)
	{
		var libraryId = _state.GetWellKnownId(libraryKey);
		foreach (var factory in factories)
		{
			var card = factory() with { OwnerId = playerId, ControllerId = playerId };
			(_state, _) = _state.AddObject(card, parentId: libraryId);
		}
	}

	// Zoo aggro + tricks: lots of 1-drops with abilities, a few finishers, and interactive spells
	private static readonly Func<Card>[] HumanDeck =
	[
		// 1-drops
		CardLibrary.GoblinGuide, // 2/2 haste
		CardLibrary.WildNacatl, // 2/2
		CardLibrary.KirdApe, // 2/3
		CardLibrary.LoamLion, // 2/3
		CardLibrary.SavannahLions, // 2/1
		CardLibrary.LlanowarElves, // 1/1 mana ramp
		CardLibrary.RagingGoblin, // 1/1 haste
		// 2-drops
		CardLibrary.GrizzlyBears, // 2/2
		CardLibrary.KalonianTusker, // 3/3
		CardLibrary.Tarmogoyf, // */1+*
		CardLibrary.DarkConfidant, // 1/4, upkeep draw+life
		CardLibrary.QasaliPridemage, // 2/2, destroy activated
		// 3-drops
		CardLibrary.WallOfThorns, // 2/5 taunt
		CardLibrary.GeistOfSaintTraft, // 2/2, attack creates 4/4 flying token
		CardLibrary.HillGiant, // 3/4
		CardLibrary.ProdigalSorcerer, // 1/1, ping activated
		// Finishers
		CardLibrary.MahamotiDjinn, // 6/7 flying
		CardLibrary.CrawWurm, // 8/4
		// Spells
		CardLibrary.LightningBolt, // deal 3 to any target
		CardLibrary.LightningBolt,
		CardLibrary.DoomBlade, // destroy target creature
		CardLibrary.GiantGrowth, // +3/+3
		CardLibrary.WrathOfGod, // destroy all creatures
		CardLibrary.CarefulStudy, // draw 2, discard 2
		CardLibrary.TellingTime, // look at top 3, arrange
	];

	// Goblins tribal: fast aggro with haste creatures, token makers, and tribal synergies
	private static readonly Func<Card>[] AiDeck =
	[
		// 1-drops
		CardLibrary.GoblinGuide, // 2/2 haste
		CardLibrary.GoblinGuide,
		CardLibrary.RagingGoblin, // 1/1 haste
		CardLibrary.RagingGoblin,
		CardLibrary.GoblinLackey, // 1/1, deals damage → put goblin from hand into play
		CardLibrary.GoblinLackey,
		// 2-drops
		CardLibrary.WarrenInstigator, // 1/1 double strike, same trigger as Lackey
		// 3-drops
		CardLibrary.GoblinChieftain, // 2/2 haste, +1/+1 and haste to other goblins
		CardLibrary.GoblinChieftain,
		CardLibrary.WallOfThorns, // 2/5 taunt
		CardLibrary.HillGiant, // 3/4
		// 4-drops
		CardLibrary.KrenkoMobBoss, // 3/3, creates X goblin tokens
		// 5-drops
		CardLibrary.SiegeGangCommander, // 2/2, ETB creates 3 goblin tokens, sac ability
		CardLibrary.SiegeGangCommander,
		// Finisher
		CardLibrary.MahamotiDjinn, // 6/7 flying
		// Spells
		CardLibrary.LightningBolt,
		CardLibrary.LightningBolt,
		CardLibrary.DoomBlade,
		CardLibrary.WrathOfGod,
		CardLibrary.GoblinGrenade, // 1 mana, sac a goblin, deal 5 to any target
		CardLibrary.GoblinGrenade,
	];
}
