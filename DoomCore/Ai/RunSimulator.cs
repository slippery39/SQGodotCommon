using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>One battle, as the sim recorded it. Everything a balance question asks about a floor.</summary>
public record FloorResult
{
	public int Floor { get; init; }
	public string Scenario { get; init; } = "";
	public string Opponent { get; init; } = "";
	public int Turns { get; init; }
	public int LifeBefore { get; init; }
	public int LifeAfter { get; init; }
	public int DoomsFired { get; init; }
	public int DeckSize { get; init; }

	/// <summary>Cleared, Died, or Stalled.</summary>
	public string Outcome { get; init; } = "";

	public int LifeLost => LifeBefore - LifeAfter;
}

public record RunResult
{
	public int Seed { get; init; }

	/// <summary>The last floor the run stood on. 20 means it walked out the other side.</summary>
	public int FloorReached { get; init; }

	public bool ActComplete { get; init; }
	public string EndReason { get; init; } = "";
	public int TotalTurns { get; init; }

	public ImmutableList<FloorResult> Floors { get; init; } = ImmutableList<FloorResult>.Empty;

	/// <summary>Reward cards taken, in order. **The only unbiased handle on card value** — see RunSimulator.</summary>
	public ImmutableList<string> TakenRewards { get; init; } = ImmutableList<string>.Empty;

	public ImmutableList<string> FinalDeck { get; init; } = ImmutableList<string>.Empty;
}

/// <summary>
/// Plays a whole run with <see cref="DoomBot"/> and records what happened.
///
/// It mirrors the GODOT flow, not the console's: battle, `AfterBattle`, then a reward pick from
/// `RewardsFor(seed, after.Floor)`. `DoomConsole` has never offered rewards, so a run played there
/// is strictly harder than the real game and its numbers would understate every floor.
///
/// **Rewards are picked at RANDOM, deliberately.** A greedy picker would make the per-card table
/// measure the picker's taste rather than the card's worth, and with a flat pool it would barely
/// outplay random anyway. Once the table exists, a picker that reads it is the obvious next step.
/// </summary>
public static class RunSimulator
{
	/// <summary>
	/// A battle nobody can finish. Reachable: an Opponent that heals faster than a stalled board
	/// can hit it. Recorded as Stalled rather than looping forever — and a Stalled floor in the
	/// results is itself a balance finding, not just a guard.
	/// </summary>
	public const int MaxTurnsPerBattle = 60;

	public static RunResult Play(int seed, DoomEvalWeights? weights = null)
	{
		var w = weights ?? new DoomEvalWeights();
		var run = StarterContent.NewRun(seed);
		var picker = new Random(seed);

		var floors = ImmutableList.CreateBuilder<FloorResult>();
		var taken = ImmutableList.CreateBuilder<string>();
		var totalTurns = 0;
		var stalled = false;

		while (!run.IsOver && !stalled)
		{
			// A rest floor is not a battle and records no FloorResult, so the survival table simply
			// has no row for floors 4, 8, 12 and 16. That absence is the honest reading.
			if (StarterContent.FloorKindFor(run.Floor) == FloorKind.Rest)
			{
				run = run.Rest(StarterContent.RestHealFor(run.MaxLife));
				continue;
			}

			var scenario = StarterContent.ScenarioFor(seed, run.Floor);
			var (state, _) = run.StartBattle(
				scenario,
				StarterContent.CountdownFor(scenario),
				StarterContent.EnemiesFor(run.Floor, seed),
				opponent: StarterContent.OpponentFor(run.Floor, seed)
			);

			var turns = 0;
			while (!state.GetBattle().IsOver && turns < MaxTurnsPerBattle)
			{
				state = DoomBot.PlayTurn(state, run, w);
				(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();
				turns++;
			}

			totalTurns += turns;
			var battle = state.GetBattle();
			stalled = !battle.IsOver;

			// A stalled battle never ends, so it never resolves. `AfterBattle` on one would apply
			// the doom transforms AND advance the floor for a fight that is still going.
			var after = stalled
				? run with
				{
					Life = state.GetPlayer().Life,
				}
				: run.AfterBattle(state);

			floors.Add(
				new FloorResult
				{
					Floor = run.Floor,
					Scenario = scenario.ToString(),
					Opponent = StarterContent.OpponentFor(run.Floor, seed).Name,
					Turns = turns,
					LifeBefore = run.Life,
					LifeAfter = after.Life,
					DoomsFired = battle.DoomsFired,
					DeckSize = run.Deck.Count,
					Outcome =
						stalled ? "Stalled"
						: battle.OpponentDefeated ? "Cleared"
						: "Died",
				}
			);

			run = after;

			if (stalled || run.IsOver)
				break;

			// Mirrors DoomBoard.ResolveBattle: the rewards offered are the ones for the floor you
			// are about to walk into, not the one you just cleared.
			var rewards = StarterContent.RewardsFor(seed, run.Floor);
			if (!rewards.IsEmpty)
			{
				var pick = rewards[picker.Next(rewards.Length)];
				taken.Add(pick.Name);
				run = run.WithCard(pick);
			}
		}

		return new RunResult
		{
			Seed = seed,
			// A death leaves the floor where it fell — `AfterBattle` only advances it on a clear —
			// so this is already the floor the run reached. The clamp is for the act being walked
			// out of, where Floor is one past the last one that existed.
			FloorReached = Math.Min(run.Floor, Run.ActLength),
			ActComplete = run.IsActComplete,
			EndReason = stalled ? $"Stalled after {MaxTurnsPerBattle} turns" : run.OverReason,
			TotalTurns = totalTurns,
			Floors = floors.ToImmutable(),
			TakenRewards = taken.ToImmutable(),
			FinalDeck = [.. run.Deck.Select(c => c.Name)],
		};
	}

	/// <summary>
	/// Plays seeds 1..count. **Parallel.For into a pre-allocated array, not PLINQ** — same speed
	/// and the results stay in seed order, so two sim files diff line for line.
	/// </summary>
	public static RunResult[] PlayMany(int count, DoomEvalWeights? weights = null)
	{
		var results = new RunResult[count];
		Parallel.For(0, count, i => results[i] = Play(i + 1, weights));
		return results;
	}
}
