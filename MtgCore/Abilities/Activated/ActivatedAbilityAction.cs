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
///   - The ability has not already been activated this turn
///   - The player has enough mana
///
/// Execute:
///   - Opens the resolution scope (SuppressPostProcessor = true)
///   - Marks the ability as HasActivated = true
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

		var summoningSickness =
			card.GetComponent<CreatureComponent>()?.HasSummoningSickness ?? false;
		if (summoningSickness)
			return ValidationResult.Invalid(
				"This creature has summoning sickness and can't activate abilities"
			);

		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		if (AbilityIndex < 0 || AbilityIndex >= abilities.Count)
			return ValidationResult.Invalid($"Ability index {AbilityIndex} is out of range");

		var ability = abilities[AbilityIndex];

		if (ability.HasActivated)
			return ValidationResult.Invalid("This ability has already been activated this turn");

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
		if (ability.Effect.TargetingStrategy.RequiresUserSelection)
		{
			var context = new TargetingContext
			{
				GameState = gameState,
				SourceCardId = CardId,
				CastingPlayerId = ActivatingPlayerId,
			};
			if (!ability.Effect.TargetingStrategy.ValidateTargets(TargetIds, context))
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

		// Open the resolution scope — SBE and trigger evaluation deferred until
		// EndResolutionScopeAction clears this flag.
		state = state with
		{
			SuppressPostProcessor = true,
		};

		// Mark ability as used this turn
		var updatedComponents = card.Components;
		var abilityCount = 0;
		for (int i = 0; i < card.Components.Count; i++)
		{
			if (card.Components[i] is ActivatedAbilityComponent ab)
			{
				if (abilityCount == AbilityIndex)
				{
					updatedComponents = updatedComponents.SetItem(
						i,
						ab with
						{
							HasActivated = true,
						}
					);
				}
				abilityCount++;
			}
		}
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

		// Build target map for ResolveEffectAction (single effect at index 0)
		var targetIds = TargetIds.IsEmpty
			? ImmutableDictionary<int, ImmutableList<int>>.Empty
			: ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(0, TargetIds);

		return new ActionResult(
			state.SpawnActions(
				[
					new ResolveEffectAction
					{
						Effects = ImmutableList.Create(ability.Effect),
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
