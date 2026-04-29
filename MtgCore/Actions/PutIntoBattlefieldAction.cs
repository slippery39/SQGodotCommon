using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Places one or more creature cards directly onto the battlefield without paying mana.
/// Used by Goblin Lackey, Warren Instigator, and similar "put into play" effects.
///
/// Unlike PlayCreatureAction, this bypasses mana cost validation and does not emit
/// CreaturePlayedEvent. It stamps HasSummoningSickness and emits CreatureEnteredBattlefieldEvent
/// so ETB triggers fire the same way as for normally cast creatures.
///
/// CardIdContextKey: when set, reads a single card ID from pipeline context and includes
/// it in the targets. Used by storm pipelines (Dragonstorm) where SelectCardFromLibraryAction
/// writes the chosen card ID and this action deploys it. Ignored if the context value is 0.
/// </summary>
public record PutIntoBattlefieldAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;
	public string CardIdContextKey { get; init; } = "";

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var targets = TargetIds;
		if (!string.IsNullOrEmpty(CardIdContextKey))
		{
			var contextId = GetInput<int>(CardIdContextKey, 0);
			if (contextId != 0)
				targets = targets.Add(contextId);
		}

		var creatures = targets
			.Where(id => state.HasObject(id))
			.Select(id => state.GetObject(id) as Card)
			.Where(card => card?.HasComponent<CreatureComponent>() == true)
			.ToList();

		foreach (var card in creatures)
		{
			var battlefieldId = state.GetPlayerZoneId(card!.ControllerId, ZoneType.Battlefield);

			var creature = card.GetComponent<CreatureComponent>()!;
			var updatedCard = card.WithComponentReplaced(
				creature with
				{
					HasSummoningSickness = !creature.HasHaste,
				}
			);
			state = state.UpdateObject(card.Id, updatedCard).MoveObject(card.Id, battlefieldId);

			var enteredEvent = new CreatureEnteredBattlefieldEvent
			{
				CardId = card.Id,
				PlayerId = card.ControllerId,
			};
			events = events.Add(enteredEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		var missingId = TargetIds.FirstOrDefault(id => !gameState.HasObject(id));
		return missingId != 0
			? ValidationResult.Invalid($"Target {missingId} no longer exists")
			: ValidationResult.Valid;
	}
}
