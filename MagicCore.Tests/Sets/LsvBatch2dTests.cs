using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4d1, LSVPower — white's tier 1 made of tested pieces: each card's ability FIRES, against its PRINTED
/// text, through real casting and the real stack.
/// </summary>
public class LsvBatch2dTests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition Relic => TestGames.Inline("Relic", "{2}", "Artifact");

	static CardDefinition Shrine => TestGames.Inline("Shrine", "{2}", "Enchantment");

	static CardDefinition Sword => TestGames.Inline("Sword", "{2}", "Artifact — Equipment");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	int Tokens(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.IsToken && c.Name == name);

	bool InExile(int card) => s.IsIn(card, s.GetCard(card).OwnerId, ZoneType.Exile);

	int Put(CardDefinition card, int player, ZoneType zone)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([attackers.Contains(d.CreatureId) ? them : 0]);
	}

	/// <summary>TEST SETUP: my lands untap — generic costs are paid with the Plains first, and white runs out.</summary>
	void UntapMyLands()
	{
		foreach (var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsTapped).ToList())
			s = s.UpdateObject(land.Id, land with { IsTapped = false });
	}

	/// <summary>Answers a choice offering any of these ids with those; any other with its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Picking(params int[] ids) =>
		c =>
		{
			var picks = c.Options.Select(o => o.Id).Intersect(ids).ToList();
			return picks.Count > 0 ? picks : c.Options.Take(c.MinChoices).Select(o => o.Id);
		};

	int EffectiveManaValue(int player, int card) =>
		MagicCore.Rules.Costs.Of(s, player, s.GetCard(card)).ManaValue;

	[Test]
	public void ThrabenInspector_EntersInvestigating()
	{
		Cast(Hand(Lsv("Thraben Inspector"), me), []);
		Assert.That(Tokens(me, "Clue"), Nu.EqualTo(1));
	}

	[Test]
	public void CatharCommando_SacrificedToDestroyAnArtifactOrEnchantment()
	{
		var commando = Field(Lsv("Cathar Commando"), me);
		var relic = Field(Relic, them);
		var shrine = Field(Shrine, them);
		Field(Bear, them);
		Assert.That(
			Activations(commando).Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { relic, shrine }),
			"an artifact or an enchantment, never a creature"
		);
		s = s.Act(Activations(commando).First(a => a.Targets[0] == relic));
		Resolve();
		Assert.That(Alive(relic), Nu.False);
		Assert.That(Alive(commando), Nu.False, "sacrificed as the cost");
	}

	[Test]
	public void LuminarchAspirant_BeginningOfCombat_ACounterOnMyCreature()
	{
		Field(Lsv("Luminarch Aspirant"), me);
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		Assert.That(
			s.GetPendingChoice()!.Options.Select(o => o.Id),
			Nu.Not.Contains(theirs),
			"a creature you control"
		);
		s = s.Choose([bear]);
		Resolve();
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void AgentBishop_BeginningOfCombat_ACounterOnEachOfUpToTwoCreatures()
	{
		Field(Lsv("Agent Bishop, Man in Black"), me);
		var mine = Field(Bear, me);
		var theirs = Field(Bear, them);
		var targets = new[] { mine, theirs };
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseTargetChoice);
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice t => [targets[t.Index]],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(mine).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.GetCard(theirs).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void SpectacularSpiderMan_GainsFlying_ThenSacrificedForHexproofAndIndestructible()
	{
		var spider = Field(Lsv("Spectacular Spider-Man"), me);
		var bear = Field(Bear, me);
		Assert.That(s.Has(s.GetCard(spider), "Flying"), Nu.False);
		s = s.Act(Activations(spider).Single(a => a.AbilityIndex == 0));
		Resolve();
		Assert.That(s.Has(s.GetCard(spider), "Flying"));
		Assert.That(s.Has(s.GetCard(bear), "Hexproof"), Nu.False);
		s = s.Act(Activations(spider).Single(a => a.AbilityIndex == 1));
		Resolve();
		Assert.That(Alive(spider), Nu.False, "sacrificed as the cost");
		Assert.That(s.Has(s.GetCard(bear), "Hexproof"));
		Assert.That(s.IsIndestructible(s.GetCard(bear)));
	}

	[Test]
	public void StoneforgeMystic_EntersSearchingForAnEquipment()
	{
		var sword = Put(Sword, me, ZoneType.Library);
		var relic = Put(Relic, me, ZoneType.Library);
		var offered = new List<int>();
		var mystic = Hand(Lsv("Stoneforge Mystic"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = mystic });
		Resolve(c =>
		{
			if (c is ChooseFromLibraryChoice)
				offered.AddRange(c.Options.Select(o => o.Id));
			return Picking(sword)(c);
		});
		Assert.That(offered, Nu.EquivalentTo(new[] { sword }), "an Equipment card only");
		Assert.That(offered, Nu.Not.Contains(relic));
		Assert.That(s.IsIn(sword, me, ZoneType.Hand));
	}

	[Test]
	public void StoneforgeMystic_PutsAnEquipmentFromHandOntoTheBattlefield()
	{
		var mystic = Field(Lsv("Stoneforge Mystic"), me);
		var sword = Hand(Sword, me);
		s = s.Act(Activations(mystic).Single());
		Resolve(Picking(sword));
		Assert.That(Alive(sword));
		Assert.That(s.GetCard(mystic).IsTapped);
	}

	[Test]
	public void Thalia_NoncreatureSpellsCostOneMore_ForBothPlayers()
	{
		var mine = Hand(Boon, me);
		var theirs = Hand(Boon, them);
		var creature = Hand(Bear, me);
		Assert.That(EffectiveManaValue(me, mine), Nu.EqualTo(1));
		Field(Lsv("Thalia, Guardian of Thraben"), me);
		Assert.That(EffectiveManaValue(me, mine), Nu.EqualTo(2));
		Assert.That(EffectiveManaValue(them, theirs), Nu.EqualTo(2));
		Assert.That(
			EffectiveManaValue(me, creature),
			Nu.EqualTo(1),
			"creature spells aren't taxed"
		);
	}

	[Test]
	public void EliteSpellbinder_ExilesANonlandCardFromTheirHand_PlayableForTwoMore()
	{
		s = TestGames.EmptyHands(s);
		var boon = Hand(Boon, them);
		var spellbinder = Hand(Lsv("Elite Spellbinder"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spellbinder });
		Resolve(Picking(them, boon));
		Assert.That(InExile(boon));
		Assert.That(s.GetCard(boon).GetComponent<PlayableFromExile>()?.PlayerId, Nu.EqualTo(them));
		Assert.That(EffectiveManaValue(them, boon), Nu.EqualTo(3), "{1}, and {2} more");
	}

	[Test]
	public void Loran_EntersDestroyingAnArtifactOrEnchantment()
	{
		var relic = Field(Relic, them);
		CastWithTrigger(Hand(Lsv("Loran of the Third Path"), me), [], relic);
		Assert.That(Alive(relic), Nu.False);
	}

	[Test]
	public void Loran_Tapped_YouAndTargetOpponentEachDraw()
	{
		var loran = Field(Lsv("Loran of the Third Path"), me);
		var (mine, theirs) = (HandCount(me), HandCount(them));
		s = s.Act(Activations(loran).Single());
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(mine + 1));
		Assert.That(HandCount(them), Nu.EqualTo(theirs + 1));
	}

	[Test]
	public void SanguineEvangelist_ABatAsItEnters_AndAsItDies()
	{
		var evangelist = Hand(Lsv("Sanguine Evangelist"), me);
		Cast(evangelist, []);
		Assert.That(Tokens(me, "Bat"), Nu.EqualTo(1));
		var bat = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Bat");
		Assert.That(s.Has(bat, "Flying"));
		Assert.That((s.Power(bat), s.Toughness(bat)), Nu.EqualTo((1, 1)));
		Cast(Hand(TestCards.Kill(), me), [], evangelist);
		Assert.That(Tokens(me, "Bat"), Nu.EqualTo(2));
	}

	[Test]
	public void SanguineEvangelist_BattleCry_EachOtherAttackerGetsPlusOne()
	{
		var evangelist = Field(Lsv("Sanguine Evangelist"), me);
		var bear = Field(Bear, me);
		var home = Field(Bear, me);
		AttackWith(evangelist, bear);
		Resolve();
		Assert.That(s.Power(s.GetCard(bear)), Nu.EqualTo(3));
		Assert.That(s.Power(s.GetCard(evangelist)), Nu.EqualTo(2), "each OTHER attacking creature");
		Assert.That(s.Power(s.GetCard(home)), Nu.EqualTo(2), "each other ATTACKING creature");
	}

	[Test]
	public void AuriokSalvagers_ReturnsAnArtifactOfManaValueOneOrLess()
	{
		var salvagers = Field(Lsv("Auriok Salvagers"), me);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Graveyard);
		Put(Relic, me, ZoneType.Graveyard);
		Assert.That(
			Activations(salvagers).Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { trinket }),
			"mana value 1 or less"
		);
		s = s.Act(Activations(salvagers).Single());
		Resolve();
		Assert.That(s.IsIn(trinket, me, ZoneType.Hand));
	}

	[Test]
	public void Reprieve_ReturnsTheSpellToItsOwnersHand_AndDraws()
	{
		var life = Life(them);
		var spell = TheirSpellOnTheStack(Boon);
		var reprieve = Hand(Lsv("Reprieve"), me);
		var hand = HandCount(me);
		Cast(reprieve, [], spell);
		Assert.That(s.IsIn(spell, them, ZoneType.Hand));
		Assert.That(Life(them), Nu.EqualTo(life), "it never resolved");
		Assert.That(HandCount(me), Nu.EqualTo(hand), "Reprieve left, a card drawn");
	}

	[Test]
	public void Exorcise_ExilesAnArtifactAnEnchantmentOrACreatureWithPowerFourOrMore()
	{
		var big = Field(TestGames.Creature("Big", "4/4"), them);
		var small = Field(TestGames.Creature("Small", "3/3"), them);
		var relic = Field(Relic, them);
		var shrine = Field(Shrine, them);
		var exorcise = Hand(Lsv("Exorcise"), me);
		Assert.That(
			Casts(exorcise).Select(c => c.Targets[0]),
			Nu.EquivalentTo(new[] { big, relic, shrine })
		);
		Assert.That(Casts(exorcise).Select(c => c.Targets[0]), Nu.Not.Contains(small));
		Cast(exorcise, [], big);
		Assert.That(InExile(big));
	}

	[Test]
	public void PortableHole_ExilesTheirCheapPermanentUntilItLeaves()
	{
		var cheap = Field(TestGames.CreatureCosting("{2}", "Cheap", "2/2"), them);
		var dear = Field(TestGames.CreatureCosting("{3}", "Dear", "3/3"), them);
		var offered = new List<int>();
		var hole = Hand(Lsv("Portable Hole"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = hole });
		Resolve(c =>
		{
			if (c is ChooseTargetChoice)
				offered.AddRange(c.Options.Select(o => o.Id));
			return Picking(cheap)(c);
		});
		Assert.That(
			offered,
			Nu.EquivalentTo(new[] { cheap }),
			"mana value 2 or less, an opponent's"
		);
		Assert.That(offered, Nu.Not.Contains(dear));
		Assert.That(InExile(cheap));
		Cast(Hand(TestCards.Kill(MagicCore.Targeting.TargetSpec.Permanent()), me), [], hole);
		Assert.That(Alive(cheap), "back once the Hole has left");
	}

	[Test]
	public void LeylineBinding_Domain_CostsOneLessPerBasicLandType_AndExilesUntilItLeaves()
	{
		var bear = Field(Bear, them);
		var binding = Hand(Lsv("Leyline Binding"), me);
		Assert.That(s.GetCard(binding).ManaCost.ManaValue, Nu.EqualTo(6));
		Assert.That(EffectiveManaValue(me, binding), Nu.EqualTo(1), "five basic land types: {W}");
		CastWithTrigger(binding, [], bear);
		Assert.That(InExile(bear));
	}

	[Test]
	public void LingeringSouls_TwoFlyingSpirits_AndAgainByFlashback()
	{
		var souls = Hand(Lsv("Lingering Souls"), me);
		Cast(souls, []);
		Assert.That(Tokens(me, "Spirit"), Nu.EqualTo(2));
		var spirit = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Spirit");
		Assert.That(s.Has(spirit, "Flying"));
		Cast(souls, []);
		Assert.That(Tokens(me, "Spirit"), Nu.EqualTo(4));
		Assert.That(InExile(souls), "flashback exiles it");
	}

	[Test]
	public void TouchTheSpiritRealm_EntersExilingAnArtifactOrCreature()
	{
		var bear = Field(Bear, them);
		CastWithTrigger(Hand(Lsv("Touch the Spirit Realm"), me), [], bear);
		Assert.That(InExile(bear));
	}

	[Test]
	public void TouchTheSpiritRealm_Channel_ExilesUntilTheNextEndStep()
	{
		var bear = Field(Bear, them);
		var touch = Hand(Lsv("Touch the Spirit Realm"), me);
		s = s.Act(Activations(touch).Single(a => a.Targets[0] == bear));
		Resolve();
		Assert.That(s.IsIn(touch, me, ZoneType.Graveyard), "discarded as the cost");
		Assert.That(InExile(bear));
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.IsOnBattlefield(bear));
		Assert.That(Alive(bear));
		Assert.That(
			s.GetGame().TurnNumber,
			Nu.EqualTo(turn),
			"at the beginning of the next end step"
		);
	}

	[Test]
	public void GlimmerLens_ForMirrodin_ARebelWearingIt()
	{
		var lens = Hand(Lsv("Glimmer Lens"), me);
		Cast(lens, []);
		var rebel = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.IsToken && c.Name == "Rebel");
		Assert.That((s.Power(rebel), s.Toughness(rebel)), Nu.EqualTo((2, 2)));
		Assert.That(s.GetCard(lens).AttachedToId, Nu.EqualTo(rebel.Id));
	}

	[Test]
	public void GlimmerLens_DrawsWhenTheEquippedCreatureAndAnotherAttack_NotAlone()
	{
		var lens = Field(Lsv("Glimmer Lens"), me);
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		s = MagicCore.Rules.Attachments.Attach(s, lens, bear);
		AttackWith(bear);
		Assert.That(s.StackIsEmpty(), "attacking alone: no trigger");
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 2);
		AttackWith(bear, other);
		Assert.That(s.StackIsEmpty(), Nu.False, "the equipped creature and another");
		var hand = HandCount(me);
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void GlimmerLens_AnotherCreatureAttackingWithoutTheEquippedOne_DrawsNothing()
	{
		var lens = Field(Lsv("Glimmer Lens"), me);
		var bear = Field(Bear, me);
		var other = Field(Bear, me);
		var third = Field(Bear, me);
		s = MagicCore.Rules.Attachments.Attach(s, lens, bear);
		AttackWith(other, third);
		Assert.That(s.StackIsEmpty(), "the equipped creature stayed home");
	}

	[Test]
	public void StaffOfTheStoryteller_ASpirit_AStoryCounterPerBatchOfTokens_RemovedToDraw()
	{
		var staff = Hand(Lsv("Staff of the Storyteller"), me);
		Cast(staff, []);
		Assert.That(Tokens(me, "Spirit"), Nu.EqualTo(1));
		Assert.That(s.GetCard(staff).CountersOf(CounterKinds.Story), Nu.EqualTo(1));
		Cast(Hand(Lsv("Lingering Souls"), me), []);
		Assert.That(
			s.GetCard(staff).CountersOf(CounterKinds.Story),
			Nu.EqualTo(2),
			"one or more: two tokens at once are one counter"
		);
		Cast(Hand(Bear, me), []);
		Assert.That(s.GetCard(staff).CountersOf(CounterKinds.Story), Nu.EqualTo(2), "not a token");
		UntapMyLands();
		var hand = HandCount(me);
		s = s.Act(Activations(staff).Single());
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
		Assert.That(s.GetCard(staff).CountersOf(CounterKinds.Story), Nu.EqualTo(1));
	}

	[Test]
	public void StaffOfTheStoryteller_WithNoStoryCounter_CantDraw()
	{
		var staff = Field(Lsv("Staff of the Storyteller"), me);
		Assert.That(Activations(staff), Nu.Empty);
		s = s.PutCounters(staff, CounterKinds.Story, 1);
		Assert.That(Activations(staff).Count(), Nu.EqualTo(1));
	}

	[Test]
	public void SageOfTheSkies_CopiedWhenAnotherSpellWasCastThisTurn()
	{
		int Sages() =>
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Sage of the Skies");
		Cast(Hand(Lsv("Sage of the Skies"), me), []);
		Assert.That(Sages(), Nu.EqualTo(1), "the first spell this turn: no copy");
		Cast(Hand(Lsv("Sage of the Skies"), me), []);
		Assert.That(Sages(), Nu.EqualTo(3), "another spell was cast: the copy becomes a token");
		Assert.That(Tokens(me, "Sage of the Skies"), Nu.EqualTo(1));
	}

	[Test]
	public void EnlightenedTutor_AnArtifactOrEnchantmentOnTopOfTheLibrary()
	{
		var relic = Put(Relic, me, ZoneType.Library);
		var shrine = Put(Shrine, me, ZoneType.Library);
		var bear = Put(Bear, me, ZoneType.Library);
		var offered = new List<int>();
		var tutor = Hand(Lsv("Enlightened Tutor"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = tutor });
		Resolve(c =>
		{
			if (c is ChooseFromLibraryChoice)
				offered.AddRange(c.Options.Select(o => o.Id));
			return Picking(shrine)(c);
		});
		Assert.That(offered, Nu.SupersetOf(new[] { relic, shrine })); // and the deck's own
		Assert.That(offered, Nu.Not.Contains(bear));
		Assert.That(
			offered
				.Select(s.GetCard)
				.All(c => c.Is(CardType.Artifact) || c.Is(CardType.Enchantment))
		);
		Assert.That(s.CardsIn(me, ZoneType.Library).First().Id, Nu.EqualTo(shrine));
	}

	// ===== MC-4.4d2a: white's options, zones and tokens =====

	[TestCase(4, 0)]
	[TestCase(5, 1)]
	public void JackedRabbit_Ravenous_XCounters_AndACardOnlyWhenXIsFiveOrMore(int x, int drawn)
	{
		var rabbit = Hand(Lsv("Jacked Rabbit"), me);
		var before = HandCount(me) - 1; // the Rabbit leaves the hand
		s = s.Act(Casts(rabbit).Single(c => c.X == x));
		Resolve();
		Assert.That(s.GetCard(rabbit).PlusOneCounters, Nu.EqualTo(x));
		Assert.That(HandCount(me), Nu.EqualTo(before + drawn));
	}

	[Test]
	public void Oust_SecondFromTheTopOfItsOwnersLibrary_ItsControllerGainsThree()
	{
		var bear = Field(Bear, them);
		var library = s.CardsIn(them, ZoneType.Library).Select(c => c.Id).ToList();
		var life = Life(them);
		Cast(Hand(Lsv("Oust"), me), [], bear);
		Assert.That(
			s.CardsIn(them, ZoneType.Library).Select(c => c.Id),
			Nu.EqualTo(library.Take(1).Append(bear).Concat(library.Skip(1)))
		);
		Assert.That(Life(them), Nu.EqualTo(life + 3));
	}

	[TestCase(0)]
	[TestCase(2)]
	public void UnexpectedlyAbsent_JustBeneathTheTopXCardsOfItsOwnersLibrary(int x)
	{
		var relic = Field(Relic, them);
		var library = s.CardsIn(them, ZoneType.Library).Select(c => c.Id).ToList();
		var absent = Hand(Lsv("Unexpectedly Absent"), me);
		s = s.Act(Casts(absent).Single(c => c.X == x && c.Targets.Contains(relic)));
		Resolve();
		Assert.That(
			s.CardsIn(them, ZoneType.Library).Select(c => c.Id),
			Nu.EqualTo(library.Take(x).Append(relic).Concat(library.Skip(x)))
		);
	}

	[Test]
	public void UnexpectedlyAbsent_ALibraryWithFewerThanXCards_TheBottom_AndNeverALand()
	{
		var relic = Field(Relic, them);
		foreach (var card in s.CardsIn(them, ZoneType.Library).Skip(2).ToList()) // TEST SETUP: two cards left
			s = s.MoveCard(card.Id, s.ZoneId(them, ZoneType.Exile));
		var absent = Hand(Lsv("Unexpectedly Absent"), me);
		var land = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;
		Assert.That(Casts(absent).Any(c => c.Targets.Contains(land)), Nu.False, "nonland");
		s = s.Act(Casts(absent).Single(c => c.X == 5 && c.Targets.Contains(relic)));
		Resolve();
		Assert.That(s.CardsIn(them, ZoneType.Library).Last().Id, Nu.EqualTo(relic));
		Assert.That(s.CardsIn(them, ZoneType.Library).Count(), Nu.EqualTo(3));
	}

	[TestCase(true)]
	[TestCase(false)]
	public void Phelia_Attacking_ExilesAnotherNonlandPermanentUntilTheNextEndStep_ACounterIfItEnteredUnderMyControl(
		bool mine
	)
	{
		var phelia = Field(Lsv("Phelia, Exuberant Shepherd"), me);
		var relic = Field(Relic, mine ? me : them);
		AttackWith(phelia);
		var offered = s.GetPendingChoice()!.Options.Select(o => o.Id).ToList();
		Assert.That(offered, Nu.SupersetOf(new[] { relic }));
		Assert.That(offered, Nu.Not.Contains(phelia), "up to one OTHER target");
		Assert.That(
			offered.Where(id => id != 0).Select(s.GetCard).Any(c => c.Is(CardType.Land)),
			Nu.False,
			"nonland"
		);
		Resolve(On<ChooseTargetChoice>(relic));
		Assert.That(InExile(relic));
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.IsOnBattlefield(relic));
		Assert.That(
			s.GetGame().TurnNumber,
			Nu.EqualTo(turn),
			"at the beginning of the next end step"
		);
		Assert.That(
			s.GetCard(relic).ControllerId,
			Nu.EqualTo(mine ? me : them),
			"its owner's control"
		);
		Assert.That(s.GetCard(phelia).PlusOneCounters, Nu.EqualTo(mine ? 1 : 0));
	}

	[Test]
	public void Phelia_BlinkedInResponse_IsANewObject_AndGetsNoCounter()
	{
		var phelia = Field(Lsv("Phelia, Exuberant Shepherd"), me);
		var relic = Field(Relic, me);
		AttackWith(phelia);
		s = s.Choose([relic]); // her trigger goes on the stack
		var blink = Hand(
			TestCards.Targeted(
				"Blink",
				MagicCore.Targeting.TargetSpec.Creature,
				new Flicker(Ref.Target())
			),
			me
		);
		Cast(blink, [], phelia);
		Assert.That(InExile(relic), "the trigger still resolved");
		s = TestGames.PassUntil(s, x => x.IsOnBattlefield(relic));
		Assert.That(Alive(phelia));
		Assert.That(
			s.GetCard(phelia).PlusOneCounters,
			Nu.EqualTo(0),
			"not the Phelia that attacked"
		);
	}

	[Test]
	public void Phelia_GoneBeforeTheEndStep_TheCardStillReturns()
	{
		var phelia = Field(Lsv("Phelia, Exuberant Shepherd"), me);
		var relic = Field(Relic, me);
		AttackWith(phelia);
		Resolve(On<ChooseTargetChoice>(relic));
		Cast(Hand(TestCards.Kill(), me), [], phelia);
		Assert.That(Alive(phelia), Nu.False);
		s = TestGames.PassUntil(s, x => x.IsOnBattlefield(relic));
		Assert.That(Alive(relic));
	}

	int Elspeth()
	{
		var elspeth = Hand(Lsv("Elspeth, Storm Slayer"), me);
		Cast(elspeth, []);
		UntapMyLands();
		return elspeth;
	}

	void Loyalty(int walker, int ability, params int[] targets)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = ability,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	static CardData SoldierToken =>
		global::MagicCore.Sets.Tokens.Token("Soldier", "Creature — Soldier", "1", "1", ["W"]);

	[Test]
	public void Elspeth_PlusOne_ASoldier_AndTwiceThatManyTokensUnderMyControl()
	{
		var elspeth = Elspeth();
		Loyalty(elspeth, 0);
		Assert.That(Tokens(me, "Soldier"), Nu.EqualTo(2), "one Soldier, twice that many");
		Assert.That(s.GetCard(elspeth).Loyalty, Nu.EqualTo(6));
		var soldier = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Soldier");
		Assert.That((s.Power(soldier), s.Toughness(soldier)), Nu.EqualTo((1, 1)));
	}

	[Test]
	public void Elspeth_DoublesATokenCopyOfMine_AndNoTokenOfTheirs()
	{
		Elspeth();
		var bear = Field(Bear, me);
		var twin = TestCards.Targeted(
			"Twin",
			MagicCore.Targeting.TargetSpec.Creature,
			new CreateTokenCopy(Ref.Target())
		);
		Cast(Hand(twin, me), [], bear);
		Assert.That(Tokens(me, "Bear"), Nu.EqualTo(2), "a copy, twice");
		TheirSpellOnTheStack(
			TestGames.Inline("Recruit", "{1}", "Instant", null, new CreateTokens(SoldierToken, 1))
		);
		Resolve();
		Assert.That(Tokens(them, "Soldier"), Nu.EqualTo(1), "under YOUR control");
	}

	[Test]
	public void Elspeth_Zero_ACounterOnEachCreatureOfMine_FlyingUntilMyNextTurn()
	{
		var elspeth = Elspeth();
		var bear = Field(Bear, me);
		var theirs = Field(Bear, them);
		Loyalty(elspeth, 1);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.Has(s.GetCard(bear), "Flying"));
		Assert.That(s.GetCard(theirs).PlusOneCounters, Nu.EqualTo(0));
		Assert.That(s.Has(s.GetCard(theirs), "Flying"), Nu.False);
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(s.Has(s.GetCard(bear), "Flying"), "until my next turn");
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == me);
		Assert.That(s.Has(s.GetCard(bear), "Flying"), Nu.False);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void Elspeth_MinusThree_DestroysTheirCreatureWithManaValueThreeOrGreater()
	{
		var elspeth = Elspeth();
		Field(Bear, them); // mana value 1
		var big = Field(TestGames.CreatureCosting("{3}", "Ogre", "3/3"), them);
		Field(TestGames.CreatureCosting("{3}", "Ogre", "3/3"), me);
		Assert.That(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Where(a => a.CardId == elspeth && a.AbilityIndex == 2)
				.Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { big })
		);
		Loyalty(elspeth, 2, big);
		Assert.That(Alive(big), Nu.False);
		Assert.That(s.GetCard(elspeth).Loyalty, Nu.EqualTo(2));
	}

	static CardDefinition Raise =>
		TestCards.Targeted(
			"Raise",
			MagicCore.Targeting.TargetSpec.InAnyGraveyard(),
			new MoveTo(Ref.Target(), ZoneType.Battlefield) { From = [ZoneType.Graveyard] }
		);

	[Test]
	public void ContainmentPriest_ANontokenCreatureThatWasntCast_IsExiledInstead()
	{
		Field(Lsv("Containment Priest"), me);
		var mine = Put(Lsv("Thraben Inspector"), me, ZoneType.Graveyard);
		var theirs = Put(Bear, them, ZoneType.Graveyard);
		var relic = Put(Relic, me, ZoneType.Graveyard);
		Cast(Hand(Raise, me), [], mine);
		Assert.That(InExile(mine), "exiled instead");
		Assert.That(Tokens(me, "Clue"), Nu.EqualTo(0), "it never entered");
		UntapMyLands();
		Cast(Hand(Raise, me), [], theirs);
		Assert.That(InExile(theirs), "anyone's — to its owner's exile");
		UntapMyLands();
		Cast(Hand(Raise, me), [], relic);
		Assert.That(Alive(relic), "a noncreature enters");
		UntapMyLands();
		var cast = Hand(Lsv("Thraben Inspector"), me);
		Cast(cast, []);
		Assert.That(Alive(cast), "a creature that was cast enters");
		Assert.That(Tokens(me, "Clue"), Nu.EqualTo(1));
		UntapMyLands();
		Cast(Hand(Lsv("Lingering Souls"), me), []);
		Assert.That(Tokens(me, "Spirit"), Nu.EqualTo(2), "a token enters");
	}

	[Test]
	public void EnduringInnocence_DrawsOnceATurn_ForAnotherCreatureOfMineWithPowerTwoOrLess()
	{
		Field(Lsv("Enduring Innocence"), me);
		var hand = HandCount(me);
		Cast(Hand(TestGames.Creature("Ogre", "3/3"), me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand), "power 2 or less");
		Cast(Hand(Bear, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
		Cast(Hand(Bear, me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "only once each turn");
	}

	[Test]
	public void EnduringInnocence_Dies_ReturnsAsAnEnchantmentThatIsNoCreature()
	{
		var innocence = Field(Lsv("Enduring Innocence"), me);
		Field(Lsv("Enduring Innocence"), me);
		var hand = HandCount(me);
		Cast(Hand(TestCards.Kill(), me), [], innocence);
		Assert.That(Alive(innocence), "returned to the battlefield");
		var returned = s.GetCard(innocence);
		Assert.That(returned.Is(CardType.Enchantment), "it's an enchantment");
		Assert.That(returned.Is(CardType.Creature), Nu.False, "it's not a creature");
		Assert.That(returned.ControllerId, Nu.EqualTo(me));
		Assert.That(HandCount(me), Nu.EqualTo(hand), "no creature entered for the other one");
		Cast(Hand(Bear, me), []);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + 2),
			"a creature entering draws for both of them"
		);
		s = s.MoveCard(innocence, s.ZoneId(me, ZoneType.Hand)); // TEST SETUP: it leaves
		Assert.That(s.GetCard(innocence).Is(CardType.Creature), "a new object, as printed");
	}

	[Test]
	public void EnduringInnocence_DyingUnderContainmentPriest_StillReturns_ItEntersAsNoCreature()
	{
		Field(Lsv("Containment Priest"), them);
		var innocence = Field(Lsv("Enduring Innocence"), me);
		var bear = Put(Bear, me, ZoneType.Graveyard);
		Cast(Hand(TestCards.Kill(), me), [], innocence);
		Assert.That(Alive(innocence), "no creature would enter");
		UntapMyLands();
		Cast(Hand(Raise, me), [], bear);
		Assert.That(InExile(bear), "as a creature that wasn't cast is");
	}

	[Test]
	public void Phelia_UnderContainmentPriest_ACreatureCardStaysExiled_AndNoCounter()
	{
		Field(Lsv("Containment Priest"), them);
		var phelia = Field(Lsv("Phelia, Exuberant Shepherd"), me);
		var bear = Field(Bear, me);
		AttackWith(phelia);
		Resolve(On<ChooseTargetChoice>(bear));
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(InExile(bear), "it would enter and wasn't cast");
		Assert.That(s.GetCard(phelia).PlusOneCounters, Nu.EqualTo(0), "it never entered");
	}

	[Test]
	public void JackedRabbit_Attacking_ARabbitPerPower()
	{
		var rabbit = Field(Lsv("Jacked Rabbit"), me);
		s = s.UpdateObject(rabbit, s.GetCard(rabbit) with { PlusOneCounters = 2 }); // TEST SETUP: a 3/4
		AttackWith(rabbit);
		Resolve();
		Assert.That(Tokens(me, "Rabbit"), Nu.EqualTo(3));
		var token = s.CardsIn(me, ZoneType.Battlefield).First(c => c.IsToken && c.Name == "Rabbit");
		Assert.That((s.Power(token), s.Toughness(token)), Nu.EqualTo((1, 1)));
	}

	// ===== MC-4.4d2b: restrictions and timing =====

	/// <summary>The same card with "{T}: You gain 1 life."</summary>
	static CardDefinition WithTap(CardDefinition card) =>
		card with
		{
			Components =
			[
				.. card.Components,
				new ActivatedAbilityComponent(ManaCost.None, [new GainLife(Ref.You, 1)])
				{
					Costs = [new TapSelfCost()],
				},
			],
		};

	bool LoyaltyOffered(int walker) =>
		s.LegalActions().OfType<ActivateLoyaltyAction>().Any(a => a.CardId == walker);

	[Test]
	public void ArchonOfEmeria_EachPlayerCastsOneSpellEachTurn_AbilitiesStillActivate()
	{
		Field(Lsv("Archon of Emeria"), me);
		var relic = Field(WithTap(Relic), me);
		var first = Hand(Boon, me);
		var second = Hand(Boon, me);
		Assert.That(Casts(second).Any(), "TEST SETUP: castable before any spell this turn");
		Cast(first, []);
		Assert.That(Casts(second).Any(), Nu.False, "more than one spell this turn");
		Assert.That(Activations(relic).Any(), "an ability isn't a spell");

		var theirs = Hand(Boon, them);
		var theirSecond = Hand(Boon, them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(Casts(theirs).Any(), "each player: their first this turn");
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = theirs });
		Assert.That(Casts(theirSecond).Any(), Nu.False, "their second");
	}

	[Test]
	public void ArchonOfEmeria_TheirNonbasicLandsEnterTapped()
	{
		Field(Lsv("Archon of Emeria"), me);
		var theirs = Put(Lsv("Strip Mine"), them, ZoneType.Hand);
		s = s.EnterBattlefield(theirs, them);
		var basic = Put(TestGames.Card("Plains"), them, ZoneType.Hand);
		s = s.EnterBattlefield(basic, them);
		var mine = Hand(Lsv("Strip Mine"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = mine });
		Assert.That(s.GetCard(theirs).IsTapped, "a nonbasic land an opponent controls");
		Assert.That(s.GetCard(basic).IsTapped, Nu.False, "a basic land");
		Assert.That(s.GetCard(mine).IsTapped, Nu.False, "my own");
	}

	[Test]
	public void VoiceOfVictory_Mobilize_TwoTappedAttackingWarriors_SacrificedAtTheEndStep()
	{
		var voice = Field(Lsv("Voice of Victory"), me);
		var life = Life(them);
		AttackWith(voice);
		Resolve();
		var warriors = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.IsToken && c.Name == "Warrior")
			.ToList();
		Assert.That(warriors, Has.Count.EqualTo(2));
		Assert.That(warriors.All(w => w.IsTapped), "tapped");
		Assert.That(warriors.All(w => w.AttackingId == them), "and attacking");
		Assert.That(warriors.Select(w => (s.Power(w), s.Toughness(w))), Nu.All.EqualTo((1, 1)));
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.That(Life(them), Nu.EqualTo(life - 3), "the Voice's 1 and each Warrior's 1");
		Assert.That(Tokens(me, "Warrior"), Nu.EqualTo(2), "still there after combat");
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(Tokens(me, "Warrior"), Nu.EqualTo(0), "sacrificed at the next end step");
	}

	[Test]
	public void VoiceOfVictory_OpponentsCantCastSpells_DuringMyTurn()
	{
		Field(Lsv("Voice of Victory"), me);
		var relic = Field(WithTap(Relic), them);
		var mine = Hand(Boon, me);
		var theirs = Hand(Boon, them);
		Assert.That(Casts(mine).Any(), "my own spells");
		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(s.GetGame().PriorityPlayerId, Nu.EqualTo(them), "TEST SETUP: they may act");
		Assert.That(Casts(theirs).Any(), Nu.False, "an opponent's spell during my turn");
		Assert.That(Activations(relic).Any(), "an ability isn't a spell");
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId == them
		);
		Assert.That(Casts(theirs).Any(), "during their own turn");
	}

	[Test]
	public void ClarionConqueror_NoAbilitiesOfArtifactsCreaturesOrPlaneswalkers_ManaAbilitiesToo()
	{
		var elspeth = Elspeth();
		var relic = Field(WithTap(Relic), me);
		var monk = Field(WithTap(Bear), me);
		var shrine = Field(WithTap(Shrine), me);
		var mox = Field(Lsv("Mox Pearl"), me);
		var cycler = Hand(Bear with { Components = [Scripting.Cycling("{1}")] }, me);
		var spell = Hand(Bear, me);
		bool Taps() =>
			new TapForManaAction
			{
				PlayerId = me,
				CardId = mox,
				Color = ManaColor.White,
			}
				.ValidateAdd(s)
				.IsValid;
		Assert.That(
			(Activations(relic).Any(), Activations(monk).Any(), LoyaltyOffered(elspeth), Taps()),
			Nu.EqualTo((true, true, true, true)),
			"TEST SETUP: each can be activated"
		);

		Field(Lsv("Clarion Conqueror"), them);
		Assert.That(Activations(relic).Any(), Nu.False, "an artifact's");
		Assert.That(Activations(monk).Any(), Nu.False, "a creature's");
		Assert.That(LoyaltyOffered(elspeth), Nu.False, "a planeswalker's");
		Assert.That(Taps(), Nu.False, "a mana ability is an activated ability");
		Assert.That(Activations(shrine).Any(), "an enchantment's");
		Assert.That(Activations(cycler).Any(), "a creature CARD in my hand is no creature");
		Assert.That(Casts(spell).Any(), "spells are still cast, with the lands' mana");
	}

	[Test]
	public void ClarionConqueror_ACreaturesManaPaysForNothing_ACardInHandStillDoes()
	{
		Field(Lsv("Llanowar Elves"), me);
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.Tap(land.Id); // TEST SETUP: only the Elves can pay
		var green = ManaCost.Parse("{G}");
		Assert.That(ManaPayment.Find(s, me, green, 0), Nu.Not.Null, "TEST SETUP: the Elves pay");
		Field(Lsv("Clarion Conqueror"), them);
		Assert.That(ManaPayment.Find(s, me, green, 0), Nu.Null, "the Elves can't be tapped for it");
		Hand(Lsv("Elvish Spirit Guide"), me);
		Assert.That(ManaPayment.Find(s, me, green, 0), Nu.Not.Null, "the Guide in my hand can");
	}

	[Test]
	public void ClarionConqueror_AnArtifactsManaAbilityThatCostsMana_PaysForNothingEither()
	{
		// "{T}: Add {C}." and "{1}, {T}: Add one mana of any color."
		Field(
			Relic with
			{
				Components =
				[
					new ManaAbilityComponent(ManaColor.Colorless),
					ManaAbilityComponent.OfAnyOneColor(1) with
					{
						Cost = ManaCost.Parse("{1}"),
					},
				],
			},
			me
		);
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land) && c.Name != "Forest")
				.ToList()
		)
			s = s.Tap(land.Id); // TEST SETUP: Forests and the Relic — blue only through its {1} ability
		var blue = ManaCost.Parse("{U}");
		Assert.That(
			ManaPayment.Find(s, me, blue, 0),
			Nu.Not.Null,
			"TEST SETUP: a Forest pays its {1}"
		);
		Field(Lsv("Clarion Conqueror"), them);
		Assert.That(ManaPayment.Find(s, me, blue, 0), Nu.Null, "that ability can't be activated");
	}

	[Test]
	public void ALockOrAnAbilityTaxAPermanentGains_IsFoundToo()
	{
		// No card gains either yet — but the "is one in play" flags must not miss the day one does.
		var bear = Field(Bear, me);
		var relic = Field(WithTap(Relic), me);
		Hand(Boon, me);
		int RelicCosts() =>
			MagicCore
				.Rules.Costs.OfAbility(
					s,
					s.ActivatedAbilitiesOf(s.GetCard(relic)).Single(),
					[],
					me,
					relic
				)
				.ManaValue;
		Assert.That(
			(MagicCore.Turns.Combat.LockedDown(s, me), RelicCosts()),
			Nu.EqualTo((false, 0)),
			"TEST SETUP: neither yet"
		);
		var hush = TestCards.Targeted(
			"Hush",
			MagicCore.Targeting.TargetSpec.Creature,
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
			{
				Gains = [new CantCastOrActivate(), new CostChange(2) { Abilities = true }],
			}
		);
		Cast(Hand(hush, me), [], bear);
		Assert.That(MagicCore.Turns.Combat.LockedDown(s, me), "the lock it gained");
		Assert.That(RelicCosts(), Nu.EqualTo(2), "the tax it gained");
	}

	int Emperor()
	{
		var emperor = Hand(Lsv("The Wandering Emperor"), me);
		Cast(emperor, []);
		UntapMyLands();
		return emperor;
	}

	[Test]
	public void TheWanderingEmperor_FlashedIn_ActivatesAtInstantSpeed_OnlyTheTurnSheEntered()
	{
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.BeginCombat && x.GetGame().PriorityPlayerId == me
		);
		var emperor = Emperor();
		Assert.That(Alive(emperor), "flash: cast at the beginning of combat");
		Loyalty(emperor, 1);
		var samurai = s.CardsIn(me, ZoneType.Battlefield)
			.Single(c => c.IsToken && c.Name == "Samurai");
		Assert.That(
			(s.Power(samurai), s.Toughness(samurai), s.Has(samurai, "Vigilance")),
			Nu.EqualTo((2, 2, true))
		);
		Assert.That(s.GetCard(emperor).Loyalty, Nu.EqualTo(3 - 1));

		var entered = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > entered
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.BeginCombat
				&& x.GetGame().PriorityPlayerId == me
		);
		Assert.That(LoyaltyOffered(emperor), Nu.False, "she entered an earlier turn");
	}

	[Test]
	public void TheWanderingEmperor_PlusOne_ACounterAndFirstStrikeUntilEndOfTurn()
	{
		var emperor = Emperor();
		var bear = Field(Bear, me);
		Assert.That(
			s.LegalActions()
				.OfType<ActivateLoyaltyAction>()
				.Any(a => a.CardId == emperor && a.AbilityIndex == 0 && a.Targets is [0]),
			"UP TO one target creature"
		);
		Loyalty(emperor, 0, bear);
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(s.Has(s.GetCard(bear), "First strike"));
		Assert.That(s.GetCard(emperor).Loyalty, Nu.EqualTo(3 + 1));
		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(s.Has(s.GetCard(bear), "First strike"), Nu.False, "until end of turn");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(1), "the counter stays");
	}

	[Test]
	public void TheWanderingEmperor_MinusTwo_ExilesATappedCreature_AndTwoLife()
	{
		var emperor = Emperor();
		var tapped = Field(Bear, them);
		var untapped = Field(Bear, them);
		s = s.Tap(tapped); // TEST SETUP
		var life = Life(me);
		var offered = s.LegalActions()
			.OfType<ActivateLoyaltyAction>()
			.Where(a => a.CardId == emperor && a.AbilityIndex == 2)
			.SelectMany(a => a.Targets)
			.ToList();
		Assert.That(offered, Nu.EqualTo(new[] { tapped }), "target TAPPED creature");
		Loyalty(emperor, 2, tapped);
		Assert.That(
			(InExile(tapped), Alive(untapped), Life(me)),
			Nu.EqualTo((true, true, life + 2))
		);
	}

	[Test]
	public void JacesMachinations_OnlyJacePlaneswalkers_AtInstantSpeed()
	{
		var elspeth = Elspeth();
		Cast(Hand("Jace's Machinations", me), []);
		UntapMyLands();
		var jace = Jace(me)!;
		var boon = Hand(Boon, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = boon }); // the stack isn't empty now
		Assert.That(LoyaltyOffered(jace.Id), "a Jace planeswalker");
		Assert.That(LoyaltyOffered(elspeth), Nu.False, "no other planeswalker");
	}

	[Test]
	public void AnointedPeacekeeper_LooksAtTheirHand_NamesAnyCard_ItsSpellsAndAbilitiesCostTwoMore()
	{
		var commando = Lsv("Cathar Commando");
		var theirSpell = Hand(commando, them);
		var theirBear = Hand(Bear, them);
		var theirs = Field(commando, them);
		var theirShrine = Field(WithTap(Shrine), them);
		var mine = Field(commando, me);
		var mySpell = Hand(commando, me);
		var relic = Field(Relic, me);
		Assert.That(
			s.GetCard(theirSpell).KnownTo & Knowers.Opponent,
			Nu.EqualTo(Knowers.None),
			"TEST SETUP: unseen"
		);

		var keeper = Hand(Lsv("Anointed Peacekeeper"), me);
		var landOffered = false;
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = keeper });
		Resolve(c =>
		{
			switch (c)
			{
				case ChooseKeywordChoice k:
					landOffered = k.Keywords.Contains("Strip Mine");
					return [k.Keywords.IndexOf("Cathar Commando") + 1];
				default:
					return c.Options.Take(c.MinChoices).Select(o => o.Id);
			}
		});
		Assert.That(landOffered, "any card name: a land's too");
		Assert.That(
			CardRegistry.CardNames(s),
			Has.No.Member("Strip Mine"),
			"Kai Winn's stay nonland"
		);
		Assert.That(
			s.GetCard(theirSpell).KnownTo & Knowers.Opponent,
			Nu.EqualTo(Knowers.Opponent),
			"look at an opponent's hand"
		);
		Assert.That(
			EffectiveManaValue(them, theirSpell),
			Nu.EqualTo(2 + 2),
			"their spell so named"
		);
		Assert.That(EffectiveManaValue(them, theirBear), Nu.EqualTo(1), "another name");
		Assert.That(EffectiveManaValue(me, mySpell), Nu.EqualTo(2), "my own spell so named");

		int AbilityCost(int player, int card) =>
			MagicCore
				.Rules.Costs.OfAbility(
					s,
					s.ActivatedAbilitiesOf(s.GetCard(card)).Single(),
					[],
					player,
					card
				)
				.ManaValue;
		Assert.That(AbilityCost(them, theirs), Nu.EqualTo(1 + 2), "abilities of sources so named");
		Assert.That(AbilityCost(me, mine), Nu.EqualTo(1 + 2), "mine too: any such source");
		Assert.That(AbilityCost(them, theirShrine), Nu.EqualTo(0), "another name");

		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(Activations(theirs).Single(a => a.Targets.Contains(relic)));
		Resolve();
		Assert.That(Alive(relic), Nu.False, "it was activated");
		Assert.That(
			s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && c.IsTapped),
			Nu.EqualTo(3),
			"{1}, and {2} more"
		);
	}

	// ===== MC-4.4d3: leaving the battlefield =====

	static CardDefinition Costing(string cost) =>
		TestGames.CreatureCosting(cost, "Costing " + cost, "2/2");

	static CardDefinition Forest => TestGames.Card("Forest");

	/// <summary>"{1} instant": target permanent destroyed (Graveyard), or put into its owner's hand or exile.</summary>
	static CardDefinition Remove(ZoneType to) =>
		to == ZoneType.Graveyard
			? TestCards.Kill(MagicCore.Targeting.TargetSpec.Permanent())
			: TestCards.Targeted(
				"Remove",
				MagicCore.Targeting.TargetSpec.Permanent(),
				new MoveTo(Ref.Target(), to)
			);

	/// <summary>I cast Skyclave Apparition, its entering trigger exiling this — or nothing.</summary>
	int Apparition(params int[] exiling)
	{
		var apparition = Hand(Lsv("Skyclave Apparition"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = apparition });
		Resolve(Picking(exiling));
		UntapMyLands();
		return apparition;
	}

	int Illusions(int player) => Tokens(player, "Illusion");

	[Test]
	public void SkyclaveApparition_Enters_ExilesANonlandNontokenPermanentIDontControl_OfManaValueFourOrLess()
	{
		var four = Field(Costing("{4}"), them);
		Field(Costing("{5}"), them);
		Field(Costing("{2}"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Conscript",
					"{1}",
					"Instant",
					null,
					new CreateTokens(SoldierToken, 1) { ControlledBy = Ref.EachOpponent }
				),
				me
			),
			[]
		);
		Assert.That(Tokens(them, "Soldier"), Nu.EqualTo(1), "a token of theirs, mana value 0");
		UntapMyLands();
		var offered = new List<int>();
		var apparition = Hand(Lsv("Skyclave Apparition"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = apparition });
		Resolve(c =>
		{
			if (c is ChooseTargetChoice)
				offered.AddRange(c.Options.Select(o => o.Id).Where(id => id != 0));
			return Picking(four)(c);
		});
		Assert.That(
			offered,
			Nu.EquivalentTo(new[] { four }),
			"not their 5-drop, their land, their token, nor my own"
		);
		Assert.That(InExile(four));
	}

	[TestCase(ZoneType.Graveyard)]
	[TestCase(ZoneType.Hand)]
	[TestCase(ZoneType.Exile)]
	public void SkyclaveApparition_LeavesTheBattlefield_TheExiledCardsOwnerCreatesAnIllusionOfItsManaValue(
		ZoneType to
	)
	{
		var three = Field(Costing("{3}"), them);
		var apparition = Apparition(three);
		Assert.That(InExile(three));
		Cast(Hand(Remove(to), me), [], apparition);
		Assert.That(Alive(apparition), Nu.False);
		var illusion = s.CardsIn(them, ZoneType.Battlefield).Single(c => c.IsToken);
		Assert.That(illusion.Name, Nu.EqualTo("Illusion"));
		Assert.That(illusion.Colors, Nu.EquivalentTo(new[] { ManaColor.Blue }));
		Assert.That(
			(s.Power(illusion), s.Toughness(illusion)),
			Nu.EqualTo((3, 3)),
			"X/X, X the exiled card's mana value"
		);
		Assert.That(Illusions(me), Nu.EqualTo(0), "the exiled card's owner creates it");
		Assert.That(InExile(three), "the card stays exiled");
	}

	[Test]
	public void SkyclaveApparition_ThatExiledNothing_LeavesNoToken()
	{
		var three = Field(Costing("{3}"), them);
		var first = Apparition();
		Assert.That(Alive(three), "up to one: none");
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], first);
		Assert.That(Illusions(them) + Illusions(me), Nu.EqualTo(0), "no exiled card, no owner");
		UntapMyLands();
		var second = Apparition(three);
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], second);
		Assert.That(Illusions(them), Nu.EqualTo(1), "and one that did exile a card makes one");
	}

	[Test]
	public void SkyclaveApparition_TheTokenIsTheExiledCardsOwners_NotItsControllers()
	{
		var stolen = Field(Costing("{2}"), me);
		s = s.GainControl(stolen, them);
		var apparition = Apparition(stolen);
		Assert.That(s.IsIn(stolen, me, ZoneType.Exile), "my card, which they controlled");
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], apparition);
		Assert.That(Illusions(me), Nu.EqualTo(1), "its owner creates the token");
		Assert.That(Illusions(them), Nu.EqualTo(0));
		Assert.That(
			s.Power(s.CardsIn(me, ZoneType.Battlefield).Single(c => c.IsToken)),
			Nu.EqualTo(2)
		);
	}

	[Test]
	public void SkyclaveApparition_Returned_IsANewObject_ThatExiledNoCard()
	{
		var three = Field(Costing("{3}"), them);
		var apparition = Apparition(three);
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], apparition);
		Assert.That(Illusions(them), Nu.EqualTo(1));
		UntapMyLands();
		Cast(Hand(Raise, me), [], apparition);
		Assert.That(Alive(apparition), "back, exiling nothing");
		UntapMyLands();
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], apparition);
		Assert.That(Alive(apparition), Nu.False);
		Assert.That(
			Illusions(them),
			Nu.EqualTo(1),
			"it left again, and triggered: the card in exile was another object's"
		);
	}

	[Test]
	public void OnlyAPermanentThatReadsItsOwnLeaving_StagesAZoneChange()
	{
		var bear = Field(Bear, me);
		var apparition = Field(Lsv("Skyclave Apparition"), me);
		s = s.MoveCard(bear, s.ZoneId(me, ZoneType.Hand));
		Assert.That(
			s.PendingGameEvents.OfType<MagicCore.Turns.ChangedZoneEvent>(),
			Nu.Empty,
			"MoveCard is the hottest path: nothing staged for a permanent with no leaving ability"
		);
		s = s.MoveCard(apparition, s.ZoneId(me, ZoneType.Hand));
		Assert.That(
			s.PendingGameEvents.OfType<MagicCore.Turns.ChangedZoneEvent>().Count(),
			Nu.EqualTo(1)
		);
	}

	[Test]
	public void ACardLinkedToNoObject_IsExiledByNoSource()
	{
		var relic = Field(Relic, me);
		var linked = Put(Boon, me, ZoneType.Exile);
		var orphan = Put(Boon, me, ZoneType.Exile);
		s = s.UpdateObject(
			linked,
			(Card)s.GetCard(linked).WithComponent(new ExiledBy(relic, s.GetCard(relic).Incarnation))
		);
		// a link made by a source already gone names no object of it (incarnation -1)
		s = s.UpdateObject(orphan, (Card)s.GetCard(orphan).WithComponent(new ExiledBy(987654, -1)));
		bool ExiledBy(int card, int source) =>
			MagicCore.Filters.Is.ExiledBySource.Matches(
				s,
				s.GetCard(card),
				new MagicCore.Filters.FilterContext(me, source)
			);
		Assert.That(ExiledBy(linked, relic), "a static asks the source as it is now");
		Assert.That(ExiledBy(orphan, 987654), Nu.False, "no object, no link");
	}

	[Test]
	public void TheOwnerOfSeveralCards_IsNamedOnce()
	{
		Put(Boon, me, ZoneType.Graveyard);
		Put(Boon, me, ZoneType.Graveyard);
		var life = Life(me);
		Cast(
			Hand(
				TestGames.Inline(
					"Tithe",
					"{1}",
					"Instant",
					null,
					new GainLife(Ref.OwnerOf(Ref.CardsInYourGraveyard), 1)
				),
				me
			),
			[]
		);
		Assert.That(Life(me), Nu.EqualTo(life + 1), "two cards of mine: I am their owner once");
	}

	[Test]
	public void SerraParagon_AnXSpell_OnlyWithAnXThatKeepsItsManaValueThreeOrLess()
	{
		var mold = TestGames.Inline("Mold", "{X}{1}", "Artifact");
		var held = Hand(mold, me);
		Assert.That(Casts(held).Max(c => c.X), Nu.GreaterThan(2), "from my hand, any X");
		Field(Lsv("Serra Paragon"), me);
		var buried = Put(mold, me, ZoneType.Graveyard);
		Assert.That(
			Casts(buried).Select(c => c.X).Distinct(),
			Nu.EquivalentTo(new[] { 0, 1, 2 }),
			"a permanent spell with mana value 3 or less: {1} and X"
		);
	}

	[Test]
	public void SerraParagon_OnceEachOfMyTurns_ALandOrAPermanentSpellOfManaValueThreeOrLess_FromMyGraveyard()
	{
		Field(Lsv("Serra Paragon"), me);
		var forest = Put(Forest, me, ZoneType.Graveyard);
		var three = Put(Costing("{3}"), me, ZoneType.Graveyard);
		var four = Put(Costing("{4}"), me, ZoneType.Graveyard);
		var boon = Put(Boon, me, ZoneType.Graveyard);
		bool MayPlay(int land) =>
			new PlayLandAction { PlayerId = me, CardId = land }
				.ValidateAdd(s)
				.IsValid;
		Assert.That(Casts(three), Nu.Not.Empty, "a permanent spell with mana value 3 or less");
		Assert.That(MayPlay(forest), "a land");
		Assert.That(Casts(four), Nu.Empty, "mana value 4");
		Assert.That(Casts(boon), Nu.Empty, "an instant is no permanent spell");
		Cast(three, []);
		Assert.That(Alive(three), "it resolves onto the battlefield");
		Assert.That(MayPlay(forest), Nu.False, "once during each of your turns");
	}

	[Test]
	public void SerraParagon_ThePermanentCastThisWay_PutIntoAGraveyard_IsExiledAndIGainTwo_UntilItLeaves()
	{
		var paragon = Field(Lsv("Serra Paragon"), me);
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var life = Life(me);
		Cast(bear, []);
		Assert.That(Alive(bear));
		UntapMyLands();
		Cast(Hand(Remove(ZoneType.Hand), me), [], bear);
		Assert.That(s.IsIn(bear, me, ZoneType.Hand));
		Assert.That(Life(me), Nu.EqualTo(life), "returned to my hand: put into no graveyard");
		UntapMyLands();
		Cast(bear, []);
		UntapMyLands();
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard), "cast from my hand, it gained nothing");
		Assert.That(Life(me), Nu.EqualTo(life));
		// TEST SETUP: the Paragon's once-a-turn permission is unused again
		s = s.UpdateObject(paragon, s.GetCard(paragon) with { PermissionUsedOn = 0 });
		UntapMyLands();
		Cast(bear, []);
		UntapMyLands();
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(InExile(bear), "exile it");
		Assert.That(Life(me), Nu.EqualTo(life + 2), "and you gain 2 life");
	}

	[Test]
	public void SerraParagon_ALandPlayedThisWay_GainsItToo_ThoughNoCreatureDies()
	{
		Field(Lsv("Serra Paragon"), me);
		var forest = Put(Forest, me, ZoneType.Graveyard);
		var life = Life(me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = forest });
		Assert.That(Alive(forest));
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber > turn);
		s = TestGames.PassUntil(s, x => x.GetGame().PriorityPlayerId == me);
		Cast(Hand(Remove(ZoneType.Graveyard), me), [], forest);
		Assert.That(InExile(forest), "put into a graveyard from the battlefield: exile it");
		Assert.That(Life(me), Nu.EqualTo(life + 2), "a turn later: it has it until it leaves");
	}

	[Test]
	public void SerraParagon_ReturnedBeforeItsTriggerResolves_IsAnotherObject_AndIsNotExiled()
	{
		Field(Lsv("Serra Paragon"), me);
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var life = Life(me);
		Cast(bear, []);
		UntapMyLands();
		var kill = Hand(TestCards.Kill(), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = kill,
				Targets = [bear],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Assert.That(s.IsIn(bear, me, ZoneType.Graveyard), "dead, its trigger on the stack");
		Assert.That(s.StackIsEmpty(), Nu.False);
		Cast(Hand(Raise, me), [], bear);
		Assert.That(Alive(bear), "exile it: only from the graveyard it was put into");
		Assert.That(Life(me), Nu.EqualTo(life + 2), "and you gain 2 life");
	}
}
