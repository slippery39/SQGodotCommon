using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Turns;

/// <summary>
/// Moves the game through steps and hands out priority. The ONLY place either happens: every
/// action that ends a step or gives priority calls in here.
/// </summary>
public static class TurnStructure
{
	public const int MaxHandSize = 7;

	/// <summary>
	/// CR 117.5: state-based actions are checked every time a player would receive priority.
	/// Triggers go on the stack at this same point once they exist.
	/// </summary>
	public static GameState GivePriority(GameState s, int playerId)
	{
		s = StateBasedActions.Check(s);
		var game = s.GetGame();
		return s.WithGame(game with { PriorityPlayerId = s.IsOver() ? 0 : playerId });
	}

	public static GameState StartFirstTurn(GameState s)
	{
		var game = s.GetGame();
		s = s.WithGame(game with { ActivePlayerId = game.StartingPlayerId, TurnNumber = 1 });
		return EnterStep(s, Step.Untap);
	}

	/// <summary>Both players passed with an empty stack: mana empties (CR 500.4) and the next step begins.</summary>
	public static GameState EndStep(GameState s)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
			s = s.WithPlayer(s.GetPlayer(id) with { ManaPool = ManaPool.Empty });

		var game = s.GetGame();
		if (game.Step != Step.Cleanup)
			return EnterStep(s, game.Step + 1);

		s = s.WithGame(
			game with
			{
				ActivePlayerId = s.OpponentOf(game.ActivePlayerId),
				TurnNumber = game.TurnNumber + 1,
			}
		);
		return EnterStep(s, Step.Untap);
	}

	static GameState EnterStep(GameState s, Step step)
	{
		var game = s.GetGame() with { Step = step, PriorityPlayerId = 0, PassesInSuccession = 0 };
		s = s.WithGame(game);
		var active = game.ActivePlayerId;

		switch (step)
		{
			case Step.Untap:
				foreach (var card in s.CardsIn(active, ZoneType.Battlefield).Where(c => c.IsTapped))
					s = s.UpdateObject(card.Id, card with { IsTapped = false });
				s = s.WithPlayer(s.GetPlayer(active) with { LandsPlayedThisTurn = 0 });
				return EndStep(s); // nobody gets priority during untap (CR 502.4)

			case Step.Draw:
				// The starting player skips their first draw (CR 103.8a).
				if (game.TurnNumber > 1)
					s = s.Draw(active);
				return GivePriority(s, active);

			case Step.DeclareAttackers:
				return Combat.BeginDeclareAttackers(s);

			case Step.DeclareBlockers:
				return Combat.BeginDeclareBlockers(s);

			case Step.FirstStrikeDamage:
				if (!Combat.Attackers(s).Any() || !Combat.AnyFirstStrike(s))
					return EndStep(s);
				return GivePriority(Combat.DealDamage(s, firstStrikeStep: true), active);

			case Step.CombatDamage:
				if (!Combat.Attackers(s).Any())
					return EndStep(s);
				return GivePriority(Combat.DealDamage(s, firstStrikeStep: false), active);

			case Step.PostCombatMain:
				return GivePriority(Combat.EndCombat(s), active);

			case Step.Cleanup:
				s = Combat.RemoveDamage(s);
				var excess = s.CardsIn(active, ZoneType.Hand).Count() - MaxHandSize;
				if (excess <= 0)
					return EndStep(s); // no priority in cleanup unless something triggers (CR 514.3)
				return s.SpawnAction(
					new PipelineAction
					{
						Steps =
						[
							new ChooseCardsFromHandChoice
							{
								PlayerId = active,
								MinChoices = excess,
								MaxChoices = excess,
								Prompt = $"Discard {excess} to hand size",
							},
							new DiscardChosenAction { PlayerId = active },
							new EndStepAction(),
						],
					}
				);

			default:
				return GivePriority(s, active);
		}
	}
}

public static class StateBasedActions
{
	/// <summary>
	/// CR 704.5. ponytail: one pass — nothing yet makes one state-based action cause another. Loop
	/// until stable once dying can trigger or change toughness.
	/// </summary>
	public static GameState Check(GameState s)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		{
			var player = s.GetPlayer(id);
			if (!player.HasLost && (player.Life <= 0 || player.AttemptedDrawFromEmptyLibrary))
				s = s.WithPlayer(player with { HasLost = true });

			var dying = s.CardsIn(id, ZoneType.Battlefield)
				.Where(c =>
					c.Is(CardType.Creature)
					&& (
						c.Toughness <= 0
						|| c.Damage >= c.Toughness
						|| (c.DamagedByDeathtouch && c.Damage > 0)
					)
				)
				.ToList();
			foreach (var card in dying)
				s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
		}
		return s;
	}
}
