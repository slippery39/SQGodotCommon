using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h2e, LSVPower — black's zones and combat: revolt (Fatal Push), "can't be blocked except by three or more"
/// (Troll of Khazad-dûm), shadow, an opponent's graveyard exiled with void counters and a free play from among them
/// (Dauthi Voidwalker), escape and "if it would leave the battlefield, exile it instead" (From the Catacombs).
/// </summary>
public class LsvBatch2h2eTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Costing(string cost) => TestGames.CreatureCosting(cost, "Costly", "3/3");

	static CardDefinition Bounce =>
		TestCards.Targeted(
			"Bounce",
			TargetSpec.Creature,
			new MoveTo(Ref.Target(), ZoneType.Hand) { From = [ZoneType.Battlefield] }
		);

	// ===== Fatal Push =====

	[Test]
	public void FatalPush_ManaValueTwoOrLess_NotThree()
	{
		var two = Field(Costing("{2}"), them);
		var three = Field(Costing("{3}"), them);
		Cast(Hand(Lsv("Fatal Push"), me), [], three);
		Assert.That(Alive(three), Nu.True, "no revolt: mana value 2 or less only");
		Cast(Hand(Lsv("Fatal Push"), me), [], two);
		Assert.That(Alive(two), Nu.False);
	}

	[Test]
	public void FatalPush_Revolt_FourOrLessOnceAPermanentOfYoursLeft()
	{
		var four = Field(Costing("{4}"), them);
		var five = Field(Costing("{5}"), them);
		var theirs = Field(Bear, them);
		Cast(Hand(TestCards.Kill(), me), [], theirs);
		Cast(Hand(Lsv("Fatal Push"), me), [], four);
		Assert.That(Alive(four), Nu.True, "their permanent left — not one under your control");
		var mine = Field(Bear, me);
		Cast(Hand(TestCards.Kill(), me), [], mine);
		Cast(Hand(Lsv("Fatal Push"), me), [], five);
		Assert.That(Alive(five), Nu.True, "revolt: 4 or less");
		Cast(Hand(Lsv("Fatal Push"), me), [], four);
		Assert.That(Alive(four), Nu.False);
	}

	// ===== Troll of Khazad-dûm =====

	void TrollAttacks(int troll, int blockers)
	{
		var bears = Enumerable.Range(0, blockers).Select(_ => Field(Bear, them)).ToList();
		AttackWith(
			c =>
				c switch
				{
					DeclareBlockerChoice b when bears.Contains(b.CreatureId) => [troll],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				},
			troll
		);
	}

	[Test]
	public void Troll_TwoBlockersAreDropped()
	{
		var troll = Field(Lsv("Troll of Khazad-dûm"), me);
		var life = Life(them);
		TrollAttacks(troll, 2);
		Assert.That(Life(them), Nu.EqualTo(life - 6), "two can't block it: unblocked");
	}

	[Test]
	public void Troll_ThreeBlockersBlock()
	{
		var troll = Field(Lsv("Troll of Khazad-dûm"), me);
		var life = Life(them);
		TrollAttacks(troll, 3);
		Assert.That(Life(them), Nu.EqualTo(life), "three or more block it");
	}

	// ===== Dauthi Voidwalker =====

	[Test]
	public void Dauthi_Shadow_BlocksAndIsBlockedOnlyByShadow()
	{
		var dauthiId = Field(Lsv("Dauthi Voidwalker"), me);
		var bearId = Field(Bear, them);
		var shadeId = Field(TestGames.Creature("Shade", "1/1", "Shadow"), them);
		var (dauthi, bear, shade) = (s.GetCard(dauthiId), s.GetCard(bearId), s.GetCard(shadeId));
		Assert.That(Combat.CanBlock(s, bear, dauthi), Nu.False, "can be blocked only by shadow");
		Assert.That(Combat.CanBlock(s, dauthi, bear), Nu.False, "can block only shadow");
		Assert.That(Combat.CanBlock(s, shade, dauthi), Nu.True);
		Assert.That(Combat.CanBlock(s, dauthi, shade), Nu.True);
	}

	[Test]
	public void Dauthi_TheirCardsAreExiledWithAVoidCounter_MineAreNot()
	{
		Field(Lsv("Dauthi Voidwalker"), me);
		var theirs = Field(Bear, them);
		var mine = Field(Bear, me);
		Cast(Hand(TestCards.Kill(), me), [], theirs);
		Cast(Hand(TestCards.Kill(), me), [], mine);
		Assert.That(InExile(theirs), Nu.True, "an opponent's graveyard: exiled instead");
		Assert.That(s.GetCard(theirs).CountersOf(CounterKinds.Void), Nu.EqualTo(1));
		Assert.That(InGraveyard(mine), Nu.True);
		var spell = TheirSpellOnTheStack(TestGames.Inline("Theirs", "{U}", "Instant"));
		Resolve();
		Assert.That(InExile(spell), Nu.True, "from anywhere: their resolved instant");
	}

	[Test]
	public void Dauthi_SacrificedPlaysAVoidCardFree()
	{
		var dauthi = Field(Lsv("Dauthi Voidwalker"), me);
		var bear = Field(Bear, them);
		var plain = Put(Bear, them, ZoneType.Exile); // exiled, no void counter
		Cast(Hand(TestCards.Kill(), me), [], bear);
		s = s.Act(Activations(dauthi).Single());
		var asked = ResolveAsking(On<ChooseAmongChoice>(bear));
		Assert.That(
			Offered<ChooseAmongChoice>(asked),
			Nu.EquivalentTo(new[] { bear }),
			"a void counter only"
		);
		Assert.That(InGraveyard(dauthi), Nu.True, "sacrificed");
		Assert.That(InExile(bear), Nu.True, "chosen, not moved");
		Assert.That(
			s.GetCard(bear).CountersOf(CounterKinds.Void),
			Nu.EqualTo(1),
			"the same object"
		);
		Assert.That(Casts(plain), Nu.Empty);
		var lands = UntappedLands();
		var cast = Casts(bear).Single();
		Assert.That(cast.WithoutPayingManaCost, Nu.True);
		s = s.Act(cast);
		Resolve();
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Id == bear), Nu.True);
		Assert.That(UntappedLands(), Nu.EqualTo(lands), "without paying its mana cost");
	}

	// ===== From the Catacombs =====

	int Catacombs(int target)
	{
		Cast(Hand(Lsv("From the Catacombs"), me), [], target);
		return target;
	}

	[Test]
	public void Catacombs_TheirCreatureMineWithACorpseCounterAndTheInitiative_ExiledAsItDies()
	{
		var bear = Catacombs(Put(Bear, them, ZoneType.Graveyard));
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Any(c => c.Id == bear),
			Nu.True,
			"under your control"
		);
		Assert.That(s.GetCard(bear).CountersOf(CounterKinds.Corpse), Nu.EqualTo(1));
		Assert.That(InitiativeComponent.HolderOf(s), Nu.EqualTo(me));
		Cast(Hand(TestCards.Kill(), me), [], bear);
		Assert.That(InExile(bear), Nu.True, "would leave: exiled instead");
	}

	[Test]
	public void Catacombs_ExiledInsteadOfReturningToHand()
	{
		var bear = Catacombs(Put(Bear, me, ZoneType.Graveyard));
		Cast(Hand(Bounce, me), [], bear);
		Assert.That(InExile(bear), Nu.True, "anywhere else: not its owner's hand");
	}

	[Test]
	public void Catacombs_Escape_FiveOtherCardsExiled_BackToTheGraveyard()
	{
		var catacombs = Put(Lsv("From the Catacombs"), me, ZoneType.Graveyard);
		var bear = Put(Bear, them, ZoneType.Graveyard);
		var others = Enumerable
			.Range(0, 4)
			.Select(_ => Put(TestCards.Fodder, me, ZoneType.Graveyard))
			.ToList();
		Assert.That(Casts(catacombs), Nu.Empty, "four other cards: no escape");
		others.Add(Put(TestCards.Fodder, me, ZoneType.Graveyard));
		s = s.Act(Casts(catacombs).Single(c => c.Targets.Contains(bear)));
		Resolve();
		Assert.That(others.All(InExile), Nu.True, "exile five other cards");
		Assert.That(Alive(bear), Nu.True);
		Assert.That(
			InGraveyard(catacombs),
			Nu.True,
			"escape isn't flashback: back to the graveyard"
		);
	}

	[Test]
	public void Catacombs_GrantedFlashback_PaysNoEscapeCost()
	{
		var catacombs = Put(Lsv("From the Catacombs"), me, ZoneType.Graveyard);
		Put(Bear, them, ZoneType.Graveyard);
		var card = s.GetCard(catacombs);
		s = s.UpdateObject(
			catacombs,
			card.WithComponent(new GrantedFlashback(s.GetGame().TurnNumber))
		);
		Assert.That(
			Casts(catacombs),
			Nu.Not.Empty,
			"flashback: no five other cards to exile (CR 118.9a)"
		);
	}
}
