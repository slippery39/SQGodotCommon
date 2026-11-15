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
}
