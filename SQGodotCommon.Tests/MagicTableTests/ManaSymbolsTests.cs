using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>Costs drawn as Scryfall's mana symbols (story LIM-5.1).</summary>
public class ManaSymbolsTests
{
	[Test]
	public void ACost_BecomesOneImagePerSymbol()
	{
		Assert.That(
			ManaSymbols.Bbcode("{2}{W/U}", 20),
			Is.EqualTo(
				$"[img=20x20]{ManaSymbols.Dir}2.svg[/img][img=20x20]{ManaSymbols.Dir}WU.svg[/img]"
			)
		);
		Assert.That(ManaSymbols.Bbcode(null, 20), Is.Empty, "a land has no cost");
	}

	[Test]
	public void EverySymbolTheSetsPrint_HasItsFile()
	{
		var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
		while (!File.Exists(Path.Combine(dir.FullName, "SQGodotCommon.sln")))
			dir = dir.Parent!;
		var symbols = Path.Combine(
			dir.FullName,
			"SQGodotCommon",
			"MagicTable",
			"Assets",
			"Symbols"
		);
		var missing = CardRegistry
			.Sets.SelectMany(CardDataSet.Load)
			.SelectMany(p =>
				new[] { p.Card.ManaCost }.Concat(p.Card.CardFaces?.Select(f => f.ManaCost) ?? [])
			)
			.SelectMany(cost =>
				Regex.Matches(cost ?? "", @"\{([^}]+)\}").Select(m => m.Groups[1].Value)
			)
			.Distinct()
			.Where(symbol => !File.Exists(Path.Combine(symbols, ManaSymbols.File(symbol))))
			.ToList();
		Assert.That(missing, Is.Empty, "run python tools/fetch_mana_symbols.py");
	}
}
