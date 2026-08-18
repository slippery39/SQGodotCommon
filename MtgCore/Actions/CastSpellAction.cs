using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a spell from a player's hand.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the casting player, has a SpellComponent,
/// has valid targets, and that the player has enough mana.
///
/// Execute moves the card to the stack, spends mana, and spawns ResolveSpellAction.
/// </summary>
public record CastSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	/// <summary>
	/// Payment selections for selection-based additional costs (sacrifice, discard).
	/// Key = index into card.AdditionalCastCosts. Resource costs (life) need no entry here.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> AdditionalCostPayments { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	/// <summary>
	/// The X chosen for an {X} spell. Ignored on a card with no XCostComponent.
	/// Carried on the action rather than the card so two copies of the same card can be cast
	/// for different X.
	/// </summary>
	public int XValue { get; init; } = 0;

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"Card {CardId} does not exist");

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return ValidationResult.Invalid($"Object {CardId} is not a card");

		if (card.ControllerId != CastingPlayerId)
			return ValidationResult.Invalid("You do not control this card");

		if (!gameState.IsInCastableZone(CardId, CastingPlayerId))
			return ValidationResult.Invalid("Card is not in your hand");

		var spellComponent = card.GetComponent<SpellComponent>();
		if (spellComponent == null)
			return ValidationResult.Invalid("Card is not a spell");

		// A counter trap is never cast — it fires from hand on its own. Without this it would be
		// castable for full price and do nothing at all, and the AI would happily do that.
		if (card.HasComponent<CounterTrapComponent>())
			return ValidationResult.Invalid(
				"A counterspell trap cannot be cast; it fires from hand"
			);

		foreach (var restriction in card.GetComponents<CastRestrictionComponent>())
			if (!restriction.CanCast(gameState, CastingPlayerId))
				return ValidationResult.Invalid(restriction.Describe());

		var player = gameState.GetPlayer(CastingPlayerId);
		var effectiveCost = ComputeEffectiveCost(gameState, card, CastingPlayerId);
		if (player.CurrentMana < effectiveCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {effectiveCost})"
			);

		var costsResult = ValidateAdditionalCosts(gameState, card);
		if (!costsResult.IsValid)
			return costsResult;

		return ValidateTargets(gameState, spellComponent);
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var state = PayAdditionalCosts(gameState, card);

		var player = state.GetPlayer(CastingPlayerId);

		// Convoke's reduction is measured before mana is spent, so the same number of creatures
		// that paid are the ones exhausted below.
		var convokeUsed = card.HasComponent<ConvokeComponent>()
			? Math.Min(state.CountConvokers(CastingPlayerId), card.ManaCost + XValue)
			: 0;

		state = state.UpdateObject(
			CastingPlayerId,
			player with
			{
				CurrentMana =
					player.CurrentMana - ComputeEffectiveCost(state, card, CastingPlayerId),
			}
		);

		state = ExhaustConvokers(state, card, CastingPlayerId, convokeUsed);
		state = state.MoveObject(CardId, state.GetStackId());

		var game = state.TryGetGame();
		if (game != null)
			state = state.UpdateObject(
				game.Id,
				game with
				{
					SpellsCastThisTurn = game.SpellsCastThisTurn + 1,
				}
			);

		var castEvent = new SpellCastEvent { CardId = CardId, CastingPlayerId = CastingPlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(castEvent) };

		// Counter traps fire AFTER the cast is counted and announced — a countered spell was
		// still cast, so prowess and storm see it. If one fires, the card has already been moved
		// and no resolve action must be spawned.
		var counter = CounterTrapEngine.TryCounterCast(state, CardId, CastingPlayerId);
		state = counter.State;
		if (counter.Countered)
			return new ActionResult(state).WithEvent(castEvent);

		return new ActionResult(
			state.SpawnAction(
				new ResolveSpellAction
				{
					CardId = CardId,
					CastingPlayerId = CastingPlayerId,
					TargetIds = TargetIds,
					XValue = XValue,
				}
			)
		).WithEvent(castEvent);
	}

	private ValidationResult ValidateAdditionalCosts(GameState gameState, Card card)
	{
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: ImmutableList<int>.Empty;
			var result = cost.Validate(gameState, CastingPlayerId, CardId, paymentIds);
			if (!result.IsValid)
				return result;
		}
		return ValidationResult.Valid;
	}

	private ValidationResult ValidateTargets(GameState gameState, SpellComponent spellComponent)
	{
		var context = new TargetingContext
		{
			GameState = gameState,
			SourceCardId = CardId,
			CastingPlayerId = CastingPlayerId,
		};

		for (int i = 0; i < spellComponent.Effects.Count; i++)
		{
			var effect = spellComponent.Effects[i];
			if (!effect.TargetingStrategy.RequiresUserSelection)
				continue;

			if (!TargetIds.TryGetValue(i, out var targets))
				return ValidationResult.Invalid($"No targets provided for effect {i}");

			if (!effect.TargetingStrategy.ValidateTargets(targets, context))
				return ValidationResult.Invalid($"Invalid targets for effect {i}");
		}
		return ValidationResult.Valid;
	}

	private GameState PayAdditionalCosts(GameState state, Card card)
	{
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: ImmutableList<int>.Empty;
			state = cost.Pay(state, CastingPlayerId, CardId, paymentIds);
		}
		return state;
	}

	private int ComputeEffectiveCost(GameState state, Card card, int playerId) =>
		state.ComputeEffectiveCost(card, playerId, XValue);

	/// <summary>
	/// Exhausts the creatures that helped cast a convoke spell — the "tap" half of convoke.
	/// Exactly as many as the cost reduction actually used, so a spell whose cost was already
	/// 0 taps nobody.
	/// </summary>
	private static GameState ExhaustConvokers(GameState state, Card card, int playerId, int used)
	{
		if (used <= 0 || !card.HasComponent<ConvokeComponent>())
			return state;

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var remaining = used;

		foreach (var permanent in state.GetCardsInZone(battlefieldId).ToList())
		{
			if (remaining == 0)
				break;
			if (permanent.ControllerId != playerId)
				continue;

			var creature = permanent.GetComponent<CreatureComponent>();
			if (creature == null || creature.IsExhausted || creature.HasAttacked)
				continue;

			state = state.UpdateObject(
				permanent.Id,
				permanent.WithComponentReplaced(creature with { IsExhausted = true })
			);
			state = state with
			{
				PendingGameEvents = state.PendingGameEvents.Add(
					new CreatureExhaustedEvent { CreatureId = permanent.Id }
				),
			};
			remaining--;
		}

		return state;
	}
}
