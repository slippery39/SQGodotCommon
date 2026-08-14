using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Fires at the start of the controller's turn when the number of spells cast during the
/// previous turn falls within [Minimum, Maximum]. The werewolf transform condition.
///
/// Reads MtgGame.SpellsCastLastTurn, which StartTurnAction rolls over from
/// SpellsCastThisTurn before zeroing it. "Last turn" therefore means the immediately
/// preceding half-turn, since a turn here is one player's turn.
///
/// Day face uses Maximum = 0 ("no spells were cast last turn — transform").
/// Night face uses Minimum = 2 ("two or more spells were cast last turn — transform back").
/// </summary>
public record SpellsCastLastTurnCondition : TriggerCondition
{
	public int Minimum { get; init; } = 0;
	public int Maximum { get; init; } = int.MaxValue;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not TurnStartedEvent turnStarted)
			return false;
		if (turnStarted.PlayerId != context.ControllingPlayerId)
			return false;

		var game = context.GameState.TryGetGame();
		if (game == null)
			return false;

		return game.SpellsCastLastTurn >= Minimum && game.SpellsCastLastTurn <= Maximum;
	}
}
