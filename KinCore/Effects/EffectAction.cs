using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Implemented by actions that need target ids injected before they are spawned. The template is
/// never mutated — <see cref="WithTargets"/> returns a new record.
/// </summary>
public interface ITargetedAction
{
	GameAction WithTargets(ImmutableList<int> targetIds);
}

/// <summary>
/// Base for every effect — an action that changes the game by affecting things: deal damage, gain
/// life, draw, summon.
///
/// **Ported from MtgCore's `EffectAction`, not reinvented.** The shape is proven: one action per
/// effect, all its targets in one instance, looped in Execute. What is NOT ported is the targeting
/// half. MTG targets are a player's CHOICE, which is why `TargetingStrategy` and
/// `TargetSpecifications` come to 523 lines there. **A DOOMJAM target is a RULE** — "the Opponent",
/// "every enemy", "your unit in this lane" — resolved from the board at the moment it fires. See
/// <see cref="KinTargeting"/>, which replaces all of it.
///
/// The context-key indirection is left out too. It exists in MtgCore so one action can read a value
/// another action produced through the pipeline; nothing in DOOMJAM chains effects yet, and adding
/// it before something needs it would be machinery with no user.
///
/// **Lenient on resolve, like MtgCore**: an effect only fizzles if EVERY target is gone. Individual
/// missing ones are skipped, because a unit dying before an effect resolves is normal.
/// </summary>
public abstract record EffectAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	/// <summary>The object this effect came from — the card played, the enemy that died.</summary>
	public int SourceId { get; init; }

	/// <summary>
	/// What this effect's amount is multiplied by, read off the board when it resolves.
	///
	/// **On the base rather than on each action**, because "scales with something" is a property of
	/// an effect and not of damage in particular — healing, drawing and buffing all want it. Default
	/// <see cref="CountOf.None"/> means a multiplier of 1, so every effect authored before this
	/// existed still deals exactly what it says.
	/// </summary>
	public CountOf PerEach { get; init; } = CountOf.None;

	/// <summary>The authored amount times what the board says. Use this, never a raw Amount.</summary>
	protected int Scaled(GameState state, int amount) =>
		amount * KinCounts.Multiplier(state, PerEach);

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		if (TargetIds.IsEmpty)
			return ValidationResult.Valid;

		return TargetIds.All(id => !gameState.HasObject(id))
			? ValidationResult.Invalid("every target is gone")
			: ValidationResult.Valid;
	}
}
