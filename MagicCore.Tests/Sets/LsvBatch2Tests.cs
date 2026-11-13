using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4a, LSVPower's batch 2 — the costs batch 1 left out: each card's ability FIRES, against its PRINTED text,
/// through real casting; and each new option (an alternative cost's If and mana, "or" between two costs, an exile
/// from the hand that picks, a coloured replicate, a delayed trigger at the next main phase with a fixed X, "that
/// player" for a player target) with the case that tells it from its absence.
/// </summary>
public class LsvBatch2Tests : FraBoardTest
{
	static CardDefinition Lsv(string name) => CardRegistry.Get(name, "LSV");

	static CardDefinition Boon =>
		TestGames.Inline("Boon", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition BigBoon =>
		TestGames.Inline("Big Boon", "{3}", "Instant", null, new GainLife(Ref.You, 1));

	static int Untapped(GameState s, int player, string subtype) =>
		s.CardsIn(player, ZoneType.Battlefield)
			.Count(c => !c.IsTapped && c.Subtypes.Contains(subtype));

	/// <summary>Every land of mine tapped but these: what pays is only what is left.</summary>
	void TapMyLandsBut(params string[] keep)
	{
		var left = keep.ToList();
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			if (land.Subtypes.FirstOrDefault(left.Contains) is { } kept)
			{
				left.Remove(kept);
				s = s.Untap(land.Id);
			}
			else
				s = s.Tap(land.Id);
	}

	// ===== An alternative cost on the card: costs, an If, mana =====

	[Test]
	public void ForceOfWill_Free_PaysOneLife_AndExilesOnlyABlueCard()
	{
		var spell = TheirSpellOnTheStack(Boon);
		var force = Hand(Lsv("Force of Will"), me);
		var blue = Hand(Lsv("Counterspell"), me);
		var red = Hand(Lsv("Lightning Bolt"), me);
		TapMyLandsBut();
		var casts = Casts(force).ToList();
		Assert.That(
			casts.Select(c => c.ExileId),
			Nu.EquivalentTo(new[] { blue }),
			"a blue card only"
		);
		Assert.That(casts.All(c => c.WithoutPayingManaCost));
		s = s.Act(casts.Single());
		Resolve();
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "countered");
		Assert.That(s.IsIn(blue, me, ZoneType.Exile) && s.IsIn(red, me, ZoneType.Hand));
		Assert.That(Life(me), Nu.EqualTo(19), "pay 1 life");
	}

	[Test]
	public void ForceOfNegation_FreeOnlyOnTheirTurn_ExilesWhatItCounters()
	{
		var force = Hand(Lsv("Force of Negation"), me);
		Hand(Lsv("Counterspell"), me);
		Assert.That(
			MagicCore.Rules.Playable.FreeCast(s, s.GetCard(force), me),
			Nu.Null,
			"my turn: no free cast"
		);
		var theirTurn = s.WithGame(s.GetGame() with { ActivePlayerId = them });
		Assert.That(
			MagicCore.Rules.Playable.FreeCast(theirTurn, theirTurn.GetCard(force), me),
			Nu.Not.Null,
			"not my turn: free"
		);
		var spell = TheirSpellOnTheStack(Boon);
		s = s.Act(Casts(force).First(c => !c.WithoutPayingManaCost && c.Targets.Single() == spell));
		Resolve();
		Assert.That(s.IsIn(spell, them, ZoneType.Exile), "exiled instead of its graveyard");
	}

