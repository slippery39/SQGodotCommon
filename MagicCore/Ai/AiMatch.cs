using System.Collections.Concurrent;
using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Sets.Fra;

namespace MagicCore.Ai;

/// <summary>
/// Two AIs against each other over every ordered FRA deck pairing, every game played twice with the
/// AIs swapping seats — the same decks, seats, seed and coin flip, so only the AI differs. Every
/// strength claim about a MagicCore AI comes from here, recorded in docs/findings/magic-ai.md.
///
/// Results are deterministic for the seeds; only the timings depend on the machine. Games run in
/// parallel unless <c>sequential</c>, each search on ONE thread so the games fill the cores (AI-6.2) —
/// the results and the cost count are the same either way, and the clock is not: parallel games
/// share the CPU unevenly, so only <c>sequential</c> timings are evidence.
/// </summary>
public static class AiMatch
{
	/// <summary>Past this many decisions a game is unfinished — it counts as a draw and is reported.</summary>
	public const int DecisionLimit = 20_000;

	/// <summary>The AIs a match can name that aren't the search — with <see cref="Searches"/>, the one list.</summary>
	static readonly Dictionary<string, Func<int, int, IAi>> Others =
		new()
		{
			["greedy"] = (player, _) => new GreedyAi(player),
			["random"] = (player, seed) => new RandomAi(player, seed * 31 + player),
		};

	/// <summary>
	/// The searches a match can name, by their settings — beside "beam+…" and "beam-held=…". Factories,
	/// not values: a search's held values load the card-value table (BeamSearchAi.DefaultRollout), which
	/// must happen only where a search is wanted, not whenever AiMatch is first touched.
	/// </summary>
	static readonly Dictionary<string, Func<BeamSearchAi.Settings>> Searches =
		new()
		{
			// The search as it plays: held cards at half their measured value (AI-1).
			["beam"] = () => new(),
			// The search before held values, for A/B runs.
			["beam-plain"] = () => new BeamSearchAi.Settings().WithoutHeld(),
			// The search whose simulated opponent casts from its real hand (AI-3.1) — beam+casts.
			["beam-casts"] = () =>
				Combined("beam+casts")
				?? throw new InvalidOperationException("The 'casts' model was renamed"),
		};

