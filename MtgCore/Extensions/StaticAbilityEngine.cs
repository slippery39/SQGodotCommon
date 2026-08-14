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

		// If the new card is itself a battlefield-active static source, register and apply it.
		// Graveyard-active abilities are deliberately ignored here — they register later, via
		// ProcessZoneSourceEntered, so the two zones never double-apply the same card.
		var hasBattlefieldStatic = newCard
			.GetComponents<StaticAbilityComponent>()
			.Any(a => a.ActiveInZone == ZoneType.Battlefield);
		if (!hasBattlefieldStatic)
			return state;

		return RegisterSourceAndApply(state, newCardId, gameId);
	}

	/// <summary>
	/// Activates a card's static abilities from a non-battlefield zone — currently the
	/// graveyard (Wonder: "while this is in your graveyard, creatures you control have Flying").
	///
	/// Called by CheckStateBasedEffectsAction on CardEnteredGraveyardEvent. Registration is
	/// identical to the battlefield path; the only difference is which abilities qualify,
	/// which ApplySourceToTarget decides by comparing each ability's ActiveInZone against the
	/// source card's actual current zone.
	/// </summary>
	public static GameState ProcessZoneSourceEntered(GameState state, int cardId, int gameId)
	{
		if (state.GetObject(cardId) is not Card card)
			return state;

		// Battlefield-active abilities are handled by ProcessPermanentEntered, not here.
		var hasZoneStatic = card.GetComponents<StaticAbilityComponent>()
			.Any(a => a.ActiveInZone != ZoneType.Battlefield);
		if (!hasZoneStatic)
			return state;

		return RegisterSourceAndApply(state, cardId, gameId);
	}

	/// <summary>
	/// Deactivates a card's zone-based static abilities when it leaves that zone — cast from
	/// the graveyard, reanimated, or exiled. Called on CardLeftGraveyardEvent.
	/// </summary>
	public static GameState ProcessZoneSourceLeft(GameState state, int cardId, int gameId)
	{
		var game = (MtgGame)state.GetObject(gameId);
		return game.StaticSourceIds.Contains(cardId)
			? UnregisterSourceAndStrip(state, cardId, gameId)
			: state;
	}

	/// Registers a card in StaticSourceIds and stamps its active abilities onto every
	/// matching permanent on its controller's battlefield.
	private static GameState RegisterSourceAndApply(GameState state, int sourceId, int gameId)
	{
		if (state.GetObject(sourceId) is not Card sourceCard)
			return state;

		var game = (MtgGame)state.GetObject(gameId);
		state = state.UpdateObject(
			gameId,
			game with
			{
				StaticSourceIds = game.StaticSourceIds.Add(sourceId),
			}
		);

		var battlefieldId = state.GetPlayerZoneId(sourceCard.ControllerId, ZoneType.Battlefield);
		foreach (var target in state.GetCardsInZone(battlefieldId))
		{
			if (target.Id == sourceId)
				continue;

			state = ApplySourceToTarget(state, gameId, sourceId, target.Id);
		}

		return state;
	}

	public static GameState ProcessPermanentLeft(GameState state, int leavingCardId, int gameId)
	{
		var game = (MtgGame)state.GetObject(gameId);

		// If the departing card was a static source, clean up its applied effects.
		// A card with a graveyard-active static is unregistered here and re-registered by
		// ProcessZoneSourceEntered once it lands in the graveyard — CheckStateBasedEffects
		// processes PermanentLeftBattlefieldEvent before CardEnteredGraveyardEvent, so the
		// battlefield effects are always stripped before the graveyard effects are stamped.
		if (game.StaticSourceIds.Contains(leavingCardId))
		{
			state = UnregisterSourceAndStrip(state, leavingCardId, gameId);
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
			for (int i = 0; i < updatedComponents.Length; i++)
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
	/// Strips every component this source stamped onto other cards and unregisters it from
	/// StaticSourceIds. Shared by the battlefield leave path and the zone leave path — the
	/// cleanup is identical, only the triggering event differs.
	/// </summary>
	private static GameState UnregisterSourceAndStrip(GameState state, int sourceId, int gameId)
	{
		if (state.GetObject(sourceId) is Card sourceCard)
		{
			foreach (var ability in sourceCard.GetComponents<StaticAbilityComponent>())
			{
				foreach (var affectedId in ability.AffectedIds)
				{
					if (state.GetObject(affectedId) is not Card affectedCard)
						continue;

					var builder = ImmutableArray.CreateBuilder<GameComponent>();
					var removed = false;
					foreach (var c in affectedCard.Components)
					{
						// UntilEndOfTurn grants are owned by StartTurnAction, not by the
						// static source — an "until end of turn" effect outlives its
						// source leaving play.
						if (
							(c is AppliedStaticPTBoost b && b.SourceCardId == sourceId)
							|| (
								c is AppliedKeywordComponent k
								&& k.SourceCardId == sourceId
								&& k.Duration != ModifierDuration.UntilEndOfTurn
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

			// Clear the reverse index so a later re-registration starts clean.
			var cleared = sourceCard.Components;
			for (int i = 0; i < cleared.Length; i++)
				if (cleared[i] is StaticAbilityComponent sa && !sa.AffectedIds.IsEmpty)
					cleared = cleared.SetItem(i, sa with { AffectedIds = [] });

			if (cleared != sourceCard.Components)
				state = state.UpdateObject(sourceId, sourceCard with { Components = cleared });
		}

		var game = (MtgGame)state.GetObject(gameId);
		return state.UpdateObject(
			gameId,
			game with
			{
				StaticSourceIds = game.StaticSourceIds.Remove(sourceId),
			}
		);
	}

	/// <summary>
	/// True when the source card currently sits in the zone its ability requires.
	/// Checked per ability, so a card can carry a battlefield static and a graveyard static
	/// and have exactly one of them live at a time.
	/// </summary>
	private static bool IsAbilityActive(
		GameState state,
		Card source,
		StaticAbilityComponent ability
	)
	{
		var zoneId = state.GetCardZoneId(source.Id);
		if (zoneId == state.GetPlayerZoneId(source.OwnerId, ability.ActiveInZone))
			return true;

		// Battlefield zones are keyed by controller, graveyards by owner — check both so a
		// stolen or token permanent still resolves correctly.
		return zoneId == state.GetPlayerZoneId(source.ControllerId, ability.ActiveInZone);
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

		for (int i = 0; i < sourceCard.Components.Length; i++)
		{
			if (sourceCard.Components[i] is not StaticAbilityComponent ability)
				continue;

			// Only abilities whose required zone matches where the source actually is.
			if (!IsAbilityActive(state, sourceCard, ability))
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
						GrantsLifelink = keywords.GrantsLifelink,
						GrantsTrample = keywords.GrantsTrample,
						GrantsShroud = keywords.GrantsShroud,
						GrantsHexproof = keywords.GrantsHexproof,
						GrantsDeathtouch = keywords.GrantsDeathtouch,
					}
				),
			};
		}

		return target;
	}
}
