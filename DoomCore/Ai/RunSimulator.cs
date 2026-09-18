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

	/// <summary>Which apocalypse this run chose to live through.</summary>
	public string Theme { get; init; } = "";

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
	/// Extra tickets in the reward bag that mean "take nothing", so the bot declines roughly one
	/// offer in four. **A guess, and the only guess in this file** — a real player declines far more
	/// deliberately than this, weighing the card against a deck they can picture.
	///
	/// It exists because taking a card after EVERY battle is not neutral: it is the most bloated
	/// deck the game can produce, and it was silently the baseline for runs 1-18.
	/// </summary>
	private const int SkipWeight = 1;

	/// <summary>
	/// A battle nobody can finish. Reachable: an Opponent that heals faster than a stalled board
	/// can hit it. Recorded as Stalled rather than looping forever — and a Stalled floor in the
	/// results is itself a balance finding, not just a guard.
	/// </summary>
	public const int MaxTurnsPerBattle = 60;

	/// <summary>
	/// Spends gold at a shop. **A deliberately crude priority, and the second guess in this file.**
	///
	/// Thinning first, because combat v3 makes the deck the whole of your per-turn output and the
	/// worst card in a deck you draw five from every turn is doing active harm. Then healing when
	/// badly hurt, then a card if there is still gold.
	///
	/// It removes the cheapest-looking card by the same crude `CardValue` the evaluator uses, which
	/// is a stat sum and not a measurement. **This is a floor on how well shopping can go, not a
	/// model of how a player shops** — a picker that reads the measured card table would replace it
	/// and would move every number that follows.
	/// </summary>
	private static Run Shop(Run run, int seed)
	{
		var offer = StarterContent.ShopFor(run.Theme, seed, run.Floor, run.CardsRemoved);

		if (run.Deck.Count > Run.MinDeckSize && run.Gold >= offer.RemovalPrice)
		{
			var worst = run.Deck.OrderBy(Worth).First();
			run = run.RemoveCard(worst.RunCardId, offer.RemovalPrice);
		}

		if (run.Life < run.MaxLife / 2 && run.Gold >= offer.HealPrice)
			run = run.BuyHeal(offer.HealPrice, offer.HealAmount);

		if (!offer.Cards.IsEmpty && run.Gold >= offer.CardPrice)
			run = run.BuyCard(offer.Cards.MaxBy(Worth)!, offer.CardPrice);

		return run with
		{
			Floor = run.Floor + 1,
		};

		static int Worth(RunCard card) =>
			card.IsUnit ? card.Power + card.Toughness - card.Cost * 4 : 12 - card.Cost * 4;
	}

	public static RunResult Play(int seed, DoomEvalWeights? weights = null)
	{
		var w = weights ?? new DoomEvalWeights();

		// **Every run now plays all three acts, in order.** Themes used to be dealt round-robin
		// across seeds so one `sim N` covered every act; a run IS every act now, so the theme is a
		// question about which floor you are standing on. Seed still decides enemies, rewards and
		// Opponent traits.
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
			var kind = StarterContent.FloorKindFor(run.Floor);

			if (kind == FloorKind.Rest)
			{
				run = run.Rest(StarterContent.RestHealFor(run.MaxLife));
				continue;
			}

			if (kind == FloorKind.Shop)
			{
				run = Shop(run, seed);
				continue;
			}

			// An event floor is walked past because events do not exist. None are placed in
			// `ActMap.Layout` either, so this is unreachable today and is here so that adding one
			// cannot silently behave like a rest.
			if (kind == FloorKind.Event)
			{
				run = run with { Floor = run.Floor + 1 };
				continue;
			}

			var scenario = StarterContent.ScenarioFor(run.Theme, run.Floor);
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
			var rewards = StarterContent.RewardsFor(run.Theme, seed, run.Floor);
			if (!rewards.IsEmpty)
			{
				// **SKIPPING IS A LEGAL MOVE AND THE BOT USED TO NEVER MAKE IT.** The Godot reward
				// screen has always offered it — `DoomIntermission.OfferRewards` says so out loud —
				// but this loop took a card after every single battle, so every number ever
				// measured described a deck growing by one card a floor with no declines. That is
				// the WORST case a player can construct, not the one they play.
				//
				// It matters far more under combat v3: you draw five a turn and the deck IS your
				// per-turn output, so a card you would not play is actively crowding out one you
				// would. It matters more again once acts chain, where never declining would build
				// a deck of fifty-plus that nobody would ever own.
				//
				// **Still deliberately not a GREEDY picker.** Random-with-a-skip keeps the per-card
				// table measuring cards rather than the picker's taste — see the class comment.
				// The skip rate is a flat guess, not a strategy; a picker that reads the measured
				// table is the obvious next step and would replace both.
				var pick = picker.Next(rewards.Length + SkipWeight);
				if (pick < rewards.Length)
				{
					taken.Add(rewards[pick].Name);
					run = run.WithCard(rewards[pick]);
				}
			}
		}

		return new RunResult
		{
			Seed = seed,

			// The act the run ENDED in, which is what a survival table wants to group by. It is no
			// longer a property of the run — every run walks all three.
			Theme = ThemeLibrary.Of(ActMap.ThemeFor(run.Floor)).Name,
			// A death leaves the floor where it fell — `AfterBattle` only advances it on a clear —
			// so this is already the floor the run reached. The clamp is for the act being walked
			// out of, where Floor is one past the last one that existed.
			FloorReached = Math.Min(run.Floor, Run.RunLength),
			ActComplete = run.IsActComplete,
			EndReason = stalled ? $"Stalled after {MaxTurnsPerBattle} turns" : run.OverReason,
			TotalTurns = totalTurns,
			Floors = floors.ToImmutable(),
			TakenRewards = taken.ToImmutable(),
			FinalDeck = [.. run.Deck.Select(c => c.Name)],
		};
	}

	/// <summary>
	/// Plays seeds 1..count. **Into a pre-allocated array, not PLINQ** — the results stay in seed
	/// order, so two sim files diff line for line.
	///
	/// **The bottleneck here is the GC, not this loop.** Run cost varies enormously once an act is
	/// winnable, so range partitioning looked like the culprit — it was not: chunking seeds one at
	/// a time measured 3.6x -> 3.1x of 16 logical cores, i.e. nothing. The sim is ALLOCATION bound,
	/// because `ImmutableGameObjects` rebuilds a GameState for every action and `DoomBot` simulates
	/// a whole turn per candidate line. Server GC in `DoomConsole.csproj` took the same 300-run
	/// workload from 693s to 224s; this loop was never the problem.
	/// </summary>
	public static RunResult[] PlayMany(int count, DoomEvalWeights? weights = null)
	{
		var results = new RunResult[count];
		Parallel.For(0, count, i => results[i] = Play(i + 1, weights));
		return results;
	}
}
