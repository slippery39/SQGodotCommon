using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// At what pick position does a deck stop being willing to splash an off-colour card? Answers the
/// deckbuilding rule directly: an aggressive deck should demand a much better card before it pays
/// for a third colour than an expensive deck does.
/// </summary>
[TestFixture]
[Explicit("Diagnostic.")]
public class SplashEconomicsDiagnostic
{
	private static Card Spell(string name, ManaColor color, int cost) =>
		CardFactory.Creature(name, manaCost: cost, power: 2, toughness: 2).Build() with
		{
			ColorPips = ManaPool.Empty.Add(color, 1),
		};

	[Test]
	public void WhereDoesTheSplashStopBeingWorthIt()
	{
		var w = TestContext.Out;
		foreach (var cost in new[] { 1, 2, 3, 4, 5 })
		{
			var flip = -1;
			for (var position = 0; position < 25; position++)
			{
				// One off-colour card inserted at `position`; 25 on-colour cards otherwise, so the
				// deck can always fill 23 without it.
				var pool = new List<Card>();
				for (var i = 0; i < 26; i++)
				{
					if (i == position)
						pool.Add(Spell("Splash", ManaColor.Blue, cost));
					pool.Add(Spell($"Red{i}", ManaColor.Red, cost));
				}

				var deck = Draft.ChooseDeck(pool);
				var took = deck.Spells.Take(deck.Supported).Any(c => c.ColorPips.Blue > 0);
				if (!took)
				{
					flip = position;
					break;
				}
			}

			w.WriteLine(
				flip < 0
					? $"cost {cost}: splashes at every pick position tested"
					: $"cost {cost}: stops splashing once the card is pick {flip + 1} or later"
			);
		}
	}
}
