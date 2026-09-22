using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// TAG ALONG. The companion is the one thing no apocalypse can take, and the one thing that
/// remembers every apocalypse it survived.
/// </summary>
public class CompanionTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static Run RunWith(Companion companion, params RunCard[] deck) =>
		new Run
		{
			Life = 60,
			MaxLife = 60,
			Companion = companion,
		}.WithCards(deck);

	private static Companion Dog =>
		new()
		{
			Name = "Ash",
			BasePower = 1,
			BaseToughness = 3,
		};

	private static RunCard Unit(string name, int power, int toughness, int cost = 0) =>
		new()
		{
			Name = name,
			Cost = cost,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
		};

	/// <summary>
	/// Defaults to the COMPANION'S lane, because every test here is about the companion. An enemy
	/// parked anywhere else would never meet it and the test would pass by not happening.
	/// </summary>
	private static Enemy Enemy(int attack, int health = 50, int lane = KinBattle.LaneCount / 2) =>
		new()
		{
			Name = attack > 0 ? "Wretch" : "Idler",
			Health = health,
			MaxHealth = health,
			Intent = attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = attack,
			Lane = lane,
		};

	/// <summary>
	/// Ends turns until the battle is over, or until the cap.
	///
	/// The cap turns a rules regression into a failed test rather than a hung suite, which is how
	/// a countdown that stopped resetting was found in the first place.
	/// </summary>
	private static GameState PlayOut(GameState state, int turns = 3)
	{
		for (var turn = 0; turn < turns; turn++)
		{
			if (state.GetBattle().IsOver)
				return state;

			(state, _) = Do(state, new EndTurnAction());
		}

		return state;
	}

	[Test]
	public void TheCompanionIsOnTheFieldFromTheFirstTurnWithoutBeingPlayed()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));
		var (state, _) = run.StartBattle([Enemy(0)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		Assert.That(companion.Name, Does.StartWith("Ash"));
		Assert.That(companion.Unit().Power, Is.EqualTo(Dog.BasePower));
		Assert.That(companion.Unit().Toughness, Is.EqualTo(Dog.BaseToughness));
		Assert.That(state.CardsIn(ZoneType.Hand).Any(c => c.Name.StartsWith("Ash")), Is.False);
	}

	// ===== The doom cannot touch it =====

	/// <summary>
	/// Flood sweeps every unit off the board. The companion stays: it is not a card and has nowhere
	/// to be discarded TO — sending it to Discard would make it drawable.
	/// </summary>
	// **Six tests lived here and were cut with the mechanic they described (2026-09-18):** marks
	// accumulating, being carried into the next battle's stat line, and being collapsed in the
	// companion's name. Every apocalypse survived used to stamp a permanent +N/+N on the companion.
	// It was cut on playtest feedback — "I never liked this mechanic" — and it was a stat trickle
	// nobody chose attached to a name that grew until it had to be collapsed to stay on screen.
	//
	// What SURVIVES below is everything about the companion that was never about marks: it is on the
	// board free, no transform can touch it, and its death is not a deck event.




	[Test]
	public void TheCompanionBlocksLikeAnyUnitSoABadDrawIsNeverAnEmptyBoard()
	{
		var run = RunWith(Dog);
		Assert.That(run.Deck, Is.Empty, "no cards at all");

		var (state, _) = run.StartBattle([Enemy(5)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetPlayer().Life, Is.EqualTo(58), "3 toughness absorbed 3 of 5");
	}

	[Test]
	public void ADeadCompanionDoesNotFeedZombieAndReturnsNextBattle()
	{
		var run = RunWith(Dog, Unit("Filler", 1, 1));

		// 9 damage against a 1/3 companion blocking: it dies every time.
		var (state, _) = run.StartBattle([Enemy(9)]);

		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());

		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(events.OfType<UnitDiedEvent>().Any(e => e.CardName.StartsWith("Ash")), Is.True);
		Assert.That(
			state.GetBattle().DiedThisTurnRunCardIds,
			Is.Empty,
			"a companion death is not a deck event"
		);
		Assert.That(state.HasObject(companion.Id), Is.False, "it left the battle, not to Discard");

		var after = run.AfterBattle(PlayOut(state));

		Assert.That(
			after.Deck.Any(c => c.Name == "Zombie"),
			Is.False,
			"no free Zombie from it dying"
		);

		var (next, _) = after.StartBattle([Enemy(0)]);
		Assert.That(
			next.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"it comes back"
		);
	}

	// ===== The roster =====

	/// <summary>
	/// **Every companion on the roster must actually DO something, and this is the only proof.**
	///
	/// An ability that never fires throws nothing, renders fine and reads as working — this repo
	/// has found four silent no-ops that way. So each case below drives a real battle and asserts
	/// the CONSEQUENCE: life gained, damage dealt, a stat that moved. Never that an effect was
	/// declared.
	///
	/// Two of these would be blank if written on the obvious trigger. `OnTurnStart` sees an empty
	/// field (units withdraw at end of turn) and a zeroed `CardsPlayedThisTurn`, so Bramble and
	/// Tally both have to pay at turn END. That is exactly the mistake this test exists to catch.
	/// </summary>
	[Test]
	public void BramblePaysLifeForWhatSurvived()
	{
		// **Starts HURT on purpose.** A heal at full life is clamped away, so the obvious version
		// of this test asserts 0 and passes for the wrong reason — which is the exact failure
		// shape these tests exist to catch.
		var run = new Run
		{
			Life = 40,
			MaxLife = 60,
			Companion = StarterContent.Bramble,
		};
		var (state, _) = run.StartBattle([Enemy(attack: 0, lane: 0)], opponentHealth: 500);

		var before = state.GetPlayer().Life;
		(state, _) = Do(state, new EndTurnAction());

		// The companion is the only unit standing, so exactly one unit is counted.
		Assert.That(
			state.GetPlayer().Life - before,
			Is.EqualTo(2),
			"2 life per unit still standing, and the companion is one"
		);
	}

	[Test]
	public void TallyPaysTheOpponentForEveryCardPlayed()
	{
		var run = RunWith(StarterContent.Tally, Unit("Cheap", 1, 1), Unit("Cheaper", 1, 1));
		var (state, _) = run.StartBattle([Enemy(attack: 0, lane: 0)], opponentHealth: 500);

		var played = 0;
		foreach (var card in state.CardsIn(ZoneType.Hand).Take(2).ToList())
		{
			var lane = state.OpenLanes().FirstOrDefault(-1);
			if (lane < 0)
				continue;

			var (next, ok) = state.TryAddAction(
				new PlayCardAction { CardId = card.Id, Lane = lane }
			);
			if (!ok)
				continue;

			(state, _) = next.ProcessAllActions();
			played++;
		}

		Assume.That(played, Is.GreaterThan(0), "the test has to actually play a card");

		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new EndTurnAction());
		var dealt = before - state.GetOpponent().Health;

		// The lanes also trade, so this asserts the ability is IN there rather than an exact total.
		Assert.That(
			dealt,
			Is.GreaterThanOrEqualTo(3 * played),
			$"3 to the Opponent per card played, and {played} were played"
		);
	}

	[Test]
	public void PikeHitsTheOpponentEveryTurn()
	{
		var run = RunWith(StarterContent.Pike);
		var (state, _) = run.StartBattle([Enemy(attack: 0, lane: 0)], opponentHealth: 500);

		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new EndTurnAction());

		// 6 from the ability, plus whatever the companion's own power put through its empty lane.
		Assert.That(before - state.GetOpponent().Health, Is.GreaterThanOrEqualTo(6));
	}

	[Test]
	public void MossBurnsTheLanesEitherSideOfIt()
	{
		// Lane 2 is the companion's, so lane 1 is adjacent and lane 4 is not. Both are needed:
		// a splash that hit EVERYTHING would pass a test that only looked at the neighbour.
		var run = RunWith(StarterContent.Moss);
		var (state, _) = run.StartBattle(
			[Enemy(attack: 0, health: 50, lane: 1), Enemy(attack: 0, health: 50, lane: 4)],
			opponentHealth: 500
		);

		(state, _) = Do(state, new EndTurnAction());

		var neighbour = state.LivingEnemies().Single(e => e.Lane == 1);
		var faraway = state.LivingEnemies().Single(e => e.Lane == 4);

		Assert.Multiple(() =>
		{
			Assert.That(neighbour.Health, Is.EqualTo(45), "5 to the lane next door");
			Assert.That(faraway.Health, Is.EqualTo(50), "and nothing to a lane two over");
		});
	}

	[Test]
	public void EveryCompanionOnTheRosterDeclaresAnAbilityWithText()
	{
		Assert.Multiple(() =>
		{
			foreach (var companion in StarterContent.Roster)
			{
				Assert.That(
					companion.Effects,
					Is.Not.Empty,
					$"{companion.Name} does nothing at all"
				);
				Assert.That(
					companion.Effects.Any(e => e.Text.Length > 0),
					Is.True,
					$"{companion.Name} has no text, so the player is never told what it does"
				);
			}
		});
	}

	// ===== Upgrades — the run's power curve =====

	[Test]
	public void AStatUpgradeRaisesTheCompanionForTheNextBattle()
	{
		var run = RunWith(StarterContent.Bramble)
			.WithCompanionUpgrade(new CompanionUpgrade { Power = 5, Toughness = 8 });

		Assert.Multiple(() =>
		{
			Assert.That(run.Companion.Power, Is.EqualTo(StarterContent.Bramble.Power + 5));
			Assert.That(run.Companion.Toughness, Is.EqualTo(StarterContent.Bramble.Toughness + 8));
		});

		// The board is what matters, not the record — read it off the unit that actually stands.
		var (state, _) = run.StartBattle([Enemy(attack: 0, lane: 0)], opponentHealth: 500);
		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>()).Unit();

		Assert.That(companion.Power, Is.EqualTo(StarterContent.Bramble.Power + 5));
	}

	/// <summary>
	/// **Echo must copy EVERY effect, and Ash is the case that proves it.**
	///
	/// Ash's ability is a symmetric pair — a buff at turn start and the same buff negated at turn
	/// end — which is the entire duration system. Copying only the first effect would double the
	/// buff and leave the unwind single, so Ash would gain 2 power every turn and never give it
	/// back. Nothing would throw; the companion would just quietly run away with the game.
	///
	/// So this asserts the NET after a full turn, not the list length. A test that counted effects
	/// would pass on the broken version.
	/// </summary>
	[Test]
	public void EchoDoublesAnAbilityWithoutBreakingItsUnwind()
	{
		var echoed = StarterContent.StarterCompanion.With(
			new CompanionUpgrade { EchoesAbility = true }
		);

		var run = RunWith(echoed);
		var (state, _) = run.StartBattle([Enemy(attack: 0, lane: 0)], opponentHealth: 500);

		var before = Companion(state).Power;

		// A full turn with nothing dying: the buff reads zero, so an intact pair nets zero.
		(state, _) = Do(state, new EndTurnAction());
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			Companion(state).Power,
			Is.EqualTo(before),
			"an echoed pair must still unwind to nothing — a doubled buff with a single unwind "
				+ "drifts upward every turn and nothing reports it"
		);
	}

	/// <summary>
	/// **Echo has to double the OUTPUT, and nothing proved that until this test.**
	///
	/// `EchoDoublesAnAbilityWithoutBreakingItsUnwind` asserts a symmetric pair still nets zero,
	/// which an entirely INERT Echo also passes, and
	/// `EchoCopiesWhatTheCompanionHasAtTheTimeItIsTaken` counts list entries — the exact shape
	/// this repo keeps rediscovering, a test that proves the construction rather than the
	/// consequence. So this reads the Opponent's health, which is the only thing that settles it.
	///
	/// Pike is the case worth using: a flat 6 to the Opponent at end of turn, no board reads, so
	/// the number cannot come from anywhere else.
	/// </summary>
	[Test]
	public void EchoActuallyDoublesWhatTheCompanionDealsToTheOpponent()
	{
		var echo = StarterContent.UpgradePool.Single(u => u.Name == "Echo");

		var plain = Damage(StarterContent.Pike);
		var echoed = Damage(StarterContent.Pike.With(echo));

		Assert.Multiple(() =>
		{
			Assert.That(plain, Is.EqualTo(6), "Pike deals a flat 6 at end of turn");
			Assert.That(
				echoed,
				Is.EqualTo(12),
				"an echoed Pike must deal it TWICE — an Echo that changes no number on the board "
					+ "is a rare that costs the player a pick and does nothing"
			);
		});
	}

	/// <summary>
	/// What one end of turn takes off the Opponent, from the ABILITY alone.
	///
	/// **The enemy in the companion's lane is the whole point of this helper.** An open lane sends
	/// the companion's power straight at the Opponent, so without a blocker this reads 8 + 6 = 14
	/// for Pike and the ability's share is buried in it. A wall that outlives the turn absorbs the
	/// body and leaves only the ability's damage on the Opponent.
	/// </summary>
	private static int Damage(Companion companion)
	{
		var (state, _) = RunWith(companion)
			.StartBattle([Enemy(attack: 0, health: 500)], opponentHealth: 500);
		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new EndTurnAction());
		return before - state.GetOpponent().Health;
	}

	[Test]
	public void EchoCopiesWhatTheCompanionHasAtTheTimeItIsTaken()
	{
		var barbed = StarterContent.UpgradePool.Single(u => u.Name == "Barbed");
		var echo = StarterContent.UpgradePool.Single(u => u.Name == "Echo");

		var early = StarterContent.Pike.With(echo).With(barbed);
		var late = StarterContent.Pike.With(barbed).With(echo);

		Assert.That(
			late.Effects,
			Has.Count.GreaterThan(early.Effects.Count),
			"an echo taken later copies more, which is the only interaction between upgrades"
		);
	}

	[Test]
	public void EveryOfferedUpgradeIsDistinctAndSaysWhatItDoes()
	{
		var floor = StarterContent.FloorsPerUpgrade;
		Assume.That(StarterContent.OffersUpgradeOn(floor), Is.True);

		var offered = StarterContent.UpgradesFor(seed: 4, floor);

		Assert.Multiple(() =>
		{
			Assert.That(offered, Has.Length.EqualTo(3));
			Assert.That(
				offered.Select(u => u.Name).Distinct().Count(),
				Is.EqualTo(3),
				"three offers without replacement, or a floor can offer the same thing twice"
			);

			foreach (var upgrade in offered)
			{
				Assert.That(upgrade.Text, Is.Not.Empty, $"{upgrade.Name} tells the player nothing");
				Assert.That(
					upgrade.Power != 0
						|| upgrade.Toughness != 0
						|| !upgrade.Effects.IsEmpty
						|| upgrade.EchoesAbility,
					Is.True,
					$"{upgrade.Name} does nothing at all"
				);
			}
		});
	}

	/// <summary>Every upgrade in the pool must actually change the companion it is applied to.</summary>
	[Test]
	public void MostFloorsOfferNoUpgradeAtAll()
	{
		// Read from the dial, never restated — the frequency is a balance number and will move.
		Assert.Multiple(() =>
		{
			Assert.That(
				StarterContent.UpgradesFor(1, StarterContent.FloorsPerUpgrade),
				Is.Not.Empty
			);
			Assert.That(
				StarterContent.UpgradesFor(1, StarterContent.FloorsPerUpgrade + 1),
				Is.Empty,
				"an upgrade every floor measured 82.5% completion against a 25% target"
			);
		});
	}

	[Test]
	public void NoUpgradeInThePoolIsInert()
	{
		Assert.Multiple(() =>
		{
			foreach (var upgrade in StarterContent.UpgradePool)
			{
				var before = StarterContent.StarterCompanion;
				var after = before.With(upgrade);

				Assert.That(
					after.Power != before.Power
						|| after.Toughness != before.Toughness
						|| after.Effects.Count != before.Effects.Count,
					Is.True,
					$"{upgrade.Name} left the companion identical"
				);
			}
		});
	}

	private static UnitComponent Companion(GameState state) =>
		state.Units().Single(u => u.HasComponent<CompanionComponent>()).Unit();
}
