using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g, LSVPower — blue's tier 3: each card's ability FIRES, against its PRINTED text, through real casting and
/// the real stack. g1: protection (CR 702.16) — True-Name Nemesis.
/// </summary>
public class LsvBatch2gTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>"{1} sorcery: 2 damage to each creature" — damage that targets nothing.</summary>
	static CardDefinition Pyroclasm =>
		new(
			TestGames.Inline("Pyroclasm", "{1}", "Sorcery").Data,
			[new SpellEffectsComponent([new DealDamage(Ref.AllCreatures, 2)])]
		);

	/// <summary>I cast True-Name Nemesis and choose a player: "An opponent" (option 1) or "You" (option 2).</summary>
	int Nemesis(bool opponent)
	{
		var nemesis = Hand(Lsv("True-Name Nemesis"), me);
		var asked = CastAsking(nemesis, On<ChooseKeywordChoice>(opponent ? 1 : 2));
		Assert.That(asked.OfType<ChooseKeywordChoice>().Count(), Nu.EqualTo(1), "choose a player");
		Assert.That(
			s.GetCard(nemesis).GetComponent<ChosenPlayer>()?.PlayerId,
			Nu.EqualTo(opponent ? them : me)
		);
		return nemesis;
	}

	[Test]
	public void TrueNameNemesis_TheChosenPlayersSpellsCantTargetIt()
	{
		var nemesis = Nemesis(opponent: true);
		var theirs = Hand(TestCards.Kill(), them);
		var mine = Hand(TestCards.Kill(), me);

		Assert.That(
			TargetSpec.Creature.IsLegal(s, them, theirs, nemesis),
			Nu.False,
			"protection from them: not a legal target for their spell"
		);
		Assert.That(TargetSpec.Creature.IsLegal(s, me, mine, nemesis), "my own spell still can");
		Assert.That(
			TargetSpec.Creature.IsLegal(s, me, theirs, nemesis),
			"their card cast by ME (Robber of the Rich) is my spell — still in their hand as it is cast"
		);
	}

	[Test]
	public void TrueNameNemesis_DamageFromTheChosenPlayersSourcesIsPrevented()
	{
		var nemesis = Nemesis(opponent: false); // protection from ME: my own Pyroclasm
		var bear = Field(Bear, me);

		Cast(Hand(Pyroclasm, me), []);

		Assert.That(s.GetCard(nemesis).Damage, Nu.EqualTo(0), "no damage marked");
		Assert.That(Alive(nemesis));
		Assert.That(Alive(bear), Nu.False, "a 2/2 without protection dies");
	}

	[Test]
	public void TrueNameNemesis_CantBeBlockedByTheChosenPlayersCreatures()
	{
		var nemesis = Nemesis(opponent: true);
		var other = Field(Bear, me);
		var blocker = Field(Bear, them);

		Assert.That(
			Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(nemesis)),
			Nu.False,
			"their creature can't block it"
		);
		Assert.That(Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(other)), "another, it can");
	}

	/// <summary>TEST SETUP: attached as they were before it gained protection (Attach itself refuses).</summary>
	void AttachedTo(int attachment, int host) =>
		s = s.UpdateObject(
			attachment,
			s.GetCard(attachment) with
			{
				AttachedToId = host,
				AttachedToIncarnation = s.GetCard(host).Incarnation,
			}
		);

	[Test]
	public void TrueNameNemesis_TheChosenPlayersAurasAndEquipmentFallOff()
	{
		var nemesis = Nemesis(opponent: false); // protection from ME: my own attachments
		var kitesail = Field(CardRegistry.Get("Medic's Kitesail"), me);
		var aura = Field(CardRegistry.Get("De-evolve"), me);
		AttachedTo(kitesail, nemesis);
		AttachedTo(aura, nemesis);

		s = StateBasedActions.Check(s);

		Assert.That(s.GetCard(kitesail).AttachedToId, Nu.EqualTo(0), "the Equipment falls off");
		Assert.That(InGraveyard(aura), "the Aura has no legal host: it dies");
		Assert.That(Alive(kitesail));
	}

	[Test]
	public void TrueNameNemesis_AnAttachToItDoesNothing()
	{
		var nemesis = Nemesis(opponent: false);
		var kitesail = Field(CardRegistry.Get("Medic's Kitesail"), me);
		var bear = Field(Bear, me);
		s = Attachments.Attach(s, kitesail, bear);

		s = Attachments.Attach(s, kitesail, nemesis); // Cori-Steel Cutter's "attach it to" — not targeting

		Assert.That(
			s.GetCard(kitesail).AttachedToId,
			Nu.EqualTo(bear),
			"CR 701.3b: it stays where it was"
		);
	}

	// ===== g1b: a copy makes its own "as this enters" choices (CR 707.5) =====

	[Test]
	public void PhyrexianMetamorph_AsACopyOfTrueNameNemesis_ChoosesAPlayer_AndHasProtection()
	{
		var original = Nemesis(opponent: true);
		var metamorph = Hand(Lsv("Phyrexian Metamorph"), me);

		var asked = CastAsking(
			metamorph,
			c =>
				c switch
				{
					ChooseCreatureToCopyChoice => [original],
					ChooseKeywordChoice => [1], // "An opponent"
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);

		Assert.That(s.GetCard(metamorph).Name, Nu.EqualTo("True-Name Nemesis"));
		Assert.That(
			asked.OfType<ChooseKeywordChoice>().Count(),
			Nu.EqualTo(1),
			"the copy chooses a player"
		);
		Assert.That(s.GetCard(metamorph).GetComponent<ChosenPlayer>()?.PlayerId, Nu.EqualTo(them));
		var kill = Hand(TestCards.Kill(), them);
		Assert.That(
			TargetSpec.Creature.IsLegal(s, them, kill, metamorph),
			Nu.False,
			"protection from them"
		);
	}

	[Test]
	public void FleshDuplicate_AsACopyOfKaiWinn_ChoosesAName()
	{
		var kai = Field(CardRegistry.Get("Kai Winn, Faithless Leader"), them);
		var duplicate = Hand(CardRegistry.Get("Flesh Duplicate"), me);

		var asked = CastAsking(
			duplicate,
			c =>
				c switch
				{
					ChooseCreatureToCopyChoice => [kai],
					_ => c.Options.Take(Math.Max(1, c.MinChoices)).Select(o => o.Id),
				}
		);

		Assert.That(s.GetCard(duplicate).Name, Nu.EqualTo("Kai Winn, Faithless Leader"));
		Assert.That(
			asked.OfType<ChooseKeywordChoice>().Count(c => c.NamesACard),
			Nu.EqualTo(1),
			"a name"
		);
		Assert.That(s.GetCard(duplicate).GetComponent<ChosenName>(), Nu.Not.Null);
	}

	// ===== g2: delve (CR 702.66) =====

	/// <summary>TEST SETUP: n bears in my graveyard.</summary>
	List<int> Graveyard(int n) =>
		[.. Enumerable.Range(0, n).Select(_ => Put(Bear, me, ZoneType.Graveyard))];

	/// <summary>The delve picks asked after a cast (ExileFromGraveyardCost.PickAfter).</summary>
	static int DelvePicks(List<ChoiceAction> asked) =>
		asked.OfType<ChooseAmongChoice>().Count(c => c.Prompt == "Exile from your graveyard");

	[Test]
	public void TreasureCruise_DelvePaysForGeneric_TheCardsPickedAfterTheCast()
	{
		var binned = Graveyard(5);
		var cruise = Hand(Lsv("Treasure Cruise"), me);
		Assert.That(
			Casts(cruise).Select(c => c.Delve),
			Nu.EquivalentTo(Enumerable.Range(0, 6)),
			"one cast per number exiled, up to the graveyard's five"
		);
		Assert.That(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = cruise,
				Delve = 6,
			}
				.ValidateAdd(s)
				.IsValid,
			Nu.False,
			"only five to exile"
		);
		var lands = UntappedLands();
		var hand = HandCount(me);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = cruise,
				Delve = 5,
			}
		);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(3), "{7}{U} less five: {2}{U}");
		var asked = ResolveAsking(c => c.Options.Take(c.MinChoices).Select(o => o.Id));

		Assert.That(DelvePicks(asked), Nu.EqualTo(5), "five cards picked, one at a time");
		Assert.That(binned.All(InExile), "the five exiled");
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 3), "Draw three cards");
	}

	[Test]
	public void TreasureCruise_DelvesNoMoreThanTheGenericInItsCost()
	{
		Graveyard(10);
		var cruise = Hand(Lsv("Treasure Cruise"), me);

		Assert.That(Casts(cruise).Max(c => c.Delve), Nu.EqualTo(7), "{7}: seven, not ten");
		Assert.That(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = cruise,
				Delve = 8,
			}
				.ValidateAdd(s)
				.IsValid,
			Nu.False,
			"never the {U}"
		);
	}

	[Test]
	public void DigThroughTime_TakesTwoOfSeven_TheRestOnTheBottom_KnownToMe()
	{
		Graveyard(6);
		var seven = Enumerable.Range(0, 7).Select(_ => OnTop(Bear)).Reverse().ToList(); // top first
		var dig = Hand(Lsv("Dig Through Time"), me);
		var lands = UntappedLands();
		var hand = HandCount(me);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = dig,
				Delve = 6,
			}
		);
		Assert.That(lands - UntappedLands(), Nu.EqualTo(2), "{6}{U}{U} less six: {U}{U}");
		var asked = ResolveAsking(c => c.Options.Take(c.MinChoices).Select(o => o.Id));

		Assert.That(DelvePicks(asked), Nu.EqualTo(6));
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 2), "Put two of them into your hand");
		var library = Library(me);
		var rest = seven.Where(id => s.IsIn(id, me, ZoneType.Library)).ToList();
		Assert.That(rest, Has.Count.EqualTo(5));
		Assert.That(library.TakeLast(5), Nu.EquivalentTo(rest), "the rest on the bottom");
		Assert.That(
			rest.Select(id => s.GetCard(id).KnownTo),
			Nu.All.EqualTo(Knowers.Owner),
			"in any order: I know where they went"
		);
	}

	/// <summary>The AI's delve picks are a rule, never searched (Shayne, 2026-11-17): the first card left in the graveyard.</summary>
	[Test]
	public void Delve_TheAiExilesTheFirstCardsInTheGraveyard()
	{
		var binned = Graveyard(4);
		var cruise = Hand(Lsv("Treasure Cruise"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = cruise,
				Delve = 2,
			}
		);
		var ai = new global::MagicCore.Ai.GreedyAi(me);

		foreach (var expected in binned.Take(2))
		{
			var choice = s.GetPendingChoice()!;
			Assert.That(choice, Nu.InstanceOf<ChooseAmongChoice>().With.Property("Delve").True);
			var pick = ai.Answer(s, choice).ToList();
			Assert.That(pick, Nu.EqualTo(new[] { expected }));
			s = s.Choose(pick);
		}
		Assert.That(binned.Skip(2).All(InGraveyard), "the rest stay");
	}

	/// <summary>
	/// The review: delve's bound is each cast's own total — an {X} counts (CR 702.66a), so the generator can't read the
	/// printed cost alone (it once offered no delve here: {X}{U} prints no generic).
	/// </summary>
	[Test]
	public void Delve_IsBoundedByTheCastsTotal_XIncluded()
	{
		Graveyard(5);
		var spell = Hand(
			TestGames.Inline("X Delver", "{X}{U}", "Sorcery", ["Delve"], new DrawCards(Ref.You, 1)),
			me
		);

		Assert.That(
			Casts(spell).Where(c => c.X == 3).Select(c => c.Delve),
			Nu.EquivalentTo(Enumerable.Range(0, 4)),
			"X = 3: delve 0 to 3"
		);
	}

	/// <summary>A big board: delve is one cast per number, never one per set of cards (C(20,7) = 77,520).</summary>
	[Test]
	public void Delve_OnABigBoard_IsOneCastPerNumber()
	{
		foreach (var _ in Enumerable.Range(0, 12))
		{
			Field(Bear, me);
			Field(TestCards.Trinket, me);
		}
		foreach (var _ in Enumerable.Range(0, 5))
			Hand(Bear, me);
		Graveyard(20);
		var cruise = Hand(Lsv("Treasure Cruise"), me);
		var dig = Hand(Lsv("Dig Through Time"), me);

		Assert.That(Casts(cruise).Count(), Nu.EqualTo(8), "delve 0 to 7");
		Assert.That(Casts(dig).Count(), Nu.EqualTo(7), "delve 0 to 6");
	}
}
