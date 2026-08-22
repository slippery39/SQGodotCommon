using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Grants keyword abilities to target creatures by stamping an AppliedKeywordComponent.
///
/// This is the effect-driven counterpart to StaticGrantKeywordAbility: a static ability
/// grants keywords for as long as its source is on the battlefield, whereas this action
/// grants them for a fixed duration regardless of any source persisting. Used by combat
/// tricks and ETB pumps ("creatures you control gain Flying until end of turn").
///
/// UntilEndOfTurn grants are cleared by StartTurnAction via ClearEndOfTurnModifiers,
/// the same path that clears temporary P/T buffs — so a trick and a pump expire together.
/// StaticAbilityEngine deliberately skips UntilEndOfTurn grants during source cleanup.
/// </summary>
public record GrantKeywordAction : EffectAction
{
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilEndOfTurn;
	public int SourceCardId { get; init; } = 0;

	public bool GrantsHaste { get; init; } = false;
	public bool GrantsFlying { get; init; } = false;
	public bool GrantsTaunt { get; init; } = false;
	public bool GrantsReach { get; init; } = false;
	public bool GrantsLifelink { get; init; } = false;
	public bool GrantsTrample { get; init; } = false;
	public bool GrantsShroud { get; init; } = false;
	public bool GrantsHexproof { get; init; } = false;
	public bool GrantsDeathtouch { get; init; } = false;
	public bool GrantsFirstStrike { get; init; } = false;
	public bool GrantsDoubleStrike { get; init; } = false;
	public bool GrantsIndestructible { get; init; } = false;
	public bool GrantsExalted { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card card)
				continue;

			// Planeswalkers as well as creatures. Taunt is the one keyword that means something on
			// a walker — "creatures attack Gideon if able" — and AttackAction's Taunt scan reads
			// AppliedKeywordComponent off planeswalkers for exactly that. Everything else that
			// consumes these grants goes through GetEffectiveStats, which is only ever asked about
			// creatures, so a stray grant on a walker is inert rather than harmful.
			if (
				!card.HasComponent<CreatureComponent>()
				&& !card.HasComponent<PlaneswalkerComponent>()
			)
				continue;

			var granted = new AppliedKeywordComponent
			{
				SourceCardId = SourceCardId,
				Duration = Duration,
				GrantsHaste = GrantsHaste,
				GrantsFlying = GrantsFlying,
				GrantsTaunt = GrantsTaunt,
				GrantsReach = GrantsReach,
				GrantsLifelink = GrantsLifelink,
				GrantsTrample = GrantsTrample,
				GrantsShroud = GrantsShroud,
				GrantsHexproof = GrantsHexproof,
				GrantsDeathtouch = GrantsDeathtouch,
				GrantsFirstStrike = GrantsFirstStrike,
				GrantsDoubleStrike = GrantsDoubleStrike,
				GrantsIndestructible = GrantsIndestructible,
				GrantsExalted = GrantsExalted,
			};

			state = state.UpdateObject(
				targetId,
				card with
				{
					Components = card.Components.Add(granted),
				}
			);
		}

		return new ActionResult(state);
	}
}
