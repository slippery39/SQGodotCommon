using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Sets.Lsv;
using MagicCore.Targeting;
using MagicCore.Turns;
using MIs = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4l4a + l4b, LSVPower — colourless' five new pieces, each against its printed text: computed colours (Painter's
/// Servant, and with Grindstone a mill that ends with the library; Celestial Colonnade, Creeping Tar Pit, Restless
/// Vinestalk), an entering's cost (Mox Diamond, Multiversal Passage — the shocklands moved onto it), skipping a turn
/// (Time Vault, and with Voltaic Key a turn after every turn that ends with the library), beginning the game on the
/// battlefield (Gemstone Caverns), exert and mana that grants (Arena of Glory).
/// </summary>
public class LsvBatch2l4Tests : FraBoardTest
{
	/// <summary>An inline card WITH a colour (TestGames' inline cards have none): a 2/2 creature unless said.</summary>
	static CardDefinition Coloured(
		string name,
		string cost,
		string color,
		string typeLine = "Creature — Test"
	)
	{
		var pt = typeLine.StartsWith("Creature") ? "2" : null;
		return new(
			new CardData(name, "normal", cost, 0, typeLine, null, pt, pt, null, [color], [], null),
			[]
		);
	}

	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>Every "you may" answered yes; a colour choice blue; any other choice its minimum.</summary>
	static IEnumerable<int> Yes(ChoiceAction c) =>
		c switch
		{
			YesNoChoice => [1],
			ChooseColorChoice => [(int)ManaColor.Blue + 1],
			_ => Min(c),
		};

	void Activate(int card, params int[] targets) =>
		s = s.Act(Activations(card).First(a => a.Targets.SequenceEqual(targets)));

	ImmutableArray<ManaColor> Colors(int card) => s.ColorsOf(s.GetCard(card));

