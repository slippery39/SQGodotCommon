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

	/// <summary>
	/// **A rare must be able to turn up on floor 1.** This is the whole reason rarity is a weight
	/// and not a floor gate: an early rare you get to build the rest of the run around is where a
	/// memorable run comes from, and gating it away buys a tidier difficulty curve with the best
	/// runs in the game.
	///
	/// Floor-gating WAS built and measured first — `docs/findings/doom-balance.md` run 9 — so this
	/// test is the shape of a decision, not a precaution.
	/// </summary>
	[Test]
	public void ARareCanBeOfferedOnTheFirstFloor()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
		{
			var offeredOnFloorOne = Enumerable
				.Range(1, 200)
				.SelectMany(seed => StarterContent.RewardsFor(theme, seed, floor: 1))
				.ToList();

			Assert.That(
				offeredOnFloorOne.Select(c => c.Rarity),
				Has.Some.EqualTo(DoomRarity.Rare),
				$"{theme} never offered a rare on floor 1 across 200 seeds — rarity is gating"
			);
		}
	}

	/// <summary>
	/// Rarity is doing something: rarer means rarer, in the order the weights say.
	///
	/// Reads <see cref="StarterContent.WeightOf"/> for the ORDER rather than asserting a share, so
	/// retuning the weights does not break it — but a rarity that stopped being rare, or a weight
	/// table that stopped being consulted, still does.
	/// </summary>
	[Test]
	public void RarerCardsAreOfferedLessOften()
	{
		var offers = ThemeLibrary
			.All.SelectMany(t =>
				Enumerable
					.Range(1, 400)
					.SelectMany(seed =>
						Enumerable
							.Range(1, Run.ActLength)
							.SelectMany(floor => StarterContent.RewardsFor(t.Theme, seed, floor))
					)
			)
			.ToList();

		// Per CARD, not per rarity — there are more uncommons than rares in the pool, so the
		// class totals would compare bag sizes rather than the weights.
		double ShareEach(DoomRarity rarity) =>
			(double)offers.Count(c => c.Rarity == rarity)
			/ ThemeLibrary.All.Sum(t =>
				StarterContent.RewardPool(t.Theme).Count(c => c.Rarity == rarity)
			);

		Assert.That(
			ShareEach(DoomRarity.Common),
			Is.GreaterThan(ShareEach(DoomRarity.Uncommon)),
			"a common is offered no more often than an uncommon"
		);
		Assert.That(
			ShareEach(DoomRarity.Uncommon),
			Is.GreaterThan(ShareEach(DoomRarity.Rare)),
			"an uncommon is offered no more often than a rare"
		);
	}

	/// <summary>
	/// Every card is offerable on every floor. The counterpart to
	/// <see cref="ARareCanBeOfferedOnTheFirstFloor"/>: that one proves rares reach floor 1, this
	/// one proves nothing anywhere is gated.
	/// </summary>
	[Test]
	public void NoFloorOffersASmallerPoolThanAnyOther()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
		{
			var pool = StarterContent.RewardPool(theme).Length;

			foreach (var floor in Enumerable.Range(1, Run.ActLength))
			{
				var offered = StarterContent.RewardsFor(theme, seed: 7, floor: floor, count: pool);

				Assert.That(
					offered.Select(c => c.Name).Distinct().Count(),
					Is.EqualTo(pool),
					$"{theme} floor {floor} could not offer the whole pool — something gates it"
				);
			}
		}
	}

	/// <summary>
	/// **A reward screen with fewer than three cards is a decision that is not one.** Cheap to
	/// hold, and the thing any change to how rewards are drawn can quietly break.
	/// </summary>
	[Test]
	public void EveryFloorOffersThreeDistinctCards()
	{
		foreach (var theme in ThemeLibrary.All.Select(t => t.Theme))
		foreach (var floor in Enumerable.Range(1, Run.ActLength))
			Assert.That(
				StarterContent
					.RewardsFor(theme, seed: 7, floor: floor)
					.Select(c => c.Name)
					.Distinct()
					.Count(),
				Is.EqualTo(3),
				$"{theme} floor {floor} could not offer three distinct cards"
			);
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
	///
	/// **The card is defined INLINE.** It used to fetch Gravedigger out of the reward pool, and
	/// when Gravedigger's death trigger was redesigned into a per-turn one this failed — a test
	/// about a MECHANISM, broken by a content decision it was never asserting. That is the whole
	/// reason for the inline rule, caught in the act.
	/// </summary>
	[Test]
	public void AUnitsDeathTriggerFiresAndHitsTheOpponent()
	{
		var gravedigger = new RunCard
		{
			Name = "Gravedigger",
			Cost = 1,
			IsUnit = true,
			Power = 4,
			Toughness = 4,
			Effects =
			[
				new DoomEffect
				{
					Trigger = EffectTrigger.OnDeath,
					Target = DoomTarget.Opponent,
					Template = new DealDamageAction { Amount = 16 },
					Text = "on death: 16 to the Opponent",
				},
			],
		};

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
		// **Selected BY TRIGGER, not by name.** This test used to name Reactor Crew, and the v3
		// pool re-cut gave that card an OnPlay effect instead — so the test failed with "healed
		// nothing", which reads like a broken trigger rather than a renamed one. Asking the act
		// for a card that still has the trigger means a re-cut can move abilities around freely,
		// and the assertion below fails LOUDLY and correctly if the act ever has none.
		var doomCard = StarterContent
			.RewardPool(DoomTheme.LongEmergency)
			.FirstOrDefault(c =>
				c.IsUnit && c.Effects.Any(e => e.Trigger == EffectTrigger.OnDoomFires)
			);

		Assert.That(
			doomCard,
			Is.Not.Null,
			"The Long Emergency has no unit with a Doom trigger at all. That is the act's whole "
				+ "identity — its dooms read what is STANDING — so either restore one or this "
				+ "test is asserting a design that no longer exists."
		);

		var run = new Run { Life = 100, MaxLife = 200 }.WithCards([doomCard!]);

		// Countdown 1, so ending one turn lands the apocalypse. An enemy to be hit by whatever the
		// trigger does, in a lane the unit is not holding.
		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 1,
			[EnemyLibrary.SiegeHulk.ToEnemy(lane: 0)],
			opponentHealth: 500
		);

		var card = state.CardsIn(ZoneType.Hand).Single(c => c.Name == doomCard!.Name);
		(state, _) = state
			.AddAction(new PlayCardAction { CardId = card.Id, Lane = 2 })
			.ProcessAllActions();

		// Snapshot everything the trigger could plausibly move, so this does not have to know
		// WHICH doom card the act happens to ship.
		var life = state.GetPlayer().Life;
		var enemyHealth = state.LivingEnemies().Sum(e => e.Health);
		var hand = state.CardsIn(ZoneType.Hand).Count();

		(state, _) = state.AddAction(new EndTurnAction()).ProcessAllActions();

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "the doom should have landed");
		Assert.That(
			state.GetPlayer().Life != life
				|| state.LivingEnemies().Sum(e => e.Health) != enemyHealth
				|| state.CardsIn(ZoneType.Hand).Count() != hand,
			Is.True,
			$"{doomCard!.Name} was standing when the doom fired and nothing happened — "
				+ "life, enemy health and hand size were all unchanged"
		);
	}
}