	/// <summary>
	/// "beam+casts+shuffle": the search with each named simulation model switched on (story AI-3) —
	/// the one list, so a combination needs no name of its own.
	/// </summary>
	static readonly Dictionary<string, Func<BeamSearchAi.Settings, BeamSearchAi.Settings>> Models =
		new()
		{
			// casts and responds play the WHOLE hand; the default plays only what we know (AI-10.4)
			["casts"] = Opponent(Rollout.OpponentModel.CastsFromHand),
			// It also responds at instant speed in our turn (AI-3.6) — casts from hand too: beam+responds.
			["responds"] = Opponent(Rollout.OpponentModel.Responds),
			// The board-only opponent, never casting even what we know — before AI-10.4: beam+boardonly.
			["boardonly"] = Opponent(Rollout.OpponentModel.BoardOnly),
			// The library: honest by default since AI-3.9 (shuffle, kept as a name); the real order — the
			// search sees its own future draws, 5pp or more stronger — is beam+realorder.
			["shuffle"] = o => o with { Library = Hidden.Library.ShuffleUnknown },
			["realorder"] = o => o with { Library = Hidden.Library.RealOrder },
			// The evaluator without its threat term (AI-5.2) — the search before it: beam+nothreat.
			["nothreat"] = o => o with { Without = o.Without | Evaluator.Terms.Threat },
			// Without the six keywords AI-5.3 added to a creature's value: beam+nokeywords.
			["nokeywords"] = o => o with { Without = o.Without | Evaluator.Terms.MoreKeywords },
			// The look-ahead's safe block counting damage once, before AI-8.3: beam+oldblocks.
			["oldblocks"] = o => o with { Without = o.Without | Evaluator.Terms.BlockSurvival },
			// The look-ahead's defender never gang-blocks, before AI-15: beam+nogang.
			// The simulated opponent has no trick in its own combat, before AI-17.10: beam+notheircombat.
			["notheircombat"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.TheirCombatTricks,
				},
			// A simulated attacker judged against the look-ahead's weaker defender, before AI-19.21: beam+nodefender.
			["nodefender"] = o => o with { Without = o.Without | Evaluator.Terms.TrueDefender },
			// A surveil on the stack is priced at nothing, before AI-17.11: beam+nosurveilprice.
			["nosurveilprice"] = o => o with { Without = o.Without | Evaluator.Terms.SurveilPrice },
			// The search may pass with a land to play, before AI-13.7: beam+nolanddrop.
			["nolanddrop"] = o => o with { Without = o.Without | Evaluator.Terms.LandDrop },
			// A Spacecraft's charge counted past its station threshold, before 2026-11-09: beam+nostationcap.
			["nostationcap"] = o => o with { Without = o.Without | Evaluator.Terms.StationCap },
			// A land in hand at a flat 0.3 whatever it unlocks, before AI-9.1: beam+nolandunlock.
			["nolandunlock"] = o => o with { Without = o.Without | Evaluator.Terms.LandUnlock },
			// A surveil decided by the rollout alone, lands never binned, before AI-17.20: beam+nosurveillands.
			["nosurveillands"] = o => o with { Without = o.Without | Evaluator.Terms.SurveilLands },
			// A surveil keeps every spell and branches it by rollout, before the spells rule: beam+nosurveilspells.
			["nosurveilspells"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.SurveilSpells,
				},
			// "Exile the top card, you may play it" priced as the sample's card, before 2026-11-09: beam+noimpulsevalue.
			["noimpulsevalue"] = o => o with { Without = o.Without | Evaluator.Terms.ImpulseValue },
			// A held card priced as castable however few lands we have, before AI-17.21: beam+nomanashort.
			["nomanashort"] = o => o with { Without = o.Without | Evaluator.Terms.ManaShort },
			// The simulated opponent never uses its permanents' abilities in its own turn, before AI-17.22: beam+notheirabilities.
			["notheirabilities"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.TheirAbilities,
				},
			// Only instants and flash held as options, before AI-13.10: beam+instantoptions.
			["instantoptions"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.TargetedOptions,
				},
			// Every stop passed costs a search level, as before AI-13.12 (part 1): beam+countpasses.
			["countpasses"] = o => o with { FreePasses = false },
			// Acting now wins a tie with the same move later this turn, as before AI-13.12 (part 2): beam+actnow.
			["actnow"] = o => o with { WaitOnTies = false },
			// Acting wins a tie by the pass bias, no potential score, before AI-13.11: beam+passbias.
			["passbias"] = o => o with { Without = o.Without | Evaluator.Terms.Potential },
			// Marked damage priced as lost toughness, no damage potential, before AI-19.22: beam+nodamagepotential.
			["nodamagepotential"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.DamagePotential,
				},
			// Burn at their face whenever it scores, before the burn rule (AI-17.6): beam+faceburn.
			["faceburn"] = o => o with { Without = o.Without | Evaluator.Terms.SaveBurn },
			// A held option's turns left read in each line, not at the root, before AI-13.9: beam+liveoptionturns.
			["liveoptionturns"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.RootOptionTurns,
				},
			// The next attack's clock reads this turn's grants (can't block, flying) as lasting, before AI-17.9: beam+thisturngrants.
			["thisturngrants"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.NextTurnGrants,
				},
			["nogang"] = o => o with { Without = o.Without | Evaluator.Terms.GangBlocks },
			// A finished game scores a flat win or loss, its board unread, as before AI-17.6: beam+flatend.
			["flatend"] = o => o with { Without = o.Without | Evaluator.Terms.EndBoard },
			// The look-ahead stops right after our own turn, their swing back unseen, as before AI-17.6: beam+nocounter.
			["nocounter"] = WithRollout(r => r with { CounterAttack = false }),
			// A fresh search at every decision, no plan committed and replayed (review item 9, AI-17.6): beam+noplans.
			["noplans"] = o => o with { CommitPlans = false },
			// The cheap look-ahead's chooser's best one level deep, targets and "you may" only, as before: beam+shallowsettle.
			["shallowsettle"] = o => o with { Without = o.Without | Evaluator.Terms.SettleDeep },
			// Every graveyard card worth nothing, even one that can return itself, as before AI-17.6: beam+deadgraveyard.
			["deadgraveyard"] = o =>
				o with
				{
					Without = o.Without | Evaluator.Terms.GraveyardAccess,
				},
			// Settle answers a target first-legal and declines every "you may", as before AI-17.6: beam+quicksettle.
			["quicksettle"] = o => o with { Without = o.Without | Evaluator.Terms.SettleChoices },
			// Life below 6 without the growing risk of being finished, as before AI-17.6: beam+flatlowlife.
			["flatlowlife"] = o => o with { Without = o.Without | Evaluator.Terms.LowLifeRisk },
			// The look-ahead's defender never chump-blocks, facing lethal or not, before AI-17.3: beam+nochump.
			["nochump"] = o => o with { Without = o.Without | Evaluator.Terms.ChumpBlocks },
			// The threat term without a trampler's excess over its blocker (AI-8.4): beam+notrample.
			["notrample"] = o => o with { Without = o.Without | Evaluator.Terms.Trample },
			// A planeswalker as a flat body and 0.35 a counter (before AI-8.2): beam+walkercount.
			["walkercount"] = o => o with { Without = o.Without | Evaluator.Terms.WalkerUse },
			// A planeswalker at half of one activation, not its lifetime (before AI-9.3): beam+walkernow.
			["walkernow"] = o => o with { Without = o.Without | Evaluator.Terms.WalkerLifetime },
			// A held instant or flash card at its plain held price, not as an option (before AI-13): beam+nooption.
			["nooption"] = o => o with { Without = o.Without | Evaluator.Terms.HoldOption },
			// Every card in the opponent's hand priced blind, known or not (before AI-9.5): beam+blindhand.
			["blindhand"] = o => o with { Without = o.Without | Evaluator.Terms.KnownHand },
			// GreedyAi plays our side in the rollouts (AI-3.7) — not "greedy", the AI: beam+greedyrollout.
			["greedyrollout"] = WithRollout(r => r with { Ours = Rollout.OurPolicy.Greedy }),
			// The rollout's combat (AI-3.8): an attack judged all-in as well as alone — beam+allin; a
			// target choice by its owner's best answer — the default since AI-17.3 (beam+firsttarget is the old).
			// Since AI-17.6 it REPLACES the look-ahead's whole attacks (the default), not adds to them.
			["allin"] = WithRollout(r => r with { Attacks = Rollout.Attacks.AllInToo }),
			// The search's tree declares a creature at a time, before AI-17.3: beam+eachalone.
			["eachalone"] = o => o with { WholeAttacks = false },
			// A choice of several picks gets GreedyAi's one answer, not every combination, as before AI-17.6: beam+onepick.
			["onepick"] = o => o with { BranchPicks = false },
			// The look-ahead judges each attacker alone, as before AI-17.6: beam+eachalonelookahead.
			["eachalonelookahead"] = WithRollout(r =>
				r with
				{
					Attacks = Rollout.Attacks.EachAlone,
				}
			),
			// The first legal target, as before AI-17.3: beam+firsttarget.
			["firsttarget"] = WithRollout(r => r with { Targets = Rollout.Targets.FirstLegal }),
			// The simulated opponent's planeswalkers idle unless a casting model judges them worth it —
			// before AI-8.1: beam+boardonly+idlewalkers (the default opponent casts, so its walkers act by our policy).
			["idlewalkers"] = WithRollout(r => r with { TheirWalkers = false }),
			// Won lines tie again, without the opponent's life lost this turn (before AI-10): beam+noprogress.
			["noprogress"] = WithRollout(r =>
				r with
				{
					Progress = Rollout.ProgressBy.None,
					LostLines = false,
				}
			),
			// A won line's progress is their life alone, not the turn's whole evaluation (before AI-16): beam+lifeprogress.
			["lifeprogress"] = WithRollout(r =>
				r with
				{
					Progress = Rollout.ProgressBy.TheirLife,
					LostLines = false,
				}
			),
			// Lost lines on one turn tie again, the board as our turn ends not counted (before AI-16.4): beam+nolostlines.
			["nolostlines"] = WithRollout(r => r with { LostLines = false }),
			// Any win found stops the search, however slow, as before AI-17.3: beam+anywin.
			["anywin"] = o => o with { WinNowOnly = false },
			// "You may" declined in the look-ahead, as before AI-17.2: beam+nomays.
			["nomays"] = WithRollout(r => r with { Mays = false }),
			// The opponent's unseen hand drawn again from its unseen cards (AI-3.4) — matters only
			// when the simulated opponent casts: beam+casts+unseen.
			["unseen"] = o => o with { Hand = Hidden.Hand.ShuffleUnseen },
			// Its unseen hand GUESSED from the colours it has shown, never its real cards (AI-3.5).
			["guess"] = o => o with { Hand = Hidden.Hand.GuessedFromColors },
		};

