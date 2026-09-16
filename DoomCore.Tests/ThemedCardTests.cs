using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The shared core plus each act's own slice.
///
/// **Card effects are the thing being guarded here.** Every unit in the game was a vanilla stat
/// line until these; a unit whose effect never fires looks exactly like one that worked, and the
/// only way to know is to test the consequence rather than the construction.
/// </summary>
public class ThemedCardTests
{
	[Test]
	public void AnActOnlyEverOffersItsOwnCardsOrTheSharedCore()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
		{
			foreach (var card in StarterContent.RewardPool(theme))
				Assert.That(
					card.Theme,
					Is.Null.Or.EqualTo(theme),
					$"{theme} offers {card.Name}, which belongs to {card.Theme}"
				);

			// And the slice is actually reaching the offer, not just the pool.
			var offered = Enumerable
				.Range(1, Run.ActLength)
				.SelectMany(f => StarterContent.RewardsFor(theme, seed: 7, floor: f))
				.ToList();

			Assert.That(
				offered.Select(c => c.Theme),
				Has.Some.EqualTo(theme),
				$"{theme} never actually offered one of its own cards across a whole act"
			);
		}
	}

	[Test]
	public void EveryActHasCardsOfItsOwn()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
			Assert.That(
				StarterContent.RewardPool(theme).Count(c => c.Theme == theme),
				Is.GreaterThan(0),
				$"{theme} is a reskin of the shared pool"
			);
	}

	/// <summary>
	/// An inert card is REFUSED by PlayCardAction, so a themed card with a malformed effect would
	/// be unplayable rather than merely disappointing. Cheaper to catch here than in a run.
	/// </summary>
	[Test]
	public void NoCardInAnyPoolIsInert()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
		foreach (var card in StarterContent.RewardPool(theme))
		{
			Assert.That(
				card.IsUnit || !card.Effects.IsEmpty,
				Is.True,
				$"{card.Name} is neither a body nor an effect, so it cannot be played at all"
			);

			foreach (var effect in card.Effects)
			{
				Assert.That(
					effect.Template,
					Is.Not.Null,
					$"{card.Name} has an effect with no action"
				);
				Assert.That(
					effect.Text,
					Is.Not.Empty,
					$"{card.Name} has an effect the player is never told about"
				);
			}
		}
	}

	/// <summary>
	/// A unit that pays out WHEN IT DIES. The trigger fires from ClearTheDead, which is a different
	/// path from the OnPlay the four rites use — and nothing in the game exercised it on a card.
	/// </summary>
	[Test]
	public void AUnitsDeathTriggerFiresAndHitsTheOpponent()
	{
		var gravedigger = StarterContent
			.RewardPool(DoomTheme.Rising)
			.Single(c => c.Name == "Gravedigger");

		var run = new Run { Life = 200, MaxLife = 200 }.WithCards([gravedigger]);
		var (state, _) = run.StartBattle(
			DoomScenario.Zombie,
			countdown: 9,
			[EnemyLibrary.SiegeHulk.ToEnemy(lane: 0)],
			opponentHealth: 500
		);

		var card = state.CardsIn(ZoneType.Hand).Single(c => c.Name == "Gravedigger");
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = card.Id, Lane = 0 })
			.ProcessAllActions();

		var before = state.GetOpponent().Health;

		// Siege Hulk out-hits a 4/4, so ending the turn kills it and the trigger has to fire.
		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.Units().Any(u => u.Name == "Gravedigger" && !u.Unit().IsDead), Is.False);
		Assert.That(
			state.GetOpponent().Health,
			Is.LessThan(before),
			"the Gravedigger died and its on-death effect did nothing"
		);
	}

	/// <summary>
	/// A unit that pays out WHEN THE DOOM FIRES — the most thematic trigger in the game and the one
	/// no card used before these. It only fires for a unit standing on the field, which is the
	/// whole reason the act that wants survivors is the act that gets these.
	/// </summary>
	[Test]
	public void AUnitsDoomTriggerFiresWhenTheApocalypseLands()
	{
		var crew = StarterContent
			.RewardPool(DoomTheme.LongEmergency)
			.Single(c => c.Name == "Reactor Crew");

		var run = new Run { Life = 100, MaxLife = 200 }.WithCards([crew]);

		// Countdown 1, so ending one turn lands the apocalypse.
		var (state, _) = run.StartBattle(DoomScenario.Flood, countdown: 1, [], opponentHealth: 500);

		var card = state.CardsIn(ZoneType.Hand).Single(c => c.Name == "Reactor Crew");
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = card.Id, Lane = 2 })
			.ProcessAllActions();

		var before = state.GetPlayer().Life;
		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "the doom should have landed");
		Assert.That(
			state.GetPlayer().Life,
			Is.GreaterThan(before),
			"the Reactor Crew was standing when the doom fired and healed nothing"
		);
	}
}
