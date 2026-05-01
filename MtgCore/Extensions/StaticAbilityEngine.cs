using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Maintains applied static ability components on battlefield permanents.
///
/// Called by CheckStateBasedEffectsAction in response to CreatureEnteredBattlefieldEvent
/// and PermanentLeftBattlefieldEvent. Keeps the applied component state in sync with
/// which static sources are in play and which permanents they affect.
///
/// ETB (ProcessPermanentEntered):
///   - Stamps AppliedStaticPTBoost / AppliedKeywordComponent from existing sources onto the
///     new permanent if their filter matches.
///   - If the new permanent is itself a static source, stamps its effects onto all matching
///     permanents already on the battlefield and registers it in MtgGame.StaticSourceIds.
///
/// LTB (ProcessPermanentLeft):
///   - If the departing card was a static source: removes its applied components from all
///     cards in its AffectedIds and removes it from StaticSourceIds.
///   - Removes the departing card's ID from the AffectedIds of any remaining sources.
///
/// Restriction: static abilities currently only apply to permanents controlled by the same
/// player as the source (sufficient for all current cards).
/// </summary>
public static class StaticAbilityEngine
{
	public static GameState ProcessPermanentEntered(GameState state, int newCardId, int gameId)
	{
		var newCard = state.GetObject(newCardId) as Card;
		if (newCard == null)
			return state;

		var game = (MtgGame)state.GetObject(gameId);

		// Apply existing static sources to the new card
		foreach (var sourceId in game.StaticSourceIds)
		{
			var sourceCard = state.GetObject(sourceId) as Card;
			if (sourceCard == null)
				continue;

			// Current design: static abilities only affect same-controller permanents
			if (sourceCard.ControllerId != newCard.ControllerId)
				continue;

			state = ApplySourceToTarget(state, gameId, sourceId, newCardId);
		}

		// If the new card is itself a static source, register it and apply to existing permanents
		if (!newCard.HasComponent<StaticAbilityComponent>())
			return state;

		game = (MtgGame)state.GetObject(gameId);
		state = state.UpdateObject(
			gameId,
			game with
			{
				StaticSourceIds = game.StaticSourceIds.Add(newCardId),
			}
		);

		var battlefieldId = state.GetPlayerZoneId(newCard.ControllerId, ZoneType.Battlefield);
		foreach (var target in state.GetCardsInZone(battlefieldId))
		{
			if (target.Id == newCardId)
				continue;

			state = ApplySourceToTarget(state, gameId, newCardId, target.Id);
		}

		return state;
	}

	public static GameState ProcessPermanentLeft(GameState state, int leavingCardId, int gameId)
	{
		var game = (MtgGame)state.GetObject(gameId);

		// If the departing card was a static source, clean up its applied effects
		if (game.StaticSourceIds.Contains(leavingCardId))
		{
			var leavingCard = state.GetObject(leavingCardId) as Card;
			if (leavingCard != null)
			{
				foreach (var ability in leavingCard.GetComponents<StaticAbilityComponent>())
				{
					foreach (var affectedId in ability.AffectedIds)
					{
						if (!state.HasObject(affectedId))
							continue;

						var affectedCard = state.GetObject(affectedId) as Card;
						if (affectedCard == null)
							continue;

						var builder = ImmutableList.CreateBuilder<GameComponent>();
						var removed = false;
						foreach (var c in affectedCard.Components)
						{
							if (
								(c is AppliedStaticPTBoost b && b.SourceCardId == leavingCardId)
								|| (
									c is AppliedKeywordComponent k
									&& k.SourceCardId == leavingCardId
								)
							)
							{
								removed = true;
								continue;
							}
							builder.Add(c);
						}

						if (removed)
							state = state.UpdateObject(
								affectedId,
								affectedCard with
								{
									Components = builder.ToImmutable(),
								}
							);
					}
				}
			}

			game = (MtgGame)state.GetObject(gameId);
			state = state.UpdateObject(
				gameId,
				game with
				{
					StaticSourceIds = game.StaticSourceIds.Remove(leavingCardId),
				}
			);
			game = (MtgGame)state.GetObject(gameId);
		}

		// Remove the departing card from AffectedIds of remaining sources (index maintenance)
		foreach (var sourceId in game.StaticSourceIds)
		{
			var sourceCard = state.GetObject(sourceId) as Card;
			if (sourceCard == null)
				continue;

			var modified = false;
			var updatedComponents = sourceCard.Components;
			for (int i = 0; i < updatedComponents.Count; i++)
			{
				if (updatedComponents[i] is not StaticAbilityComponent sa)
					continue;

				if (!sa.AffectedIds.Contains(leavingCardId))
					continue;

				updatedComponents = updatedComponents.SetItem(
					i,
					sa with
					{
						AffectedIds = sa.AffectedIds.Remove(leavingCardId),
					}
				);
				modified = true;
			}

			if (modified)
				state = state.UpdateObject(
					sourceId,
					sourceCard with
					{
						Components = updatedComponents,
					}
				);
		}

		return state;
	}

	/// <summary>
	/// Checks all StaticAbilityComponents on sourceId against targetId and stamps the
	/// appropriate applied components if the filter matches. Updates AffectedIds on the source.
	/// </summary>
	private static GameState ApplySourceToTarget(
		GameState state,
		int gameId,
		int sourceId,
		int targetId
	)
	{
		var sourceCard = state.GetObject(sourceId) as Card;
		var targetCard = state.GetObject(targetId) as Card;
		if (sourceCard == null || targetCard == null)
			return state;

		var targetContext = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = targetCard.ControllerId,
			SourceCardId = sourceId,
		};

		for (int i = 0; i < sourceCard.Components.Count; i++)
		{
			if (sourceCard.Components[i] is not StaticAbilityComponent ability)
				continue;

			if (!ability.Filter.IsSatisfiedBy(targetId, targetContext))
				continue;

			// Stamp the effect onto the target
			targetCard = StampEffect(targetCard, sourceId, ability);
			state = state.UpdateObject(targetId, targetCard);

			// Record the target in the source's AffectedIds
			var updatedAbility = ability with
			{
				AffectedIds = ability.AffectedIds.Add(targetId),
			};
			var updatedSourceComponents = sourceCard.Components.SetItem(i, updatedAbility);
			sourceCard = sourceCard with { Components = updatedSourceComponents };
			state = state.UpdateObject(sourceId, sourceCard);

			// Refresh targetContext so subsequent abilities on the same source see updated state
			targetContext = targetContext with
			{
				GameState = state,
			};
		}

		return state;
	}

	private static Card StampEffect(Card target, int sourceId, StaticAbilityComponent ability)
	{
		if (ability is StaticPTBoostAbility ptBoost)
		{
			return target with
			{
				Components = target.Components.Add(
					new AppliedStaticPTBoost
					{
						SourceCardId = sourceId,
						PowerBonus = ptBoost.PowerBonus,
						ToughnessBonus = ptBoost.ToughnessBonus,
						Duration = ModifierDuration.Permanent,
					}
				),
			};
		}

		if (ability is StaticGrantKeywordAbility keywords)
		{
			return target with
			{
				Components = target.Components.Add(
					new AppliedKeywordComponent
					{
						SourceCardId = sourceId,
						GrantsHaste = keywords.GrantsHaste,
						GrantsFlying = keywords.GrantsFlying,
						GrantsTaunt = keywords.GrantsTaunt,
						GrantsReach = keywords.GrantsReach,
					}
				),
			};
		}

		return target;
	}
}
