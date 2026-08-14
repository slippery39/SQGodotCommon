using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Places creature cards onto the battlefield and fires the ETB ceremony for both
/// existing cards (moved from another zone) and newly created cards (from a CardTemplate).
///
/// Existing cards: populate TargetIds / CardIdContextKey.
/// Used by Goblin Lackey, Warren Instigator, Dragonstorm, and similar effects.
///
/// New cards: populate CardTemplate (ControllerId must be set on the template).
/// Used by CreateCardAction when spawning tokens or other created permanents.
///
/// Both paths share ApplyEtbCeremony: stamps HasSummoningSickness and emits
/// CreatureEnteredBattlefieldEvent so ETB triggers fire uniformly.
///
/// CardIdContextKey: when set, reads a single card ID from pipeline context and
/// includes it in the targets. Ignored if the context value is 0.
/// </summary>
public record PutIntoBattlefieldAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;
	public string CardIdContextKey { get; init; } = "";
	public Card? CardTemplate { get; init; } = null;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		if (CardTemplate != null)
		{
			var battlefieldId = state.GetPlayerZoneId(
				CardTemplate.ControllerId,
				ZoneType.Battlefield
			);
			(state, var addedCard) = state.AddObject(CardTemplate, battlefieldId);
			var (newState, etbEvents) = ApplyEtbCeremony(state, addedCard);
			return new ActionResult(newState) { Events = etbEvents };
		}

		var targets = TargetIds;
		if (!string.IsNullOrEmpty(CardIdContextKey))
		{
			var contextId = GetInput<int>(CardIdContextKey, 0);
			if (contextId != 0)
				targets = targets.Add(contextId);
		}

		var cards = targets
			.Where(id => state.HasObject(id))
			.Select(id => state.GetObject(id) as Card)
			.Where(card => card?.HasComponent<CreatureComponent>() == true)
			.ToList();

		foreach (var card in cards)
		{
			var battlefieldId = state.GetPlayerZoneId(card!.ControllerId, ZoneType.Battlefield);
			state = state.MoveCardTracked(card.Id, battlefieldId);
			var (newState, etbEvents) = ApplyEtbCeremony(state, card);
			state = newState;
			events = events.AddRange(etbEvents);
		}

		return new ActionResult(state) { Events = events };
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyEtbCeremony(
		GameState state,
		Card card
	)
	{
		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return (state, ImmutableList<GameEvent>.Empty);

		var stamped = card.WithComponentReplaced(
			creature with
			{
				HasSummoningSickness = !creature.HasHaste,
			}
		);
		state = state.UpdateObject(card.Id, stamped);

		var enteredEvent = new CreatureEnteredBattlefieldEvent
		{
			CardId = card.Id,
			PlayerId = card.ControllerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };
		return (state, ImmutableList.Create<GameEvent>(enteredEvent));
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		if (CardTemplate != null)
			return ValidationResult.Valid;

		var missingId = TargetIds.FirstOrDefault(id => !gameState.HasObject(id));
		return missingId != 0
			? ValidationResult.Invalid($"Target {missingId} no longer exists")
			: ValidationResult.Valid;
	}
}
