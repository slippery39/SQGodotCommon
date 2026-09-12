using DoomCore;
using ImmutableGameObjects;

namespace DoomConsole;

/// <summary>
/// Terminal front end. This is the REMOTE surface: it needs no Godot, so a battle can be played
/// and a rules change tested from anywhere `dotnet run` works.
///
/// Presentation only. It never modifies state directly and never constructs an action the player
/// did not ask for — everything goes through DoomStateExtensions.
/// </summary>
public static class Program
{
	public static void Main(string[] args)
	{
		var seed = args.Length > 0 && int.TryParse(args[0], out var s) ? s : Environment.TickCount;

		Console.WriteLine();
		Console.WriteLine("  DOOMJAM — you cannot stop it, only decide what it takes.");
		Console.WriteLine($"  seed {seed}   (pass it as an argument to replay this run)");
		Renderer.DrawHelp();

		var run = StarterContent.NewRun(seed);

		while (!run.IsOver)
		{
			var scenario = StarterContent.ScenarioFor(seed, run.Floor);
			var result = PlayBattle(run, scenario);

			if (result is null)
				return; // quit

			run = result;

			if (run.IsOver)
				break;

			Console.WriteLine();
			Console.WriteLine(
				$"  Survived. Floor {run.Floor}/{Run.ActLength}, deck {run.Deck.Count} cards, {run.Life} life."
			);
			Renderer.DrawCompanion(run);
			Renderer.DrawDeck(run);
		}

		Console.WriteLine();
		Console.WriteLine(
			$"  The run ended on floor {run.Floor} of {Run.ActLength}. {run.OverReason}"
		);
		Console.WriteLine();
	}

	/// <summary>Returns the run after the battle, or null if the player quit.</summary>
	private static Run? PlayBattle(Run run, DoomScenario scenario)
	{
		var (state, events) = run.StartBattle(
			scenario,
			StarterContent.CountdownFor(scenario),
			[StarterContent.EnemyFor(run.Floor)]
		);
		Renderer.DrawEvents(events);

		while (!state.GetBattle().IsOver)
		{
			Renderer.DrawBattle(state, run);
			Console.Write("> ");

			var input = Console.ReadLine();
			if (input is null)
				return null;

			var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
				continue;

			switch (parts[0].ToLowerInvariant())
			{
				case "q":
					return null;

				case "?":
					Renderer.DrawHelp();
					break;

				case "d":
					Renderer.DrawCompanion(run);
					Renderer.DrawDeck(run);
					break;

				case "p":
					state = Submit(state, parts, ids => new PlayCardAction { CardId = ids[0] }, 1);
					break;

				case "a":
					state = Submit(
						state,
						parts,
						ids => new AssignAction
						{
							UnitId = ids[0],
							EnemyId = ids[1],
							Assignment = Assignment.Attack,
						},
						2
					);
					break;

				case "b":
					state = Submit(
						state,
						parts,
						ids => new AssignAction
						{
							UnitId = ids[0],
							EnemyId = ids[1],
							Assignment = Assignment.Block,
						},
						2
					);
					break;

				case "e":
					(state, var turnEvents) = state
						.AddAction(new EndTurnAction())
						.ProcessAllActions();
					Renderer.DrawEvents(turnEvents);
					break;

				default:
					Console.WriteLine("  ? for help");
					break;
			}
		}

		return run.AfterBattle(state);
	}

	/// <summary>
	/// Parses ids and submits through TryAddAction, so a rejected action prints the engine's own
	/// reason rather than silently doing nothing — a click that does nothing is the worst bug a
	/// card game front end can have.
	/// </summary>
	private static GameState Submit(
		GameState state,
		string[] parts,
		Func<int[], GameAction> build,
		int expectedIds
	)
	{
		if (parts.Length < expectedIds + 1)
		{
			Console.WriteLine($"  needs {expectedIds} id(s)");
			return state;
		}

		var ids = new int[expectedIds];
		for (var i = 0; i < expectedIds; i++)
		{
			if (!int.TryParse(parts[i + 1], out ids[i]))
			{
				Console.WriteLine($"  '{parts[i + 1]}' is not an id");
				return state;
			}
		}

		var action = build(ids);
		var validation = action.ValidateAdd(state);
		if (!validation.IsValid)
		{
			Console.WriteLine($"  can't: {validation.Reason}");
			return state;
		}

		var (next, events) = state.AddAction(action).ProcessAllActions();
		Renderer.DrawEvents(events);
		return next;
	}
}