	static Func<BeamSearchAi.Settings, BeamSearchAi.Settings> Opponent(
		Rollout.OpponentModel model
	) => WithRollout(r => r with { Opponent = model, KnownOnly = false });

	/// <summary>A model that changes the rollout — from the search's own default (held values in).</summary>
	static Func<BeamSearchAi.Settings, BeamSearchAi.Settings> WithRollout(
		Func<Rollout.Settings, Rollout.Settings> change
	) => o => o with { RolloutSettings = change(o.RolloutSettings ?? BeamSearchAi.DefaultRollout) };

	static BeamSearchAi.Settings? Combined(string name) =>
		name.Split('+') is ["beam", .. var models]
		&& models.Length > 0
		&& models.All(Models.ContainsKey)
		// Only an opponent that plays its whole hand reads it: without casts or responds, unseen or guess measure nothing.
		&& (
			!(models.Contains("unseen") || models.Contains("guess"))
			|| models.Contains("casts")
			|| models.Contains("responds")
		)
		&& models.Count(m => m is "casts" or "responds" or "boardonly") <= 1 // one opponent model
		&& !(models.Contains("shuffle") && models.Contains("realorder")) // one library mode
		// GreedyAi makes all our rollout decisions: allin and firsttarget would reach only the opponent's.
		&& !(
			models.Contains("greedyrollout")
			&& (models.Contains("allin") || models.Contains("firsttarget"))
		)
		&& !(models.Contains("unseen") && models.Contains("guess")) // one hand mode
			? models.Aggregate(new BeamSearchAi.Settings(), (o, m) => Models[m](o))
			: null;

