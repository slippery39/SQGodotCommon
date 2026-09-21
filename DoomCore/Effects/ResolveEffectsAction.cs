using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Fires every effect a holder declares for one trigger.
///
/// **The three steps are MtgCore's `ResolveEffectAction`**: resolve the targets, inject them into
/// the template, spawn the result. What changes is step one — targets come from a rule rather than
/// from a player, so there is no choice to await and no pipeline context to thread.
///
/// Effects spawn rather than execute inline so each one is a real action the engine orders, can
/// validate on resolve, and can have its own follow-ups. That is the machinery
/// `ImmutableGameObjects` already provides; adding a second way to run an effect would be the
/// mistake.
/// </summary>
public record ResolveEffectsAction : GameAction
{
	/// <summary>The thing the effects belong to — a played card, a dying enemy.</summary>
	public int SourceId { get; init; }

	public EffectTrigger Trigger { get; init; }

	public ImmutableList<DoomEffect> Effects { get; init; } = ImmutableList<DoomEffect>.Empty;

	/// <summary>
	/// The lane the source was dropped on, for a source that has no body of its own.
	/// **This is what makes a rite a targeted card** — see <see cref="DoomTargeting.Resolve"/>.
	/// Null everywhere else, and every lane-scoped target then resolves to nothing.
	/// </summary>
	public int? PlayedLane { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		foreach (var effect in Effects)
		{
			if (effect.Trigger != Trigger)
				continue;

			if (effect.Template is null)
				throw new InvalidOperationException(
					$"A {Trigger} effect on object {SourceId} has no Template. An effect that "
						+ "resolves to nothing looks exactly like one that worked."
				);

			var targets = DoomTargeting.Resolve(state, effect.Target, SourceId, PlayedLane);

			// A rule that came back empty is a legitimate outcome — no enemy in that lane — so it
			// is skipped rather than throwing. The throw above is for a MALFORMED effect, which is
			// an authoring mistake and should never reach a player.
			if (effect.Target != DoomTarget.None && targets.IsEmpty)
				continue;

			var action = effect.Template switch
			{
				EffectAction typed => typed with { SourceId = SourceId, TargetIds = targets },
				ITargetedAction targeted => targeted.WithTargets(targets),
				var plain => plain,
			};

			state = state.SpawnAction(action);
		}

		return new ActionResult(state);
	}
}
