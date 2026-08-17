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

		// Any permanent, not just creatures. Filtering on CreatureComponent alone silently
		// dropped planeswalkers, artifacts and enchantments — a reanimation effect aimed at one
		// simply did nothing, with no error.
		//
		// CreatureComponent is accepted on its own as well as PermanentComponent. Production
		// cards always carry both, but a creature is a permanent by definition, and requiring
		// the marker here would reject the many hand-built test creatures that omit it.
		var cards = targets
			.Where(id => state.HasObject(id))
			.Select(id => state.GetObject(id) as Card)
			.Where(card =>
				card?.HasComponent<PermanentComponent>() == true
				|| card?.HasComponent<CreatureComponent>() == true
			)
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
		// A planeswalker enters at its starting loyalty and with its activation available, so a
		// walker that died and was reanimated comes back whole rather than at 0.
		if (card.HasComponent<PlaneswalkerComponent>())
		{
			state = state.StampPlaneswalkerEntry(card.Id);

			var walkerEntered = new PermanentEnteredBattlefieldEvent
			{
				CardId = card.Id,
				PlayerId = card.ControllerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(walkerEntered) };
			return (state, ImmutableList.Create<GameEvent>(walkerEntered));
		}

		// Clone resolves BEFORE summoning sickness is stamped, so the copy is treated as a fresh
		// creature rather than inheriting the original's attack state.
		if (card.HasComponent<CopyOnEnterComponent>())
		{
			state = state.ApplyCopyOnEnter(card.Id);
			card = (Card)state.GetObject(card.Id);
		}

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