	/// <summary>The fixed names; beside them, "beam-held=0.25" is the search at that held fraction.</summary>
	public static IReadOnlyCollection<string> Names => [.. Others.Keys, .. Searches.Keys];

	/// <summary>What --match accepts, for a usage line.</summary>
	public static string Usage =>
		$"{string.Join(", ", Names)}, beam-held=<fraction>, or beam+<model>[+<model>…] ({string.Join(", ", Models.Keys)}; unseen and guess need casts or responds)";

	// Names only, never the factories: a broken one fails in Create, with its own message.
	public static bool IsKnown(string name) =>
		Others.ContainsKey(name)
		|| Searches.ContainsKey(name)
		|| Combined(name) is not null
		|| HeldFraction(name) is not null;

	/// <param name="searchThreads">A search's <see cref="BeamSearchAi.Settings.Threads"/> — 1 when games run in parallel.</param>
	public static IAi Create(string name, int playerId, int seed, int? searchThreads = null) =>
		Others.TryGetValue(name, out var make) ? make(playerId, seed)
		: SearchSettings(name) is { } search
			? new BeamSearchAi(playerId, search with { Threads = searchThreads ?? search.Threads })
		: throw new ArgumentException($"Unknown AI '{name}' — known: {Usage}");

	static BeamSearchAi.Settings? SearchSettings(string name) =>
		Searches.TryGetValue(name, out var settings) ? settings()
		: Combined(name) is { } combined ? combined
		: HeldFraction(name) is { } fraction ? new BeamSearchAi.Settings().WithHeld(fraction)
		: null;

	/// <summary>"beam-held=0.25" → 0.25 (invariant culture, so "0.25" reads alike on every machine).</summary>
	static double? HeldFraction(string name) =>
		name.StartsWith("beam-held=", StringComparison.Ordinal)
		&& double.TryParse(
			name["beam-held=".Length..],
			System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture,
			out var f
		)
		&& double.IsFinite(f)
		&& f >= 0 // NaN would make every score NaN; a negative one is the flat price under a held label
			? f
			: null;

