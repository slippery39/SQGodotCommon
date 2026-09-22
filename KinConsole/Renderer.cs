using ImmutableGameObjects;
using KinCore;

namespace KinConsole;

/// <summary>
/// Draws a battle. Presentation only — it reads state and prints, and never decides anything.
/// Every question it asks goes through KinStateExtensions or KinPreviewer.
/// </summary>
public static class Renderer
{
	public static void DrawBattle(GameState state, Run run)
	{
		var battle = state.GetBattle();
		var player = state.GetPlayer();

		Console.WriteLine();
		Console.WriteLine(new string('=', 72));

		Console.WriteLine(new string('-', 72));
		Console.WriteLine(
			$" Floor {run.Floor}   Life {player.Life}/{player.MaxLife}   Energy {player.Energy}/{player.MaxEnergy}   Turn {battle.TurnNumber}"
		);
		Console.WriteLine(new string('-', 72));

		DrawLanes(state);
		DrawHand(state);

		Console.WriteLine(new string('=', 72));
	}

	private static string Stats(RunCard card) =>
		card.IsUnit ? $"{card.Power}/{card.Toughness}" : "rite";

	/// <summary>
	/// The board as FIVE LANES, because a lane is the matchup and the matchup is the whole game.
	/// Enemy above, your unit below, one column each. Two separate lists would hide the only thing
	/// the player actually decides.
	/// </summary>
	private static void DrawLanes(GameState state)
	{
		var opponent = state.GetOpponent();
		Console.WriteLine(
			$" OPPONENT  {opponent.Health}/{opponent.MaxHealth} hp   <- kill this to win"
		);

		var header = "  ";
		var enemyRow = "  ";
		var unitRow = "  ";

		for (var lane = 0; lane < KinBattle.LaneCount; lane++)
		{
			var enemy = state.EnemyInLane(lane);
			var card = state.UnitInLane(lane);

			header += Cell($"L{lane}");

			enemyRow += Cell(enemy is null ? "-" : $"{Short(enemy.Name)} {enemy.Health}hp");

			unitRow += Cell(
				card is null
					? "-"
					: $"{(card.HasComponent<CompanionComponent>() ? "@" : "")}{Short(card.Name)} {card.Unit().Power}/{card.Unit().RemainingToughness}"
			);
		}

		Console.WriteLine(header);
		Console.WriteLine(enemyRow + "   <- them");

		if (opponent.NextSummon is { } coming)
			Console.WriteLine(
				$"   incoming: {coming.Name} {coming.Health}hp/{coming.Attack}atk into L{coming.Lane} at end of turn"
			);

		Console.WriteLine(unitRow + "   <- you");

		// The damage YOUR uncontested lanes will land on the Opponent. Same reasoning as the
		// incoming figure below: making the player add it up per lane turns the game into a chore.
		var outgoing = 0;
		for (var lane = 0; lane < KinBattle.LaneCount; lane++)
			if (state.EnemyInLane(lane) is null && state.UnitInLane(lane) is { } free)
				outgoing += free.Unit().Power;

		if (outgoing > 0)
			Console.WriteLine($"   your open lanes will hit the Opponent for {outgoing}");

		// The damage an OPEN lane will let through. This is the number the player is actually
		// playing against, and making them add it up per enemy is how a lane game becomes a chore.
		var incoming = 0;
		foreach (var lane in state.OpenLanes())
		{
			var enemy = state.EnemyInLane(lane);
			if (enemy is { Intent: IntentKind.Attack })
				incoming += enemy.IntentAmount;
		}

		if (incoming > 0)
			Console.WriteLine($"   open lanes will cost you {incoming} life this turn");
	}

	private static string Cell(string s) => s.PadRight(13)[..13];

	/// <summary>
	/// A name that fits a lane column. Trims the punctuation a cut leaves dangling — the companion
	/// renders as "Ash — Glowing, Barnacled", and a blind 6-char slice showed "Ash — ".
	/// </summary>
	private static string Short(string name)
	{
		var cut = name.Length <= 6 ? name : name[..6];
		return cut.TrimEnd(' ', '-', '—', ',');
	}

