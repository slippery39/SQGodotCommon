using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using MIs = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4m3, LSVPower — multicolour's new pieces, each against its printed text: a split card (Life // Death), control
/// of a spell (Invert Polarity), what an opponent can't make you do (Tamiyo, Collector of Tales), and companion
/// (Lurrus of the Dream-Den, Lutri, the Spellchaser, Zirda, the Dawnwaker).
/// </summary>
public class LsvBatch2m3Tests : FraBoardTest
{
	const string Lurrus = "Lurrus of the Dream-Den";

	static CardDefinition Instant(string name, params GameAction[] effects) =>
		TestGames.Inline(name, "{1}", "Instant", null, effects);

	CastSpellAction Half(int card, int face, params int[] targets) =>
		new()
		{
			PlayerId = me,
			CardId = card,
			Face = face,
			Targets = [.. targets],
		};

	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	// ===== split cards =====

	[Test]
	public void LifeDeath_IsCastAsEitherHalf_NeverWhole_AndOffTheStackHasBothCosts()
	{
		var card = Hand(Lsv("Life // Death"), me);
		var ogre = Put(TestGames.Creature("Ogre", "3/3"), me, ZoneType.Graveyard);

		Assert.That(Casts(card).Select(c => c.Face).Distinct(), Nu.EquivalentTo(new[] { 1, 2 }));
		Assert.That(
			new CastSpellAction { PlayerId = me, CardId = card }
				.ValidateAdd(s)
				.IsValid,
			Nu.False,
			"never cast as the whole card"
		);
		var whole = s.GetCard(card);
		Assert.That(
			(whole.ManaCost.ManaValue, s.ColorsOf(whole).Length),
			Nu.EqualTo((3, 2)),
			"{G} and {1}{B}: mana value 3, green and black"
		);

		s = s.Act(Half(card, 2, ogre));
		var onStack = s.GetCard(card);
		Assert.That(
			(onStack.Name, onStack.ManaCost.ManaValue),
			Nu.EqualTo(("Death", 2)),
			"on the stack it is Death"
		);
	}

