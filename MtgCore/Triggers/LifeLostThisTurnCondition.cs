using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "At the beginning of your end step, if a player lost N or more life this turn, ..."
/// — Knight of the Ebon Legion.
///
/// The mirror of LifeGainedThisTurnCondition, with one deliberate difference: it asks about
/// ANY player by default, not just the controller. That is what the card says, and it is the
/// clause that makes the Knight a threat rather than a durdle — the four life is almost always
/// the four you just dealt with it.
///
/// Set ControllerOnly to narrow it to "you lost N life this turn" instead.
///
/// MtgPlayer.LifeLostThisTurn accumulates in LoseLifeAction, DrainLifeAction and the
/// player-damage path of DealDamageAction — all after replacement modifiers, so a prevention
/// effect correctly keeps the threshold from being met. StartTurnAction zeroes it for BOTH
/// players; see the comment there for why the active-player-only reset the other counters use
/// would be wrong here.
///
/// Checking at end of turn rather than on each loss matters: the card asks a "this turn"
/// question, so it must be answered once the turn's losses are all in. Pair it with
/// MaxTriggersPerTurn = 1 so a turn cannot fire it more than once.
/// </summary>
public record LifeLostThisTurnCondition : TriggerCondition
{
	public int Minimum { get; init; } = 4;
	public bool ControllerOnly { get; init; } = false;

	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context)
	{
		if (gameEvent is not TurnEndedEvent)
			return false;

		if (ControllerOnly)
			return context.GameState.GetObject(context.ControllingPlayerId) is MtgPlayer self
				&& self.LifeLostThisTurn >= Minimum;

		return new[] { MtgObjectKeys.Player1, MtgObjectKeys.Player2 }.Any(key =>
			context.GameState.GetObject(context.GameState.GetWellKnownId(key)) is MtgPlayer p
			&& p.LifeLostThisTurn >= Minimum
		);
	}
}

/// <summary>
/// "Whenever this creature blocks a creature" — Wall of Frost.
///
/// With no blocking, a wall's substitute is Taunt: attackers are compelled into it. So the
/// blocking clause becomes "whenever a creature attacks THIS creature", which needs the
/// defender's identity — and CreatureAttackedEvent carried only the attacker.
///
/// This is a condition rather than an EventTriggerCondition Filter because a Filter is evaluated
/// against the event's SUBJECT, which is the attacker. The question here is about the other end
/// of the attack.
///
/// The effect gets the attacker via ContextKeys.TriggerSubjectId, so "that creature" means the
/// one that actually attacked, not every creature the opponent controls.
/// </summary>
public record AttackedThisCardCondition : TriggerCondition
{
	public override bool IsSatisfiedBy(GameEvent gameEvent, TriggerContext context) =>
		gameEvent is CreatureAttackedEvent e && e.TargetId == context.SourceCardId;
}
