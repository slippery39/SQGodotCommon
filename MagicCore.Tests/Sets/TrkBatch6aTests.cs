using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using Is = MagicCore.Filters.Is;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-5.3 batch 6a: TRK's shocklands (pay 2 life as it enters, or it enters tapped), Engineered
/// Explosives' sunburst, Dominion Infiltration's X targets and changeling, Nyota Uhura's number chosen
/// as she enters, and Mirror Universe Intrusion's kicked copies — through real casting and playing.
/// </summary>
public class TrkBatch6aTests : FraBoardTest
{
	static CardDefinition Trk(string name) => CardRegistry.Get(name, "TRK");

	static Func<ChoiceAction, IEnumerable<int>> Answering(bool yes) =>
		c =>
			c switch
			{
				YesNoChoice y => yes ? [y.Options[0].Id] : [],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};

	static IEnumerable<int> NeverAsked(ChoiceAction c) =>
		c is YesNoChoice ? throw new AssertionException($"asked: {c}") : [];

	int PlayShock(Func<ChoiceAction, IEnumerable<int>> answer)
	{
		var crypt = Hand(Trk("Blood Crypt"), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = crypt });
		Resolve(answer);
		return crypt;
	}

	[Test]
	public void Shockland_Paying2Life_EntersUntapped()
	{
		var life = Life(me);
		var crypt = PlayShock(Answering(yes: true));
		Assert.That(s.GetCard(crypt).IsTapped, Nu.False);
		Assert.That(Life(me), Nu.EqualTo(life - 2), "you may pay 2 life");
		Assert.That(s.GetPlayer(me).ThisTurn.LifeLost, Nu.EqualTo(2), "paying life is losing it");
	}

	[Test]
	public void Shockland_NotPaying_EntersTapped()
	{
		var life = Life(me);
		var crypt = PlayShock(Answering(yes: false));
		Assert.That(s.GetCard(crypt).IsTapped, Nu.True, "If you don't, it enters tapped.");
		Assert.That(Life(me), Nu.EqualTo(life));
	}

	[Test]
	public void Shockland_At1Life_CantPay_EntersTapped()
	{
		s = s.UpdateObject(me, s.GetPlayer(me) with { Life = 1 });
		var crypt = PlayShock(NeverAsked);
		Assert.That(s.GetCard(crypt).IsTapped, Nu.True, "CR 119.4: 2 life can't be paid from 1");
		Assert.That(Life(me), Nu.EqualTo(1));
	}

	[Test]
	public void Shockland_UnderWarpSpeed_EntersUntapped_NotAsked()
	{
		Field(Trk("Warp Speed"), me);
		var life = Life(me);
		var crypt = PlayShock(NeverAsked);
		Assert.That(s.GetCard(crypt).IsTapped, Nu.False, "lands you control enter untapped");
		Assert.That(Life(me), Nu.EqualTo(life));
	}

	[Test]
	public void Shockland_UnderRootMaze_EntersTapped_NotAsked()
	{
		Field(CardRegistry.Get("Root Maze"), them);
		var crypt = PlayShock(NeverAsked);
		Assert.That(s.GetCard(crypt).IsTapped, Nu.True, "artifacts and lands enter tapped");
	}

	// ===== Sunburst: Engineered Explosives =====

	/// <summary>Taps every land of mine but these basics (one of each name given).</summary>
	void OnlyUntapped(params string[] names)
	{
		var left = names.ToList();
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			if (!left.Remove(land.Name))
				s = s.UpdateObject(land.Id, land with { IsTapped = true });
	}

	int CastExplosives(int x)
	{
		var ee = Hand(Trk("Engineered Explosives"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = ee,
				X = x,
			}
		);
		Resolve();
		return ee;
	}

	[Test]
	public void EngineeredExplosives_PaysXWithTheMostColours_AChargeCounterEach()
	{
		OnlyUntapped("Plains", "Plains", "Plains", "Island", "Swamp");
		var ee = CastExplosives(3);
		Assert.That(
			s.GetCard(ee).ChargeCounters,
			Nu.EqualTo(3),
			"a charge counter for each color of mana spent — W, U and B, not three Plains"
		);
	}

	[Test]
	public void EngineeredExplosives_OneColourOnly_OneCounter()
	{
		OnlyUntapped("Plains", "Plains");
		var ee = CastExplosives(2);
		Assert.That(s.GetCard(ee).ChargeCounters, Nu.EqualTo(1), "two white mana is one color");
	}

	[Test]
	public void EngineeredExplosives_NeverSacrificesATreasureForAColour()
	{
		OnlyUntapped("Plains", "Plains");
		var treasure = Field(new CardDefinition(Tokens.Treasure, [Tokens.SacrificeForMana(1)]), me);
		var ee = CastExplosives(2);
		Assert.That(s.GetCard(ee).ChargeCounters, Nu.EqualTo(1), "two Plains: one color");
		Assert.That(Alive(treasure), Nu.True, "the player picked X, not to spend the Treasure");
	}

	[Test]
	public void EngineeredExplosives_Copied_TheCopySpentNoMana()
	{
		var ee = Hand(Trk("Engineered Explosives"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = ee,
				X = 2,
			}
		);
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [ee],
			}
		);
		Resolve();
		var tokens = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.Name == "Engineered Explosives")
			.ToList();
		Assert.That(
			tokens.Select(c => c.ChargeCounters),
			Nu.EquivalentTo(new[] { 2, 0 }),
			"CR 702.44c"
		);
	}

	[Test]
	public void EngineeredExplosives_Sacrificed_DestroysEachNonlandOfItsCounters()
	{
		OnlyUntapped("Plains", "Island", "Swamp", "Mountain");
		var ee = CastExplosives(2); // W and U: two counters; Swamp and Mountain left for {2}
		var two = Field(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"), them);
		var mine = Field(TestGames.CreatureCosting("{G}{G}", "Cub", "2/2"), me);
		var three = Field(TestGames.CreatureCosting("{2}{G}", "Ape", "3/3"), them);
		var one = Field(TestGames.Creature("Elf", "1/1"), them);
		s = s.Act(Activations(ee).Single());
		Resolve();
		Assert.That(Alive(ee), Nu.False, "Sacrifice this artifact");
		Assert.That(
			Alive(two) || Alive(mine),
			Nu.False,
			"each nonland permanent with mana value 2"
		);
		Assert.That(Alive(three), Nu.True, "mana value 3 survives");
		Assert.That(Alive(one), Nu.True, "mana value 1 survives");
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land)),
			Nu.EqualTo(20),
			"nonland"
		);
	}

	// ===== X targets and changeling: Dominion Infiltration =====

	int CastInfiltration(int x, params int[] targets)
	{
		var di = Hand(Trk("Dominion Infiltration"), me);
		s = s.Act(Casts(di).Single(c => c.X == x));
		Resolve(c =>
			c switch
			{
				ChooseXTargetsChoice => targets,
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		return di;
	}

	[Test]
	public void DominionInfiltration_XTargets_OneCastPerX_TheTargetsChosenAsItIsCast()
	{
		Field(TestGames.Creature("A", "1/1"), them);
		Field(TestGames.Creature("B", "1/1"), them);
		Field(TestGames.Creature("C", "1/1"), them);
		Field(TestGames.Creature("Mine", "1/1"), me);
		var di = Hand(Trk("Dominion Infiltration"), me);
		var twos = Casts(di).Where(c => c.X == 2).ToList();
		Assert.That(twos, Has.Count.EqualTo(1), "one cast per X — never one per set (2^n)");
		Assert.That(twos[0].ChoosesTargets, Nu.True);
		Assert.That(
			Casts(di).Where(c => c.X == 4),
			Nu.Empty,
			"only three creatures an opponent controls"
		);
		s = s.Act(twos[0]);
		var choice = (ChooseXTargetsChoice)s.GetPendingChoice()!;
		Assert.That(choice.Options, Has.Count.EqualTo(3), "creatures your opponents control");
		Assert.That((choice.MinChoices, choice.MaxChoices), Nu.EqualTo((2, 2)), "exactly X");
		var a = s.CardsIn(them, ZoneType.Battlefield).First(c => c.Name == "A").Id;
		var repeat = new CastSpellAction
		{
			PlayerId = me,
			CardId = di,
			X = 3,
			Targets = [a, a + 1, a],
		};
		Assert.That(repeat.ValidateAdd(s).IsValid, Nu.False, "X targets are all different");
	}

	[Test]
	public void DominionInfiltration_CopiesEachTarget_ChangelingHaste_ExiledAtTheEndStep()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		var ape = Field(TestGames.Creature("Ape", "3/3"), them);
		Field(TestGames.Creature("Untouched", "1/1"), them);
		var di = CastInfiltration(2, bear, ape);
		Assert.That(s.IsIn(di, me, ZoneType.Graveyard), Nu.True, "cast and resolved");
		var tokens = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken).ToList();
		Assert.That(
			tokens.Select(c => c.Name),
			Nu.EquivalentTo(new[] { "Bear", "Ape" }),
			"for each of them"
		);
		var token = tokens[0];
		Assert.That(s.Has(token, "Haste"), Nu.True, "those tokens gain haste");
		var ctx = new global::MagicCore.Filters.FilterContext(me);
		Assert.That(
			Is.Subtype("Officer").Matches(s, token, ctx),
			Nu.True,
			"changeling: every creature type"
		);
		Assert.That(Is.Subtype("Borg").Matches(s, token, ctx), Nu.True);
		Assert.That(Is.Subtype("Plains").Matches(s, token, ctx), Nu.False, "not a land type");
		Assert.That(
			Is.Subtype("Officer").Matches(s, s.GetCard(bear), ctx),
			Nu.False,
			"the original isn't"
		);
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == turn + 1);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken),
			Nu.Empty,
			"exiled at the next end step"
		);
	}

	[Test]
	public void DominionInfiltration_ACopysNewTargets_NeverOneCreatureTwice()
	{
		var a = Field(TestGames.Creature("A", "1/1"), them);
		var b = Field(TestGames.Creature("B", "1/1"), them);
		var c = Field(TestGames.Creature("C", "1/1"), them);
		var di = Hand(Trk("Dominion Infiltration"), me);
		s = s.Act(Casts(di).Single(x => x.X == 3));
		s = s.Choose([a, b, c]);
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [di],
			}
		);
		Resolve(ch =>
			ch switch
			{
				YesNoChoice y => [y.Options[0].Id],
				ChooseTargetChoice t => [t.Options[0].Id], // the first offered, each word
				_ => ch.Options.Take(ch.MinChoices).Select(o => o.Id),
			}
		);
		var copies = s.CardsIn(me, ZoneType.Battlefield).Where(x => x.IsToken).GroupBy(x => x.Name);
		Assert.That(
			copies.ToDictionary(g => g.Key, g => g.Count()),
			Nu.EquivalentTo(
				new Dictionary<string, int>
				{
					["A"] = 2,
					["B"] = 2,
					["C"] = 2,
				}
			),
			"each word of the copy a different creature"
		);
	}

	[Test]
	public void DominionInfiltration_TheAi_CopiesTheBiggest()
	{
		Field(TestGames.Creature("Small", "1/1"), them);
		var big = Field(TestGames.Creature("Big", "5/5"), them);
		var mid = Field(TestGames.Creature("Mid", "3/3"), them);
		var di = Hand(Trk("Dominion Infiltration"), me);
		s = s.Act(Casts(di).Single(c => c.X == 2));
		var picks = new global::MagicCore.Ai.GreedyAi(me).Answer(s, s.GetPendingChoice()!);
		Assert.That(picks, Nu.EquivalentTo(new[] { big, mid }));
	}

	// ===== A number chosen as it enters: Nyota Uhura =====

	int NyotaChoosing(int number)
	{
		var nyota = Hand(Trk("Nyota Uhura, Communications"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = nyota });
		Resolve(c =>
			c switch
			{
				ChooseNumberChoice => [number + 1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		return nyota;
	}

	[Test]
	public void NyotaUhura_AnOpponentCastsTheChosenManaValue_YouDraw()
	{
		NyotaChoosing(2);
		var hand = HandCount(me);
		TheirSpellOnTheStack(TestGames.Inline("Two", "{1}{U}", "Instant"));
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "mana value equal to the chosen number");
		TheirSpellOnTheStack(TestGames.Inline("Three", "{2}{U}", "Instant"));
		Resolve();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "mana value 3 isn't");
	}

	[Test]
	public void NyotaUhura_MyOwnSpell_NoDraw()
	{
		NyotaChoosing(1);
		var one = Hand(TestGames.Inline("One", "{U}", "Instant"), me);
		var hand = HandCount(me);
		Cast(one, []);
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1), "an opponent casts");
	}

	[Test]
	public void NyotaUhura_TheAi_NamesTheOpponentsCommonestKnownManaValue()
	{
		Field(TestGames.CreatureCosting("{2}{G}", "Ape", "3/3"), them);
		Field(TestGames.CreatureCosting("{1}{G}{G}", "Ape2", "3/3"), them);
		Field(TestGames.CreatureCosting("{G}", "Elf", "1/1"), them);
		var hidden = Hand(TestGames.CreatureCosting("{G}", "Hidden", "1/1"), them);
		Hand(TestGames.CreatureCosting("{G}", "Hidden2", "1/1"), them);
		var ai = new global::MagicCore.Ai.GreedyAi(me);
		var choice = new ChooseNumberChoice(me, 10) { NamesAManaValue = true };
		Assert.That(
			ai.Answer(s, choice),
			Nu.EqualTo(new[] { 3 + 1 }),
			"two threes seen; the ones in hand unseen"
		);
		Assert.That(s.GetCard(hidden).KnownTo & Knowers.Opponent, Nu.EqualTo(Knowers.None));
	}

	[Test]
	public void NyotaUhura_TheAi_NothingKnown_Names2()
	{
		var choice = new ChooseNumberChoice(me, 10) { NamesAManaValue = true };
		Assert.That(
			new global::MagicCore.Ai.GreedyAi(me).Answer(s, choice),
			Nu.EqualTo(new[] { 2 + 1 })
		);
	}

	// ===== Kicked copies: Mirror Universe Intrusion =====

	[Test]
	public void MirrorUniverseIntrusion_CopiesTargetCreatureWithMenace()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), them);
		Field(TestGames.Creature("Ape", "3/3"), them);
		Cast(Hand(Trk("Mirror Universe Intrusion"), me), [], bear);
		var tokens = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken).ToList();
		Assert.That(tokens.Select(c => c.Name), Nu.EqualTo(new[] { "Bear" }));
		Assert.That(s.Has(tokens[0], "Menace"), Nu.True, "except it has menace");
	}

	[Test]
	public void MirrorUniverseIntrusion_Kicked_CopiesEachCreatureTargetOpponentControls()
	{
		Field(TestGames.Creature("Bear", "2/2"), them);
		Field(TestGames.Creature("Ape", "3/3"), them);
		Field(TestGames.Creature("Mine", "1/1"), me);
		var mui = Hand(Trk("Mirror Universe Intrusion"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = mui,
				Kicked = true,
				Targets = [them],
			}
		);
		Resolve();
		var tokens = s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken).ToList();
		Assert.That(
			tokens.Select(c => c.Name),
			Nu.EquivalentTo(new[] { "Bear", "Ape" }),
			"for each creature target opponent controls"
		);
		Assert.That(tokens.All(c => s.Has(c, "Menace")), Nu.True);
	}
}