	[Test]
	public void Life_AllYourLandsBecome11Creatures_ThenItGoesToTheGraveyard()
	{
		var card = Hand(Lsv("Life // Death"), me);
		s = s.Act(Half(card, 1));
		Resolve();

		var lands = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList();
		Assert.That(
			lands.All(l => l.Is(CardType.Creature) && s.Power(l) == 1 && s.Toughness(l) == 1),
			Nu.True
		);
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Any(c => c.Is(CardType.Creature)),
			Nu.False,
			"only yours"
		);
		Assert.That(
			(InGraveyard(card), s.GetCard(card).Name),
			Nu.EqualTo((true, "Life // Death")),
			"resolved, a split card goes to the graveyard, whole again: no Adventure's exile"
		);
	}

	[Test]
	public void Death_ReturnsACreatureCard_AndYouLoseLifeEqualToItsManaValue()
	{
		var card = Hand(Lsv("Life // Death"), me);
		var ogre = Put(TestGames.CreatureCosting("{3}", "Ogre", "3/3"), me, ZoneType.Graveyard);
		var manaValue = s.GetCard(ogre).ManaCost.ManaValue;
		var life = Life(me);

		s = s.Act(Half(card, 2, ogre));
		Resolve();

		Assert.That((Alive(ogre), Life(me)), Nu.EqualTo((true, life - manaValue)));
	}

	// ===== control of a spell =====

	[Test]
	public void InvertPolarity_WonTheSpellIsYoursWithNewTargets_LostItIsCountered()
	{
		var outcomes = new HashSet<string>();
		for (var seed = 0; seed < 16; seed++) // a fair flip off the game's seeded random: both sides come up
		{
			Board();
			s = s with { RngSeed = seed };
			// "you gain 3": whose spell it is shows in who gains
			var shock = Hand(
				TestCards.Targeted(
					"Drain",
					TargetSpec.Player(),
					new DealDamage(Ref.Target(), 3),
					new GainLife(Ref.You, 3)
				),
				them
			);
			var invert = Hand(Lsv("Invert Polarity"), me);
			s = s.Act(new PassPriorityAction { PlayerId = me });
			s = s.Act(
				new CastSpellAction
				{
					PlayerId = them,
					CardId = shock,
					Targets = [me],
				}
			);
			s = s.Act(new PassPriorityAction { PlayerId = them });
			var (mine, theirs) = (Life(me), Life(them));
			s = s.Act(
				new CastSpellAction
				{
					PlayerId = me,
					CardId = invert,
					Targets = [shock],
				}
			);
			var retargeted = false;
			Resolve(c =>
			{
				if (c is ChooseTargetChoice t && t.Options.Any(o => o.Id == them))
				{
					retargeted = true;
					return [them];
				}
				return Min(c);
			});

			if (retargeted)
			{
				Assert.That(
					(Life(me), Life(them)),
					Nu.EqualTo((mine + 3, theirs - 3)),
					"won: the Drain resolved as mine, at them"
				);
				outcomes.Add("won");
			}
			else
			{
				Assert.That(
					(Life(me), Life(them), InGraveyard(shock)),
					Nu.EqualTo((mine, theirs, true)),
					"lost: countered"
				);
				outcomes.Add("lost");
			}
		}
		Assert.That(outcomes, Nu.EquivalentTo(new[] { "won", "lost" }));
	}

	[Test]
	public void InvertPolarity_AKeptTarget_DoesNotBecomeTheTargetAgain()
	{
		for (var seed = 0; seed < 16; seed++)
		{
			Board();
			s = s with { RngSeed = seed };
			// "whenever this becomes the target of a spell or ability an opponent controls, you gain 5"
			var bear = Field(
				TestCards.WithTrigger(
					TestGames.Creature("Bear", "2/2"),
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					new GainLife(Ref.You, 5)
				),
				them
			);
			var pump = Hand(
				TestCards.Targeted(
					"Pump",
					TargetSpec.Creature,
					new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)
				),
				them
			);
			var invert = Hand(Lsv("Invert Polarity"), me);
			s = s.Act(new PassPriorityAction { PlayerId = me });
			s = s.Act(
				new CastSpellAction
				{
					PlayerId = them,
					CardId = pump,
					Targets = [bear],
				}
			);
			s = s.Act(new PassPriorityAction { PlayerId = them });
			var life = Life(them);
			s = s.Act(
				new CastSpellAction
				{
					PlayerId = me,
					CardId = invert,
					Targets = [pump],
				}
			);
			var won = false;
			Resolve(c =>
			{
				if (c is ChooseTargetChoice t && t.Options.Any(o => o.Id == bear))
				{
					won = true;
					return [bear]; // the same target
				}
				return Min(c);
			});
			if (!won)
				continue;
			Assert.That(Life(them), Nu.EqualTo(life), "the Bear was already the Pump's target");
			Assert.That(
				s.Power(s.GetCard(bear)),
				Nu.EqualTo(3),
				"and the Pump, now mine, resolved"
			);
			return;
		}
		Assert.Fail("no seed won the flip");
	}

	// ===== what an opponent can't make you do =====

	[Test]
	public void Tamiyo_TheirDiscardAndSacrificeDoNothing_YourOwnStillDo()
	{
		var tamiyo = Field(Lsv("Tamiyo, Collector of Tales"), me);
		s = s.PutCounters(tamiyo, CounterKinds.Loyalty, 5); // placed with none, the state check would take her
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Hand(TestCards.Fodder, me);
		Hand(TestCards.Fodder, me);
		var cards = HandCount(me);

		var edict = Hand(
			Instant(
				"Edict",
				new Discard(Ref.EachOpponent, 1),
				new EachSacrifices(Ref.EachOpponent, MIs.Creature),
				new DiscardHand(Ref.EachOpponent)
			),
			them
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = edict });
		var asked = ResolveAsking(c => c.Options.Take(Math.Max(1, c.MinChoices)).Select(o => o.Id));
		Assert.That(
			asked.OfType<ChooseCardsFromHandChoice>(),
			Nu.Empty,
			"nothing to discard is asked"
		);
		Assert.That((HandCount(me), Alive(bear)), Nu.EqualTo((cards, true)), "an opponent's spell");

		var mine = Hand(
			Instant("Mine", new Discard(Ref.You, 1), new EachSacrifices(Ref.You, MIs.Creature)),
			me
		);
		cards = HandCount(me);
		Cast(mine, []);
		Assert.That(
			(HandCount(me), Alive(bear)),
			Nu.EqualTo((cards - 2, false)),
			"your own spell: it left the hand, then a card was discarded"
		);
	}

	[Test]
	public void Tamiyo_PlusOne_AllCardsOfTheNameToYourHand_TheRestToTheGraveyard_EachTimeItsOwnName()
	{
		var tamiyo = Field(Lsv("Tamiyo, Collector of Tales"), me);
		s = s.PutCounters(tamiyo, CounterKinds.Loyalty, 5);
		// the top four, top first
		string[] names = ["Invert Polarity", "Life // Death", "Invert Polarity", Lurrus];
		var top = names.Reverse().Select(n => OnTop(Lsv(n))).Reverse().ToList();

		PlusOne(tamiyo, "Invert Polarity");
		Assert.That(
			top.Select(id => s.IsIn(id, me, ZoneType.Hand)),
			Nu.EqualTo(new[] { true, false, true, false })
		);
		Assert.That(top.Count(InGraveyard), Nu.EqualTo(2), "the rest into the graveyard");

		ToMyMain(); // before the cards go on top: my draw step would take Lurrus
		var next = new[] { "Life // Death", Lurrus }.Select(n => OnTop(Lsv(n))).ToList(); // Lurrus on top
		PlusOne(tamiyo, Lurrus);
		Assert.That(
			next.Select(id => s.IsIn(id, me, ZoneType.Hand)),
			Nu.EqualTo(new[] { false, true }),
			"the new name, not the first one's"
		);
	}

	void PlusOne(int tamiyo, string name)
	{
		var index = global::MagicCore
			.Actions.Loyalty.AbilitiesOf(s, s.GetCard(tamiyo))
			.FindIndex(a => a.Cost == 1);
		s = s.Act(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.First(a => a.CardId == tamiyo && a.AbilityIndex == index)
		);
		Resolve(c =>
			c is ChooseKeywordChoice
				? c.Options.Where(o => o.DisplayText == name).Select(o => o.Id)
				: Min(c)
		);
	}

	/// <summary>On to my next turn's first main phase.</summary>
	void ToMyMain()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
		);
	}

	// ===== companion =====

	[Test]
	public void Companion_OnlyAChosenOneWhoseConditionHolds_IsMarked_AndTheRecordKeepsIt()
	{
		var bear = TestGames.Creature("Bear", "2/2");
		var forests = Enumerable.Repeat(TestGames.Card("Forest"), 30).ToList();
		List<CardDefinition> cheap = [.. forests, .. Enumerable.Repeat(bear, 10)];
		List<CardDefinition> dear =
		[
			.. cheap.Skip(1),
			TestGames.CreatureCosting("{3}", "Ogre", "3/3"),
		];
		List<CardDefinition> named =
		[
			.. forests,
			.. Enumerable.Range(0, 10).Select(i => TestGames.Creature($"Bear {i}", "2/2")),
		];
		List<CardDefinition> tapping = [.. forests, .. Enumerable.Repeat(WithTap(bear), 10)];

		bool Chosen(
			List<CardDefinition> deck,
			string companion,
			List<CardDefinition>? sideboard = null
		)
		{
			var game = MagicRules.NewGame(
				deck,
				cheap,
				1,
				sideboard1: sideboard,
				companion1: companion
			);
			return game.CardsIn(game.Player1Id(), ZoneType.Sideboard)
				.Single(c => c.Name == companion)
				.HasComponent<ChosenCompanion>();
		}

		Assert.That(
			Chosen(cheap, Lurrus),
			Nu.True,
			"Lurrus: each permanent card mana value 2 or less"
		);
		Assert.That(Chosen(dear, Lurrus), Nu.False, "a 3-drop: an ordinary sideboard card");
		Assert.That(
			Chosen(cheap, Lurrus, [Lsv(Lurrus), TestCards.Fodder]),
			Nu.True,
			"found in the sideboard given"
		);
		Assert.That(
			Chosen(cheap, "Lutri, the Spellchaser"),
			Nu.False,
			"Lutri: ten Bears share a name"
		);
		Assert.That(Chosen(named, "Lutri, the Spellchaser"), Nu.True, "lands may share one");
		Assert.That(Chosen(cheap, "Zirda, the Dawnwaker"), Nu.False, "Zirda: a vanilla Bear");
		Assert.That(
			Chosen(tapping, "Zirda, the Dawnwaker"),
			Nu.True,
			"a Forest's mana ability counts"
		);

		List<CardDefinition> lands = [.. forests, .. forests.Take(10)]; // a record names its cards
		var record = GameRecord.New(1, lands, lands, ("A", "B"), companion1: Lurrus);
		var started = GameRecord.FromJson(record.ToJson()).Start();
		Assert.That(
			started
				.CardsIn(started.Player1Id(), ZoneType.Sideboard)
				.Single()
				.HasComponent<ChosenCompanion>(),
			Nu.True,
			"a replay chooses it again"
		);
	}

	[Test]
	public void Companion_AnArenaExportsCompanionSection_IsTheBuildsCompanion_InThePoolOnce()
	{
		var export = string.Join(
			"\n",
			"Companion",
			$"1 {Lurrus} (IKO) 226",
			"",
			"Deck",
			"17 Life /// Death (LSV) 190",
			"",
			"Sideboard",
			$"1 {Lurrus} (IKO) 226",
			"1 Invert Polarity (LSV) 2"
		);
		var deck = global::MagicCore.Sets.Fra.ArenaDeck.Parse(export, "LSV");
		Assert.That((deck.Ok, deck.Deck.Count, deck.Companion), Nu.EqualTo((true, 17, Lurrus)));
		var pool = global::MagicCore.Sets.Fra.ArenaDeck.ReadPool(export, "LSV");
		Assert.That(
			(pool.Pool.Count(n => n == Lurrus), pool.Companion),
			Nu.EqualTo((1, Lurrus)),
			"listed in both sections, one card"
		);
		var fra = global::MagicCore.Sets.Fra.ArenaDeck.ReadPool(export, "FRA");
		Assert.That(
			(fra.Companion, fra.LeftOut.Count(n => n == Lurrus)),
			Nu.EqualTo(((string?)null, 2)),
			"not the set's: left out by name, as any card"
		);
		var build = global::MagicCore.Limited.SealedBuildMatch.FromExport("mine", export, "LSV");
		Assert.That(
			(build.Companion, build.Sideboard.Count(c => c.Data.Name == Lurrus)),
			Nu.EqualTo((Lurrus, 1))
		);
	}

	[Test]
	public void Companion_TheBuilderChoosesOne_WhoseConditionTheMainMeets_InItsColours()
	{
		var ratings = new Dictionary<string, double>();
		IReadOnlyList<string> plains = [.. Enumerable.Repeat("Plains", 40)];
		ManaColor[] whiteBlack = [ManaColor.White, ManaColor.Black];
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.CompanionFor(
				plains,
				[Lurrus, "Invert Polarity"],
				whiteBlack,
				ratings
			),
			Nu.EqualTo(Lurrus)
		);
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.CompanionFor(
				plains,
				[Lurrus],
				[ManaColor.Green, ManaColor.Red],
				ratings
			),
			Nu.Null,
			"a green-red deck can't cast it"
		);
		Assert.That(
			global::MagicCore.Limited.SealedBuilder.CompanionFor(
				[.. plains.Skip(1), "Tamiyo, Collector of Tales"],
				[Lurrus],
				whiteBlack,
				ratings
			),
			Nu.Null,
			"a 4-mana planeswalker in the main: Lurrus isn't met"
		);
	}

	[Test]
	public void Companion_ForThree_IntoYourHand_AsASorcery_Once()
	{
		s = s.WithSideboard(me, [Lsv(Lurrus)]);
		var lurrus = s.CardsIn(me, ZoneType.Sideboard).Single().Id;
		IEnumerable<PutCompanionIntoHandAction> Offered() =>
			s.LegalActions().OfType<PutCompanionIntoHandAction>();
		Assert.That(Offered(), Nu.Empty, "not chosen: an ordinary sideboard card");

		s = s.UpdateObject(lurrus, s.GetCard(lurrus).WithComponent(new ChosenCompanion()));
		var shock = Hand(TestCards.Shock(1, TargetSpec.Player()), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [them],
			}
		);
		Assert.That(Offered(), Nu.Empty, "with a spell on the stack: as a sorcery only");
		Resolve();

		var lands = UntappedLands();
		s = s.Act(Offered().Single());
		Assert.That(
			(s.IsIn(lurrus, me, ZoneType.Hand), UntappedLands(), s.StackIsEmpty()),
			Nu.EqualTo((true, lands - 3, true)),
			"{3}, no stack"
		);
		Assert.That(Offered(), Nu.Empty, "once");
	}
}
