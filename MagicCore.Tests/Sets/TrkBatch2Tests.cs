using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.2 Station and MC-5.3 batch 2 (the TRK commons and uncommons waiting on it): Station through real
/// activation (tier 3), and each tier-2 card's new piece against its PRINTED text. Tier-1 cards are
/// checked by their card-sheet entry (MagicCore/TrkCardSheet.md).
/// </summary>
public class TrkBatch2Tests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	void Activate(ActivateAbilityAction action)
	{
		s = s.Act(action);
		Resolve();
	}

	ActivateAbilityAction Station(int ship, int tapping) =>
		Activations(ship).Single(a => a.TapId == tapping);

	int Counters(int card) => s.GetCard(card).ChargeCounters;

	void SetCounters(int card, int n) =>
		s = s.UpdateObject(card, s.GetCard(card) with { ChargeCounters = n });

	void Pump(int card, int power) =>
		s = s.UpdateObject(card, (Card)s.GetCard(card).WithComponent(new UntilEndOfTurn(power, 0)));

	int UntappedLands(int p) =>
		s.CardsIn(p, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	// ===== Tier 3: Station (MC-5.2) =====

	[Test]
	public void Station_TapsTheCreatureYouChoose_CountersEqualItsPower_ACreatureAtSix()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var bear = Field(Bear, me);
		var giant = Field(TestGames.Creature("Giant", "4/4"), me);
		Field(TestGames.Creature("Theirs", "5/5"), them);
		Assert.That(s.GetCard(ship).Is(CardType.Creature), Is.False, "not a creature below 6+");

		Assert.That(
			Activations(ship).Select(a => a.TapId),
			Is.EquivalentTo(new[] { bear, giant }),
			"one activation per creature to tap — another creature YOU control"
		);
		Activate(Station(ship, giant));
		Assert.That(s.GetCard(giant).IsTapped, Is.True, "tapped as the cost");
		Assert.That(Counters(ship), Is.EqualTo(4), "charge counters equal to its power");
		Assert.That(s.GetCard(ship).Is(CardType.Creature), Is.False, "4 is below 6");
		Assert.That(s.Has(s.GetCard(ship), "Flying"), Is.False, "6+ | Flying — not yet");

		Activate(Station(ship, bear));
		var oberth = s.GetCard(ship);
		Assert.That(Counters(ship), Is.EqualTo(6));
		Assert.That(
			oberth.Is(CardType.Creature) && oberth.Is(CardType.Artifact),
			Is.True,
			"It's an artifact creature at 6+"
		);
		Assert.That((s.Power(oberth), s.Toughness(oberth)), Is.EqualTo((2, 2)), "its P/T box");
		Assert.That(s.Has(oberth, "Flying"), Is.True, "6+ | Flying");
		Assert.That(Activations(ship), Is.Empty, "never itself: ANOTHER creature");
	}

	[Test]
	public void Station_LosingCounters_LosesTheCreatureAndItsAbilities()
	{
		var ship = Field(Trk("Enterprise NX-01"), me);
		SetCounters(ship, 6);
		Assert.That(s.GetCard(ship).Is(CardType.Creature), Is.True);
		Assert.That(s.Has(s.GetCard(ship), "Vigilance"), Is.True, "6+ | Flying, vigilance");
		SetCounters(ship, 5);
		Assert.That(s.GetCard(ship).Is(CardType.Creature), Is.False);
		Assert.That(s.Has(s.GetCard(ship), "Flying"), Is.False);
	}

	[Test]
	public void Station_OnlyAsASorcery_AndSummoningSicknessDoesNotMatter()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var newcomer = Hand(Bear, me);
		s = s.EnterBattlefield(newcomer, me);
		Assert.That(Combat.IsSummoningSick(s, s.GetCard(newcomer)), Is.True);
		var bear = Field(Bear, me);
		Assert.That(
			Activations(ship).Select(a => a.TapId),
			Does.Contain(newcomer),
			"tapping for a cost isn't {T}"
		);

		s = s.Act(Station(ship, bear));
		Assert.That(
			Activations(ship),
			Is.Empty,
			"station only as a sorcery: not with it on the stack"
		);
	}

	[Test]
	public void Station_ItsPowerIsReadAsItResolves()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var bear = Field(Bear, me);
		s = s.Act(Station(ship, bear));
		Pump(bear, 3); // in response
		Resolve();
		Assert.That(Counters(ship), Is.EqualTo(5), "the power as the ability resolves");
	}

	[Test]
	public void Station_TheTappedCreatureGone_ItsLastKnownPower()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var bear = Field(Bear, me);
		s = s.UpdateObject(bear, s.GetCard(bear) with { PlusOneCounters = 2 }); // a 4/4
		s = s.Act(Station(ship, bear));
		s = s.MoveCard(bear, s.ZoneId(me, ZoneType.Graveyard)); // killed in response
		Resolve();
		Assert.That(Counters(ship), Is.EqualTo(4), "as it last was, not the 2/2 in the graveyard");
	}

	[Test]
	public void Station_NegativePower_PutsNoCountersAndRemovesNone()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		SetCounters(ship, 3);
		var bear = Field(Bear, me);
		Pump(bear, -5);
		Activate(Station(ship, bear));
		Assert.That(Counters(ship), Is.EqualTo(3));
	}

	[Test]
	public void Station_TheTappedCreatureIsInTheMoveKey_OnlyWhenThereIsOne()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		Field(Bear, me);
		Field(TestGames.Creature("Giant", "4/4"), me);
		var core = Field(Trk("Warp Core"), me);

		var keys = Activations(ship).Select(a => (string)s.MoveKey(a)).ToList();
		Assert.That(
			keys.Distinct().Count(),
			Is.EqualTo(2),
			"tapping a different creature is another move"
		);
		var charge = Activations(core).Single(a => a.Targets.Single() == ship);
		Assert.That((string)s.MoveKey(charge), Does.Not.Contain("|t"), "older keys unchanged");

		Activate(charge);
		Assert.That(
			Counters(ship),
			Is.EqualTo(2),
			"Warp Core: two charge counters on target Spacecraft"
		);
	}

	// AI-18 (Shayne, 2026-11-07: "identify and group permanents that are exactly the same"): Common Goal over 64
	// Troubling Tribbles was 14 910 casts and hung the TRK cast-gains run. the AI is offered one per kind of
	// permanent (AiMoves); DistinctMoves and LegalActions stay complete. A damaged Bear is not the same Bear - it stands alone.
	[TestCase(false)]
	[TestCase(true)]
	public void AiMoves_GroupsPermanentsThatAreExactlyTheSame(bool oneDamaged)
	{
		var goal = Hand(Trk("Common Goal"), me);
		var bears = Enumerable.Range(0, 12).Select(_ => Field(Bear, me)).ToList();
		Field(TestGames.Creature("Ogre", "3/3"), them);
		if (oneDamaged)
			s = s.UpdateObject(bears[0], s.GetCard(bears[0]) with { Damage = 1 }); // TEST SETUP

		var all = Casts(goal).Count();
		var distinct = s.AiMoves(s.LegalActions())
			.OfType<CastSpellAction>()
			.Count(a => a.CardId == goal);

		Assert.That(all, Is.GreaterThan(MagicRules.TwinsFrom), "a list long enough to group");
		// a Bear alone or two Bears: 2 casts; with the damaged one (D) too - D, a Bear, D and a Bear, two Bears: 4
		Assert.That(distinct, Is.EqualTo(oneDamaged ? 4 : 2));
	}

	[Test]
	public void Station_TheAiValuesProgressTowardItsThreshold()
	{
		var ship = Field(Trk("Oberth-Class Starship"), me);
		var scores = new[] { 0, 3, 5 }
			.Select(n =>
			{
				SetCounters(ship, n);
				return global::MagicCore.Ai.Evaluator.Evaluate(s, me);
			})
			.ToList();
		Assert.That(scores, Is.Ordered.Ascending);
		Assert.That(scores.Distinct().Count(), Is.EqualTo(3), "more counters, more value");
	}

	// ===== Tier 2: a tested piece with a new option =====

	[Test]
	public void Excelsior_DestroysOnlyATappedCreatureAnOpponentControls()
	{
		var tapped = Field(Bear, them);
		s = s.UpdateObject(tapped, s.GetCard(tapped) with { IsTapped = true });
		var untapped = Field(Bear, them);
		var mine = Field(Bear, me);
		s = s.UpdateObject(mine, s.GetCard(mine) with { IsTapped = true });
		var offered = new List<int>();
		var excelsior = Hand(Trk("U.S.S. Excelsior, Cutting Edge"), me);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = excelsior });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => offered = [.. choice.Options.Select(o => o.Id)],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			offered,
			Is.EqualTo(new[] { tapped }),
			"target TAPPED creature an opponent controls"
		);
		Assert.That(Alive(tapped), Is.False);
		Assert.That(Alive(untapped) && Alive(mine), Is.True);
	}

	[Test]
	public void Voyager_PutsTheFirstTwoLandsOntoTheBattlefieldTapped()
	{
		var library = s.ZoneId(me, ZoneType.Library);
		int Top(CardDefinition card)
		{
			int id;
			(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
			s = s.MoveObjectToFront(id, library);
			return id;
		}
		var forest = Top(TestGames.Card("Forest"));
		var fodder = Top(TestCards.Fodder);
		var island = Top(TestGames.Card("Island"));
		var trinket = Top(TestCards.Trinket); // the top: Trinket, Island, Fodder, Forest

		Cast(Hand(Trk("U.S.S. Voyager, Lone Wanderer"), me), []);
		Assert.That(
			new[] { island, forest }.All(l => Alive(l) && s.GetCard(l).IsTapped),
			Is.True,
			"two land cards, onto the battlefield tapped"
		);
		Assert.That(
			new[] { trinket, fodder }.All(c => s.IsIn(c, me, ZoneType.Library)),
			Is.True,
			"the rest back in the library"
		);
	}

	[Test]
	public void GuidanceFailure_CostsOneLess_AtAnAttacker_ItsOwnerChoosesTheBottom()
	{
		var raider = Field(TestGames.Creature("Raider", "3/3"), them);
		s = s.UpdateObject(raider, s.GetCard(raider) with { AttackingId = me });
		var spell = Hand(Trk("Guidance Failure"), me);
		var lands = UntappedLands(me);

		Cast(spell, [], raider); // its owner answers "no": the bottom
		Assert.That(
			lands - UntappedLands(me),
			Is.EqualTo(3),
			"{3}{U}, {1} less: it targets an attacker"
		);
		Assert.That(s.CardsIn(them, ZoneType.Library).Last().Id, Is.EqualTo(raider));
	}

	[Test]
	public void GuidanceFailure_FullCost_AtANonAttacker_ItsOwnerChoosesTheTop()
	{
		var bear = Field(Bear, them);
		var spell = Hand(Trk("Guidance Failure"), me);
		var lands = UntappedLands(me);
		var asked = 0;

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = spell,
				Targets = [bear],
			}
		);
		Resolve(choice =>
		{
			if (choice is not YesNoChoice)
				return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
			asked = s.DecidingPlayerId();
			return choice.Options.Select(o => o.Id); // "Put it on top?" — yes
		});
		Assert.That(asked, Is.EqualTo(them), "its OWNER chooses");
		Assert.That(lands - UntappedLands(me), Is.EqualTo(4));
		Assert.That(s.CardsIn(them, ZoneType.Library).First().Id, Is.EqualTo(bear));
	}

	[Test]
	public void StrayFromHistory_RepairTheTimeline_ExilesTheChosenCard()
	{
		s = TestGames.EmptyHands(s);
		var spell = Hand(TestCards.Fodder, them);
		var land = Hand(TestGames.Card("Plains"), them);

		Cast(Hand(Trk("Stray from History"), me), [1], them);
		Assert.That(
			s.IsIn(spell, them, ZoneType.Exile),
			Is.True,
			"you choose a nonland card — exile it"
		);
		Assert.That(s.IsIn(land, them, ZoneType.Hand), Is.True);
	}

	[Test]
	public void StrayFromHistory_RewriteThePast_ReturnsACreatureAndASpacecraft()
	{
		int ship,
			bear;
		(s, ship) = TestGames.Put(s, me, Trk("Oberth-Class Starship"), ZoneType.Graveyard);
		(s, bear) = TestGames.Put(s, me, Bear, ZoneType.Graveyard);

		Cast(Hand(Trk("Stray from History"), me), [0], ship, bear);
		Assert.That(
			new[] { ship, bear }.All(c => s.IsIn(c, me, ZoneType.Hand)),
			Is.True,
			"one or two target creature and/or Spacecraft cards"
		);
	}

	[Test]
	public void Sarcophagus_ChargesOnADeath_OnlyWhileItIsntACreature()
	{
		var sarcophagus = Field(Trk("Sarcophagus, Ship of the Dead"), me);
		var bear = Field(Bear, me);
		Assert.That(
			s.Power(s.GetCard(bear)),
			Is.EqualTo(3),
			"other creatures you control get +1/+0"
		);

		Cast(Hand(TestCards.Kill(), me), [], Field(Bear, them));
		Assert.That(Counters(sarcophagus), Is.EqualTo(1), "a creature died: a charge counter");

		SetCounters(sarcophagus, 10);
		Assert.That(s.GetCard(sarcophagus).Is(CardType.Creature), Is.True, "10+");
		Assert.That(s.Power(s.GetCard(sarcophagus)), Is.EqualTo(4), "OTHER creatures: not itself");
		Cast(Hand(TestCards.Kill(), me), [], Field(Bear, them));
		Assert.That(Counters(sarcophagus), Is.EqualTo(10), "if Sarcophagus isn't a creature");
	}
}
