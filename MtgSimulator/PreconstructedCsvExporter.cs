namespace MtgSimulator;

public static class PreconstructedCsvExporter
{
	private const string OutputDirectory = "sim_results";

	public static string Export(PreconstructedStats stats)
	{
		Directory.CreateDirectory(OutputDirectory);
		var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		var filePath = Path.Combine(OutputDirectory, $"precon_{timestamp}.csv");

		using var writer = new StreamWriter(filePath);

		WriteSection(
			writer,
			"Overall Deck Win Rates",
			"Deck,Games,Wins,WinRate,OnPlayGames,OnPlayWinRate,OnDrawGames,OnDrawWinRate,AvgWinTurn,MinWinTurn,MaxWinTurn",
			stats.GetDeckStats(),
			r =>
				$"{r.DeckName},{r.TotalGames},{r.Wins},{r.WinRate:F4}"
				+ $",{r.OnPlayGames},{r.OnPlayWinRate:F4},{r.OnDrawGames},{r.OnDrawWinRate:F4}"
				+ $",{r.AvgWinTurn:F2},{r.MinWinTurn},{r.MaxWinTurn}"
		);

		writer.WriteLine();

		WriteSection(
			writer,
			"Matchup Win Rates",
			"Deck,Opponent,Games,Wins,WinRate,OnPlayGames,OnPlayWinRate,OnDrawGames,OnDrawWinRate",
			stats.GetMatchupStats(),
			r =>
				$"{r.DeckName},{r.OpponentDeckName},{r.TotalGames},{r.Wins},{r.WinRate:F4}"
				+ $",{r.OnPlayGames},{r.OnPlayWinRate:F4},{r.OnDrawGames},{r.OnDrawWinRate:F4}"
		);

		writer.WriteLine();

		WriteSection(
			writer,
			"Card GIH Win Rates (Overall)",
			"Deck,Card,GIHGames,GIHWins,GIHWinRate,AvgCopiesDrawn,AvgCopiesPlayed",
			stats.GetCardStats(),
			r =>
				$"{r.DeckName},{Escape(r.CardName)},{r.GihGames},{r.GihWins}"
				+ $",{r.GihWinRate:F4},{r.AvgCopiesDrawn:F3},{r.AvgCopiesPlayed:F3}"
		);

		writer.WriteLine();

		WriteSection(
			writer,
			"Card GIH Win Rates (Per Matchup)",
			"Deck,Opponent,Card,GIHGames,GIHWins,GIHWinRate,AvgCopiesDrawn,AvgCopiesPlayed",
			stats.GetCardMatchupStats(),
			r =>
				$"{r.DeckName},{r.OpponentDeckName},{Escape(r.CardName)},{r.GihGames},{r.GihWins}"
				+ $",{r.GihWinRate:F4},{r.AvgCopiesDrawn:F3},{r.AvgCopiesPlayed:F3}"
		);

		return filePath;
	}

	private static void WriteSection<T>(
		StreamWriter writer,
		string title,
		string header,
		IReadOnlyList<T> rows,
		Func<T, string> format
	)
	{
		writer.WriteLine($"# {title}");
		writer.WriteLine(header);
		foreach (var row in rows)
			writer.WriteLine(format(row));
	}

	// Card names like "Krenko, Mob Boss" contain commas and must be quoted.
	private static string Escape(string value) =>
		value.Contains(',') || value.Contains('"') || value.Contains('\n')
			? $"\"{value.Replace("\"", "\"\"")}\""
			: value;
}
