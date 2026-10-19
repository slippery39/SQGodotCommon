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
		// Only a cleanup in which something triggered gives priority; after it, another cleanup (CR 514.3a).
		return EnterStep(s, game.Step == Step.Cleanup ? Step.Cleanup : game.Step + 1);
	}

	/// <summary>
	/// "Until your next turn" (CR 611.2b) ends as that player's turn begins: from permanents, and the
	/// effects kept in the command zone, which then cease to exist.
	/// </summary>
	static GameState EndUntilYourNextTurn(GameState s, int playerId)
	{
		foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var zone in new[] { ZoneType.Battlefield, ZoneType.Command })
		foreach (
			var c in s.CardsIn(p, zone)
				.Where(c =>
					c.Components.Any(x => x is UntilEndOfTurn u && u.UntilTurnOf == playerId)
				)
				.ToList()
		)
		{
			var left = c.Components.RemoveAll(x =>
				x is UntilEndOfTurn u && u.UntilTurnOf == playerId
			);
			s =
				zone == ZoneType.Command && left.IsEmpty
					? s.RemoveObject(c.Id)
					: s.UpdateObject(c.Id, c with { Components = left });
		}
		return s;
	}

	/// <summary>
	/// The cleanup is done (CR 514.3): the next turn — unless something triggered or a state-based
	/// action applies (Tinybones seeing a discard to hand size). Then players get priority in the
	/// cleanup, and once they pass with an empty stack, another cleanup step begins (EndStep).
	/// </summary>
	public static GameState FinishCleanup(GameState s)
	{
		if (Triggers.Collect(s).Count > 0 || !ReferenceEquals(StateBasedActions.Check(s), s))
			return GivePriority(s, s.GetGame().ActivePlayerId);
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
			s = s.WithPlayer(s.GetPlayer(id) with { ManaPool = ManaPool.Empty });
		var game = s.GetGame();
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
		// "At the beginning of …" (CR 503.1a, 504.2, 505.4, 507.1, 513.1a) — the main phase is the first one
		// only: Gardenize's "your first main phase".
		if (step is Step.Upkeep or Step.Draw or Step.PreCombatMain or Step.BeginCombat or Step.End)
			s = s.Raise(new StepBeganEvent(step));

		switch (step)
		{
			case Step.Untap:
				s = EndUntilYourNextTurn(s, active);
				// Held down by an Aura (Infinite Coursework) — found once for the step.
				var heldDown = new[] { s.Player1Id(), s.Player2Id() }
					.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
					.Where(a => a.HasComponent<Effects.EnchantedDoesntUntap>())
					.Select(a => (a.AttachedToId, a.AttachedToIncarnation))
					.ToHashSet();
				foreach (
					var card in s.CardsIn(active, ZoneType.Battlefield)
						.Where(c =>
							c.IsTapped
							&& !s.HasAbility<Effects.DoesntUntapInYourUntapStep>(c)
							&& !heldDown.Contains((c.Id, c.Incarnation))
						)
						.ToList()
				)
					s = s.Untap(card.Id);
				foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
				{
					s = s.WithPlayer(
						s.GetPlayer(id) with
						{
							LastTurn = s.GetPlayer(id).ThisTurn,
							ThisTurn = new(),
						}
					);
					// "This turn" effects end with the tally. ponytail: at the next turn's start, not in
					// cleanup (CR 514.2), as the tally flags they replaced did; differs only if a cleanup
					// gives priority. Move both into cleanup if a card ever cares.
					foreach (var effect in s.ThisTurnEffects<Effects.ThisTurnEffect>(id).ToList())
						s = s.RemoveObject(effect.Id);
				}
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
					return FinishCleanup(s); // no priority in cleanup unless something triggers (CR 514.3)
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
		if (s.IsOver())
			return s; // the game ended — "you win the game" — so nothing more is checked (CR 104.2a)
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		{
			var player = s.GetPlayer(id);
			if (!player.HasLost && (player.Life <= 0 || player.AttemptedDrawFromEmptyLibrary))
				s = s.WithPlayer(player with { HasLost = true });

			// Plain loops over one read of the battlefield, read again only once something on it
			// changed: this runs at every priority, half of each step the search simulates (AI-7.3d).
			var field = s.CardsIn(id, ZoneType.Battlefield).ToList();

			List<Card>? dying = null;
			foreach (var c in field)
				if (c.Is(CardType.Creature) && IsLethal(s, c))
					(dying ??= []).Add(c);
			if (dying is not null)
			{
				foreach (var card in dying)
					s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
				field = s.CardsIn(id, ZoneType.Battlefield).ToList();
			}

			// CR 704.5i: a planeswalker with 0 loyalty — unless something says it survives (Sanctum Lurker).
			List<Card>? depleted = null;
			foreach (var c in field)
				if (c.Is(CardType.Planeswalker) && c.Loyalty <= 0)
					(depleted ??= []).Add(c);
			if (
				depleted is not null
				&& !field.Any(s.HasAbility<Effects.YourPlaneswalkersSurviveZeroLoyalty>)
			)
			{
				foreach (var card in depleted)
					s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
				field = s.CardsIn(id, ZoneType.Battlefield).ToList();
			}

			// CR 704.5j legend rule — not this turn after Hall of Echoes. ponytail: keeps a card over a
			// token (Face Yourself's copy is sacrificed anyway), then the newest; Arena asks which. Make it
			// a choice when keeping the older one (say, a tapped copy) matters to the AI.
			if (
				field.Count(c => c.Is(Supertype.Legendary)) > 1
				&& !s.ThisTurnEffects<Effects.NoLegendRule>(id).Any()
			)
			{
				var extraLegends = field
					.Where(c => c.Is(Supertype.Legendary))
					.GroupBy(c => c.Name)
					.SelectMany(g => g.OrderBy(c => c.IsToken ? 0 : 1).SkipLast(1)) // stable: newest last
					.ToList();
				foreach (var card in extraLegends)
					s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
				if (extraLegends.Count > 0)
					field = s.CardsIn(id, ZoneType.Battlefield).ToList();
			}

			var beforeAttachments = s;
			s = Rules.Attachments.Check(s, id); // CR 704.5m/n
			if (!ReferenceEquals(s, beforeAttachments))
				field = s.CardsIn(id, ZoneType.Battlefield).ToList();

			// CR 704.5q: +1/+1 and -1/-1 counters on one permanent cancel in pairs.
			foreach (var both in field)
				if (both.PlusOneCounters > 0 && both.MinusOneCounters > 0)
				{
					var pairs = Math.Min(both.PlusOneCounters, both.MinusOneCounters);
					s = s.UpdateObject(
						both.Id,
						both with
						{
							PlusOneCounters = both.PlusOneCounters - pairs,
							MinusOneCounters = both.MinusOneCounters - pairs,
						}
					);
				}

			// CR 704.5d: a token anywhere but the battlefield ceases to exist.
			foreach (var zone in OffTheBattlefield)
			{
				List<Card>? gone = null;
				foreach (var c in s.CardsIn(id, zone))
					if (c.IsToken || Rules.Prepare.IsDeadCopy(s, c))
						(gone ??= []).Add(c);
				if (gone is not null)
					foreach (var card in gone)
						s = s.RemoveObject(card.Id);
			}
		}
		return s;
	}

	static readonly ZoneType[] OffTheBattlefield =
	[
		ZoneType.Graveyard,
		ZoneType.Exile,
		ZoneType.Hand,
		ZoneType.Library,
	];

	/// <summary>Lethal: no toughness left, or damage enough to destroy it (or any from deathtouch) — 0 toughness kills even the indestructible.</summary>
	static bool IsLethal(GameState s, Card c)
	{
		var toughness = s.Toughness(c);
		return toughness <= 0
			|| (
				!s.IsIndestructible(c)
				&& (c.Damage >= toughness || (c.DamagedByDeathtouch && c.Damage > 0))
			);
	}
}
