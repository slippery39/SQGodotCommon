using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Turns;
using MIs = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4m1 + m2a, LSVPower — multicolour's options, each against its printed text: "when you do" fixing the sacrificed
/// creature's power as it last was (Minsc &amp; Boo), a counter's exile (No More Lies), a lock judged as its player's
/// sorcery timing and sorceries with flash until your next turn (Teferi, Time Raveler), a modal spell's targets one word
/// at a time (Kolaghan's Command), X divided a target at a time (Fire Covenant), cascade (Bloodbraid Challenger), plot
/// (Pillage the Bog), escape's own mana and "unless it escaped" (Phlage), a "may" asked again and a shuffle under the top
/// (Lim-Dûl's Vault), an ability tax's whose (Zirda), and an emblem's play permission (Wrenn and Six).
/// </summary>
public class LsvBatch2mTests : FraBoardTest
{
	static IEnumerable<int> Min(ChoiceAction c) => c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>Every "you may" yes, a cast offered as it resolves taken; any other choice its minimum.</summary>
	static IEnumerable<int> Yes(ChoiceAction c) =>
		c switch
		{
			YesNoChoice => [1],
			ChooseCastChoice cast => [cast.Options[0].Id],
			_ => Min(c),
		};

	void Loyalty(int walker, int cost, Func<ChoiceAction, IEnumerable<int>>? answer = null)
	{
		var index = global::MagicCore
			.Actions.Loyalty.AbilitiesOf(s, s.GetCard(walker))
			.FindIndex(a => a.Cost == cost);
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = walker,
				AbilityIndex = index,
			}
		);
		Resolve(answer);
	}

	/// <summary>To my next turn's step, with priority — every choice its minimum on the way.</summary>
	void MyNext(Step step)
	{
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber > turn
				&& x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == step
				&& x.GetGame().PriorityPlayerId != 0
		);
	}

	bool CanCast(int player, int card) =>
		new CastSpellAction { PlayerId = player, CardId = card }
			.ValidateAdd(s)
			.IsValid;

	static CardDefinition Instant(string name) =>
		TestGames.Inline(name, "{0}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition Sorcery(string name) =>
		TestGames.Inline(name, "{0}", "Sorcery", null, new GainLife(Ref.You, 1));

	// ===== m1 =====

	[Test]
	public void MinscAndBoo_MinusTwo_SacrificingBoo_DealsAndDrawsItsPowerAsItLastWas()
	{
		var minsc = Hand(Lsv("Minsc & Boo, Timeless Heroes"), me);
		CastAsking(minsc, Yes);
		var boo = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Boo").Id;
		s = s.PutCounters(boo, CounterKinds.PlusOne, 3); // a 4/4 Hamster token
		var (life, hand) = (Life(them), HandCount(me));

		Loyalty(
			minsc,
			-2,
			c =>
				c switch
				{
					ChoosePermanentChoice => [boo],
					ChooseTargetChoice => [them],
					_ => Min(c),
				}
		);

		Assert.That(s.HasObject(boo), Nu.False, "the token is gone");
		Assert.That(Life(them), Nu.EqualTo(life - 4), "X damage — its power with its counters");
		Assert.That(HandCount(me), Nu.EqualTo(hand + 4), "a Hamster: draw X cards");
	}

	[Test]
	public void Otharri_FromTheGraveyard_TappingAnUntappedRebel_ReturnsTapped()
	{
		var otharri = Put(Lsv("Otharri, Suns' Glory"), me, ZoneType.Graveyard);
		var rebel = Field(TestGames.Creature("Rebel", "2/2"), me);
		s = s.UpdateObject(rebel, s.GetCard(rebel) with { Subtypes = ["Rebel"] });

		s = s.Act(Activations(otharri).Single());
		Resolve();

		Assert.That(s.GetCard(rebel).IsTapped, Nu.True, "tap an untapped Rebel you control");
		Assert.That(
			Alive(otharri) && s.GetCard(otharri).IsTapped,
			Nu.True,
			"onto the battlefield tapped"
		);
	}

	// ===== m2a =====

	[Test]
	public void ExpressiveIteration_OneToHand_OneExiledAndPlayable_OneOnTheBottom()
	{
		var bottom = OnTop(TestGames.Creature("Bear", "2/2"));
		var exiled = OnTop(TestGames.Creature("Ogre", "3/3"));
		var taken = OnTop(TestGames.Creature("Giant", "4/4"));
		var iteration = Hand(Lsv("Expressive Iteration"), me);

		CastAsking(
			iteration,
			c =>
				c switch
				{
					ChooseAmongChoice when c.Options.Count == 3 => [taken],
					ChooseAmongChoice => [exiled],
					_ => Min(c),
				}
		);

		Assert.That(s.IsIn(taken, me, ZoneType.Hand), Nu.True);
		Assert.That(InExile(exiled) && Casts(exiled).Any(), Nu.True, "you may play it this turn");
		Assert.That(Library(me).Last(), Nu.EqualTo(bottom));
	}

	[Test]
	public void NoMoreLies_ExilesTheSpellItCounters()
	{
		var spell = TheirSpellOnTheStack(Instant("Hope"));
		var lies = Hand(Lsv("No More Lies"), me);

		Cast(lies, [], spell); // they decline to pay {3}

		Assert.That(InExile(spell), Nu.True, "exiled instead of put into its owner's graveyard");
	}

	[Test]
	public void TeferiTimeRaveler_OpponentsCastOnlyAtSorcerySpeed()
	{
		var teferi = Hand(Lsv("Teferi, Time Raveler"), me);
		Cast(teferi, []); // cast: put onto the battlefield it has no loyalty
		var theirs = Hand(Instant("Hope"), them);
		s = s.Act(new PassPriorityAction { PlayerId = me }); // my main phase: they hold priority

		Assert.That(CanCast(them, theirs), Nu.False, "not on my turn");

		MyNext(Step.Upkeep);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == them
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.GetGame().PriorityPlayerId == them
		);
		Assert.That(
			CanCast(them, theirs),
			Nu.True,
			"their main phase, the stack empty: a sorcery's timing"
		);
	}

	[Test]
	public void TeferiTimeRaveler_PlusOne_YourSorceriesHaveFlash_UntilYourNextTurn()
	{
		var teferi = Hand(Lsv("Teferi, Time Raveler"), me);
		Cast(teferi, []);
		var sorcery = Hand(Sorcery("Plan"), me);
		var held = Hand(Instant("Hope"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = held }); // I hold priority, the stack not empty

		Assert.That(CanCast(me, sorcery), Nu.False, "a sorcery, before the +1");

		Resolve();
		Loyalty(teferi, 1);
		var again = Hand(Instant("Again"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = again });

		Assert.That(CanCast(me, sorcery), Nu.True, "as though it had flash");
	}

	[Test]
	public void KolaghansCommand_ChooseTwo_OneCastPerPairOfModes_ItsTargetsWordByWord()
	{
		var artifact = Field(TestGames.Inline("Rock", "{1}", "Artifact"), them);
		foreach (var i in Enumerable.Range(0, 6))
			Field(TestGames.Creature($"Bear {i}", "2/2"), them);
		Put(TestGames.Creature("Ogre", "3/3"), me, ZoneType.Graveyard);
		var command = Hand(Lsv("Kolaghan's Command"), me);
		var life = Life(them);

		Assert.That(Casts(command).Count(), Nu.EqualTo(6), "one cast per two of the four modes");

		s = s.Act(Casts(command).Single(c => c.Modes.SequenceEqual([2, 3])));
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice t => [t.Index == 0 ? artifact : them],
				_ => Min(c),
			}
		);

		Assert.That(InGraveyard(artifact), Nu.True, "destroy target artifact");
		Assert.That(Life(them), Nu.EqualTo(life - 2), "2 damage to any target");
	}

	[Test]
	public void FireCovenant_XDividedAmongCreatures_EachShareAskedInTurn()
	{
		var a = Field(TestGames.Creature("Bear", "2/2"), them);
		var b = Field(TestGames.Creature("Ogre", "3/3"), them);
		var c = Field(TestGames.Creature("Giant", "4/4"), them);
		var covenant = Hand(Lsv("Fire Covenant"), me);
		var life = Life(me);

		s = s.Act(Casts(covenant).Single(x => x.X == 9));
		var asked = ResolveAsking(choice =>
			choice switch
			{
				ChooseXTargetsChoice => [a, b, c],
				ChooseNumberChoice n => [n.Options.Count == 7 ? 3 : 4], // 2 to the Bear, then 3 to the Ogre
				_ => Min(choice),
			}
		);

		Assert.That(Life(me), Nu.EqualTo(life - 9), "pay X life");
		Assert.That(
			asked.OfType<ChooseNumberChoice>().Count(),
			Nu.EqualTo(2),
			"a share per target but the last"
		);
		Assert.That(
			asked.OfType<ChooseNumberChoice>().First().Options.Select(o => o.Id - 1),
			Nu.EqualTo(Enumerable.Range(1, 7)),
			"one to what leaves the other two one each"
		);
		Assert.That(new[] { a, b, c }.All(InGraveyard), Nu.True, "2, 3 and the rest, 4");
	}

	[Test]
	public void BloodbraidChallenger_Cascades_PastALandAndABiggerSpell_IntoACheaperOneFree()
	{
		var cheap = OnTop(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"));
		var big = OnTop(TestGames.CreatureCosting("{5}{G}", "Wurm", "6/6"));
		var land = OnTop(TestGames.Card("Forest"));
		var challenger = Hand(Lsv("Bloodbraid Challenger"), me);

		CastAsking(challenger, Yes);

		Assert.That(Alive(cheap), Nu.True, "mana value 2, less than 5: cast free");
		Assert.That(
			Library(me).TakeLast(2),
			Nu.EquivalentTo(new[] { big, land }),
			"the rest on the bottom"
		);
	}

	[Test]
	public void PillageTheBog_Plot_ExiledNow_CastFreeOnALaterTurn()
	{
		var bog = Hand(Lsv("Pillage the Bog"), me);
		s = s.Act(Activations(bog).Single());
		Resolve();

		Assert.That(InExile(bog), Nu.True, "plotted");
		Assert.That(Casts(bog), Nu.Empty, "not this turn");

		MyNext(Step.PreCombatMain);

		Assert.That(Casts(bog).Any(c => c.WithoutPayingManaCost), Nu.True, "a later turn, free");
	}

	[Test]
	public void Phlage_CastIsSacrificed_EscapedForItsOwnCostStays()
	{
		var phlage = Hand(Lsv("Phlage, Titan of Fire's Fury"), me);
		var (mine, theirs) = (Life(me), Life(them));
		CastAsking(phlage, Targeting(them, yes: false));

		Assert.That(InGraveyard(phlage), Nu.True, "sacrificed: it didn't escape");
		Assert.That(
			(Life(me), Life(them)),
			Nu.EqualTo((mine + 3, theirs - 3)),
			"its enter trigger still"
		);

		foreach (var i in Enumerable.Range(0, 5))
			Put(TestGames.Creature($"Bear {i}", "2/2"), me, ZoneType.Graveyard);
		var lands = UntappedLands();
		CastAsking(phlage, Targeting(them, yes: false));

		Assert.That(Alive(phlage), Nu.True, "escaped: it stays");
		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(4),
			"escape's {R}{R}{W}{W}, not its {1}{R}{W}"
		);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard), Nu.Empty, "five other cards exiled");
	}

	[Test]
	public void Phlage_CastByAGraveyardMark_PaysItsManaCost_NotEscapes()
	{
		var phlage = Put(Lsv("Phlage, Titan of Fire's Fury"), me, ZoneType.Graveyard);
		var emry = Hand(
			TestGames.Inline(
				"Mark",
				"{0}",
				"Instant",
				null,
				new MakePlayable(
					Ref.All(ZoneType.Graveyard, MIs.Creature, Ref.You),
					new PlayPermission { ThisTurn = true }
				)
			),
			me
		);
		Cast(emry, []);
		var lands = UntappedLands();

		CastAsking(phlage, Targeting(them, yes: false));

		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(3),
			"its {1}{R}{W}: a mark, not its escape"
		);
		Assert.That(InGraveyard(phlage), Nu.True, "it didn't escape: sacrificed");
	}

	[Test]
	public void LimDulsVault_PayALifeToLookAgain_TheLastFiveStayOnTop()
	{
		var cards = Enumerable
			.Range(0, 10)
			.Select(i => OnTop(TestGames.Creature($"Card {i}", "1/1")))
			.ToList(); // the top is Card 9
		var vault = Hand(Lsv("Lim-Dûl's Vault"), me);
		var life = Life(me);
		var yeses = 0;

		CastAsking(
			vault,
			c =>
				c switch
				{
					YesNoChoice => yeses++ == 0 ? [1] : [],
					_ => Min(c),
				}
		);

		Assert.That(yeses, Nu.EqualTo(2), "asked again after the first look, until no");
		Assert.That(Life(me), Nu.EqualTo(life - 1));
		Assert.That(
			Library(me).Take(5),
			Nu.EquivalentTo(cards.Take(5)),
			"the second five, kept on top"
		);
	}

	[Test]
	public void Zirda_AbilitiesYouActivate_CostTwoLess_NeverBelowOne_NotYourOpponents()
	{
		var ability = new ActivatedAbilityComponent(
			ManaCost.Parse("{3}"),
			[new GainLife(Ref.You, 1)]
		);
		var rock = new CardDefinition(TestGames.Inline("Rock", "{1}", "Artifact").Data, [ability]);
		var mine = Field(rock, me);
		var theirs = Field(rock, them);
		Field(Lsv("Zirda, the Dawnwaker"), me);

		Assert.That(
			Costs.OfAbility(s, ability, [], me, mine).ManaValue,
			Nu.EqualTo(1),
			"{3} less 2"
		);
		Assert.That(
			Costs.OfAbility(s, ability, [], them, theirs).ManaValue,
			Nu.EqualTo(3),
			"theirs"
		);
		var cheap = ability with { Cost = ManaCost.Parse("{2}") };
		Assert.That(
			Costs.OfAbility(s, cheap, [], me, mine).ManaValue,
			Nu.EqualTo(1),
			"never below one mana"
		);
	}

	[Test]
	public void WrennAndSix_MinusSeven_TheEmblemRetracesInstantsAndSorceries_InLaterTurnsToo()
	{
		var wrenn = Hand(Lsv("Wrenn and Six"), me);
		Cast(wrenn, []);
		s = s.PutCounters(wrenn, CounterKinds.Loyalty, 4);
		var spell = Put(Instant("Hope"), me, ZoneType.Graveyard);
		Hand(TestGames.Card("Forest"), me);

		Assert.That(Casts(spell), Nu.Empty, "no retrace yet");

		Loyalty(wrenn, -7);
		MyNext(Step.PreCombatMain);

		Assert.That(
			Casts(spell).Any(c => c.DiscardId != 0),
			Nu.True,
			"discarding a land card, a turn later"
		);
	}
}
