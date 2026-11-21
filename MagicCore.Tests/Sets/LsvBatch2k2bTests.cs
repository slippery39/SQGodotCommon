using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4k2b, LSVPower — green's trigger, combat and casting options, FIRING against their printed text: a creature or
/// a creature SPELL becoming a target (Surrak, Elusive Hunter), "attacks that player if able" (Ursine Monstrosity), a
/// counter of any kind removed (Leatherhead, Swamp Stalker), combat damage that can't be prevented (Questing Beast),
/// "put into a graveyard from anywhere" (Worldspine Wurm), the first spell of the game cast free (Once Upon a Time), a
/// spell shuffled into its owner's library (Green Sun's Zenith) and level up (Hexdrinker).
/// </summary>
public class LsvBatch2k2bTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");
	static CardDefinition Ogre => TestGames.Creature("Ogre", "4/4");

	/// <summary>Triggers raised outside an action go on the stack at the next priority, then resolve.</summary>
	void Settle()
	{
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Resolve();
	}

	/// <summary>TEST SETUP: their planeswalker, entered — so it has its loyalty.</summary>
	int TheirPlaneswalker(int loyalty)
	{
		var walker = Hand(TestGames.Planeswalker("Walker", loyalty), them);
		s = s.EnterBattlefield(walker, them);
		return walker;
	}

	/// <summary>They cast this instant at these targets while I hold nothing on the stack; it all resolves.</summary>
	void TheyCast(CardDefinition instant, params int[] targets)
	{
		var spell = Hand(instant, them);
		if (s.StackIsEmpty())
			s = s.Act(new PassPriorityAction { PlayerId = me });
		else
			s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = spell,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	// ===== Surrak, Elusive Hunter: a creature you control or a creature spell you control becomes the target =====

	[Test]
	public void Surrak_TheirSpellTargetsMyCreature_IDraw()
	{
		Field(Lsv("Surrak, Elusive Hunter"), me);
		var bear = Field(Bear, me);
		var hand = HandCount(me);

		TheyCast(TestCards.Kill(), bear);

		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void Surrak_TheirSpellTargetsMyCreatureSpell_IDraw()
	{
		Field(Lsv("Surrak, Elusive Hunter"), me);
		var bear = Hand(Bear, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = bear });
		var hand = HandCount(me);

		TheyCast(TestCards.Targeted("Nope", TargetSpec.Spell(), new Counter(Ref.Target())), bear);

		Assert.That(InGraveyard(bear), "countered");
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "a creature spell I control was targeted");
	}

	[Test]
	public void Surrak_ACreatureCardInMyGraveyardTargeted_IsNoCreatureIControl()
	{
		Field(Lsv("Surrak, Elusive Hunter"), me);
		var bear = Put(Bear, me, ZoneType.Graveyard);
		var hand = HandCount(me);

		TheyCast(
			TestCards.Targeted(
				"Grave Robbing",
				TargetSpec.InAnyGraveyard(Is.Creature),
				new Exile(Ref.Target())
			),
			bear
		);

		Assert.That(InExile(bear), "it was targeted");
		Assert.That(HandCount(me), Nu.EqualTo(hand));
	}

	[Test]
	public void MacoStrikeTeam_ACreatureIControl_NeverACreatureSpell()
	{
		Field(CardRegistry.Get("MACO Strike Team"), me);
		int Officers() => s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Officer");
		var spell = Hand(Bear, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = spell });
		TheyCast(TestCards.Targeted("Nope", TargetSpec.Spell(), new Counter(Ref.Target())), spell);
		Assert.That(Officers(), Nu.Zero, "a creature spell is no creature I control");

		var bear = Field(Bear, me);
		TheyCast(TestCards.Kill(), bear);
		Assert.That(Officers(), Nu.EqualTo(1));
	}

	// ===== Ursine Monstrosity: mill, attacks that player if able, indestructible and +1/+1 per card type =====

	[Test]
	public void UrsineMonstrosity_BeginningOfCombat_MustAttackThePlayer_PumpedByCardTypes()
	{
		var ursine = Field(Lsv("Ursine Monstrosity"), me);
		var walker = TheirPlaneswalker(3);
		Put(Bear, me, ZoneType.Graveyard);
		var milled = OnTop(TestCards.Fodder);

		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		var ask = (DeclareAttackerChoice)s.GetPendingChoice()!;

		Assert.That(InGraveyard(milled), "mill a card");
		var card = s.GetCard(ursine);
		Assert.That(
			(s.Power(card), s.Toughness(card), s.Has(card, "Indestructible")),
			Nu.EqualTo((5, 5, true)),
			"a creature and the milled sorcery: +2/+2"
		);
		Assert.That(ask.CreatureId, Nu.EqualTo(ursine));
		Assert.That(
			ask.GetOptions(s, []).Select(o => o.Id),
			Nu.EqualTo(new[] { them }),
			$"it attacks that player — not their planeswalker ({walker}), and no \"don't attack\""
		);

		s = s.UpdateObject(
			ursine,
			s.GetCard(ursine).WithComponent(new UntilEndOfTurn { LosesAllAbilities = true })
		);
		Assert.That(
			ask.GetOptions(s, []).Select(o => o.Id),
			Nu.EqualTo(new[] { them }),
			"an effect on it, as goad: losing all abilities doesn't end it"
		);
	}

	// ===== Leatherhead, Swamp Stalker: a hexproof counter; remove a counter of any kind, when you do destroy =====

	[Test]
	public void Leatherhead_EntersWithAHexproofCounter()
	{
		var leatherhead = Hand(Lsv("Leatherhead, Swamp Stalker"), me);
		s = s.EnterBattlefield(leatherhead, me);
		var kill = Hand(TestCards.Kill(), them);

		Assert.That(s.GetCard(leatherhead).CountersOf(CounterKinds.Hexproof), Nu.EqualTo(1));
		Assert.That(TargetSpec.Creature.IsLegal(s, them, kill, leatherhead), Nu.False, "hexproof");
	}

	[Test]
	public void Leatherhead_CombatDamage_RemoveTheCounterIChoose_WhenIDoDestroyTheirArtifact()
	{
		var leatherhead = Field(Lsv("Leatherhead, Swamp Stalker"), me);
		s = s.PutCounters(leatherhead, CounterKinds.Hexproof, 1);
		s = s.PutCounters(leatherhead, CounterKinds.PlusOne, 1);
		var trinket = Field(TestCards.Trinket, them);
		Field(TestCards.Trinket, me); // "that player controls": never mine

		var kinds = 0;
		AttackWith(
			c =>
			{
				switch (c)
				{
					case YesNoChoice:
						return [1];
					case ChooseKeywordChoice k:
						kinds = k.Options.Count;
						return [k.Options.Single(o => o.DisplayText == CounterKinds.Hexproof).Id];
					case ChooseTargetChoice t:
						Assert.That(t.Options.Select(o => o.Id), Nu.EqualTo(new[] { trinket }));
						return [trinket];
					default:
						return c.Options.Take(c.MinChoices).Select(o => o.Id);
				}
			},
			leatherhead
		);

		Assert.That(kinds, Nu.EqualTo(2), "a counter of either kind on her");
		var card = s.GetCard(leatherhead);
		Assert.That(
			(card.CountersOf(CounterKinds.Hexproof), card.PlusOneCounters, s.Has(card, "Hexproof")),
			Nu.EqualTo((0, 1, false)),
			"the hexproof counter, not the +1/+1"
		);
		Assert.That(Alive(trinket), Nu.False, "when you do, destroy");
	}

	[Test]
	public void Leatherhead_WithNoCounterToRemove_IsNotAsked_AndDestroysNothing()
	{
		var leatherhead = Field(Lsv("Leatherhead, Swamp Stalker"), me);
		s = s.UpdateObject(leatherhead, s.GetCard(leatherhead) with { HasFinalityCounter = true });
		var trinket = Field(TestCards.Trinket, them);

		var asked = false;
		AttackWith(
			c =>
			{
				asked |= c is YesNoChoice;
				return c.Options.Take(c.MinChoices).Select(o => o.Id);
			},
			leatherhead
		);

		Assert.That(
			asked,
			Nu.False,
			"a finality counter is a flag RemoveCounters can't take (DesignNotes)"
		);
		Assert.That(Alive(trinket), "nothing removed: no \"when you do\"");
	}

	// ===== Questing Beast: combat damage by creatures you control can't be prevented =====

	/// <summary>A 3/4 with protection from creatures: their damage to it is prevented.</summary>
	static CardDefinition Guard
	{
		get
		{
			var guard = TestGames.Creature("Guard", "3/4");
			return guard with { Components = [.. guard.Components, new Protection(Is.Creature)] };
		}
	}

	void OgreAttacksIntoGuard(out int guard)
	{
		var ogre = Field(Ogre, me);
		var theirs = guard = Field(Guard, them);
		AttackWith(
			c =>
				c switch
				{
					DeclareBlockerChoice b when b.CreatureId == theirs => [ogre],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				},
			ogre
		);
	}

	[Test]
	public void ProtectionFromCreatures_PreventsTheOgresCombatDamage()
	{
		OgreAttacksIntoGuard(out var guard);
		Assert.That(Alive(guard), "the control: prevented without Questing Beast");
	}

	[Test]
	public void QuestingBeast_CombatDamageByMyCreatures_CantBePrevented()
	{
		Field(Lsv("Questing Beast"), me);
		OgreAttacksIntoGuard(out var guard);
		Assert.That(Alive(guard), Nu.False, "4 damage to a 3/4 with protection from creatures");
	}

	[Test]
	public void QuestingBeast_CombatDamageToThem_ThatMuchToTheirPlaneswalker()
	{
		var beast = Field(Lsv("Questing Beast"), me);
		var walker = TheirPlaneswalker(6);
		var life = Life(them);

		AttackWith(On<ChooseTargetChoice>(walker), beast);

		Assert.That(Life(them), Nu.EqualTo(life - 4));
		Assert.That(s.GetCard(walker).Loyalty, Nu.EqualTo(2), "that much: 4");
	}

	// ===== Worldspine Wurm: three Wurms as it dies; shuffled in from a graveyard, from anywhere =====

	[Test]
	public void WorldspineWurm_Dies_ThreeWurms_AndItIsShuffledIntoItsOwnersLibrary()
	{
		var wurm = Field(Lsv("Worldspine Wurm"), me);

		s = s.Destroy([wurm]);
		Settle();

		var wurms = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == "Wurm").ToList();
		Assert.That(wurms, Has.Count.EqualTo(3));
		Assert.That(
			wurms.Select(w => (s.Power(w), s.Toughness(w), s.Has(w, "Trample"))),
			Nu.All.EqualTo((5, 5, true))
		);
		Assert.That(
			s.IsIn(wurm, me, ZoneType.Library),
			"put into a graveyard from the battlefield"
		);
	}

	[Test]
	public void WorldspineWurm_DiesFaceDown_IsTheWurmInTheGraveyard_AndShuffledIn()
	{
		var wurm = Hand(Lsv("Worldspine Wurm"), me);
		s = s.EnterBattlefield(wurm, me, faceDown: true);

		s = s.Destroy([wurm]);
		Settle();

		Assert.That(
			s.IsIn(wurm, me, ZoneType.Library),
			"no look-back: read from the card in the graveyard"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Wurm"),
			Nu.Zero,
			"\"when this dies\" looks back at a face-down 2/2"
		);
	}

	[Test]
	public void WorldspineWurm_DiscardedFromMyHand_IsShuffledInToo()
	{
		var wurm = Hand(Lsv("Worldspine Wurm"), me);

		s = s.Discard(me, [wurm]);
		Settle();

		Assert.That(s.IsIn(wurm, me, ZoneType.Library), "from anywhere");
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Wurm"), Nu.Zero);
	}

	// ===== Once Upon a Time: free as the first spell you've cast this game =====

	[Test]
	public void OnceUponATime_TheFirstSpellOfTheGame_IsFree_AndTakesACreatureOrLand()
	{
		var once = Hand(Lsv("Once Upon a Time"), me);
		var fodder = OnTop(TestCards.Fodder);
		var bear = OnTop(Bear);
		var lands = UntappedLands();
		Assert.That(Casts(once).Count(c => c.WithoutPayingManaCost), Nu.EqualTo(1));

		s = s.Act(Casts(once).Single(c => c.WithoutPayingManaCost));
		var asked = ResolveAsking(c =>
			c.Options.Any(o => o.Id == bear)
				? [bear]
				: c.Options.Take(c.MinChoices).Select(o => o.Id)
		);

		Assert.That(UntappedLands(), Nu.EqualTo(lands), "without paying its mana cost");
		Assert.That(s.IsIn(bear, me, ZoneType.Hand));
		Assert.That(
			asked.SelectMany(c => c.Options).Select(o => o.Id),
			Has.Member(bear).And.No.Member(fodder),
			"a creature or land card — never the sorcery"
		);
		Assert.That(s.IsIn(fodder, me, ZoneType.Library), "the rest on the bottom");
	}

	[Test]
	public void OnceUponATime_AfterAnotherSpell_IsPaidFor()
	{
		var once = Hand(Lsv("Once Upon a Time"), me);
		Cast(Hand(TestCards.Fodder, me), []);

		Assert.That(Casts(once).Any(), "still castable");
		Assert.That(Casts(once).Any(c => c.WithoutPayingManaCost), Nu.False);
	}

	// ===== Green Sun's Zenith: a green creature with mana value X or less; shuffled into its owner's library =====

	[Test]
	public void GreenSunsZenith_AGreenCreatureWithinX_OntoTheBattlefield_AndItIsShuffledIn()
	{
		var zenith = Hand(Lsv("Green Sun's Zenith"), me);
		var caryatid = Put(Lsv("Sylvan Caryatid"), me, ZoneType.Library); // {1}{G}
		var beast = Put(Lsv("Questing Beast"), me, ZoneType.Library); // {2}{G}{G}: over X
		var bat = Put(Lsv("Deep-Cavern Bat"), me, ZoneType.Library); // {1}{B}: not green

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = zenith,
				X = 2,
			}
		);
		var asked = ResolveAsking(c =>
			c.Options.Any(o => o.Id == caryatid)
				? [caryatid]
				: c.Options.Take(c.MinChoices).Select(o => o.Id)
		);

		var offered = asked.SelectMany(c => c.Options).Select(o => o.Id).ToList();
		Assert.That(offered, Has.Member(caryatid));
		Assert.That(offered, Has.No.Member(beast).And.No.Member(bat));
		Assert.That(Alive(caryatid));
		Assert.That(s.IsIn(zenith, me, ZoneType.Library), "shuffled into its owner's library");
	}

	// ===== Hexdrinker: level up; 4/4 with protection from instants at 3, 6/6 from everything at 8 =====

	[Test]
	public void Hexdrinker_LevelUp_ALevelCounter_AtSorcerySpeed()
	{
		var hexdrinker = Field(Lsv("Hexdrinker"), me);
		var lands = UntappedLands();

		s = s.Act(Activations(hexdrinker).Single());
		Assert.That(Activations(hexdrinker), Nu.Empty, "level up only as a sorcery");
		Resolve();

		Assert.That(s.GetCard(hexdrinker).CountersOf(CounterKinds.Level), Nu.EqualTo(1));
		Assert.That(UntappedLands(), Nu.EqualTo(lands - 1), "{1}");
	}

	[Test]
	public void Hexdrinker_ByLevel_ItsSizeAndItsProtection()
	{
		var hexdrinker = Field(Lsv("Hexdrinker"), me);
		var instant = Hand(TestCards.Kill(), them);
		var sorcery = Hand(TestCards.Fodder, them);
		(int, int, bool Instants, bool Sorceries) At(int level)
		{
			var has = s.GetCard(hexdrinker).CountersOf(CounterKinds.Level);
			s = s.PutCounters(hexdrinker, CounterKinds.Level, level - has);
			var card = s.GetCard(hexdrinker);
			return (
				s.Power(card),
				s.Toughness(card),
				TargetSpec.Creature.IsLegal(s, them, instant, hexdrinker),
				TargetSpec.Creature.IsLegal(s, them, sorcery, hexdrinker)
			);
		}

		Assert.That(At(2), Nu.EqualTo((2, 1, true, true)), "below level 3: a 2/1");
		Assert.That(At(3), Nu.EqualTo((4, 4, false, true)), "LEVEL 3-7: protection from instants");
		Assert.That(At(7), Nu.EqualTo((4, 4, false, true)));
		Assert.That(At(8), Nu.EqualTo((6, 6, false, false)), "LEVEL 8+: from everything");
	}
}