	/// <summary>One game, from A's side. Winner: 1 = A, 2 = B, 0 = a draw or unfinished.</summary>
	public sealed record Game(
		string Deck1,
		string Deck2,
		int Seed,
		bool AIsPlayer1,
		int Winner,
		bool Finished,
		int Turns,
		int DecisionsA,
		int DecisionsB,
		double SecondsA,
		double SecondsB,
		double SlowestA,
		double SlowestB,
		int LandsA,
		int LandsB,
		int SpellsA,
		int SpellsB,
		long StepsA,
		long StepsB,
		long RolloutsA,
		long RolloutsB
	)
	{
		public string DeckOfA => AIsPlayer1 ? Deck1 : Deck2;
		public string DeckOfB => AIsPlayer1 ? Deck2 : Deck1;
	}

	/// <summary>
	/// At least <paramref name="games"/> games, rounded up to whole schedules: every ordered deck
	/// pairing, both seatings, per seed.
	/// </summary>
	public static Result Run(string a, string b, int games, bool sequential = false)
	{
		var pairs = Pairings(a, b);
		var clock = Stopwatch.StartNew();
		var seeds = SeedsFor(games, pairs);
		var results = PlayAll(a, b, Schedule(pairs, seeds), sequential);
		return new Result(a, b, pairs.Count, seeds, [.. results!], clock.Elapsed, sequential);
	}

	/// <summary>
	/// Games played until <see cref="EarlyStop"/> gives a verdict, or <paramref name="maxGames"/> are
	/// played (story AI-6.3). Every game up to the cap is scheduled in seed order and started as a core
	/// frees up; the test is judged on each whole batch of <see cref="SeedsPerBatch"/> seeds once all of
	/// its games are in, so where it stops depends on the results alone — never on which game finished
	/// first — and its games are the first ones a fixed <see cref="Run"/> of that size plays. Once it
	/// decides no game starts; those already running finish and are left out.
	/// </summary>
	public static Result RunUntilClear(
		string a,
		string b,
		int maxGames = 600,
		bool sequential = false
	)
	{
		var pairs = Pairings(a, b);
		var clock = Stopwatch.StartNew();
		var schedule = Schedule(pairs, SeedsFor(maxGames, pairs));
		var perBatch = SeedsPerBatch * pairs.Count * 2;
		var (judged, reading) = (0, EarlyStop.Judge([]));
		// Asked under PlayAll's lock after every game: judge each whole batch now complete, in order.
		bool Enough(Game?[] results)
		{
			while (reading.Verdict == EarlyStop.Verdict.Undecided && judged < results.Length)
			{
				var next = Math.Min(judged + perBatch, results.Length);
				if (Array.IndexOf(results, null, judged, next - judged) >= 0)
					return false;
				judged = next;
				reading = EarlyStop.Judge(PairScores(results[..judged]!), reading);
			}
			return reading.Verdict != EarlyStop.Verdict.Undecided;
		}
		var results = PlayAll(a, b, schedule, sequential, Enough);
		return new Result(
			a,
			b,
			pairs.Count,
			judged / (pairs.Count * 2),
			[.. results[..judged]!],
			clock.Elapsed,
			sequential
		)
		{
			Stop = reading,
		};
	}

	/// <summary>
	/// Seeds judged at once: 8 × 12 games (48 pairs, EarlyStop.MinPairs). Games run continuously
	/// across batches, so the size only sets how often the test is read — not how long cores wait.
	/// </summary>
	public const int SeedsPerBatch = 8;

	static List<(string x, string y)> Pairings(string a, string b)
	{
		var decks = FraDecks.All.Keys.ToList();
		var pairs = decks.SelectMany(x => decks.Where(y => y != x).Select(y => (x, y))).ToList();
		if (pairs.Count == 0)
			throw new InvalidOperationException("A match needs at least two FRA decks to pair.");
		foreach (var name in new[] { a, b }.Where(n => !IsKnown(n)))
			throw new ArgumentException($"Unknown AI '{name}' — known: {Usage}");
		return pairs;
	}

	static int SeedsFor(int games, List<(string x, string y)> pairs) =>
		Math.Max(1, (int)Math.Ceiling(games / (pairs.Count * 2.0)));

	/// <summary>Every ordered pairing, both seats, for seeds 1..<paramref name="seeds"/> — in seed order.</summary>
	static List<(string x, string y, int seed, bool aIsPlayer1)> Schedule(
		List<(string x, string y)> pairs,
		int seeds
	) =>
		[
			.. from seed in Enumerable.Range(1, seeds)
			from pair in pairs
			from aIsPlayer1 in new[] { true, false }
			select (pair.x, pair.y, seed, aIsPlayer1),
		];

