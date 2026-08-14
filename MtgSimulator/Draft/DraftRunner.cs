using System.Diagnostics;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// All-AI draft harness: drafts a table, builds a deck per seat, then plays every seat
/// against every other seat through GameRunner and reports win rates.
///
/// Seats are assigned different pickers so the report actually answers "is this picker
/// better than that one?" rather than just proving the code runs. Without trained data
/// that is Curve vs Random; with it, Trained vs Curve vs Random.
/// </summary>
public class DraftRunner
{
	private readonly DraftFormat _format;
	private readonly int _seatCount;
	private readonly int _seed;
	private readonly int _aiDepth;
	private readonly int _gamesPerPair;
	private readonly DraftTrainingData? _trained;
	private readonly double _synergyWeight;
	private readonly CardSet _set;

	/// <param name="synergyWeight">
	/// Weight on the trained picker's synergy term. Defaults to 0 because every non-zero
	/// value measured has cost win rate — see DraftPickers.Trained for the sweep. Raise it
	/// only to re-measure, and only with substantially more pair data than 464 games/pair.
	/// </param>
	/// <param name="set">
	/// Which card set to draft. Defaults to Legacy (the original CardLibrary.All pool).
	/// The trained model must have been trained on this same set — see DraftTrainingStore.PathFor.
	/// </param>
	public DraftRunner(
		DraftFormat format,
		int seatCount,
		int seed,
		int aiDepth = 3,
		int gamesPerPair = 2,
		DraftTrainingData? trained = null,
		double synergyWeight = 0.0,
		CardSet? set = null
	)
	{
		if (seatCount < 2)
			throw new ArgumentOutOfRangeException(
				nameof(seatCount),
				"Need at least two seats to play games."
			);
		_format = format;
		_seatCount = seatCount;
		_seed = seed;
		_aiDepth = aiDepth;
		_gamesPerPair = gamesPerPair;
		_trained = trained;
		_synergyWeight = synergyWeight;
		_set = set ?? SetRegistry.Default;
	}

	/// <summary>
	/// Plays the round-robin and returns wins/games per picker name, so callers (the
	/// generational training loop) can track a picker across runs instead of scraping stdout.
	/// </summary>
	public IReadOnlyDictionary<string, (int Wins, int Played)> Run(bool verbose = true)
	{
		if (verbose)
			Console.WriteLine(
				$"Drafting {_set.Name}: {_format}, {_seatCount} seats, "
					+ $"AI depth {_aiDepth}, seed {_seed}"
			);

		// With trained data the seats cycle through three pickers; without it, two.
		var labels = _trained is null
			? new[] { "Curve", "Random" }
			: ["Trained", "Curve", "Random"];
		var pickerNames = Enumerable
			.Range(0, _seatCount)
			.Select(i => labels[i % labels.Length])
			.ToList();
		var pickers = pickerNames
			.Select(
				(name, i) =>
					name switch
					{
						"Trained" => DraftPickers.Trained(
							_trained!,
							new Random(_seed + 100 + i),
							synergyWeight: _synergyWeight
						),
						"Curve" => DraftPickers.Curve,
						_ => DraftPickers.Random(new Random(_seed + 100 + i)),
					}
			)
			.ToList();

		if (verbose && _seatCount % labels.Length != 0)
			Console.WriteLine(
				$"  NOTE: {_seatCount} seats does not divide evenly by {labels.Length} pickers — "
					+ "per-picker win rates are not directly comparable."
			);

		var draftTimer = Stopwatch.StartNew();
		var final = Draft.RunToCompletion(
			Draft.Create(_format, _set.Cards, _seed, _seatCount),
			pickers
		);
		draftTimer.Stop();

		var pools = final.Seats.Select(s => s.Pool).ToList();
		if (verbose)
		{
			Console.WriteLine(
				$"Draft complete in {draftTimer.ElapsedMilliseconds}ms — "
					+ $"{pools[0].Count} cards per seat."
			);
			Console.WriteLine();
		}

		var wins = new int[_seatCount];
		var played = new int[_seatCount];
		var draws = 0;
		var gameIndex = 0;

		var gameTimer = Stopwatch.StartNew();
		for (var a = 0; a < _seatCount; a++)
		{
			for (var b = a + 1; b < _seatCount; b++)
			{
				for (var k = 0; k < _gamesPerPair; k++)
				{
					// Alternate who is on the play — otherwise the lower seat index gets a
					// free advantage in every game and swamps the picker signal.
					var (p1, p2) = k % 2 == 0 ? (a, b) : (b, a);
					var gameSeed = _seed + 1000 + gameIndex++ * 5;

					var result = PlayGame(pools[p1], pools[p2], gameSeed, _aiDepth);

					played[p1]++;
					played[p2]++;
					if (result.IsPlayer1Win)
						wins[p1]++;
					else if (result.IsPlayer2Win)
						wins[p2]++;
					else
						draws++;
				}
			}
		}
		gameTimer.Stop();

		if (verbose)
			PrintReport(
				pickerNames,
				pools,
				wins,
				played,
				draws,
				gameIndex,
				gameTimer.ElapsedMilliseconds
			);

		return pickerNames
			.Select((name, i) => (name, i))
			.GroupBy(x => x.name)
			.ToDictionary(
				g => g.Key,
				g => (Wins: g.Sum(x => wins[x.i]), Played: g.Sum(x => played[x.i]))
			);
	}