	/// <summary>To the end of this turn and into the next one's untap step, every choice its minimum (no skip).</summary>
	void PassTheTurn()
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber > turn || x.IsOver());
	}

	/// <summary>To the end of this turn, where the Vault's question is asked — answered as told.</summary>
	void ToTheVaultsQuestion(bool skip)
	{
		var turn = s.GetGame().TurnNumber;
		for (var i = 0; i < 1000 && s.GetGame().TurnNumber == turn; i++)
			s = s.GetPendingChoice() switch
			{
				YesNoChoice y when y.Prompt!.StartsWith("Skip your turn") => s.Choose(
					skip ? [1] : []
				),
				{ } c => s.Choose(Min(c)),
				null => s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId }),
			};
	}

	// ===== l4a: computed colours =====

	[Test]
	public void PaintersServant_EveryCardAnywhereIsTheChosenColourToo()
	{
		var painter = Hand(Lsv("Painter's Servant"), me);
		var asked = CastAsking(painter, Yes);
		Assert.That(asked.OfType<ChooseColorChoice>(), Nu.Not.Empty);
		var mine = Hand(TestGames.Card("Forest"), me); // a colourless card in my hand
		var red = Put(Coloured("Goblin", "{R}", "R"), them, ZoneType.Graveyard);
		var island = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Name == "Island").Id;
		Assert.That(Colors(mine), Nu.EquivalentTo(new[] { ManaColor.Blue }));
		Assert.That(Colors(red), Nu.EquivalentTo(new[] { ManaColor.Red, ManaColor.Blue }));
		Assert.That(Colors(island), Nu.EquivalentTo(new[] { ManaColor.Blue })); // a permanent too
		Assert.That(Colors(painter), Nu.EquivalentTo(new[] { ManaColor.Blue }));
		Assert.That(MIs.Color(ManaColor.Blue).Matches(s, s.GetCard(mine), new(me, 0)));
		Assert.That(MIs.Colorless.Matches(s, s.GetCard(mine), new(me, 0)), Nu.False);
		Assert.That(CanTapFor(painter, ManaColor.Blue), Nu.False); // its choice is no mana (Room of Refuge's is)
		Assert.That(ManaAbilities.ColorsOf(s, s.GetCard(painter)), Nu.Empty);
		// gone, it paints nothing
		Cast(Hand(TestCards.Kill(), me), [], painter);
		Assert.That(Colors(mine), Nu.Empty);
	}

	[Test]
	public void PaintersServant_WithGrindstone_MillsTheWholeLibrary_AndEnds()
	{
		var grindstone = Field(Lsv("Grindstone"), me);
		var library = Library(them).Count;
		Activate(grindstone, them);
		Resolve();
		Assert.That(Library(them).Count, Nu.EqualTo(library - 2)); // colourless cards share no colour: once
		Field(Lsv("Grindstone"), me);
		CastAsking(Hand(Lsv("Painter's Servant"), me), Yes);
		var second = s.CardsIn(me, ZoneType.Battlefield).Last(c => c.Name == "Grindstone").Id;
		Activate(second, them);
		Resolve();
		Assert.That(Library(them), Nu.Empty); // all blue: repeated until the library ran out
		Assert.That(s.IsOver(), Nu.False);
	}

	[Test]
	public void CelestialColonnade_BecomesA44WhiteAndBlueElemental_UntilEndOfTurn()
	{
		var land = Field(Lsv("Celestial Colonnade"), me);
		Assert.That(Colors(land), Nu.Empty);
		Activate(land);
		Resolve();
		var c = s.GetCard(land);
		Assert.That(c.Is(CardType.Creature) && c.Is(CardType.Land));
		Assert.That((s.Power(c), s.Toughness(c)), Nu.EqualTo((4, 4)));
		Assert.That(Colors(land), Nu.EquivalentTo(new[] { ManaColor.White, ManaColor.Blue }));
		Assert.That(s.Has(c, "Flying") && s.Has(c, "Vigilance"));
		Assert.That(s.HasSubtype(c, "Elemental"));
		Assert.That(MIs.Color(ManaColor.White).Matches(s, c, new(me, 0)));
		PassTheTurn();
		Assert.That(s.GetCard(land).Is(CardType.Creature), Nu.False);
		Assert.That(Colors(land), Nu.Empty);
	}

	[Test]
	public void CreepingTarPit_A32BlueAndBlackElemental_ThatCantBeBlocked()
	{
		var land = Field(Lsv("Creeping Tar Pit"), me);
		Activate(land);
		Resolve();
		var c = s.GetCard(land);
		Assert.That((s.Power(c), s.Toughness(c)), Nu.EqualTo((3, 2)));
		Assert.That(Colors(land), Nu.EquivalentTo(new[] { ManaColor.Blue, ManaColor.Black }));
		Assert.That(s.Has(c, Combat.CantBeBlocked));
	}

	[Test]
	public void RestlessVinestalk_A55_ItsAttackMakesAnotherCreatureA33()
	{
		var land = Field(Lsv("Restless Vinestalk"), me);
		var ogre = Field(TestGames.Creature("Ogre", "5/5"), them);
		Activate(land);
		Resolve();
		var c = s.GetCard(land);
		Assert.That((s.Power(c), s.Toughness(c)), Nu.EqualTo((5, 5)));
		Assert.That(Colors(land), Nu.EquivalentTo(new[] { ManaColor.Green, ManaColor.Blue }));
		Assert.That(s.Has(c, "Trample") && s.HasSubtype(c, "Plant"));
		AttackWith(Targeting(ogre, yes: false), land);
		Assert.That((s.Power(s.GetCard(ogre)), s.Toughness(s.GetCard(ogre))), Nu.EqualTo((3, 3)));
	}

	// ===== l4a: an entering's cost =====

	[TestCase(true)]
	[TestCase(false)]
	public void MoxDiamond_DiscardALandToEnter_OrItGoesToTheGraveyardInstead(bool discard)
	{
		s = TestGames.EmptyHands(s);
		var mox = Hand(Lsv("Mox Diamond"), me);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me); // offered first, were it not "a land card"
		var forest = Hand(TestGames.Card("Forest"), me);
		var asked = CastAsking(mox, c => c is YesNoChoice ? (discard ? [1] : []) : Min(c));
		Assert.That(asked.OfType<YesNoChoice>().Count(), Nu.EqualTo(1));
		Assert.That(Alive(mox), Nu.EqualTo(discard));
		Assert.That(InGraveyard(mox), Nu.EqualTo(!discard));
		Assert.That(InGraveyard(forest), Nu.EqualTo(discard));
		Assert.That(InGraveyard(bear), Nu.False);
		if (discard)
			Assert.That(CanTapFor(mox, ManaColor.Red));
	}

	[Test]
	public void MoxDiamond_NoLandInHand_GoesToTheGraveyard_Unasked()
	{
		s = TestGames.EmptyHands(s);
		var mox = Hand(Lsv("Mox Diamond"), me);
		var spell = Hand(TestGames.Creature("Bear", "2/2"), me); // a nonland card can't pay
		var asked = CastAsking(mox, Yes);
		Assert.That(asked.OfType<YesNoChoice>(), Nu.Empty);
		Assert.That(InGraveyard(mox) && !InGraveyard(spell));
	}

	/// <summary>"Return target artifact card from your graveyard to the battlefield tapped."</summary>
	static CardDefinition ReturnTapped =>
		TestCards.Targeted(
			"Return",
			TargetSpec.InYourGraveyard(MIs.Artifact),
			new MoveTo(Ref.Target(), ZoneType.Battlefield)
			{
				Tapped = true,
				From = [ZoneType.Graveyard],
			}
		);

	void CastAt(int spell, int target) =>
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = spell,
				Targets = [target],
			}
		);

	[Test]
	public void MoxDiamond_ManifestedFaceDown_HasNoAbilities_AndIsntAsked()
	{
		s = TestGames.EmptyHands(s);
		Hand(TestGames.Card("Forest"), me);
		OnTop(TestGames.Card("Plains"));
		var mox = OnTop(Lsv("Mox Diamond"));
		var dread = Hand(
			TestGames.Inline("Dread", "{0}", "Sorcery", null, LsvCards.ManifestDread),
			me
		);
		var asked = CastAsking(dread, c => c.Options.Any(o => o.Id == mox) ? [mox] : Min(c));
		Assert.That(asked.OfType<YesNoChoice>(), Nu.Empty);
		Assert.That(Alive(mox) && s.GetCard(mox).HasComponent<FaceDown>());
	}

	[Test]
	public void MoxDiamond_ReturnedTapped_StaysTappedThroughTheQuestion()
	{
		s = TestGames.EmptyHands(s);
		var mox = Put(Lsv("Mox Diamond"), me, ZoneType.Graveyard);
		Hand(TestGames.Card("Forest"), me);
		CastAt(Hand(ReturnTapped, me), mox);
		Resolve(Yes);
		Assert.That(Alive(mox) && s.GetCard(mox).IsTapped);
	}

	[Test]
	public void MoxDiamond_ReturnedFromItsGraveyard_Declined_StaysTheSameObject()
	{
		s = TestGames.EmptyHands(s); // no land: it can't pay, unasked
		var mox = Put(Lsv("Mox Diamond"), me, ZoneType.Graveyard);
		var incarnation = s.GetCard(mox).Incarnation;
		CastAt(Hand(ReturnTapped, me), mox);
		Resolve();
		Assert.That(InGraveyard(mox));
		Assert.That(s.GetCard(mox).Incarnation, Nu.EqualTo(incarnation)); // never moved graveyard to graveyard
	}

	[TestCase(true)]
	[TestCase(false)]
	public void MultiversalPassage_ChooseABasicLandType_ThenPay2LifeOrEnterTapped(bool pay)
	{
		var id = Hand(Lsv("Multiversal Passage"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = id });
		var asked = ResolveAsking(c =>
			c switch
			{
				ChooseKeywordChoice => [2], // Island
				YesNoChoice => pay ? [1] : [],
				_ => Min(c),
			}
		);
		Assert.That(asked[0], Nu.InstanceOf<ChooseKeywordChoice>()); // the type first, then the life
		Assert.That(asked[1], Nu.InstanceOf<YesNoChoice>());
		Assert.That(s.GetCard(id).IsTapped, Nu.EqualTo(!pay));
		Assert.That(Life(me), Nu.EqualTo(pay ? 18 : 20));
		Assert.That(s.HasSubtype(s.GetCard(id), "Island"));
		Assert.That(
			ManaAbilities.ColorsOf(s, s.GetCard(id)),
			Nu.EquivalentTo(new[] { ManaColor.Blue })
		);
	}

	// ===== l4b: skipping a turn =====

	[Test]
	public void TimeVault_EntersTapped_AndDoesntUntapItself()
	{
		var vault = Hand(Lsv("Time Vault"), me);
		Cast(vault, []);
		Assert.That(s.GetCard(vault).IsTapped);
		PassTheTurn();
		PassTheTurn();
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me));
		Assert.That(s.GetCard(vault).IsTapped);
	}

	[TestCase(true)]
	[TestCase(false)]
	public void TimeVault_TapForAnExtraTurn_ThenSkipThatTurnToUntapIt(bool skip)
	{
		var vault = Field(Lsv("Time Vault"), me);
		var turn = s.GetGame().TurnNumber;
		Activate(vault);
		Resolve();
		Assert.That(s.GetCard(vault).IsTapped);
		ToTheVaultsQuestion(skip);
		// skipped: the extra turn never began (not counted) and theirs is next; else my extra turn, the Vault still tapped
		Assert.That(s.GetGame().TurnNumber, Nu.EqualTo(turn + 1));
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(skip ? them : me));
		Assert.That(s.GetCard(vault).IsTapped, Nu.EqualTo(!skip));
	}

	[TestCase("Voltaic Key")]
	[TestCase("Manifold Key")]
	public void TimeVault_WithAKey_ATurnAfterEveryTurn_EndsWithTheLibrary(string untapper)
	{
		var vault = Field(Lsv("Time Vault"), me);
		var key = Field(Lsv(untapper), me);
		foreach (var card in Library(me).Skip(3).ToList()) // three draws left
			s = s.MoveCard(card, s.ZoneId(me, ZoneType.Exile));
		for (var turns = 0; turns < 10 && !s.IsOver(); turns++)
		{
			Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me)); // they never get a turn
			if (s.GetCard(vault).IsTapped)
			{
				Activate(key, vault);
				Resolve();
			}
			Activate(vault);
			Resolve();
			PassTheTurn();
			s = TestGames.PassUntil(s, x => x.IsOver() || x.GetGame().Step == Step.PreCombatMain);
		}
		Assert.That(s.IsOver());
		Assert.That(s.WinnerId(), Nu.EqualTo(them)); // decked
	}

	// ===== l4b: beginning the game on the battlefield =====

	[TestCase(true)]
	[TestCase(false)]
	public void GemstoneCaverns_NotStarting_BeginWithItOutAndALuckCounter_ExilingACard(bool begin)
	{
		s = TestGames.New();
		var first = s.GetGame().StartingPlayerId;
		var second = s.OpponentOf(first);
		s = s.Act(s.LegalActions().OfType<KeepHandAction>().Single());
		var caverns = Put(Lsv("Gemstone Caverns"), second, ZoneType.Hand);
		s = s.Act(s.LegalActions().OfType<KeepHandAction>().Single());
		Assert.That(s.GetPendingChoice(), Nu.InstanceOf<YesNoChoice>());
		Assert.That(s.GetPendingChoiceDecidingPlayerId(), Nu.EqualTo(second));
		s = s.Choose(begin ? [1] : []);
		if (begin)
			s = s.Choose(Min(s.GetPendingChoice()!)); // the card to exile
		Assert.That(s.GetGame().TurnNumber, Nu.EqualTo(1));
		Assert.That(s.IsOnBattlefield(caverns), Nu.EqualTo(begin));
		Assert.That(s.CardsIn(second, ZoneType.Hand).Count(), Nu.EqualTo(begin ? 6 : 8));
		Assert.That(s.CardsIn(second, ZoneType.Exile).Count(), Nu.EqualTo(begin ? 1 : 0));
		if (!begin)
			return;
		Assert.That(s.GetCard(caverns).CountersOf(CounterKinds.Luck), Nu.EqualTo(1));
		Assert.That(
			ManaAbilities.ColorsOf(s, s.GetCard(caverns)),
			Nu.EquivalentTo(ManaAbilities.AnyColor)
		);
	}

	[Test]
	public void GemstoneCaverns_TheStartingPlayer_IsntAsked_AndItTapsForColourless()
	{
		s = TestGames.New();
		var first = s.GetGame().StartingPlayerId;
		var caverns = Put(Lsv("Gemstone Caverns"), first, ZoneType.Hand);
		s = s.Act(s.LegalActions().OfType<KeepHandAction>().Single());
		s = s.Act(s.LegalActions().OfType<KeepHandAction>().Single());
		Assert.That(s.GetPendingChoice(), Nu.Null);
		Assert.That(s.GetGame().TurnNumber, Nu.EqualTo(1));
		var played = Field(Lsv("Gemstone Caverns"), first);
		Assert.That(
			ManaAbilities.ColorsOf(s, s.GetCard(played)),
			Nu.EquivalentTo(new[] { ManaColor.Colorless })
		);
		Assert.That(s.IsIn(caverns, first, ZoneType.Hand));
	}

	// ===== l4b: exert, and mana that grants =====

	[TestCase(false)]
	[TestCase(true)]
	public void ArenaOfGlory_EntersTappedUnlessYouControlAMountain(bool mountain)
	{
		EmptyBoard();
		if (mountain)
			Field(TestGames.Card("Mountain"), me);
		var arena = Play("Arena of Glory");
		Resolve();
		Assert.That(s.GetCard(arena).IsTapped, Nu.EqualTo(!mountain));
	}

	[Test]
	public void ArenaOfGlory_Exerted_ItsManaGivesACreatureSpellHaste_AndItSkipsAnUntap()
	{
		EmptyBoard();
		var arena = Field(Lsv("Arena of Glory"), me);
		Field(TestGames.Card("Mountain"), me);
		var exert = s.LegalActions()
			.OfType<TapForManaAction>()
			.Single(a => a.CardId == arena && a.PaysItsCost);
		s = s.Act(exert);
		Assert.That(s.GetCard(arena).HasComponent<Exerted>());
		var goblin = Hand(Coloured("Goblin", "{R}{R}", "R"), me);
		Cast(goblin, []);
		Assert.That(Alive(goblin));
		Assert.That(s.Has(s.GetCard(goblin), "Haste"));
		PassTheTurn();
		PassTheTurn(); // my next untap step: exerted, it stays tapped
		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me));
		Assert.That(s.GetCard(arena).IsTapped);
		Assert.That(s.GetCard(arena).HasComponent<Exerted>(), Nu.False);
		PassTheTurn();
		PassTheTurn(); // and the one after, it untaps
		Assert.That(s.GetCard(arena).IsTapped, Nu.False);
	}

	[Test]
	public void ArenaOfGlory_ACopyOfTheHastySpell_GetsNoHaste()
	{
		EmptyBoard();
		var arena = Field(Lsv("Arena of Glory"), me);
		Field(TestGames.Card("Mountain"), me);
		Field(TestGames.Card("Plains"), me); // for the copy
		s = s.Act(
			s.LegalActions()
				.OfType<TapForManaAction>()
				.Single(a => a.CardId == arena && a.PaysItsCost)
		);
		var goblin = Hand(Coloured("Goblin", "{R}{R}", "R"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = goblin });
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [goblin],
			}
		);
		Resolve();
		var copy = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Goblin" && c.IsToken);
		Assert.That(s.Has(s.GetCard(goblin), "Haste"));
		Assert.That(s.Has(copy, "Haste"), Nu.False);
	}

	[Test]
	public void PaintersServant_APaintedCreatureConvokesItsColour()
	{
		EmptyBoard();
		var painter = Field(Lsv("Painter's Servant"), me);
		s = s.UpdateObject(
			painter,
			(Card)s.GetCard(painter).WithComponent(new ChosenColor(ManaColor.White))
		); // TEST SETUP: white chosen
		var saint = Hand(
			new CardDefinition(
				new CardData(
					"Saint",
					"normal",
					"{W}",
					1,
					"Sorcery",
					null,
					null,
					null,
					null,
					["W"],
					["Convoke"],
					null
				),
				[new SpellEffectsComponent([new GainLife(Ref.You, 1)])]
			),
			me
		);
		Cast(saint, []);
		Assert.That(Life(me), Nu.EqualTo(21)); // the white Servant tapped for its {W}
		Assert.That(s.GetCard(painter).IsTapped);
	}

	[Test]
	public void ArenaOfGlory_ItsManaOnANoncreatureSpell_GivesNothing()
	{
		EmptyBoard();
		var arena = Field(Lsv("Arena of Glory"), me);
		Field(TestGames.Card("Mountain"), me);
		s = s.Act(
			s.LegalActions()
				.OfType<TapForManaAction>()
				.Single(a => a.CardId == arena && a.PaysItsCost)
		);
		var relic = Hand(Coloured("Relic", "{R}{R}", "R", "Artifact"), me);
		Cast(relic, []);
		Assert.That(Alive(relic));
		Assert.That(s.Has(s.GetCard(relic), "Haste"), Nu.False);
		Assert.That(s.GetCard(relic).GetComponent<OnStackComponent>(), Nu.Null);
	}

	[Test]
	public void ArenaOfGlory_WithoutExert_ItsPlainRedGivesNothing()
	{
		EmptyBoard();
		Field(Lsv("Arena of Glory"), me);
		Field(TestGames.Card("Mountain"), me);
		var goblin = Hand(Coloured("Goblin", "{R}{R}", "R"), me);
		Cast(goblin, []);
		Assert.That(Alive(goblin));
		Assert.That(s.Has(s.GetCard(goblin), "Haste"), Nu.False);
	}

	// ===== l5: Lightning Greaves — the search's free abilities, once a turn =====

	IEnumerable<ActivateAbilityAction> SearchEquips(int greaves) =>
		s.SearchMoves().OfType<ActivateAbilityAction>().Where(a => a.CardId == greaves);

	[Test]
	public void LightningGreaves_EquippedCreatureHasHasteAndShroud_ForEquip0()
	{
		var greaves = Field(Lsv("Lightning Greaves"), me);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		Cast(bear, []);
		Assert.That(s.Has(s.GetCard(bear), "Haste"), Nu.False);
		var lands = UntappedLands();
		Activate(greaves, bear);
		Resolve();
		Assert.That(UntappedLands(), Nu.EqualTo(lands)); // {0}
		Assert.That(s.Has(s.GetCard(bear), "Haste") && s.Has(s.GetCard(bear), "Shroud"));
	}

	[Test]
	public void LightningGreaves_TheSearchIsOfferedItsEquipOnceATurn_TheRulesAlways()
	{
		var greaves = Field(Lsv("Lightning Greaves"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var ogre = Field(TestGames.Creature("Ogre", "4/4"), me);
		Assert.That(
			SearchEquips(greaves).Select(a => a.Targets[0]),
			Nu.EquivalentTo(new[] { bear, ogre })
		);
		Activate(greaves, bear);
		Resolve();
		Assert.That(SearchEquips(greaves), Nu.Empty); // used this turn
		Assert.That(Activations(greaves).Any(a => a.Targets.SequenceEqual([ogre]))); // still legal
		PassTheTurn();
		PassTheTurn();
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		Assert.That(SearchEquips(greaves), Nu.Not.Empty); // my next turn: offered again
	}

	[Test]
	public void LightningGreaves_TheSearchAIsTurnEnds()
	{
		s = TestGames.EmptyHands(s);
		Field(Lsv("Lightning Greaves"), me);
		Field(TestGames.Creature("Bear", "2/2"), me);
		Field(TestGames.Creature("Ogre", "4/4"), me);
		var turn = s.GetGame().TurnNumber;
		var decisions = 0;
		for (; decisions < 40 && s.GetGame().TurnNumber == turn && !s.IsOver(); decisions++)
		{
			s = s.AutoPass();
			if (s.GetGame().TurnNumber != turn)
				break;
			var d = new global::MagicCore.Ai.BeamSearchAi(s.DecidingPlayerId()).Decide(s);
			s = d.State;
			TestContext.Out.WriteLine(d.What);
		}
		Assert.That(
			s.GetGame().TurnNumber,
			Nu.GreaterThan(turn),
			$"still turn {turn} after {decisions} decisions"
		);
	}
}