	/// <summary>
	/// Plays the schedule, starting games in schedule order as a slot frees up — one slot with each
	/// search on every core, or one per core with each search on one thread. After each game
	/// <paramref name="enough"/> (under a lock) may say to stop: no game starts, a running one is
	/// abandoned at its next decision, and every game not finished stays null.
	/// </summary>
	static Game?[] PlayAll(
		string a,
		string b,
		List<(string x, string y, int seed, bool aIsPlayer1)> schedule,
		bool sequential,
		Func<Game?[], bool>? enough = null
	)
	{
		var results = new Game?[schedule.Count];
		var progress = new RunProgress($"match {a} vs {b}", schedule.Count);
		int? searchThreads = sequential ? null : 1;
		var gate = new object();
		using var stop = new CancellationTokenSource();
		// Capped at the cores: unbounded, the pool grows past them and games fight over them. Handed out
		// one at a time in order, so a long game never holds back the ones after it.
		Parallel.ForEach(
			Partitioner.Create(
				Enumerable.Range(0, schedule.Count),
				EnumerablePartitionerOptions.NoBuffering
			),
			new ParallelOptions
			{
				MaxDegreeOfParallelism = sequential ? 1 : Environment.ProcessorCount,
			},
			(i, loop) =>
			{
				var (x, y, seed, aIsPlayer1) = schedule[i];
				if (Play(a, b, x, y, seed, aIsPlayer1, searchThreads, stop.Token) is not { } game)
					return;
				lock (gate)
				{
					results[i] = game;
					progress.Tick(() => Standing(a, results));
					if (enough?.Invoke(results) == true)
					{
						stop.Cancel();
						loop.Stop();
					}
				}
			}
		);
		return results;
	}

	/// <summary>How the match stands: A's score over the games finished, and the wins, losses and draws behind it.</summary>
	static string Standing(string a, Game?[] results)
	{
		var played = results.OfType<Game>().ToList();
		var (win, loss) = (
			played.Count(g => g.Winner == 1),
			played.Count(g => g.Winner is not (0 or 1))
		);
		return $"{a} {played.Average(Result.ScoreOfA):P1} ({win}-{loss}-{played.Count - win - loss})";
	}

	/// <summary>A's score in each mirror pair — the independent trials (see <see cref="Result.StandardError"/>).</summary>
	static List<double> PairScores(IEnumerable<Game> games) =>
		[.. games.GroupBy(g => (g.Deck1, g.Deck2, g.Seed)).Select(p => p.Average(Result.ScoreOfA))];

	static Game? Play(
		string a,
		string b,
		string deck1,
		string deck2,
		int seed,
		bool aIsPlayer1,
		int? searchThreads,
		CancellationToken stop
	) =>
		Play(
			a,
			b,
			MagicRules.NewGame(FraDecks.All[deck1], FraDecks.All[deck2], seed),
			deck1,
			deck2,
			seed,
			aIsPlayer1,
			searchThreads,
			stop
		);

