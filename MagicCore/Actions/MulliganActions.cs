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
		if (Mulligan.DecidingPlayerId(s) != 0)
			return new ActionResult(s);
		// Gemstone Caverns: "If this card is in your opening hand and you're not the starting player, you may begin the
		// game with Gemstone Caverns on the battlefield" — asked once both hands are kept. ponytail: one such card asked
		// for, the first in hand (two Caverns would meet the legend rule).
		var second = s.OpponentOf(s.GetGame().StartingPlayerId);
		var opening = s.CardsIn(second, ZoneType.Hand)
			.FirstOrDefault(c => c.HasComponent<Effects.BeginsTheGameOnTheBattlefield>());
		if (opening is null)
			return new ActionResult(TurnStructure.StartFirstTurn(s));
		return new ActionResult(
			s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new YesNoChoice(
							second,
							$"Begin the game with {opening.Name} on the battlefield? If you do, exile a card from your hand."
						),
						new BeginOnTheBattlefieldAction(opening.Id, second),
						new StartFirstTurnAction(),
					],
				}
			)
		);
	}
}

/// <summary>
/// Gemstone Caverns, on a yes: onto the battlefield with a luck counter, then a card exiled from the hand (chosen in a
/// spawned choice, before the first turn starts).
/// </summary>
public record BeginOnTheBattlefieldAction(int CardId, int PlayerId) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		if (!YesNoChoice.SaidYes(InputContext))
			return new(s);
		s = s.EnterBattlefield(CardId, PlayerId)
			.PutCounters(CardId, Effects.CounterKinds.Luck, 1, entering: true);
		if (!s.CardsIn(PlayerId, ZoneType.Hand).Any())
			return new(s);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new ChooseCardsFromHandChoice
						{
							PlayerId = PlayerId,
							MinChoices = 1,
							MaxChoices = 1,
							Prompt = "Exile a card from your hand",
						},
						new MoveChosenToAction(PlayerId, ZoneType.Exile),
					],
				}
			)
		);
	}
}

/// <summary>The game's first turn, after a pregame choice (Gemstone Caverns).</summary>
public record StartFirstTurnAction : GameAction
{
	public override ActionResult Execute(GameState s) => new(TurnStructure.StartFirstTurn(s));
}