	[Test]
	public void Daze_Free_ReturnsAnIsland()
	{
		var spell = TheirSpellOnTheStack(Boon);
		var daze = Hand(Lsv("Daze"), me);
		TapMyLandsBut();
		var island = s.CardsIn(me, ZoneType.Battlefield)
			.First(c => c.Subtypes.Contains("Island"))
			.Id;
		var casts = Casts(daze).ToList();
		Assert.That(
			casts.All(c => s.GetCard(c.ReturnId).Subtypes.Contains("Island")),
			"only an Island"
		);
		s = s.Act(casts.First(c => c.ReturnId == island));
		Resolve(); // "unless its controller pays {1}": they don't
		Assert.That(s.IsIn(island, me, ZoneType.Hand), "returned to its owner's hand");
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "countered");
	}

	[Test]
	public void SnuffOut_FourLife_OnlyWithASwamp_DestroysOnlyNonblack()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var black = Field(
			TestGames.CreatureCosting("{B}", "Shade", "1/1") is var shade
				? shade with
				{
					Data = shade.Data with { Colors = ["B"] },
				}
				: null!,
			them
		);
		var snuff = Hand(Lsv("Snuff Out"), me);
		TapMyLandsBut();
		var casts = Casts(snuff).ToList();
		Assert.That(
			casts.Select(c => c.Targets.Single()),
			Nu.EquivalentTo(new[] { bear }),
			"nonblack"
		);
		s = s.Act(casts.Single());
		Resolve();
		Assert.That(Alive(bear), Nu.False);
		Assert.That(Alive(black));
		Assert.That(Life(me), Nu.EqualTo(16), "pay 4 life");

		foreach (
			var swamp in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Subtypes.Contains("Swamp"))
				.ToList()
		)
			s = s.MoveCard(swamp.Id, s.ZoneId(me, ZoneType.Graveyard));
		var another = Hand(Lsv("Snuff Out"), me);
		Field(TestGames.Creature("Bear", "2/2"), them);
		Assert.That(Casts(another), Nu.Empty, "no Swamp: no free cast, and no mana");
	}

	[Test]
	public void BalefulMastery_ForOneB_AnOpponentDraws_ForItsCost_TheyDont()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var mastery = Hand(Lsv("Baleful Mastery"), me);
		var casts = Casts(mastery).Where(c => c.Targets.Single() == bear).ToList();
		Assert.That(
			casts.Select(c => c.WithoutPayingManaCost),
			Nu.EquivalentTo(new[] { false, true })
		);
		Assert.That(
			casts.Select(c => Describe.Action(s, c)).Distinct().Count(),
			Nu.EqualTo(2),
			"the two casts read differently (the table groups plays by their text)"
		);
		var hand = HandCount(them);
		s = s.Act(casts.Single(c => !c.WithoutPayingManaCost));
		Resolve();
		Assert.That(s.IsIn(bear, them, ZoneType.Exile));
		Assert.That(HandCount(them), Nu.EqualTo(hand), "{3}{B} paid: nobody draws");

		var second = Field(TestGames.Creature("Bear", "2/2"), them);
		var cheap = Hand(Lsv("Baleful Mastery"), me);
		TapMyLandsBut("Swamp", "Mountain");
		var free = Casts(cheap).Single(c => c.Targets.Single() == second);
		Assert.That(free.WithoutPayingManaCost, "two lands left: only {1}{B}");
		s = s.Act(free);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).All(c => c.IsTapped),
			"{1}{B} paid: the Swamp and the Mountain"
		);
		Resolve();
		Assert.That(s.IsIn(second, them, ZoneType.Exile));
		Assert.That(HandCount(them), Nu.EqualTo(hand + 1), "{1}{B} paid: an opponent draws");
	}

	// ===== "Or" between two costs =====

	[Test]
	public void BitterTriumph_DiscardACard_OrPayThreeLife()
	{
		s = TestGames.EmptyHands(s);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var triumph = Hand(Lsv("Bitter Triumph"), me);
		var card = Hand(TestGames.Creature("Bear", "2/2"), me);
		var casts = Casts(triumph).Where(c => c.Targets.Single() == bear).ToList();
		Assert.That(casts.Select(c => c.DiscardId), Nu.EquivalentTo(new[] { 0, card }));
		s = s.Act(casts.Single(c => c.DiscardId == 0));
		Resolve();
		Assert.That(Alive(bear), Nu.False);
		Assert.That(Life(me), Nu.EqualTo(17), "pay 3 life");
		Assert.That(s.IsIn(card, me, ZoneType.Hand));

		var other = Field(TestGames.Creature("Bear", "2/2"), them);
		var second = Hand(Lsv("Bitter Triumph"), me);
		s = s.Act(Casts(second).Single(c => c.DiscardId == card && c.Targets.Single() == other));
		Resolve();
		Assert.That(Alive(other), Nu.False);
		Assert.That(Life(me), Nu.EqualTo(17), "discarded: no life");
		Assert.That(s.IsIn(card, me, ZoneType.Graveyard));
	}

	[Test]
	public void BoneShards_SacrificeACreature_OrDiscardACard_NeverBothOrNeither()
	{
		s = TestGames.EmptyHands(s);
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var mine = Field(TestGames.Creature("Squire", "1/1"), me);
		var shards = Hand(Lsv("Bone Shards"), me);
		var card = Hand(TestGames.Creature("Bear", "2/2"), me);
		var casts = Casts(shards).Where(c => c.Targets.Single() == bear).ToList();
		Assert.That(
			casts.Select(c => (c.SacrificeId, c.DiscardId)),
			Nu.EquivalentTo(new[] { (mine, 0), (0, card) })
		);
		s = s.Act(casts.Single(c => c.DiscardId == card));
		Resolve();
		Assert.That(Alive(bear), Nu.False);
		Assert.That(s.IsIn(card, me, ZoneType.Graveyard) && Alive(mine));
	}

	// ===== A coloured replicate =====

	[Test]
	public void LoseFocus_ReplicateCostsBlue()
	{
		TheirSpellOnTheStack(Boon);
		var focus = Hand(Lsv("Lose Focus"), me);
		TapMyLandsBut("Island", "Mountain", "Mountain");
		Assert.That(
			Casts(focus).Max(c => c.Replicate),
			Nu.EqualTo(0),
			"{1}{U} + {U}: one Island isn't two"
		);
		Resolve();
		var spell = TheirSpellOnTheStack(Boon);
		var again = Hand(Lsv("Lose Focus"), me);
		TapMyLandsBut("Island", "Island", "Mountain");
		s = s.Act(Casts(again).Single(c => c.Replicate == 1 && c.Targets.Single() == spell));
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).All(c => !c.Is(CardType.Land) || c.IsTapped)
		);
		Resolve();
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "countered (they don't pay)");
	}

	// ===== Chain Lightning: "that player" may pay to copy =====

	[Test]
	public void ChainLightning_ThreeDamage_ThenThatPlayerMayPayRRToCopyIt()
	{
		var chain = Hand(Lsv("Chain Lightning"), me);
		var asked = new List<int>();
		IEnumerable<int> Answer(int player)
		{
			asked.Add(player);
			return asked.Count == 1 ? [1] : []; // yes: they pay and copy it, at me; then I don't
		}
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = chain,
				Targets = [them],
			}
		);
		Resolve(choice =>
			choice switch
			{
				YesNoChoice yes => Answer(yes.PlayerId),
				ChooseTargetChoice => [me],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			asked,
			Nu.EqualTo(new[] { them, me }),
			"the player dealt damage is asked, then the copy's"
		);
		Assert.That(Life(them), Nu.EqualTo(17));
		Assert.That(Life(me), Nu.EqualTo(17), "their copy hit me");
		Assert.That(Untapped(s, them, "Mountain"), Nu.EqualTo(2), "they paid {R}{R}");
	}

	// ===== Mana Drain: mana at your next main phase =====

	[Test]
	public void ManaDrain_CountersIt_ThenItsManaValueAsColorlessAtYourNextMainPhase()
	{
		var spell = TheirSpellOnTheStack(BigBoon);
		Cast(Hand(Lsv("Mana Drain"), me), [], spell);
		Assert.That(s.IsIn(spell, them, ZoneType.Graveyard), "countered");
		Assert.That(s.GetPlayer(me).ManaPool[ManaColor.Colorless], Nu.EqualTo(0), "not yet");
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Resolve();
		Assert.That(
			s.GetPlayer(me).ManaPool[ManaColor.Colorless],
			Nu.EqualTo(3),
			"my precombat main: the next main phase is this turn's second"
		);
	}

	[Test]
	public void ManaDrain_InYourUpkeep_TheManaComesAtThisTurnsFirstMainPhase()
	{
		s = s.WithGame(s.GetGame() with { Step = Step.Upkeep });
		var spell = TheirSpellOnTheStack(BigBoon);
		Cast(Hand(Lsv("Mana Drain"), me), [], spell);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PreCombatMain);
		Resolve();
		Assert.That(
			s.GetPlayer(me).ManaPool[ManaColor.Colorless],
			Nu.EqualTo(3),
			"the next main phase"
		);
	}
}
