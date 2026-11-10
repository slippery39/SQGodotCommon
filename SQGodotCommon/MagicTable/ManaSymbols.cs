using System.Text.RegularExpressions;

namespace MagicTable;

/// <summary>
/// A printed cost as Scryfall's mana symbols (LIM-5.1): "{2}{W/U}" → BBCode images for a RichTextLabel.
/// The files are Scryfall's own (tools/fetch_mana_symbols.py), named as Scryfall names them: the symbol
/// without its braces and slashes ({2/W} is 2W.svg). Plain C#, tested.
/// ponytail: a symbol with no file ({17}+, none in FRA or TRK) draws nothing — the test lists what the sets print.
/// </summary>
public static class ManaSymbols
{
	public const string Dir = "res://MagicTable/Assets/Symbols/";

	/// <summary>The file a symbol's image is, by its text inside the braces: "W/U" → "WU.svg".</summary>
	public static string File(string symbol) => $"{symbol.Replace("/", "")}.svg";

	/// <summary>Each {symbol} as an image <paramref name="size"/> px square; anything else stays text.</summary>
	public static string Bbcode(string? cost, int size) =>
		cost is null
			? ""
			: Regex.Replace(
				cost,
				@"\{([^}]+)\}",
				m => $"[img={size}x{size}]{Dir}{File(m.Groups[1].Value)}[/img]"
			);
}
