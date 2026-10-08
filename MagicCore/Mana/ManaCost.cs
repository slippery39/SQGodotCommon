using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MagicCore;

public enum SymbolKind
{
	Generic, // {3} — Amount
	Colored, // {W} — Color; {C} is Colored with Color = Colorless (only colourless mana pays it)
	Hybrid, // {W/U} — Color or Color2
	TwoBrid, // {2/W} — Color, or two of anything
	X,
}

public record ManaSymbol(
	SymbolKind Kind,
	ManaColor Color = default,
	ManaColor Color2 = default,
	int Amount = 0
);

/// <summary>A printed mana cost, parsed from Scryfall's "{2}{W/U}{X}" notation.</summary>
public record ManaCost(ImmutableArray<ManaSymbol> Symbols)
{
	public static readonly ManaCost None = new(ImmutableArray<ManaSymbol>.Empty);

	public bool HasX => Symbols.Any(s => s.Kind == SymbolKind.X);

	/// <summary>CR 202.3: X is 0 everywhere but the stack; {2/W} counts 2.</summary>
	public int ManaValue =>
		Symbols.Sum(s =>
			s.Kind switch
			{
				SymbolKind.Generic => s.Amount,
				SymbolKind.TwoBrid => 2,
				SymbolKind.X => 0,
				_ => 1,
			}
		);

	/// <summary>
	/// Adds (positive) or removes (negative) generic mana. A reduction only ever touches generic —
	/// coloured pips stay (CR 601.2f).
	/// </summary>
	public ManaCost WithGeneric(int delta)
	{
		if (delta > 0)
		{
			var generic = Symbols.ToList().FindIndex(sym => sym.Kind == SymbolKind.Generic);
			return generic >= 0
				? new(
					Symbols.SetItem(
						generic,
						Symbols[generic] with
						{
							Amount = Symbols[generic].Amount + delta,
						}
					)
				)
				: new(Symbols.Insert(0, new ManaSymbol(SymbolKind.Generic, Amount: delta)));
		}
		var symbols = Symbols.ToBuilder();
		for (var i = 0; i < symbols.Count && delta < 0; i++)
		{
			if (symbols[i].Kind != SymbolKind.Generic)
				continue;
			var cut = Math.Min(symbols[i].Amount, -delta);
			symbols[i] = symbols[i] with { Amount = symbols[i].Amount - cut };
			delta += cut;
		}
		return new(
			symbols
				.Where(sym => sym is not { Kind: SymbolKind.Generic, Amount: 0 })
				.ToImmutableArray()
		);
	}

	public static ManaCost Parse(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return None;
		return new(
			Regex
				.Matches(text, @"\{([^}]+)\}")
				.Select(m => ParseSymbol(m.Groups[1].Value))
				.ToImmutableArray()
		);
	}

	static ManaSymbol ParseSymbol(string s)
	{
		if (int.TryParse(s, out var n))
			return new(SymbolKind.Generic, Amount: n);
		if (s == "X")
			return new(SymbolKind.X);
		var parts = s.Split('/');
		if (parts.Length == 1)
			return new(SymbolKind.Colored, ColorOf(s));
		if (parts[0] == "2")
			return new(SymbolKind.TwoBrid, ColorOf(parts[1]));
		if (parts.Length == 2 && parts[1] != "P")
			return new(SymbolKind.Hybrid, ColorOf(parts[0]), ColorOf(parts[1]));
		throw new FormatException($"Unsupported mana symbol {{{s}}}"); // Phyrexian: no FRA card has one
	}

	public static ManaColor ColorOf(string letter) =>
		letter switch
		{
			"W" => ManaColor.White,
			"U" => ManaColor.Blue,
			"B" => ManaColor.Black,
			"R" => ManaColor.Red,
			"G" => ManaColor.Green,
			"C" => ManaColor.Colorless,
			_ => throw new FormatException($"Unknown mana letter '{letter}'"),
		};

	static string LetterOf(ManaColor color) => "WUBRGC"[(int)color].ToString();

	/// <summary>The printed form, "{2}{W/U}" — what Parse reads. Needed wherever a cost is shown.</summary>
	public override string ToString() =>
		string.Concat(
			Symbols.Select(s =>
				s.Kind switch
				{
					SymbolKind.Generic => $"{{{s.Amount}}}",
					SymbolKind.Hybrid => $"{{{LetterOf(s.Color)}/{LetterOf(s.Color2)}}}",
					SymbolKind.TwoBrid => $"{{2/{LetterOf(s.Color)}}}",
					SymbolKind.X => "{X}",
					_ => $"{{{LetterOf(s.Color)}}}",
				}
			)
		);
}
