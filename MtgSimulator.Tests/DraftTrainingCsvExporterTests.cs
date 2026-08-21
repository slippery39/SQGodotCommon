using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The CSV is derived output nobody reads back, so it needs exactly two guarantees: it appears
/// whenever a model is saved, and a card name with a comma in it does not shift every column to
/// the right. The cube is full of the latter — "Krenko, Mob Boss", "Goreclaw, Terror of Qal
/// Sisma", "Nissa, Vastwood Seer".
/// </summary>
[TestFixture]
public class DraftTrainingCsvExporterTests
{
	private string _dir = "";

	[SetUp]
	public void Setup()
	{
		_dir = Path.Combine(Path.GetTempPath(), "csvexport_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_dir);
	}

	[TearDown]
	public void Cleanup()
	{
		if (Directory.Exists(_dir))
			Directory.Delete(_dir, recursive: true);
	}

	private static DraftTrainingData Model() =>
		DraftTrainingData.Empty with
		{
			Perspectives = 200,
			Wins = 100,
			Cards =
			[
				new CardStat("Krenko, Mob Boss", Games: 100, Wins: 70, DeckGames: 200),
				new CardStat("Lightning Bolt", Games: 100, Wins: 30, DeckGames: 200),
			],
		};

	/// <summary>
	/// Saving the model must produce the CSV with no separate call — that is the whole reason the
	/// export lives inside DraftTrainingStore.Save rather than in the console.
	/// </summary>
	[Test]
	public void Save_AlsoWritesTheCsv()
	{
		var jsonPath = Path.Combine(_dir, "draft_training_tst.json");

		DraftTrainingStore.Save(Model(), jsonPath);

		Assert.That(File.Exists(Path.Combine(_dir, "draft_training_tst.csv")), Is.True);
	}

	[Test]
	public void CardNamesWithCommas_AreQuoted_SoColumnsDoNotShift()
	{
		var path = DraftTrainingCsvExporter.Export(
			Model(),
			Path.Combine(_dir, "draft_training_tst.json")
		);

		var dataRows = File.ReadAllLines(path)
			.SkipWhile(l => !l.StartsWith("Name,"))
			.Skip(1)
			.Where(l => l.Length > 0)
			.ToList();

		Assert.Multiple(() =>
		{
			Assert.That(dataRows, Has.Count.EqualTo(2));
			Assert.That(
				dataRows[0],
				Does.StartWith("\"Krenko, Mob Boss\","),
				"an unquoted comma name silently shifts every later column"
			);
			// Ten columns means the comma inside the name was not treated as a separator.
			Assert.That(SplitCsv(dataRows[0]), Has.Count.EqualTo(10));
		});
	}

	/// <summary>
	/// Best first. The two ends of this file are the interesting ones — the bombs and the cards
	/// that might be doing nothing — and a name sort buries both in the middle.
	/// </summary>
	[Test]
	public void Rows_AreSortedByDeltaDescending()
	{
		var path = DraftTrainingCsvExporter.Export(
			Model(),
			Path.Combine(_dir, "draft_training_tst.json")
		);

		var names = File.ReadAllLines(path)
			.SkipWhile(l => !l.StartsWith("Name,"))
			.Skip(1)
			.Where(l => l.Length > 0)
			.Select(l => SplitCsv(l)[0])
			.ToList();

		Assert.That(names, Is.EqualTo(new[] { "Krenko, Mob Boss", "Lightning Bolt" }));
	}

	/// Minimal RFC-4180 split — enough to prove quoting works.
	private static List<string> SplitCsv(string line)
	{
		var fields = new List<string>();
		var current = "";
		var inQuotes = false;

		for (var i = 0; i < line.Length; i++)
		{
			var ch = line[i];
			if (ch == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}
			if (ch == ',' && !inQuotes)
			{
				fields.Add(current);
				current = "";
				continue;
			}
			current += ch;
		}

		fields.Add(current);
		return fields;
	}
}
