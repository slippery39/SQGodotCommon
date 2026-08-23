using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Stamps a damage-prevention replacement onto each target player for the rest of the turn —
/// Safe Passage, Harm's Way.
///
/// The component goes on the PLAYER, not a permanent: a one-shot instant has no permanent to
/// live on. ReplacementEngine.ApplyReplacements scans player components for exactly this reason.
///
/// PreventsCreatureDamage covers the "and creatures you control" half of Safe Passage: it stamps
/// a second component for DamageToCreature, since the two are separate ReplaceableEvents.
///
/// DURATION DEFAULTS TO UntilYourNextTurn, NOT UntilEndOfTurn, and that is what makes prevention
/// a real card here rather than reminder text. With no priority window a spell can only be cast on
/// your own turn, while combat damage to you only arrives on the opponent's — so an end-of-turn
/// shield expired before every attack it existed to stop. Safe Passage was effectively blank.
///
/// A CREATURE target stamps the shield on that creature instead, which is how "protection" is
/// reskinned here (Gods Willing): protection from a colour is unreachable — cards have no colour —
/// but "damage can't touch it for a turn cycle" saves it from removal and wins its combat, which
/// is what the card is actually cast for. ReplacementEngine's scope rule is what keeps the
/// creature-stamped shield from spilling onto the rest of the board; read it before changing this.
/// Only the DamageToCreature half is stamped on a creature — a creature cannot be dealt the
/// player half.
/// </summary>
public record PreventDamageAction : EffectAction
{
	public bool PreventAll { get; init; } = true;
	public int Amount { get; init; } = 2;
	public bool PreventsCreatureDamage { get; init; } = true;

	/// <summary>How long the shield lasts. See the note above before changing this.</summary>
	public ModifierDuration Duration { get; init; } = ModifierDuration.UntilYourNextTurn;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is Card creatureCard)
			{
				if (!creatureCard.HasComponent<CreatureComponent>())
					continue;

				state = state.UpdateObject(
					targetId,
					creatureCard with
					{
						Components = creatureCard.Components.Add(
							new DamagePreventionComponent
							{
								Target = ReplaceableEvent.DamageToCreature,
								PreventAll = PreventAll,
								Amount = Amount,
								Duration = Duration,
							}
						),
					}
				);
				continue;
			}

			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var added = player.Components.Add(
				new DamagePreventionComponent
				{
					Target = ReplaceableEvent.DamageToPlayer,
					PreventAll = PreventAll,
					Amount = Amount,
					Duration = Duration,
				}
			);

			if (PreventsCreatureDamage)
				added = added.Add(
					new DamagePreventionComponent
					{
						Target = ReplaceableEvent.DamageToCreature,
						PreventAll = PreventAll,
						Amount = Amount,
						Duration = Duration,
					}
				);

			state = state.UpdateObject(targetId, player with { Components = added });
		}

		return new ActionResult(state);
	}
}
