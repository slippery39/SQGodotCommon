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

		// The X paid to cast this creature, for EntersWithCountersComponent.FromXValue. Injected by
		// ResolveCreatureAction; 0 for every other route onto the battlefield, which is correct —
		// a reanimated Hydra was not cast and has no X.
		var xValue = GetInput<int>(ContextKeys.XValue, 0);

		if (CardTemplate != null)
		{
			var battlefieldId = state.GetPlayerZoneId(
				CardTemplate.ControllerId,
				ZoneType.Battlefield
			);
			(state, var addedCard) = state.AddObject(CardTemplate, battlefieldId);
			var (newState, etbEvents) = ApplyEtbCeremony(state, addedCard, xValue);
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

		// Reanimation says "under YOUR control", and a card in a graveyard still carries the
		// ControllerId it had in play. Without this, raiding an opponent's graveyard handed the
		// creature straight back to them — worse than doing nothing, and silent. Affects
		// Necromantic Summons, Endless Obedience and Liliana Vess's ultimate.
		//
		// Deliberately scoped to cards coming FROM A GRAVEYARD. This action is also the single
		// entry point for cast creatures and freshly created tokens, and for those the template's
		// ControllerId is already authoritative — a token deliberately created under another
		// player's control must not be silently reassigned to the caster.
		var newControllerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);

		foreach (var card in cards)
		{
			var fromGraveyard = state.GetCardZone(card!.Id) is { ZoneType: ZoneType.Graveyard };

			var controlled =
				fromGraveyard && newControllerId != 0 && card.ControllerId != newControllerId
					? card with
					{
						ControllerId = newControllerId,
					}
					: card;

			if (!ReferenceEquals(controlled, card))
				state = state.UpdateObject(controlled.Id, controlled);

			var battlefieldId = state.GetPlayerZoneId(
				controlled.ControllerId,
				ZoneType.Battlefield
			);
			state = state.MoveCardTracked(controlled.Id, battlefieldId);
			var (newState, etbEvents) = ApplyEtbCeremony(state, controlled, xValue);
			state = newState;
			events = events.AddRange(etbEvents);
		}

		return new ActionResult(state) { Events = events };
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyEtbCeremony(
		GameState state,
		Card card,
		int xValue
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

		var stamped = (Card)
			card.WithComponentReplaced(creature with { HasSummoningSickness = !creature.HasHaste });

		stamped = ApplyEntryCounters(stamped, xValue);
		state = state.UpdateObject(card.Id, stamped);

		var enteredEvent = new CreatureEnteredBattlefieldEvent
		{
			CardId = card.Id,
			PlayerId = card.ControllerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };
		return (state, ImmutableList.Create<GameEvent>(enteredEvent));
	}

	/// <summary>
	/// Clears any +1/+1 counters the card arrived with, then applies its
	/// EntersWithCountersComponent if it has one.
	///
	/// THE STRIP LIVES HERE, ON ENTRY, RATHER THAN IN MoveCardTracked ON EXIT — and the choice is
	/// load-bearing. Real MTG says two things that pull in opposite directions: counters cease to
	/// exist when a permanent changes zones, but a leaves-the-battlefield trigger uses the
	/// permanent's LAST KNOWN information. Stripping on exit honours the first and breaks the
	/// second, which is exactly what happens to Chasm Skulker today — MoveCardTracked removes its
	/// StaticPowerToughnessModifier counters before CheckStateBasedEffectsAction resolves its own
	/// death trigger, so it reads power 1 and makes zero tokens.
	///
	/// Stripping on entry honours both: nothing can come back onto the battlefield carrying old
	/// counters, and a creature sitting in the graveyard still knows how many it had when it died.
	/// The visible cost is that a dead Hydra prints its counters in the graveyard text box, which
	/// is true — it did have them.
	///
	/// Consequently PlusOneCounterComponent is deliberately ABSENT from MoveCardTracked's closed
	/// strip list in ZoneTransitionExtensions. Do not add it there.
	/// </summary>
	private static Card ApplyEntryCounters(Card card, int xValue)
	{
		var entersWith = card.GetComponent<EntersWithCountersComponent>();
		var existing = card.GetComponent<PlusOneCounterComponent>();

		if (entersWith == null && existing == null)
			return card;

		var components = card.Components;
		if (existing != null)
			components = components.Remove(existing);

		if (entersWith == null)
			return card with { Components = components };

		var count = entersWith.FromXValue ? xValue : entersWith.Count;
		if (count <= 0)
			return card with { Components = components };

		return card with
		{
			Components = components.Add(new PlusOneCounterComponent { Count = count }),
		};
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
