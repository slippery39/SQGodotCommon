using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Create a token that's a copy of target creature" — Kiki-Jiki, Splinter Twin, Followed Footsteps.
///
/// **The engine had a copy rule but no copy ACTION, and the difference is what a combo needs.**
/// <see cref="CopyOnEnterComponent"/> copies whatever has the highest effective power on either
/// battlefield, chosen by the engine (see <see cref="CloneEngine"/> for why). That is right for
/// Clone, and useless for a combo: the deck needs to copy the ONE creature the loop runs on, and a
/// power tiebreak deciding which creature gets copied would make the combo work or not work
/// depending on what else happened to be in play.
///
/// This copies the RESOLVED TARGET, so the card's own targeting filter decides what is copied —
/// which is also what makes the archetype discoverable. A copier whose filter names a subtype gives
/// <c>PoolFeatures</c> a narrow demand to build a core around; "any creature you control" is 400+
/// cards and the breadth gate correctly throws it away.
///
/// The token is placed by spawning <see cref="PutIntoBattlefieldAction"/>, the single path every
/// creature takes onto the battlefield — so **the copy's own ETB trigger fires**, which is the
/// entire mechanism of an untap loop, and a copied Hydra enters with counters exactly as a cast one
/// does.
///
/// Two components are stripped from the copy, each for a reason that is silent when wrong:
///
/// - <see cref="CopyOnEnterComponent"/> — a copy is a copy of what it copied, not a fresh chance to
///   choose. Same rule <see cref="CloneEngine"/> already applies.
/// - Everything battlefield-stamped (applied boosts, granted keywords, equipment boosts). A token
///   copy is a copy of the PRINTED card, so copying a creature wearing an anthem must not bake that
///   anthem in permanently — the anthem re-applies to the token on its own when it enters.
/// </summary>
public record CreateTokenCopyAction : EffectAction
{
	/// <summary>
	/// Tokens enter with haste. Kiki-Jiki's copies attack the turn they arrive, and without this a
	/// copy loop produces a board that cannot do anything until the following turn.
	/// </summary>
	public bool WithHaste { get; init; } = true;

	/// <summary>
	/// How many copies to make, per target.
	///
	/// Note what is deliberately ABSENT: real Kiki-Jiki and Splinter Twin sacrifice their tokens at
	/// end of turn, and nothing in this engine sweeps them — <see cref="EndTurnAction"/> has no such
	/// pass. Copies here are permanent, which makes these cards stronger than printed. Say so on any
	/// card that uses this.
	/// </summary>
	public int Count { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var controllerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);
		if (controllerId == 0 || Count <= 0)
			return new ActionResult(gameState);

		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;

			if (state.GetObject(targetId) is not Card source)
				continue;

			if (!source.HasComponent<CreatureComponent>())
				continue;

			var token = BuildToken(source, controllerId, WithHaste);

			for (var i = 0; i < Count; i++)
				state = state.SpawnAction(new PutIntoBattlefieldAction { CardTemplate = token });
		}

		return new ActionResult(state) { Events = ImmutableList<GameEvent>.Empty };
	}

	private static Card BuildToken(Card source, int controllerId, bool withHaste)
	{
		var components = source
			.Components.Where(c =>
				c
					is not CopyOnEnterComponent
						// Battlefield state, not part of the printed card — see the type comment.
						and not AppliedStaticPTBoost
						and not AppliedKeywordComponent
						and not EquippedBoostComponent
						and not StaticPowerToughnessModifier
			)
			.ToImmutableArray();

		// **Battlefield state is cleared whether or not haste is granted.** A fresh token has none
		// of the original's — copying an exhausted, damaged or frozen creature must not produce an
		// exhausted, damaged or frozen token. Folding this into the haste branch would have made a
		// no-haste copier silently inherit the source's tapped state, which in a copy loop means
		// the first copy arrives unable to act.
		components = components
			.Select(c =>
				c is CreatureComponent creature
					? creature with
					{
						HasHaste = creature.HasHaste || withHaste,
						IsExhausted = false,
						HasAttacked = false,
						WasAttackedThisTurn = false,
						Damage = 0,
						FrozenTurns = 0,
						FrozenBySourceId = 0,
					}
					: c
			)
			.ToImmutableArray();

		return new Card
		{
			Name = source.Name,
			ManaCost = source.ManaCost,
			Types = source.EffectiveTypes,
			Subtypes = source.Subtypes,
			Components = components,
			OwnerId = controllerId,
			ControllerId = controllerId,
		};
	}
}
