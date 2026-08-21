using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Writes a trained draft model out as a spreadsheet-readable CSV, one row per card.
///
/// Called from DraftTrainingStore.Save, which is the single path every training run takes, so the
/// CSV cannot drift out of step with the JSON beside it and there is no command to remember.
///
/// THE COLUMN THAT MATTERS IS DeltaPP, not WinRate. The picker scores on the SHRUNK rate's
/// distance from the prior, and raw rates mislead in two directions at once: a card seen 30 times
/// swings wildly on noise, and a card's absolute rate is meaningless without knowing the run's
/// base rate. DeltaPP is the number the AI actually drafts on. WinRate is kept beside it because
/// it is the one people read first, and hiding it would just send them back to the JSON.
///
/// ManaCost and Types are looked up from the registered sets rather than stored in the model —
/// the model is keyed by card name only. A card no longer in any set exports with blanks rather
/// than being dropped, since a stale row is evidence and a missing one is invisible.
/// </summary>
public static class DraftTrainingCsvExporter
{
	/// Mirrors DraftPickers.Trained.shrinkK. Kept in step or the CSV ranks cards differently
	/// from the picker that reads the same file.
	private const int CardShrinkK = 25;

	/// <summary>Writes <paramref name="path"/> with its extension swapped to .csv.</summary>
	public static string Export(DraftTrainingData data, string path)
	{
		var csvPath = Path.ChangeExtension(path, ".csv");

		var dir = Path.GetDirectoryName(csvPath);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);

		var cards = BuildCardIndex();

		var rows = data
			.Cards.Select(c =>
			{
				var shrunk = DraftTrainingData.Shrink(c.Wins, c.Games, data.Prior, CardShrinkK);
				cards.TryGetValue(c.Name, out var card);
				return new
				{
					c.Name,
					ManaCost = card?.ManaCost,
					Types = card == null ? "" : DescribeTypes(card),
					c.Games,
					c.Wins,
					WinRate = c.Games > 0 ? (double)c.Wins / c.Games : 0,
					Shrunk = shrunk,
					DeltaPP = 100.0 * (shrunk - data.Prior),
					c.DeckGames,
					DrawRate = c.DeckGames > 0 ? (double)c.Games / c.DeckGames : 0,
				};
			})
			// Best first, which is how these get read — the interesting ends are the top and the
			// bottom, and a name sort buries both.
			.OrderByDescending(r => r.DeltaPP)
			.ToList();

		using var writer = new StreamWriter(csvPath);

		writer.WriteLine($"# Prior,{data.Prior:F4}");
		writer.WriteLine($"# Perspectives,{data.Perspectives}");
		writer.WriteLine($"# Cards,{data.Cards.Count}");
		writer.WriteLine();
		writer.WriteLine(
			"Name,ManaCost,Types,Games,Wins,WinRate,ShrunkWinRate,DeltaPP,DeckGames,DrawRate"
		);

		foreach (var r in rows)
			writer.WriteLine(
				$"{Escape(r.Name)},{r.ManaCost},{Escape(r.Types)},{r.Games},{r.Wins}"
					+ $",{r.WinRate:F4},{r.Shrunk:F4},{r.DeltaPP:F2},{r.DeckGames},{r.DrawRate:F4}"
			);

		return csvPath;
	}

	/// <summary>
	/// Every card in every registered set, by name. Later sets do not overwrite earlier ones —
	/// a duplicated name across sets is the same card for costing purposes, and picking either is
	/// better than throwing on the collision.
	/// </summary>
	private static Dictionary<string, Card> BuildCardIndex()
	{
		var index = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
		foreach (var set in SetRegistry.All)
		foreach (var card in set.Cards)
			index.TryAdd(card.Name, card);
		return index;
	}

	private static string DescribeTypes(Card card)
	{
		var types = card.EffectiveTypes;

		// Derivation cannot separate an instant from a sorcery and reports both — printing
		// "Instant Sorcery" would look like a data error rather than an undeclared card.
		if (types.HasFlag(CardType.Instant) && types.HasFlag(CardType.Sorcery))
			return "Spell";

		var names = new List<string>();
		foreach (
			var (flag, name) in new[]
			{
				(CardType.Creature, "Creature"),
				(CardType.Artifact, "Artifact"),
				(CardType.Enchantment, "Enchantment"),
				(CardType.Planeswalker, "Planeswalker"),
				(CardType.Instant, "Instant"),
				(CardType.Sorcery, "Sorcery"),
				(CardType.Land, "Land"),
			}
		)
			if (types.HasFlag(flag))
				names.Add(name);

		return string.Join(" ", names);
	}

	/// Card names really do contain commas — "Krenko, Mob Boss", "Goreclaw, Terror of Qal Sisma".
	private static string Escape(string value) =>
		value.Contains(',') || value.Contains('"') || value.Contains('\n')
			? $"\"{value.Replace("\"", "\"\"")}\""
			: value;
}
