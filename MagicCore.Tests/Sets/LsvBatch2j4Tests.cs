using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4j4–j6, LSVPower — white's last three pieces, FIRING against their printed text through real casting and the
/// real stack: ascend and the city's blessing (Ocelot Pride), reconfigure (Lion Sash), voting (Council's Judgment).
/// </summary>
public class LsvBatch2j4Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	int Named(int p, string name) => s.CardsIn(p, ZoneType.Battlefield).Count(c => c.Name == name);

	/// <summary>TEST SETUP: my lands to the graveyard but <paramref name="keep"/> of them.</summary>
	void KeepLands(int keep)
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.Skip(keep)
				.Select(c => c.Id)
				.ToList()
		)
			s = s.MoveCard(land, s.ZoneId(me, ZoneType.Graveyard));
	}

	void ToCleanup() => s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);

	// ===== Ocelot Pride =====

	[Test]
	public void OcelotPride_GainedLife_FewPermanents_OneCatAtMyEndStep()
	{
		KeepLands(3);
		Field(Lsv("Ocelot Pride"), me);
		s = Rules.Life.Gain(s, me, 1); // TEST SETUP

		ToCleanup();

		Assert.That(Named(me, "Cat"), Nu.EqualTo(1), "four permanents and the Cat: no blessing");
		Assert.That(s.GetPlayer(me).CitysBlessing, Nu.False);
	}

	[Test]
	public void OcelotPride_NoLifeGained_NoCat()
	{
		Field(Lsv("Ocelot Pride"), me);

		ToCleanup();

		Assert.That(Named(me, "Cat"), Nu.EqualTo(0));
	}

	[Test]
	public void OcelotPride_TenPermanents_TheCitysBlessing_EachTokenThatEnteredIsCopied()
	{
		Field(Lsv("Ocelot Pride"), me); // twenty lands and Ocelot
		s = Rules.Life.Gain(s, me, 1); // TEST SETUP

		ToCleanup();

		Assert.That(s.GetPlayer(me).CitysBlessing, "ascend: the state-based check gave it");
		Assert.That(Named(me, "Cat"), Nu.EqualTo(2), "the Cat, and a copy of it");
	}

	[Test]
	public void OcelotPride_TheCatMakesTen_TheBlessingInTimeForTheCopies()
	{
		KeepLands(8);
		Field(Lsv("Ocelot Pride"), me); // nine permanents
		s = Rules.Life.Gain(s, me, 1); // TEST SETUP

		ToCleanup();

		Assert.That(
			Named(me, "Cat"),
			Nu.EqualTo(2),
			"the Cat is the tenth: ascend applies at once"
		);
	}

	[Test]
	public void TheCitysBlessing_NeedsAPermanentWithAscend_AndLastsTheGame()
	{
		var bear = Field(Bear, me); // twenty lands and a Bear
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Assert.That(s.GetPlayer(me).CitysBlessing, Nu.False, "no ascend, no blessing");

		var ocelot = Field(Lsv("Ocelot Pride"), me);
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Assert.That(s.GetPlayer(me).CitysBlessing);

		s = s.MoveCard(ocelot, s.ZoneId(me, ZoneType.Graveyard));
		KeepLands(0);
		s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
		Assert.That(s.GetPlayer(me).CitysBlessing, "for the rest of the game");
		Assert.That(Alive(bear));
	}

	// ===== Lion Sash =====

	void Activate(int card, int ability, params int[] targets)
	{
		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = me,
				CardId = card,
				AbilityIndex = ability,
				Targets = [.. targets],
			}
		);
		Resolve();
	}

	[Test]
	public void LionSash_ExilesACardFromAGraveyard_ACounterOnlyForAPermanentCard()
	{
		var sash = Field(Lsv("Lion Sash"), me);
		var creature = Put(Bear, them, ZoneType.Graveyard);
		var instant = Put(TestCards.Kill(), them, ZoneType.Graveyard);

		Activate(sash, 0, instant);
		Assert.That(InExile(instant));
		Assert.That(
			s.GetCard(sash).PlusOneCounters,
			Nu.EqualTo(0),
			"an instant isn't a permanent card"
		);

		Activate(sash, 0, creature);
		Assert.That(InExile(creature));
		Assert.That(s.GetCard(sash).PlusOneCounters, Nu.EqualTo(1));
	}

	[Test]
	public void LionSash_Reconfigure_AttachedItIsNoCreature_TheHostGetsItsCounters_UnattachedACreatureAgain()
	{
		var sash = Field(Lsv("Lion Sash"), me);
		var bear = Field(Bear, me);
		s = s.PutCounters(sash, Effects.CounterKinds.PlusOne, 2); // TEST SETUP
		Assert.That(
			Activations(sash).Any(a => a.AbilityIndex == 2),
			Nu.False,
			"unattach only while attached"
		);

		Activate(sash, 1, bear);

		var card = s.GetCard(sash);
		Assert.That(
			card.AttachedToId,
			Nu.EqualTo(bear),
			"an Equipment creature stays on: reconfigure"
		);
		Assert.That(card.Is(CardType.Creature), Nu.False, "while attached, it isn't a creature");
		Assert.That(
			(s.Power(s.GetCard(bear)), s.Toughness(s.GetCard(bear))),
			Nu.EqualTo((4, 4)),
			"+1/+1 for each counter"
		);

		Activate(sash, 2);

		card = s.GetCard(sash);
		Assert.That(card.AttachedToId, Nu.EqualTo(0));
		Assert.That(card.Is(CardType.Creature), "unattached, a creature again");
		Assert.That(
			(s.Power(card), s.Toughness(card)),
			Nu.EqualTo((3, 3)),
			"a 1/1 with two counters"
		);
		Assert.That(s.Power(s.GetCard(bear)), Nu.EqualTo(2));
	}

	[Test]
	public void LionSash_ItsHostDies_ACreatureAgain()
	{
		var sash = Field(Lsv("Lion Sash"), me);
		var bear = Field(Bear, me);
		Activate(sash, 1, bear);

		Cast(Hand(TestCards.Kill(), me), [], bear);

		Assert.That(s.GetCard(sash).Is(CardType.Creature));
	}

	// ===== Council's Judgment =====

	Func<ChoiceAction, IEnumerable<int>> Votes(int mine, int theirs) =>
		c =>
			c switch
			{
				ChooseAmongChoice v when v.DeciderId == me => [mine],
				ChooseAmongChoice => [theirs],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	[Test]
	public void CouncilsJudgment_TheSameVote_OnlyThatOneExiled()
	{
		var first = Field(Bear, them);
		var second = Field(Bear, them);

		var asked = CastAsking(Hand(Lsv("Council's Judgment"), me), Votes(first, first));

		Assert.That(InExile(first) && Alive(second));
		Assert.That(asked.OfType<ChooseAmongChoice>().Count(), Nu.EqualTo(2), "each player votes");
	}

	[Test]
	public void CouncilsJudgment_ATie_BothExiled()
	{
		var first = Field(Bear, them);
		var second = Field(Bear, them);

		CastAsking(Hand(Lsv("Council's Judgment"), me), Votes(first, second));

		Assert.That(InExile(first) && InExile(second), "tied for most votes");
	}

	[Test]
	public void CouncilsJudgment_ANonlandPermanentYouDontControl_HexproofNoBar()
	{
		var hexproof = Field(TestGames.Creature("Hexer", "2/2", "Hexproof"), them);
		var mine = Field(Bear, me);
		var land = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Is(CardType.Land)).Id;

		var asked = CastAsking(Hand(Lsv("Council's Judgment"), me), Votes(hexproof, hexproof));

		var offered = asked.OfType<ChooseAmongChoice>().First().Options.Select(o => o.Id).ToList();
		Assert.That(offered, Nu.EqualTo(new[] { hexproof }), "no land, nothing of mine");
		Assert.That(InExile(hexproof), "a vote isn't a target");
		Assert.That(Alive(mine) && Alive(land));
	}
}