	private static void DrawHand(GameState state)
	{
		Console.WriteLine(" HAND");
		var hand = state.CardsIn(ZoneType.Hand).ToList();

		if (hand.Count == 0)
		{
			Console.WriteLine("   (empty)");
			return;
		}

		var energy = state.GetPlayer().Energy;
		foreach (var card in hand)
		{
			var unit = card.GetComponent<UnitComponent>();
			var stats = unit is null ? "rite" : $"{unit.Power}/{unit.Toughness}";
			var affordable = card.Cost <= energy ? " " : "x";
			Console.WriteLine($"  {affordable}[{card.Id}] {card.Name} — cost {card.Cost}, {stats}");
		}
	}

	public static void DrawHelp()
	{
		Console.WriteLine();
		Console.WriteLine("  p <cardId> <lane>       play a card into a lane (0-4)");
		Console.WriteLine("  e                       end turn (the countdown ticks)");
		Console.WriteLine("  d                       show the deck as it stands");
		Console.WriteLine("  c                       show your companion and its marks");
		Console.WriteLine("  ?                       this help");
		Console.WriteLine("  q                       quit");
		Console.WriteLine();
		Console.WriteLine(
			"  Combat is automatic. A unit fights whatever shares its lane, both ways."
		);
		Console.WriteLine("  It absorbs up to its toughness and the EXCESS hits you; an open lane");
		Console.WriteLine(
			"  costs you the enemy's full attack. The doom fires on its clock, over and"
		);
		Console.WriteLine("  over, and never ends the battle — only killing them does.");
		Console.WriteLine();
	}

	/// <summary>
	/// The companion between battles. This is where TAG ALONG pays off — the marks are the run's
	/// history written on the one thing that survived it, so show them plainly.
	/// </summary>
	public static void DrawCompanion(Run run)
	{
		var c = run.Companion;
		Console.WriteLine();
		Console.WriteLine($" COMPANION  {c.Name}  {c.Power}/{c.Toughness}");

		// **It used to list the marks it had collected, and marks are cut.** What is worth printing
		// now is what it DOES — the ability is the whole of a companion's identity.
		foreach (var effect in c.Effects)
			Console.WriteLine($"   {effect.Text}");
	}

	public static void DrawDeck(Run run)
	{
		Console.WriteLine();
		Console.WriteLine($" DECK ({run.Deck.Count} cards)");
		foreach (var group in run.Deck.GroupBy(c => (c.Name, c.Power, c.Toughness, c.Cost)))
			Console.WriteLine(
				$"   {group.Count()}x {group.Key.Name} — cost {group.Key.Cost}, "
					+ $"{group.Key.Power}/{group.Key.Toughness}"
			);
		Console.WriteLine();
	}

	public static void DrawEvents(IEnumerable<GameEvent> events)
	{
		foreach (var e in events)
		{
			var line = e switch
			{
				PlayerDamagedEvent d => d.Absorbed > 0
					? $"  ! took {d.Amount} ({d.Absorbed} absorbed) — {d.LifeRemaining} life left"
					: $"  ! took {d.Amount} — {d.LifeRemaining} life left",
				UnitDiedEvent u => $"  + {u.CardName} died",
				EnemyDiedEvent x => $"  + {x.EnemyName} is dead",
				EnemySummonedEvent s => $"  < {s.EnemyName} drops into L{s.Lane}",
				EnemyTelegraphedEvent g => $"  ~ they are bringing up {g.EnemyName} for L{g.Lane}",
				OpponentDamagedEvent o =>
					$"  > hit the Opponent for {o.Amount} — {o.HealthRemaining} left",
				OpponentDefeatedEvent => "  *** THE OPPONENT IS DOWN — you win the battle ***",
				PlayerDiedEvent => "  *** YOU DIED ***",
				_ => null,
			};

			if (line is not null)
				Console.WriteLine(line);
		}
	}
}
