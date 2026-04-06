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
///   - Marks the ability as HasActivated = true
///   - Spends mana
///   - Resolves targets and spawns the effect action (same as ResolveSpellAction)
/// </summary>
public record ActivateAbilityAction : GameAction
{
	public int CardId { get; init; }
	public int ActivatingPlayerId { get; init; }
	public int AbilityIndex { get; init; }

	/// <summary>
	/// Targets chosen by the player for the ability's effect, if required.
	/// </summary>
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

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

		// Validate targets if required
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

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
		var ability = abilities[AbilityIndex];

		// Mark ability as used this turn
		var updatedAbilities = card.Components.OfType<ActivatedAbilityComponent>().ToList();
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
		var updatedCard = card with { Components = updatedComponents };
		var state = gameState.UpdateObject(CardId, updatedCard);

		// Spend mana
		var player = state.GetPlayer(ActivatingPlayerId);
		var updatedPlayer = player with { CurrentMana = player.CurrentMana - ability.ManaCost };
		state = state.UpdateObject(ActivatingPlayerId, updatedPlayer);

		// Resolve targets and spawn the effect action
		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = CardId,
			CastingPlayerId = ActivatingPlayerId,
		};

		var resolvedTargets = ability.Effect.TargetingStrategy.SelectionMode switch
		{
			TargetSelectionMode.UserSelect => TargetIds,
			TargetSelectionMode.AllValid => ability.Effect.TargetingStrategy.GetValidTargets(
				context
			),
			TargetSelectionMode.CastingPlayer => ImmutableList.Create(ActivatingPlayerId),
			TargetSelectionMode.None => ImmutableList<int>.Empty,
			_ => ImmutableList<int>.Empty,
		};

		GameAction effectAction = ability.Effect.ActionTemplate is ITargetedAction targeted
			? targeted.WithTargets(resolvedTargets)
			: ability.Effect.ActionTemplate;

		// Seed CastingPlayerId into InputContext for actions that use PlayerIdContextKey
		effectAction = effectAction is PipelineAction pipeline
			? pipeline with
			{
				PipelineContext = pipeline.PipelineContext.SetItem(
					ContextKeys.CastingPlayerId,
					ActivatingPlayerId
				),
			}
			: effectAction with
			{
				InputContext = effectAction.InputContext.SetItem(
					ContextKeys.CastingPlayerId,
					ActivatingPlayerId
				),
			};

		return new ActionResult(state.SpawnAction(effectAction));
	}
}
