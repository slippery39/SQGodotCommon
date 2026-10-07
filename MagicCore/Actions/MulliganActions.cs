using ImmutableGameObjects;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>
/// London mulligan (CR 103.5): redraw seven, and after keeping put one card on the bottom per
/// mulligan taken.
///
/// ponytail: players decide one at a time, starting player first, each to completion. The rules
/// have every player decide before anyone redraws; the only difference is that the second
/// player sees the first player's whole sequence rather than one round of it at a time.
/// </summary>
public static class Mulligan
{
	public const int HandSize = 7;

	/// <summary>The starting player first, then the other; 0 once both have kept.</summary>
	public static int DecidingPlayerId(GameState s)
	{
		var game = s.GetGame();
		if (game.Step != Step.PreGame)
			return 0;
		var first = game.StartingPlayerId;
		var second = s.OpponentOf(first);
		return !s.GetPlayer(first).HasKeptHand ? first
			: !s.GetPlayer(second).HasKeptHand ? second
			: 0;
	}

	public static ValidationResult Deciding(GameState s, int playerId) =>
		DecidingPlayerId(s) == playerId
			? ValidationResult.Valid
			: ValidationResult.Invalid("Not deciding a mulligan");
}

public record KeepHandAction : GameAction
{
	public int PlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState s) => Mulligan.Deciding(s, PlayerId);

	public override ActionResult Execute(GameState s)
	{
		var bottom = s.GetPlayer(PlayerId).MulligansTaken;
		if (bottom == 0)
			return new FinishKeepAction { PlayerId = PlayerId }.Execute(s);

		return new ActionResult(
			s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new ChooseCardsFromHandChoice
						{
							PlayerId = PlayerId,
							MinChoices = bottom,
							MaxChoices = bottom,
							Prompt = $"Put {bottom} on the bottom of your library",
						},
						new PutChosenOnBottomAction { PlayerId = PlayerId },
						new FinishKeepAction { PlayerId = PlayerId },
					],
				}
			)
		);
	}
}

public record MulliganAction : GameAction
{
	public int PlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState s) =>
		s.GetPlayer(PlayerId).MulligansTaken >= Mulligan.HandSize
			? ValidationResult.Invalid("Nothing left to keep")
			: Mulligan.Deciding(s, PlayerId);

	public override ActionResult Execute(GameState s)
	{
		var libraryId = s.ZoneId(PlayerId, ZoneType.Library);
		foreach (var card in s.CardsIn(PlayerId, ZoneType.Hand).ToList())
			s = s.MoveCard(card.Id, libraryId);
		s = s.Shuffle(PlayerId).Draw(PlayerId, Mulligan.HandSize);
		var player = s.GetPlayer(PlayerId);
		return new ActionResult(
			s.WithPlayer(player with { MulligansTaken = player.MulligansTaken + 1 })
		);
	}
}

public record FinishKeepAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		s = s.WithPlayer(s.GetPlayer(PlayerId) with { HasKeptHand = true });
		return new ActionResult(
			Mulligan.DecidingPlayerId(s) == 0 ? TurnStructure.StartFirstTurn(s) : s
		);
	}
}
