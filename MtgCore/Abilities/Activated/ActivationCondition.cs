using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A gate on an activated ability — "activate only if...".
///
/// Serializable record with a virtual method, the same shape as TriggerCondition and
/// PowerToughnessModifier, so it can live on a component inside GameState without any delegate.
///
/// Checked in two places, and it must be both: ActivateAbilityAction.ValidateAdd rejects an
/// illegal activation, and MtgActionGenerator skips it so the AI and the UI never offer a
/// button that does nothing.
/// </summary>
public abstract record ActivationCondition
{
	public abstract bool IsSatisfied(GameState state, int cardId, int playerId);

	/// <summary>
	/// Player-facing reason the ability is unavailable. Abstract so a new condition cannot ship
	/// without one — presentation layers must never type-switch to build this text.
	/// </summary>
	public abstract string Describe();
}

/// <summary>
/// "Activate only if you have N or more life than your starting life total."
/// — Speaker of the Heavens.
///
/// Reads MtgPlayer.StartingLife rather than assuming 20, so a variant format or a
/// starting-life-altering effect does not quietly break the gate.
/// </summary>
public record LifeAboveStartingCondition : ActivationCondition
{
	public int Amount { get; init; } = 7;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		if (state.GetObject(playerId) is not MtgPlayer player)
			return false;

		return player.Life >= player.StartingLife + Amount;
	}

	public override string Describe() => $"You need {Amount} more life than your starting total";
}

/// <summary>
/// "Activate only if an opponent controls more lands than you." — Knight of the White Orchid.
///
/// Land count is MtgPlayer.LandsPlayedTotal, not a battlefield scan: lands in this engine are
/// consumed into MaxMana and moved to exile when played (see PlayLandAction), so there are no
/// land permanents on the battlefield to count.
/// </summary>
public record ControlsMoreLandsCondition : ActivationCondition
{
	/// <summary>When true the condition is "an opponent has MORE lands than you".</summary>
	public bool OpponentHasMore { get; init; } = true;

	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		OpponentHasMore
			? LandCounts.OpponentHasMore(state, playerId)
			: LandCounts.YouHaveMore(state, playerId);

	public override string Describe() =>
		OpponentHasMore
			? "An opponent must control more lands than you"
			: "You must control more lands than an opponent";
}
