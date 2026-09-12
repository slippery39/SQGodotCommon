using DoomCore;
using ImmutableGameObjects;

namespace DoomConsole;

/// <summary>
/// Draws a battle. Presentation only — it reads state and prints, and never decides anything.
/// Every question it asks goes through DoomStateExtensions or DoomPreviewer.
/// </summary>
public static class Renderer
{
	public static void DrawBattle(GameState state, Run run)
	{
		var battle = state.GetBattle();
		var player = state.GetPlayer();

		Console.WriteLine();
		Console.WriteLine(new string('=', 72));

		DrawDoomBanner(state, run, battle);

		Console.WriteLine(new string('-', 72));
		Console.WriteLine(
			$" Floor {run.Floor}   Life {player.Life}/{player.MaxLife}   Energy {player.Energy}/{player.MaxEnergy}   Turn {battle.TurnNumber}"
		);
		Console.WriteLine(new string('-', 72));

		DrawEnemies(state);
		DrawField(state);
		DrawHand(state);

		Console.WriteLine(new string('=', 72));
	}

	/// <summary>
	/// The countdown and what it will do to the deck.
	///
	/// **Always shown.** Certainty is permission to show the player everything — the doom cannot be
	/// prevented, so hiding it buys no tension and only stops the player playing around it. This is
	/// the console's stand-in for the GO SPINNY dial, and both read the same preview.
	/// </summary>
	private static void DrawDoomBanner(GameState state, Run run, DoomBattle battle)
	{
		var preview = DoomPreviewer.Preview(run, state);

		Console.WriteLine(
			$" *** {battle.Scenario.ToString().ToUpperInvariant()} IN {battle.CountdownRemaining} "
				+ $"TURN{(battle.CountdownRemaining == 1 ? "" : "S")} ***   {preview.Flavour}"
		);
		Console.WriteLine($"   If it landed now: {preview.Summary}");

		foreach (var card in preview.Removed.Take(6))
			Console.WriteLine($"     - LOSE   {card.Name} ({Stats(card)})");
		if (preview.Removed.Count > 6)
			Console.WriteLine($"     - ...and {preview.Removed.Count - 6} more");

		foreach (var card in preview.Added.Take(6))
			Console.WriteLine($"     + GAIN   {card.Name} ({Stats(card)})");
		if (preview.Added.Count > 6)
			Console.WriteLine($"     + ...and {preview.Added.Count - 6} more");

		foreach (var change in preview.Changed.Take(6))
			Console.WriteLine(
				$"     ~ CHANGE {change.Before.Name} {Stats(change.Before)} -> {Stats(change.After)}"
			);
		if (preview.Changed.Count > 6)
			Console.WriteLine($"     ~ ...and {preview.Changed.Count - 6} more");
	}

	private static string Stats(RunCard card) =>
		card.IsUnit ? $"{card.Power}/{card.Toughness}" : "rite";

	private static void DrawEnemies(GameState state)
	{
		Console.WriteLine(" ENEMIES");
		var enemies = state.LivingEnemies().ToList();

		if (enemies.Count == 0)
		{
			Console.WriteLine("   (none left — the countdown does not care)");
			return;
		}

		foreach (var enemy in enemies)
		{
			var intent =
				enemy.Intent == IntentKind.Attack
					? $"about to hit for {enemy.IntentAmount}"
					: "waiting";
			Console.WriteLine(
				$"   [{enemy.Id}] {enemy.Name}  {enemy.Health}/{enemy.MaxHealth} hp  — {intent}"
			);
		}
	}

	private static void DrawField(GameState state)
	{
		Console.WriteLine(" YOUR FIELD");
		var units = state.Units().ToList();

		if (units.Count == 0)
		{
			Console.WriteLine("   (empty)");
			return;
		}

		foreach (var card in units)
		{
			var unit = card.Unit();
			var order = unit.Assignment switch
			{
				Assignment.Attack => $"ATTACK -> [{unit.AssignedEnemyId}]",
				Assignment.Block => $"BLOCK  -> [{unit.AssignedEnemyId}]",
				_ => "unassigned",
			};
			var hurt = unit.Damage > 0 ? $" (damaged {unit.Damage})" : "";
			Console.WriteLine(
				$"   [{card.Id}] {card.Name}  {unit.Power}/{unit.RemainingToughness}{hurt}  — {order}"
			);
		}
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
			var tags = card.Tags.IsEmpty ? "" : $" [{string.Join(",", card.Tags)}]";
			var affordable = card.Cost <= energy ? " " : "x";
			Console.WriteLine(
				$"  {affordable}[{card.Id}] {card.Name} — cost {card.Cost}, {stats}{tags}"
			);
		}
	}

	public static void DrawHelp()
	{
		Console.WriteLine();
		Console.WriteLine("  p <cardId>              play a card from hand");
		Console.WriteLine("  a <unitId> <enemyId>    attack — kills sooner, removes future damage");
		Console.WriteLine(
			"  b <unitId> <enemyId>    block — absorbs damage now, excess still hits you"
		);
		Console.WriteLine("  e                       end turn (the countdown ticks)");
		Console.WriteLine("  d                       show the deck as it stands");
		Console.WriteLine("  ?                       this help");
		Console.WriteLine("  q                       quit");
		Console.WriteLine();
		Console.WriteLine("  A unit ATTACKS or BLOCKS, never both. Blocking reduces damage, never");
		Console.WriteLine("  prevents it. The countdown cannot be stopped.");
		Console.WriteLine();
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
				IrradiatedDrawnEvent i =>
					$"  ! drawing {i.CardName} cost 1 life — {i.LifeRemaining} left",
				CountdownTickedEvent c => $"  . countdown {c.Remaining}",
				DoomResolvedEvent d =>
					$"  *** {d.Scenario.ToString().ToUpperInvariant()} LANDS ***",
				PlayerDiedEvent => "  *** YOU DIED ***",
				_ => null,
			};

			if (line is not null)
				Console.WriteLine(line);
		}
	}
}
