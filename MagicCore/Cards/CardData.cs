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
/// A card's printed rules data as Scryfall publishes it — imported by tools/import_scryfall_set.py.
/// Static reference data, never stored in GameState. P/T stay strings: Scryfall prints "*" and "1+*".
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
	string Rarity,
	string CollectorNumber,
	CardFace[]? CardFaces
);

public static class CardDataSet
{
	static readonly JsonSerializerOptions Options =
		new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

	/// <summary>Loads an imported set by its code ("FRA").</summary>
	public static IReadOnlyList<CardData> Load(string setCode)
	{
		var text =
			ResourceText($"{setCode.ToUpperInvariant()}.json")
			?? throw new ArgumentException(
				$"No imported set '{setCode}'. Run: python tools/import_scryfall_set.py {setCode}"
			);
		return JsonSerializer.Deserialize<List<CardData>>(text, Options)!;
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
