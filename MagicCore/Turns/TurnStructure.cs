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
	/// CR 117.5: every time a player would receive priority, state-based actions are checked and
	/// triggered abilities go on the stack. Triggers are collected BEFORE the check, so a creature
	/// the check kills still saw its own entry.
	/// </summary>
	public static GameState GivePriority(GameState s, int playerId)
	{
		// Collect, check, and again while the check itself raised events — a creature the check kills
		// must have its death seen in this same pass, not at some later priority.
		var triggered = new List<Triggers.Pending>();
		for (var round = 0; ; round++)
		{
			if (round == 100)
				throw new InvalidOperationException(
					"State-based actions kept raising events for 100 rounds"
				);
			triggered.AddRange(Triggers.Collect(s));
			s = StateBasedActions.Check(s with { PendingGameEvents = [] });
			if (s.PendingGameEvents.IsEmpty)
				break;
		}
		// APNAP across every round, not just within one (CR 603.3b).
		var active = s.GetGame().ActivePlayerId;
		triggered = [.. triggered.OrderBy(t => t.Source.ControllerId == active ? 0 : 1)];
		if (s.IsOver())
			return s.WithGame(s.GetGame() with { PriorityPlayerId = 0 });
		if (triggered.Count > 0)
			return Triggers.PutOnStack(
				s.WithGame(s.GetGame() with { PriorityPlayerId = 0 }),
				triggered,
				playerId
			);
		return s.WithGame(s.GetGame() with { PriorityPlayerId = playerId });
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
		if (step is Step.Upkeep or Step.Draw or Step.BeginCombat or Step.End)
			s = s.Raise(new StepBeganEvent(step)); // "at the beginning of …" (CR 503.1a, 504.2, 507.1, 513.1a)

		switch (step)
		{
			case Step.Untap:
				foreach (var card in s.CardsIn(active, ZoneType.Battlefield).Where(c => c.IsTapped))
					s = s.Untap(card.Id);
				foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
					s = s.WithPlayer(
						s.GetPlayer(id) with
						{
							LastTurn = s.GetPlayer(id).ThisTurn,
							ThisTurn = new(),
						}
					);
				s = s.WithGame(s.GetGame() with { CreaturesDiedThisTurn = 0 });
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
				s = Combat.CleanupPermanents(s);
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
							new MoveChosenToGraveyardAction { PlayerId = active },
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
	/// <summary>CR 704.3: performed again and again until none applies — a pass that changes nothing returns the same state.</summary>
	public static GameState Check(GameState s)
	{
		for (var round = 0; round < 100; round++)
		{
			var next = CheckOnce(s);
			if (ReferenceEquals(next, s))
				return s;
			s = next;
		}
		throw new InvalidOperationException("State-based actions still applied after 100 passes");
	}

	static GameState CheckOnce(GameState s)
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
						s.Toughness(c) <= 0
						|| c.Damage >= s.Toughness(c)
						|| (c.DamagedByDeathtouch && c.Damage > 0)
					)
				)
				.ToList();
			foreach (var card in dying)
				s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));

			// CR 704.5i: a planeswalker with 0 loyalty — unless something says it survives (Sanctum Lurker).
			var walkersSurvive = s.CardsIn(id, ZoneType.Battlefield)
				.Any(c => c.HasComponent<Effects.YourPlaneswalkersSurviveZeroLoyalty>());
			var depleted = s.CardsIn(id, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Planeswalker) && c.Loyalty <= 0 && !walkersSurvive)
				.ToList();
			foreach (var card in depleted)
				s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));

			// CR 704.5j legend rule. ponytail: keeps the newest; Arena asks which. Make it a choice
			// when keeping the older one (say, a tapped copy) matters to the AI.
			var extraLegends = s.CardsIn(id, ZoneType.Battlefield)
				.Where(c => c.Is(Supertype.Legendary))
				.GroupBy(c => c.Name)
				.SelectMany(g => g.SkipLast(1))
				.ToList();
			foreach (var card in extraLegends)
				s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));

			// CR 704.5d: a token anywhere but the battlefield ceases to exist.
			foreach (
				var zone in new[]
				{
					ZoneType.Graveyard,
					ZoneType.Exile,
					ZoneType.Hand,
					ZoneType.Library,
				}
			)
			foreach (
				var gone in s.CardsIn(id, zone)
					.Where(c => c.IsToken || Rules.Prepare.IsDeadCopy(s, c))
					.ToList()
			)
				s = s.RemoveObject(gone.Id);
		}
		return s;
	}
}
