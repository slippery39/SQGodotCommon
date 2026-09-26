using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **DEPLOY (`KinRelayPlan.md` R2): the only free reordering there is.** Before the fight you see
/// their line and move any of your monsters to any place in yours; FIGHT begins the battle and
/// records the order, which the run keeps for the next fight.
/// </summary>
public record DeployMoveAction : GameAction
{
	public int AllyId { get; init; }

	/// <summary>The place in your line it moves to; 0 is the front.</summary>
	public int To { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (!s.GetParty().Deploying)
			return ValidationResult.Invalid(
				"The fight has begun — only cards reorder your line now"
			);
		if (!s.LivingAllies().Any(a => a.Id == AllyId))
			return ValidationResult.Invalid("Only a monster in your line can be placed");
		return To < 0 || To >= s.LivingAllies().Count()
			? ValidationResult.Invalid("There is no such place in your line")
			: ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s) => new(s.MoveInLine(AllyId, To));
}

/// <summary>**FIGHT: the deployed order is kept, and cards can be played.**</summary>
public record BeginFightAction : GameAction
{
	public override ValidationResult ValidateAdd(GameState s) =>
		s.GetParty().Deploying
			? ValidationResult.Valid
			: ValidationResult.Invalid("The fight has already begun");

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(
			s.UpdateObject(
				party.Id,
				party with
				{
					Deploying = false,
					DeployedOrder = [.. s.LivingAllies().Select(a => a.Slot)],
				}
			)
		);
	}
}

public static class PartyDeploy
{
	public const string Waiting = "Order your line first, then FIGHT";
}