	/// <summary>
	/// Plays one game between two drafted pools. Shared with <see cref="DraftTournament"/> so
	/// the seed derivation and strategy setup live in exactly one place — the seeds are what
	/// make a draft reproducible, and two copies of this would drift.
	///
	/// <paramref name="pool1"/> is on the play. Both seats get the same AI so the result
	/// measures the decks, not the pilots.
	/// </summary>
	public static GameResult PlayGame(
		IReadOnlyList<Card> pool1,
		IReadOnlyList<Card> pool2,
		int gameSeed,
		int aiDepth
	)
	{
		var (state, ids, cardNames) = DraftGameSetup.Build(pool1, pool2);
		var aiRng = new Random(gameSeed + 4);
		var runner = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(ids, aiDepth, rng: aiRng),
			new MultiTurnBeamSearchAiStrategy(ids, aiDepth, rng: aiRng)
		);
		var (result, _) = runner.Run(
			state,
			ids,
			cardNames,
			shuffleSeed: gameSeed + 2,
			gameRngSeed: gameSeed + 3
		);
		return result;
	}

	private static void PrintReport(
		IReadOnlyList<string> pickerNames,
		IReadOnlyList<IReadOnlyList<Card>> pools,
		IReadOnlyList<int> wins,
		IReadOnlyList<int> played,
		int draws,
		int totalGames,
		long elapsedMs
	)
	{
		Console.WriteLine("  --- Draft Results ---");
		Console.WriteLine();
		Console.WriteLine(
			$"  {"Seat", -6}{"Picker", -10}{"Pool", -8}{"Avg Cost", -11}{"W/G", -10}Win%"
		);

		for (var i = 0; i < wins.Count; i++)
		{
			var avgCost = pools[i].Count == 0 ? 0 : pools[i].Average(c => c.ManaCost);
			var winRate = played[i] == 0 ? 0 : 100.0 * wins[i] / played[i];
			Console.WriteLine(
				$"  {i, -6}{pickerNames[i], -10}{pools[i].Count, -8}{avgCost, -11:F2}"
					+ $"{$"{wins[i]}/{played[i]}", -10}{winRate:F1}%"
			);
		}

		Console.WriteLine();
		foreach (var picker in pickerNames.Distinct())
			PrintPickerSummary(pickerNames, wins, played, picker);
		Console.WriteLine();
		Console.WriteLine($"  Games: {totalGames}  Draws: {draws}  Time: {elapsedMs / 1000.0:F1}s");
		Console.WriteLine();
	}

	private static void PrintPickerSummary(
		IReadOnlyList<string> pickerNames,
		IReadOnlyList<int> wins,
		IReadOnlyList<int> played,
		string picker
	)
	{
		var seats = Enumerable.Range(0, wins.Count).Where(i => pickerNames[i] == picker).ToList();
		if (seats.Count == 0)
			return;

		var totalWins = seats.Sum(i => wins[i]);
		var totalPlayed = seats.Sum(i => played[i]);
		var rate = totalPlayed == 0 ? 0 : 100.0 * totalWins / totalPlayed;
		Console.WriteLine($"  {picker, -10} {totalWins}/{totalPlayed}  ({rate:F1}%)");
	}
}
