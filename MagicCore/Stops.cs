using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;

namespace MagicCore;

/// <summary>
/// Arena's priority stops — where a front end gives a human priority instead of passing for them.
/// A convenience like <see cref="MagicRules.AutoPass"/>, never a rule: LegalActions stays complete.
/// </summary>
/// <param name="Mine">Steps of the player's own turn to stop in.</param>
/// <param name="Theirs">Steps of the opponent's turn to stop in.</param>
/// <param name="FullControl">Arena's full control: stop at every priority with anything but Pass.</param>
public sealed record Stops(
	ImmutableHashSet<Step> Mine,
	ImmutableHashSet<Step> Theirs,
	bool FullControl = false
)
{
	/// <summary>
	/// Your main phases and blocks (tricks after blocks); their attacks (act before blocks), blocks
	/// and end step.
	/// </summary>
	public static readonly Stops Arena =
		new(
			[Step.PreCombatMain, Step.DeclareBlockers, Step.PostCombatMain],
			[Step.DeclareAttackers, Step.DeclareBlockers, Step.End]
		);

	/// <summary>
	/// Does <paramref name="playerId"/> get to decide now? Anything that is not a priority window
	/// (a choice, the mulligan, someone else deciding) is always theirs to answer. In a priority
	/// window: never where Pass is the only move; always when the opponent's spell or ability is on
	/// top of the stack (Arena's smart stop — a response is possible); never over their own (Arena
	/// passes so it resolves); otherwise at the enabled steps — a combat step only with attackers.
	/// </summary>
	public bool StopsAt(GameState s, int playerId)
	{
		var game = s.GetGame();
		if (s.IsWaitingForChoice || game.Step == Step.PreGame || game.PriorityPlayerId != playerId)
			return true;
		var wanted =
			FullControl
			|| TopOfStackControllerId(s) switch
			{
				0 => (game.ActivePlayerId == playerId ? Mine : Theirs).Contains(game.Step)
					&& (
						game.Step is not (Step.DeclareAttackers or Step.DeclareBlockers)
						|| Turns.Combat.Attackers(s).Any()
					),
				var controller => controller != playerId,
			};
		// Last: LegalActions validates every candidate, the costliest question here.
		return wanted && s.LegalActions() is not [PassPriorityAction];
	}

	/// <summary>Passes for <paramref name="playerId"/> until they stop, someone else decides, or the game ends.</summary>
	public GameState PassUntilStop(GameState s, int playerId)
	{
		while (!s.IsOver() && s.DecidingPlayerId() == playerId && !StopsAt(s, playerId))
			s = s.Act(new PassPriorityAction { PlayerId = playerId });
		return s;
	}

	/// <summary>0 when the stack is empty.</summary>
	public static int TopOfStackControllerId(GameState s) =>
		s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)).FirstOrDefault() is var top and not 0
			? s.StackObjectControllerId(top)
			: 0;
}
