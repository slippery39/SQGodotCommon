using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Activates one of a permanent's activated abilities.
///
/// The ability is identified by index into the card's ActivatedAbilityComponents list,
/// allowing a card with multiple abilities to have each one activated independently.
///
/// ValidateAdd checks:
///   - The card exists and is on the battlefield
///   - The controller matches the activating player
///   - The chosen ability index is valid
///   - The ability has not exceeded its MaxActivationsPerTurn for this turn
///   - The player has enough mana
///
/// Execute:
///   - Opens the resolution scope (SuppressPostProcessor = true)
///   - Increments ActivationCount on the ability component
///   - Spends mana
///   - Spawns ResolveEffectAction with the ability's effect
///   - Spawns EndResolutionScopeAction last to close the scope
/// </summary>
public record ActivateAbilityAction : GameAction
{
	public int CardId { get; init; }
	public int ActivatingPlayerId { get; init; }
	public int AbilityIndex { get; init; }

	/// <summary>Targets chosen by the player for the ability's effect, if required.</summary>
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>
	/// Payment selections for selection-based additional costs (sacrifice, discard).
	/// Key = index into ability.AdditionalCosts. Resource costs (life) need no entry here.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> AdditionalCostPayments { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"Card {CardId} does not exist");

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return ValidationResult.Invalid("Source is not a card");

		if (card.ControllerId != ActivatingPlayerId)
			return ValidationResult.Invalid("You do not control this card");

		var zone = gameState.GetCardZone(CardId);
		if (zone.ZoneType != ZoneType.Battlefield)
			return ValidationResult.Invalid("Card is not on the battlefield");

		var creature = card.GetComponent<CreatureComponent>();
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		if (AbilityIndex < 0 || AbilityIndex >= abilities.Count)
			return ValidationResult.Invalid($"Ability index {AbilityIndex} is out of range");

		var ability = abilities[AbilityIndex];

		if (ability.RequiresTap && creature != null)
		{
			var summoningSickness = creature.HasSummoningSickness && !creature.HasHaste;
			if (summoningSickness)
				return ValidationResult.Invalid(
					"This creature has summoning sickness and can't activate this ability"
				);

			if (creature.IsExhausted)
				return ValidationResult.Invalid(
					"This creature is exhausted and can't activate this ability"
				);
		}

		if (
			ability.MaxActivationsPerTurn > 0
			&& ability.ActivationCount >= ability.MaxActivationsPerTurn
		)
			return ValidationResult.Invalid("This ability has already been activated this turn");

		if (
			ability.Condition != null
			&& !ability.Condition.IsSatisfied(gameState, CardId, ActivatingPlayerId)
		)
			return ValidationResult.Invalid(ability.Condition.Describe());

		if (ability.IsLoyaltyAbility)
		{
			var walker = card.GetComponent<PlaneswalkerComponent>();
			if (walker == null)
				return ValidationResult.Invalid("Only a planeswalker has loyalty abilities");

			// One loyalty ability per PLANESWALKER per turn — not per ability. Checking
			// ActivationCount instead would let a walker use its +1 and its -3 on the same turn.
			if (walker.HasActivatedThisTurn)
				return ValidationResult.Invalid(
					"This planeswalker has already used a loyalty ability this turn"
				);

			if (walker.Loyalty + ability.LoyaltyCost < 0)
				return ValidationResult.Invalid(
					$"Not enough loyalty (have {walker.Loyalty}, need {-ability.LoyaltyCost})"
				);
		}

