namespace MtgSimulator;

public class PreconstructedStats
{
	private readonly Dictionary<string, WinLossAccumulator> _deckStats = new();
	private readonly Dictionary<(string Deck, string Opponent), WinLossAccumulator> _matchupStats =
		new();
	private readonly Dictionary<(string Deck, string Card), CardAccumulator> _cardStats = new();
	private readonly Dictionary<
		(string Deck, string Opponent, string Card),
		CardAccumulator
	> _cardMatchupStats = new();

	public void AddResult(PreconstructedGameResult result)
	{
		var gr = result.GameResult;

		ProcessPlayerPerspective(
			deckName: result.Player1DeckName,
			opponentDeckName: result.Player2DeckName,
			isOnPlay: result.Player1IsOnPlay,
			playerWon: gr.IsPlayer1Win,
			drawnCards: gr.Player1DrawnCards
		);

		ProcessPlayerPerspective(
			deckName: result.Player2DeckName,
			opponentDeckName: result.Player1DeckName,
			isOnPlay: !result.Player1IsOnPlay,
			playerWon: gr.IsPlayer2Win,
			drawnCards: gr.Player2DrawnCards
		);
	}

	private void ProcessPlayerPerspective(
		string deckName,
		string opponentDeckName,
		bool isOnPlay,
		bool playerWon,
		IReadOnlyList<string> drawnCards
	)
	{
		Accumulate(GetOrAdd(_deckStats, deckName), isOnPlay, playerWon);
		Accumulate(GetOrAdd(_matchupStats, (deckName, opponentDeckName)), isOnPlay, playerWon);

		// GIH WR: binary per game — each unique card name counts once regardless of copies drawn
		foreach (var group in drawnCards.GroupBy(c => c))
		{
			var cardName = group.Key;
			var copies = group.Count();

			var cardOverall = GetOrAdd(_cardStats, (deckName, cardName));
			cardOverall.GihGames++;
			if (playerWon)
				cardOverall.GihWins++;
			cardOverall.TotalCopiesDrawn += copies;

			var cardMatchup = GetOrAdd(_cardMatchupStats, (deckName, opponentDeckName, cardName));
			cardMatchup.GihGames++;
			if (playerWon)
				cardMatchup.GihWins++;
			cardMatchup.TotalCopiesDrawn += copies;
		}
	}

	private static void Accumulate(WinLossAccumulator acc, bool isOnPlay, bool playerWon)
	{
		acc.TotalGames++;
		if (playerWon)
			acc.Wins++;
		if (isOnPlay)
		{
			acc.OnPlayGames++;
			if (playerWon)
				acc.OnPlayWins++;
		}
		else
		{
			acc.OnDrawGames++;
			if (playerWon)
				acc.OnDrawWins++;
		}
	}

	public IReadOnlyList<DeckWinRateRow> GetDeckStats() =>
		_deckStats
			.Select(kv => new DeckWinRateRow(
				DeckName: kv.Key,
				TotalGames: kv.Value.TotalGames,
				Wins: kv.Value.Wins,
				WinRate: WinRate(kv.Value.Wins, kv.Value.TotalGames),
				OnPlayGames: kv.Value.OnPlayGames,
				OnPlayWinRate: WinRate(kv.Value.OnPlayWins, kv.Value.OnPlayGames),
				OnDrawGames: kv.Value.OnDrawGames,
				OnDrawWinRate: WinRate(kv.Value.OnDrawWins, kv.Value.OnDrawGames)
			))
			.OrderByDescending(r => r.WinRate)
			.ToList();

	public IReadOnlyList<MatchupWinRateRow> GetMatchupStats() =>
		_matchupStats
			.Select(kv => new MatchupWinRateRow(
				DeckName: kv.Key.Deck,
				OpponentDeckName: kv.Key.Opponent,
				TotalGames: kv.Value.TotalGames,
				Wins: kv.Value.Wins,
				WinRate: WinRate(kv.Value.Wins, kv.Value.TotalGames),
				OnPlayGames: kv.Value.OnPlayGames,
				OnPlayWinRate: WinRate(kv.Value.OnPlayWins, kv.Value.OnPlayGames),
				OnDrawGames: kv.Value.OnDrawGames,
				OnDrawWinRate: WinRate(kv.Value.OnDrawWins, kv.Value.OnDrawGames)
			))
			.OrderBy(r => r.DeckName)
			.ThenBy(r => r.OpponentDeckName)
			.ToList();

	public IReadOnlyList<CardGihRow> GetCardStats() =>
		_cardStats
			.Select(kv => new CardGihRow(
				DeckName: kv.Key.Deck,
				CardName: kv.Key.Card,
				GihGames: kv.Value.GihGames,
				GihWins: kv.Value.GihWins,
				GihWinRate: WinRate(kv.Value.GihWins, kv.Value.GihGames),
				AvgCopiesDrawn: AvgCopies(kv.Value)
			))
			.OrderBy(r => r.DeckName)
			.ThenByDescending(r => r.GihWinRate)
			.ToList();

	public IReadOnlyList<CardMatchupGihRow> GetCardMatchupStats() =>
		_cardMatchupStats
			.Select(kv => new CardMatchupGihRow(
				DeckName: kv.Key.Deck,
				OpponentDeckName: kv.Key.Opponent,
				CardName: kv.Key.Card,
				GihGames: kv.Value.GihGames,
				GihWins: kv.Value.GihWins,
				GihWinRate: WinRate(kv.Value.GihWins, kv.Value.GihGames),
				AvgCopiesDrawn: AvgCopies(kv.Value)
			))
			.OrderBy(r => r.DeckName)
			.ThenBy(r => r.OpponentDeckName)
			.ThenByDescending(r => r.GihWinRate)
			.ToList();

	private static double WinRate(int wins, int total) => total == 0 ? 0.0 : wins / (double)total;

	private static double AvgCopies(CardAccumulator acc) =>
		acc.GihGames == 0 ? 0.0 : acc.TotalCopiesDrawn / (double)acc.GihGames;

	private static TValue GetOrAdd<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key)
		where TKey : notnull
		where TValue : new()
	{
		if (!dict.TryGetValue(key, out var value))
		{
			value = new TValue();
			dict[key] = value;
		}
		return value;
	}

	private class WinLossAccumulator
	{
		public int TotalGames;
		public int Wins;
		public int OnPlayGames;
		public int OnPlayWins;
		public int OnDrawGames;
		public int OnDrawWins;
	}

	private class CardAccumulator
	{
		public int GihGames;
		public int GihWins;
		public int TotalCopiesDrawn;
	}
}

public record DeckWinRateRow(
	string DeckName,
	int TotalGames,
	int Wins,
	double WinRate,
	int OnPlayGames,
	double OnPlayWinRate,
	int OnDrawGames,
	double OnDrawWinRate
);

public record MatchupWinRateRow(
	string DeckName,
	string OpponentDeckName,
	int TotalGames,
	int Wins,
	double WinRate,
	int OnPlayGames,
	double OnPlayWinRate,
	int OnDrawGames,
	double OnDrawWinRate
);

public record CardGihRow(
	string DeckName,
	string CardName,
	int GihGames,
	int GihWins,
	double GihWinRate,
	double AvgCopiesDrawn
);

public record CardMatchupGihRow(
	string DeckName,
	string OpponentDeckName,
	string CardName,
	int GihGames,
	int GihWins,
	double GihWinRate,
	double AvgCopiesDrawn
);