	/// <summary>
	/// One game from <paramref name="newGame"/> (a new game of decks named <paramref name="deck1"/> and
	/// <paramref name="deck2"/> — any decks, not only FRA's: sealed builds, LIM-1.8); null if
	/// <paramref name="stop"/> is cancelled before it ends. <paramref name="ended"/> sees the last state;
	/// <paramref name="seen"/> every state a player decides in, and the last (DATA-2: which cards were in hand);
	/// <paramref name="moved"/> every move with the state it was made in and its kind - "ai", or "rule" for a forced
	/// pass - as a <see cref="GameRecord"/> keeps them.
	/// </summary>
	public static Game? Play(
		string a,
		string b,
		GameState newGame,
		string deck1,
		string deck2,
		int seed,
		bool aIsPlayer1,
		int? searchThreads,
		CancellationToken stop = default,
		Action<GameState>? ended = null,
		Action<GameState>? seen = null,
		Action<GameState, string, string>? moved = null
	)
	{
		var s = newGame;
		var (aSeat, bSeat) = aIsPlayer1
			? (s.Player1Id(), s.Player2Id())
			: (s.Player2Id(), s.Player1Id());
		var ais = new Dictionary<int, IAi>
		{
			[aSeat] = Create(a, aSeat, seed, searchThreads),
			[bSeat] = Create(b, bSeat, seed, searchThreads),
		};
		var decisions = new Dictionary<int, int> { [aSeat] = 0, [bSeat] = 0 };
		var ticks = new Dictionary<int, long> { [aSeat] = 0, [bSeat] = 0 };
		var slowest = new Dictionary<int, long> { [aSeat] = 0, [bSeat] = 0 };
		// What a hand term can break without moving the win rate: MtgSimulator's stopped playing lands.
		var lands = new Dictionary<int, int> { [aSeat] = 0, [bSeat] = 0 };
		var spells = new Dictionary<int, int> { [aSeat] = 0, [bSeat] = 0 };
		var cost = new Dictionary<int, (long Steps, long Rollouts)>
		{
			[aSeat] = (0, 0),
			[bSeat] = (0, 0),
		};

		for (var i = 0; i < DecisionLimit && !s.IsOver(); i++)
		{
			if (stop.IsCancellationRequested)
				return null;
			s = s.AutoPass(
				moved is null
					? null
					: (before, pass) => moved(before, MoveTokens.Token(before, pass, null), "rule")
			);
			seen?.Invoke(s);
			if (s.IsOver())
				break;
			var who = s.DecidingPlayerId();
			// Both seats' tallies: a spell can be cast during the OTHER player's decision (on resolution).
			var turn = s.GetGame().TurnNumber;
			var tallies = ais.Keys.ToDictionary(p => p, p => s.GetPlayer(p).ThisTurn);
			var start = Stopwatch.GetTimestamp();
			var rolloutsBefore = RolloutsOf(ais[who]);
			var (decision, steps) = MagicRules.CountingSteps(() => ais[who].Decide(s));
			moved?.Invoke(s, MoveTokens.Token(s, decision), "ai");
			s = decision.State;
			var took = Stopwatch.GetTimestamp() - start;
			cost[who] = (
				cost[who].Steps + steps,
				cost[who].Rollouts + RolloutsOf(ais[who]) - rolloutsBefore
			);
			ticks[who] += took;
			slowest[who] = Math.Max(slowest[who], took);
			decisions[who]++;
			if (!s.IsOver() && s.GetGame().TurnNumber == turn)
				foreach (var (p, tally) in tallies)
				{
					lands[p] += s.GetPlayer(p).ThisTurn.LandsPlayed - tally.LandsPlayed;
					spells[p] += s.GetPlayer(p).ThisTurn.SpellsCast - tally.SpellsCast;
				}
		}

		seen?.Invoke(s);
		ended?.Invoke(s);
		var winner = s.WinnerId();
		return new Game(
			deck1,
			deck2,
			seed,
			aIsPlayer1,
			winner == aSeat ? 1
				: winner == bSeat ? 2
				: 0,
			s.IsOver(),
			s.GetGame().TurnNumber,
			decisions[aSeat],
			decisions[bSeat],
			(double)ticks[aSeat] / Stopwatch.Frequency,
			(double)ticks[bSeat] / Stopwatch.Frequency,
			(double)slowest[aSeat] / Stopwatch.Frequency,
			(double)slowest[bSeat] / Stopwatch.Frequency,
			lands[aSeat],
			lands[bSeat],
			spells[aSeat],
			spells[bSeat],
			cost[aSeat].Steps,
			cost[bSeat].Steps,
			cost[aSeat].Rollouts,
			cost[bSeat].Rollouts
		);
	}

	static long RolloutsOf(IAi ai) => ai is BeamSearchAi search ? search.Rollouts : 0;

