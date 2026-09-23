using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// **The companion's one free move a turn**, into any lane you are not holding with a unit.
///
/// This is what makes the companion something you steer rather than something that is merely there.
/// Its Guard soaks only the attack in ITS lane, and your life is behind that Guard — so each turn
/// asks which attack the companion should eat, or whether to step into an open lane and hit the
/// Opponent while some other lane's attack goes through to you. Before this it stood in the centre
/// for every battle of every run.
///
/// **Free and once a turn** (decided 2026-09-22). No energy, so it never competes with a card; once,
/// so it cannot dodge every attack by stepping back and forth.
/// </summary>
public record MoveCompanionAction : GameAction
{
	public int Lane { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (gameState.GetBattle().IsOver)
			return ValidationResult.Invalid("The battle is over");

		if (gameState.Companion() is not { } companion)
			return ValidationResult.Invalid("There is no companion on the field");

		if (gameState.GetBattle().CompanionMovedThisTurn)
			return ValidationResult.Invalid($"{companion.Name} has already moved this turn");

		if (Lane < 0 || Lane >= KinBattle.LaneCount)
			return ValidationResult.Invalid(
				$"Lane must be 0-{KinBattle.LaneCount - 1}, got {Lane}"
			);

		if (companion.Unit().Lane == Lane)
			return ValidationResult.Invalid($"{companion.Name} is already in lane {Lane}");

		// A lane you hold is refused rather than swapped: a swap would move a unit you chose to put
		// there, and "where does my unit go" is not a question this move should answer for you.
		if (gameState.UnitInLane(Lane) is { } held)
			return ValidationResult.Invalid($"Lane {Lane} is held by {held.Name}");

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var companion = gameState.Companion()!;
		var from = companion.Unit().Lane;

		var state = gameState.UpdateObject(
			companion.Id,
			companion.WithComponentReplaced(companion.Unit() with { Lane = Lane })
		);

		var battle = state.GetBattle();
		state = state.UpdateObject(battle.Id, battle with { CompanionMovedThisTurn = true });

		return new ActionResult(state).WithEvent(
			new CompanionMovedEvent
			{
				CardId = companion.Id,
				From = from,
				To = Lane,
			}
		);
	}
}

/// <summary>The companion changed lanes. The board animates it; nothing else reads it.</summary>
public record CompanionMovedEvent : GameEvent
{
	public int CardId { get; init; }
	public int From { get; init; }
	public int To { get; init; }
}
