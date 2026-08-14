using MtgCore;

namespace MtgSimulator;

/// One row of the standings table. <see cref="Played"/> is derived so callers do not re-add it.
public sealed record Standing(int Seat, bool IsHuman, int Wins, int Losses, int Draws)
{
	public int Played => Wins + Losses + Draws;
	public double WinRate => Played == 0 ? 0 : (double)Wins / Played;
}

/// One finished game. <see cref="WinnerSeat"/> is -1 for a draw.
public sealed record RoundResult(int SeatA, int SeatB, int WinnerSeat);

/// <summary>
/// Round-robin tournament over the pools a <see cref="Draft"/> produced: every seat plays
/// every other seat exactly once, one game per pairing, across <see cref="RoundCount"/> rounds.
///
/// Plain C# with no presentation and no Godot — same separation as <see cref="Draft"/> itself.
/// One seat is the human, whose games are played in the UI and reported back via
/// <see cref="RecordHumanResult"/>; every other pairing is simulated by
/// <see cref="SimulateRoundAsync"/>.
///
/// ponytail: the human is always on the play, because MtgGameManager hardcodes the human as
/// Player 1 and passes them first to BeginGame. DraftRunner dodges this by alternating across
/// its two games per pair; at one game per pair there is nothing to alternate. Upgrade path is
/// a "human plays second" flag threaded through MtgGameManager's constructor into its
/// BeginGame call, then alternate here on round parity.
/// </summary>
public sealed class DraftTournament
{
	private readonly IReadOnlyList<IReadOnlyList<Card>> _pools;
	private readonly int _seed;
	private readonly int _aiDepth;
	private readonly int[] _wins;
	private readonly int[] _losses;
	private readonly int[] _draws;
	private Task<IReadOnlyList<RoundResult>>? _pending;

	public int HumanSeat { get; }

	/// 0-based. Advances only via <see cref="AdvanceRound"/>, once a round's results are in.
	public int Round { get; private set; }

	/// With an even seat count every seat plays in every round, so rounds = seats - 1.
	public int RoundCount => _pools.Count - 1;

	public int SeatCount => _pools.Count;

	public bool IsComplete => Round >= RoundCount;

	public DraftTournament(
		IReadOnlyList<IReadOnlyList<Card>> pools,
		int seed,
		int humanSeat = 0,
		int aiDepth = 3
	)
	{
		if (pools.Count < 2 || pools.Count % 2 != 0)
			throw new ArgumentException(
				$"Need an even seat count of at least 2, got {pools.Count}.",
				nameof(pools)
			);
		if (humanSeat < 0 || humanSeat >= pools.Count)
			throw new ArgumentOutOfRangeException(nameof(humanSeat));

		_pools = pools;
		_seed = seed;
		_aiDepth = aiDepth;
		HumanSeat = humanSeat;
		_wins = new int[pools.Count];
		_losses = new int[pools.Count];
		_draws = new int[pools.Count];
	}

	public IReadOnlyList<Card> Pool(int seat) => _pools[seat];

	/// <summary>
	/// Circle method: seat 0 sits still while the rest rotate one position per round. Over
	/// <see cref="RoundCount"/> rounds this yields every unordered pair exactly once, and every
	/// seat exactly once per round.
	/// </summary>
	public static IReadOnlyList<(int A, int B)> Pairings(int seatCount, int round)
	{
		var rotating = seatCount - 1;
		int At(int i) => 1 + (i + round) % rotating;

		var pairs = new List<(int, int)> { (0, At(0)) };
		for (var k = 1; k < seatCount / 2; k++)
			pairs.Add((At(k), At(rotating - k)));
		return pairs;
	}

	/// The human's opponent this round, and whether the human is on the play (seat A).
	public (int Opponent, bool HumanIsSeatA) HumanPairing(int round)
	{
		foreach (var (a, b) in Pairings(SeatCount, round))
		{
			if (a == HumanSeat)
				return (b, true);
			if (b == HumanSeat)
				return (a, false);
		}
		throw new InvalidOperationException($"Seat {HumanSeat} is unpaired in round {round}.");
	}

	/// <summary>
	/// Plays every pairing in the round that does not involve the human, off the calling thread.
	///
	/// Deliberately touches no tournament state: it returns results the caller folds in with
	/// <see cref="ApplyResults"/> on the main thread. That is what makes this safe without a
	/// lock, and it mirrors the compute/apply split the Godot AI turn already uses.
	/// </summary>
	public Task<IReadOnlyList<RoundResult>> SimulateRoundAsync(int round)
	{
		// Captured before the task starts so nothing on the worker thread reads mutable fields.
		var jobs = Pairings(SeatCount, round)
			.Where(p => p.A != HumanSeat && p.B != HumanSeat)
			.ToList();
		var pools = _pools;
		var aiDepth = _aiDepth;
		var seedBase = _seed + 1000 + round * 100;

		return Task.Run<IReadOnlyList<RoundResult>>(
			() =>
				jobs.Select(
						(pair, i) =>
						{
							var result = DraftRunner.PlayGame(
								pools[pair.A],
								pools[pair.B],
								seedBase + i * 5,
								aiDepth
							);
							var winner =
								result.IsPlayer1Win ? pair.A
								: result.IsPlayer2Win ? pair.B
								: -1;
							return new RoundResult(pair.A, pair.B, winner);
						}
					)
					.ToList()
		);
	}

	/// <summary>
	/// Kicks off a round's AI games and holds the task here rather than on the caller — the UI
	/// scene that starts it is torn down while the human plays their own game, so it cannot own
	/// the handle. Idempotent per round: starting twice would double-count the results.
	/// </summary>
	public void StartRoundSim(int round)
	{
		_pending ??= SimulateRoundAsync(round);
	}

	public bool HasPendingRound => _pending != null;

	/// Awaits the in-flight round and folds it in. The fold happens after the await, on the
	/// caller's thread, so no tournament state is ever touched from the worker.
	public async Task CompletePendingRoundAsync()
	{
		if (_pending == null)
			return;
		var results = await _pending;
		_pending = null;
		ApplyResults(results);
	}

	public void ApplyResults(IEnumerable<RoundResult> results)
	{
		foreach (var r in results)
			Record(r.SeatA, r.SeatB, r.WinnerSeat);
	}

	public void RecordHumanResult(int round, bool humanWon, bool isDraw)
	{
		var (opponent, _) = HumanPairing(round);
		Record(
			HumanSeat,
			opponent,
			isDraw ? -1
				: humanWon ? HumanSeat
				: opponent
		);
	}

	private void Record(int seatA, int seatB, int winnerSeat)
	{
		if (winnerSeat < 0)
		{
			_draws[seatA]++;
			_draws[seatB]++;
			return;
		}
		var loser = winnerSeat == seatA ? seatB : seatA;
		_wins[winnerSeat]++;
		_losses[loser]++;
	}

	public void AdvanceRound()
	{
		if (!IsComplete)
			Round++;
	}

	/// Best record first. Champion is row 0 — draws break ties ahead of seat order, and there
	/// are no tiebreaker games.
	public IReadOnlyList<Standing> Standings() =>
		Enumerable
			.Range(0, SeatCount)
			.Select(i => new Standing(i, i == HumanSeat, _wins[i], _losses[i], _draws[i]))
			.OrderByDescending(s => s.Wins)
			.ThenBy(s => s.Losses)
			.ThenByDescending(s => s.Draws)
			.ThenBy(s => s.Seat)
			.ToList();
}