	public sealed record Result(
		string A,
		string B,
		int Pairings,
		int Seeds,
		IReadOnlyList<Game> Games,
		TimeSpan Elapsed,
		bool Sequential
	)
	{
		/// <summary>Where early stopping stood when the run ended (RunUntilClear only).</summary>
		public EarlyStop.Reading? Stop { get; init; }

		/// <summary>A's score: a win is 1, a draw or unfinished game half.</summary>
		public double WinRateA => Games.Average(ScoreOfA);

		public static double ScoreOfA(Game g) =>
			g.Winner switch
			{
				1 => 1.0,
				0 => 0.5,
				_ => 0.0,
			};

		/// <summary>
		/// One standard error of <see cref="WinRateA"/>, over MIRROR PAIRS, not games: the two games of
		/// a pair share decks, seed and coin flip, so they are not independent trials. An AI against
		/// itself splits every pair — zero spread, zero error — where a per-game binomial would claim
		/// ±5.9pp at 72 games.
		/// </summary>
		public double StandardError
		{
			get
			{
				var pairScores = PairScores(Games);
				return Math.Sqrt(EarlyStop.Variance(pairScores) / pairScores.Count);
			}
		}

		public string Report()
		{
			// The same AI on both sides needs telling apart in its own report.
			var (a, b) = A == B ? ($"{A} (A)", $"{B} (B)") : (A, B);
			static string Ms(double seconds, int decisions) =>
				decisions == 0 ? "-" : $"{seconds * 1000 / decisions:0.0} ms";
			static string PerDecision(long total, int decisions) =>
				decisions == 0 ? "-" : $"{(double)total / decisions:0.0}";
			var (secA, decA) = (Games.Sum(g => g.SecondsA), Games.Sum(g => g.DecisionsA));
			var (secB, decB) = (Games.Sum(g => g.SecondsB), Games.Sum(g => g.DecisionsB));
			var byDeck = Games
				.GroupBy(g => g.DeckOfA)
				.OrderBy(g => g.Key)
				.Select(g =>
					$"{g.Key} {g.Count(x => x.Winner == 1)}-{g.Count(x => x.Winner == 2)}-{g.Count(x => x.Winner == 0)}"
				);
			var unfinished = Games.Count(g => !g.Finished);
			return string.Join(
					"\n",
					$"{A} vs {B}: {Games.Count} games ({Pairings} deck pairings x {Seeds} seeds x both seats), {Elapsed.TotalSeconds:0.0} s",
					$"  {a} {WinRateA * 100:0.0}% ± {StandardError * 100:0.0}pp (1 SE) — wins {a} {Games.Count(g => g.Winner == 1)}, {b} {Games.Count(g => g.Winner == 2)}, draws {Games.Count(g => g.Winner == 0)}",
					$"  cost per decision (deterministic): {a} {PerDecision(Games.Sum(g => g.StepsA), decA)} engine steps, {PerDecision(Games.Sum(g => g.RolloutsA), decA)} rollouts; "
						+ $"{b} {PerDecision(Games.Sum(g => g.StepsB), decB)} steps, {PerDecision(Games.Sum(g => g.RolloutsB), decB)} rollouts",
					$"  time per decision{(Sequential ? "" : " (games in parallel: NOT evidence — use the cost line, or --sequential)")}: {a} {Ms(secA, decA)} (slowest {Games.Max(g => g.SlowestA) * 1000:0} ms), {b} {Ms(secB, decB)} (slowest {Games.Max(g => g.SlowestB) * 1000:0} ms)",
					$"  per game: decisions {a} {(double)decA / Games.Count:0}, {b} {(double)decB / Games.Count:0}; turns {Games.Average(g => g.Turns):0.0}",
					$"  per game: lands played {a} {Games.Average(g => g.LandsA):0.0}, {b} {Games.Average(g => g.LandsB):0.0}; "
						+ $"spells cast {a} {Games.Average(g => g.SpellsA):0.0}, {b} {Games.Average(g => g.SpellsB):0.0}",
					$"  {a}'s record by its deck (W-L-D): {string.Join(", ", byDeck)}",
					unfinished > 0
						? $"  UNFINISHED at {DecisionLimit} decisions: {unfinished} (counted as draws)"
						: "  every game finished"
				) + (Stop is null ? "" : "\n" + StopLine(Stop, a));
		}

		static string StopLine(EarlyStop.Reading stop, string a) =>
			$"  early stop: {stop.Verdict switch
			{
				EarlyStop.Verdict.Better => $"{a} BETTER by {EarlyStop.Margin * 100:0}pp or more",
				EarlyStop.Verdict.Worse => $"{a} WORSE by {EarlyStop.Margin * 100:0}pp or more",
				EarlyStop.Verdict.NoDifference => $"NO difference of {EarlyStop.Margin * 100:0}pp either way",
				_ => "UNDECIDED at the cap: any difference is too small to settle here; read the estimate",
			}} (log-likelihood: better {stop.BetterLlr:0.00}, worse {stop.WorseLlr:0.00}; decided at ±{stop.Bound:0.00}, {EarlyStop.Error * 100:0}% error per direction)";
	}
}