		var player = gameState.GetPlayer(ActivatingPlayerId);
		if (player.CurrentMana < ability.ManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {ability.ManaCost})"
			);

		return ValidateTargetsAndCosts(gameState, ability);
	}

	private ValidationResult ValidateTargetsAndCosts(
		GameState gameState,
		ActivatedAbilityComponent ability
	)
	{
		if (ability.TargetedEffect?.TargetingStrategy.RequiresUserSelection == true)
		{
			var context = new TargetingContext
			{
				GameState = gameState,
				SourceCardId = CardId,
				CastingPlayerId = ActivatingPlayerId,
			};

			// "Up to one target creature" — every planeswalker plus ability. Activating with no
			// target is legal and the effect simply does nothing; the loyalty still changes.
			// Without this a walker on an empty board could not use ANY ability, so it could
			// never build toward its ultimate.
			var optional = ability.IsLoyaltyAbility && TargetIds.IsEmpty;

			if (
				!optional
				&& !ability.TargetedEffect.TargetingStrategy.ValidateTargets(TargetIds, context)
			)
				return ValidationResult.Invalid("Invalid targets for ability");
		}

		for (int i = 0; i < ability.AdditionalCosts.Count; i++)
		{
			var cost = ability.AdditionalCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: [];
			var result = cost.Validate(gameState, ActivatingPlayerId, CardId, paymentIds);
			if (!result.IsValid)
				return result;
		}

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		var ability = abilities[AbilityIndex];

		var state = PayAdditionalCosts(gameState, ability);

		// RE-READ THE CARD. Paying a cost can modify the source card's own components —
		// RemoveCounterAdditionalCost decrements a ChargeCounterComponent on exactly this card —
		// and everything below rebuilds the component array from `card`. Using the pre-payment
		// snapshot writes the old array straight back over the payment, so the cost validates,
		// appears to be paid, and then silently is not. Dragon's Hoard would draw a card per
		// activation forever off a single gold counter.
		//
		// Latent until now only because every existing AdditionalCost touches something else:
		// sacrifice and discard move OTHER cards, life changes the player.
		if (state.HasObject(CardId) && state.GetObject(CardId) is Card paidCard)
			card = paidCard;

		// Open the resolution scope — SBE and trigger evaluation deferred until
		// EndResolutionScopeAction clears this flag.
		state = state with
		{
			SuppressPostProcessor = true,
		};

		// Increment activation count for this turn
		var updatedComponents = card.Components;
		var abilityCount = 0;
		for (int i = 0; i < card.Components.Length; i++)
		{
			if (card.Components[i] is ActivatedAbilityComponent ab)
			{
				if (abilityCount == AbilityIndex)
				{
					updatedComponents = updatedComponents.SetItem(
						i,
						ab with
						{
							ActivationCount = ab.ActivationCount + 1,
						}
					);
				}
				abilityCount++;
			}
		}
		// A tap cost exhausts the creature, which is the whole point of RequiresTap — before
		// IsExhausted existed the field only blocked activation under summoning sickness and
		// the ability was effectively free to repeat.
		if (ability.RequiresTap)
		{
			for (int i = 0; i < updatedComponents.Length; i++)
				if (updatedComponents[i] is CreatureComponent cc && !cc.IsExhausted)
				{
					updatedComponents = updatedComponents.SetItem(
						i,
						cc with
						{
							IsExhausted = true,
						}
					);
					state = state with
					{
						PendingGameEvents = state.PendingGameEvents.Add(
							new CreatureExhaustedEvent { CreatureId = CardId }
						),
					};
				}
		}

		// Pay the loyalty cost and spend the walker's one activation for the turn. Done here
		// rather than as an effect so the cost is paid even if the effect fizzles.
		if (ability.IsLoyaltyAbility)
			for (int i = 0; i < updatedComponents.Length; i++)
				if (updatedComponents[i] is PlaneswalkerComponent pw)
					updatedComponents = updatedComponents.SetItem(
						i,
						pw with
						{
							Loyalty = pw.Loyalty + ability.LoyaltyCost,
							HasActivatedThisTurn = true,
						}
					);

		state = state.UpdateObject(CardId, card with { Components = updatedComponents });

		// Spend mana
		var player = state.GetPlayer(ActivatingPlayerId);
		state = state.UpdateObject(
			ActivatingPlayerId,
			player with
			{
				CurrentMana = player.CurrentMana - ability.ManaCost,
			}
		);

		// Every effect that needs a target gets the SAME chosen one. Basri Ket's "+1: put a
		// +1/+1 counter on up to one target creature. It gains indestructible" is one target and
		// two effects; mapping only index 0 left the second effect with an empty target list, so
		// the indestructible half silently did nothing.
		var targetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		if (!TargetIds.IsEmpty)
			for (int i = 0; i < ability.Effects.Count; i++)
				if (ability.Effects[i].TargetingStrategy.RequiresUserSelection)
					targetIds = targetIds.Add(i, TargetIds);

		return new ActionResult(
			state.SpawnActions(
				[
					new ResolveEffectAction
					{
						Effects = ability.Effects,
						CastingPlayerId = ActivatingPlayerId,
						SourceCardId = CardId,
						TargetIds = targetIds,
					},
					new EndResolutionScopeAction(),
				]
			)
		);
	}

	private GameState PayAdditionalCosts(GameState state, ActivatedAbilityComponent ability)
	{
		for (int i = 0; i < ability.AdditionalCosts.Count; i++)
		{
			var cost = ability.AdditionalCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: [];
			state = cost.Pay(state, ActivatingPlayerId, CardId, paymentIds);
		}
		return state;
	}
}
