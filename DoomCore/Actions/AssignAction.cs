using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Assigns a unit to attack an enemy or to block one's telegraphed attack.
///
/// **A unit does one or the other, never both.** That either/or is the core decision of a battle:
/// attacking removes all of an enemy's FUTURE damage, blocking absorbs damage NOW. Assigning again
/// overwrites, so a player can freely change their mind until they end the turn.
/// </summary>
public record AssignAction : GameAction
{
	public int UnitId { get; init; }
	public int EnemyId { get; init; }
	public Assignment Assignment { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (Assignment == Assignment.None)
			return ValidationResult.Invalid("Use Attack or Block");

		if (!gameState.HasObject(UnitId))
			return ValidationResult.Invalid($"No unit {UnitId}");

		if (gameState.GetParent(UnitId) != gameState.ZoneId(ZoneType.Field))
			return ValidationResult.Invalid("Unit is not on the field");

		if (gameState.GetObject(UnitId) is not DoomCard c || !c.HasComponent<UnitComponent>())
			return ValidationResult.Invalid("Not a unit");

		if (!gameState.HasObject(EnemyId) || gameState.GetObject(EnemyId) is not Enemy enemy)
			return ValidationResult.Invalid($"No enemy {EnemyId}");

		if (enemy.IsDead)
			return ValidationResult.Invalid("Enemy is already dead");

		// Blocking an enemy that is not attacking would silently do nothing, which reads to a
		// player as a click that did not register.
		if (Assignment == Assignment.Block && enemy.Intent != IntentKind.Attack)
			return ValidationResult.Invalid($"{enemy.Name} is not attacking");

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (DoomCard)gameState.GetObject(UnitId);
		var unit = card.Unit();

		var state = gameState.UpdateObject(
			UnitId,
			card.WithComponentReplaced(
				unit with
				{
					Assignment = Assignment,
					AssignedEnemyId = EnemyId,
				}
			)
		);

		return new ActionResult(state);
	}
}
