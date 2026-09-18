using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// **The shipped card, not the mechanism.** `ShopTests` and `SynergyTests` prove exhaust works on an
/// inline card; these prove the card the game actually deals you is wired to use it. A flag authored
/// on a library entry and lost on the way to the battle looks exactly like a flag that worked.
/// </summary>
public class ExhaustContentTests
{
	private static RunCard Shipped(string name) =>
		Enum.GetValues<DoomTheme>()
			.SelectMany(t => StarterContent.RewardPool(t).AsEnumerable())
			.First(c => c.Name == name);

	[Test]
	public void FieldDressingIsAuthoredToExhaust()
	{
		var card = Shipped("Field Dressing");

		Assert.That(card.Exhausts, Is.True, "the shipped Field Dressing does not exhaust");
		Assert.That(
			card.Effects.Any(e => e.Text.Contains("Exhaust", StringComparison.OrdinalIgnoreCase)),
			Is.True,
			"and its rules text has to say so, or the player cannot know"
		);
	}

	/// <summary>
	/// **The one that actually matters: it heals ONCE in a battle, not every time it cycles.**
	///
	/// A one-card deck is drawn, played, and then drawn again the moment Discard reshuffles — which
	/// is every turn. Without exhaust this heals on every single turn of the fight.
	/// </summary>
	[Test]
	public void FieldDressingHealsOnceAndDoesNotComeBack()
	{
		var run = new Run { Life = 10, MaxLife = 500 }.WithCards([Shipped("Field Dressing")]);

		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 99,
			[],
			opponentHealth: 5000
		);

		var card = state.CardsIn(ZoneType.Hand).Single(c => c.Name == "Field Dressing");
		Assert.That(
			card.Exhausts,
			Is.True,
			"the flag was lost between the run deck and the battle"
		);

		(state, _) = state.AddAction(new PlayCardAction { CardId = card.Id }).ProcessAllActions();

		Assert.That(state.GetPlayer().Life, Is.GreaterThan(10), "sanity: it healed at all");

		// **Counted, not inferred from life.** The Opponent reinforces into an empty line and those
		// bodies hit you, so life moves for reasons that have nothing to do with this card — the
		// first version of this test read that as a failure and was measuring the wrong thing.
		var plays = 0;

		for (var turn = 0; turn < 6; turn++)
		{
			(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

			foreach (var inHand in state.CardsIn(ZoneType.Hand).ToList())
			{
				var (next, ok) = state.TryAddAction(new PlayCardAction { CardId = inHand.Id });
				if (ok)
				{
					(state, _) = next.ProcessAllActions();
					plays++;
				}
			}
		}

		Assert.That(
			plays,
			Is.Zero,
			"a one-card deck redrew and replayed Field Dressing — exhaust is not holding"
		);
		Assert.That(
			state.GetParent(card.Id),
			Is.EqualTo(state.ZoneId(ZoneType.Exhausted)),
			"and it should be sitting in the exhaust pile"
		);
	}

	/// <summary>
	/// **A doom transform must not strip the flag.** Nuclear and Grey Goo rewrite deck entries; if
	/// either rebuilt a card instead of `with`-ing it, an irradiated Field Dressing would quietly
	/// stop exhausting and the deck would get a healing engine nobody authored.
	/// </summary>
	[Test]
	public void ATransformedCardKeepsItsExhaust()
	{
		var card = Shipped("Field Dressing");

		var modified = card with { Power = card.Power + 2 };
		Assert.That(modified.Exhausts, Is.True);

		var battleCard = card.ToDoomCard();
		Assert.That(battleCard.Exhausts, Is.True, "ToDoomCard dropped it");
	}
}
