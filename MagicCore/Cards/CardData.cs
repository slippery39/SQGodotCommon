using System.Text.Json;

namespace MagicCore.Cards;

/// <summary>One face of a multi-face card (FRA's "prepare" layout: creature // spell).</summary>
public sealed record CardFace(
	string Name,
	string? ManaCost,
	string TypeLine,
	string? OracleText,
	string? Power,
	string? Toughness,
	string? Loyalty
);

/// <summary>
/// A card's printed RULES data as Scryfall publishes it — imported by tools/import_scryfall_set.py.
/// The same in every set that prints the card (CardRegistry checks it); what differs by set is the
/// <see cref="CardPrinting"/>. Static reference data, never stored in GameState. P/T stay strings:
/// Scryfall prints "*" and "1+*".
/// </summary>
public sealed record CardData(
	string Name,
	string Layout,
	string? ManaCost,
	double Cmc,
	string TypeLine,
	string? OracleText,
	string? Power,
	string? Toughness,
	string? Loyalty,
	string[]? Colors,
	string[] Keywords,
	CardFace[]? CardFaces
);

/// <summary>
/// One set's printing of a card: its rarity and collector number there (its art is Art/&lt;set&gt;/,
/// MagicArt). A reprint has a printing per set and ONE <see cref="CardData"/> (MC-5.5).
/// </summary>
public sealed record CardPrinting(string Set, CardData Card, string Rarity, string CollectorNumber);

public static class CardDataSet
{
	static readonly JsonSerializerOptions Options =
		new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

	// The imported file's row: rules data and printing data side by side, as Scryfall lists them.
	sealed record Row(
		string Name,
		string Layout,
		string? ManaCost,
		double Cmc,
		string TypeLine,
		string? OracleText,
		string? Power,
		string? Toughness,
		string? Loyalty,
		string[]? Colors,
		string[] Keywords,
		string Rarity,
		string CollectorNumber,
		CardFace[]? CardFaces
	);

	/// <summary>Loads an imported set's printings by its code ("FRA").</summary>
	public static IReadOnlyList<CardPrinting> Load(string setCode)
	{
		var set = setCode.ToUpperInvariant();
		var text =
			ResourceText($"{set}.json")
			?? throw new ArgumentException(
				$"No imported set '{setCode}'. Run: python tools/import_scryfall_set.py {setCode}"
			);
		return
		[
			.. JsonSerializer
				.Deserialize<List<Row>>(text, Options)!
				.Select(r => new CardPrinting(
					set,
					new(
						r.Name,
						r.Layout,
						r.ManaCost,
						r.Cmc,
						r.TypeLine,
						r.OracleText,
						r.Power,
						r.Toughness,
						r.Loyalty,
						r.Colors,
						r.Keywords,
						r.CardFaces
					),
					r.Rarity,
					r.CollectorNumber
				)),
		];
	}

	/// <summary>
	/// A file from MagicCore/Sets/, embedded by the csproj as <c>MagicCore.Sets.{file}</c> — the ONE place
	/// that name is built. Null if there is no such file.
	/// </summary>
	public static string? ResourceText(string file)
	{
		using var stream = typeof(CardDataSet).Assembly.GetManifestResourceStream(
			$"MagicCore.Sets.{file}"
		);
		if (stream is null)
			return null;
		using var reader = new StreamReader(stream);
		return reader.ReadToEnd();
	}
}
