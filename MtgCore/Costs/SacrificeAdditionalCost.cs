using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Additional cost: sacrifice one or more permanents you control.
/// Requires player selection — GetValidPayments returns IDs of sacrificeable permanents.
///
/// Filter restricts which permanents qualify (e.g. IsSubtypeSpecification("Goblin")).
/// Null filter means any permanent you control on the battlefield is valid.
///
/// Sacrifice moves the permanent directly to its owner's graveyard.
/// This is not "destroy" — indestructible does not prevent it.
///
/// GetValidPayments returns WORST FIRST, and that ordering is load-bearing rather than cosmetic.
/// MtgActionGenerator.BuildAdditionalCostPayments offers the AI exactly ONE payment per selection
/// cost — `validPayments.Take(needed)` — so whatever sits at index 0 is the only sacrifice the
/// search ever gets to consider. In zone order that is the permanent played EARLIEST, which on a
/// developed board is usually the best one, so every sacrifice outlet was priced to the AI as
/// "give up your biggest creature".
///
/// StateEvaluator then correctly refuses: losing a creature costs CreatureCountWeight (3.0) plus
/// 2.0 per power plus the race-pressure term, which nothing a sacrifice outlet buys can repay.
/// The card therefore never activated at all and scored as a blank — Barrage of Expendables at
/// 39.4%, Blood for Bones and Evolutionary Leap in the same band. That looks like a costing
/// problem in a win-rate table and is not one; see BottomOfModelCardTests for why the ~40% band
/// means "inert", not "weak".
///
/// Sorting worst-first makes the one offered payment the one a player would actually pick. It is
/// deliberately a sort rather than enumerating every candidate as its own action: the AI's
/// branching is capped at 16 (see MtgSimulator/CLAUDE.md) and a wide board would spend the whole
/// cap on which token to sacrifice.
/// </summary>
public record SacrificeAdditionalCost : AdditionalCost
{
	public TargetSpecification? Filter { get; init; }
	public int Count { get; init; } = 1;

	public override bool RequiresSelection => true;

	public override int RequiredPaymentCount => Count;

	public override string Describe() =>
		Count == 1
			? "Sacrifice a permanent you control"
			: $"Sacrifice {Count} permanents you control";

	public override ImmutableList<int> GetValidPayments(
		GameState state,
		int castingPlayerId,
		int sourceCardId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Battlefield);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		// Worst first — see the note on this type. Power then toughness then id, the exact
		// inverse of CreatureEvaluator.PickStrongest, so "best" and "worst" cannot drift apart.
		// The id tiebreak keeps this deterministic, which the training runs depend on.
		return state
			.GetCardsInZone(battlefieldId)
			.Where(c => Filter == null || Filter.IsSatisfiedBy(c.Id, context))
			.OrderBy(c => state.GetEffectivePower(c.Id))
			.ThenBy(c => state.GetEffectiveToughness(c.Id))
			.ThenBy(c => c.Id)
			.Select(c => c.Id)
			.ToImmutableList();
	}

	public override ValidationResult Validate(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		if (paymentIds.Count != Count)
			return ValidationResult.Invalid($"Must sacrifice exactly {Count} permanent(s)");

		var battlefieldId = state.GetPlayerZoneId(castingPlayerId, ZoneType.Battlefield);
		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = castingPlayerId,
			SourceCardId = sourceCardId,
		};

		foreach (var id in paymentIds)
		{
			if (!state.HasObject(id))
				return ValidationResult.Invalid($"Sacrifice target {id} does not exist");

			if (state.GetObject(id) is not Card)
				return ValidationResult.Invalid($"Sacrifice target {id} is not a card");

			if (state.GetCardZoneId(id) != battlefieldId)
				return ValidationResult.Invalid(
					$"Sacrifice target {id} is not on your battlefield"
				);

			if (Filter != null && !Filter.IsSatisfiedBy(id, context))
				return ValidationResult.Invalid(
					$"Sacrifice target {id} does not meet requirements"
				);
		}

		return ValidationResult.Valid;
	}

	public override GameState Pay(
		GameState state,
		int castingPlayerId,
		int sourceCardId,
		ImmutableList<int> paymentIds
	)
	{
		foreach (var id in paymentIds)
		{
			var card = (Card)state.GetObject(id);
			var leftEvent = new PermanentLeftBattlefieldEvent
			{
				CardId = id,
				OwnerId = card.OwnerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };
			if (card.HasSubtype("Artifact"))
			{
				var artifactEvent = new ArtifactLeftBattlefieldEvent
				{
					CardId = id,
					OwnerId = card.OwnerId,
				};
				state = state with
				{
					PendingGameEvents = state.PendingGameEvents.Add(artifactEvent),
				};
			}
			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveCardTracked(id, graveyardId);

			// A sacrificed creature has died, and every death payoff must see it. This was the
			// one death route in the engine that moved the card silently: it used MoveObject, so
			// no CardEnteredGraveyardEvent fired and every graveyard-active static went stale,
			// and it never announced CreatureDestroyedEvent, so OnAnyCreatureDies/OnSelfDies
			// no-opped on a sacrifice. Sacrifice outlets and their payoffs are the whole point of
			// the archetype, and none of them worked.
			if (card.HasComponent<CreatureComponent>())
			{
				state = state with
				{
					PendingGameEvents = state.PendingGameEvents.Add(
						new CreatureDestroyedEvent { CreatureId = id }
					),
				};
			}
		}
		return state;
	}
}
