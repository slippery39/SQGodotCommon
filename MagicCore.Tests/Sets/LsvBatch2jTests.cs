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
/// MC-4.4j2a, LSVPower — white's options, FIRING against their printed text through real casting and the real stack:
/// protection of your choice until end of turn (Mother of Runes, Giver of Runes, Seasoned Dungeoneer), a counted search
/// by another player (Winds of Abandon overloaded), "whenever a creature you control is dealt damage" (The Sensational
/// She-Hulk). j1's tested pieces are read on the card sheet.
/// </summary>
public class LsvBatch2jTests : FraBoardTest
{
	const string Jennifer = "Jennifer Walters // The Sensational She-Hulk";

	// ChooseKeywordChoice ids: White 1, Blue 2, Black 3, Red 4, Green 5, colorless 6 (Giver only).
	const int Red = 4;
	const int Colorless = 6;

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>"{R} instant: 3 damage to target creature" — a red source.</summary>
	static CardDefinition Bolt =>
		new(
			TestGames.Inline("Bolt", "{R}", "Instant").Data with
			{
				Colors = ["R"],
			},
			[
				new SpellEffectsComponent([new DealDamage(Ref.Target(), 3)])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	/// <summary>"{B} instant: destroy target creature" — a black source.</summary>
	static CardDefinition Murder =>
		new(
			TestGames.Inline("Murder", "{B}", "Instant").Data with
			{
				Colors = ["B"],
			},
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	/// <summary>I tap it for its ability on that target, choosing this option.</summary>
	List<ChoiceAction> Rune(int source, int target, int option)
	{
		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = me,
				CardId = source,
				Targets = [target],
			}
		);
		return ResolveAsking(On<ChooseKeywordChoice>(option));
	}

	// ===== Mother of Runes, Giver of Runes =====

	[Test]
	public void MotherOfRunes_ProtectionFromTheChosenColour_UntilEndOfTurn()
	{
		var mother = Field(Lsv("Mother of Runes"), me);
		var bear = Field(Bear, me);
		var bolt = Hand(Bolt, them);
		var murder = Hand(Murder, them);

		var asked = Rune(mother, bear, Red);

		Assert.That(
			Offered<ChooseKeywordChoice>(asked).Count(),
			Nu.EqualTo(5),
			"the five colours — no colorless"
		);
		Assert.That(
			TargetSpec.Creature.IsLegal(s, them, bolt, bear),
			Nu.False,
			"protection from red"
		);
		Assert.That(TargetSpec.Creature.IsLegal(s, them, murder, bear), "black still targets it");
		Assert.That(s.GetCard(mother).IsTapped);
	}

	[Test]
	public void GiverOfRunes_AnotherCreatureOnly_AndColorlessIsAChoice()
	{
		var giver = Field(Lsv("Giver of Runes"), me);
		var bear = Field(Bear, me);
		Assert.That(
			Activations(giver).SelectMany(a => a.Targets),
			Nu.EqualTo(new[] { bear }),
			"another target creature you control"
		);
		var shock = Hand(TestCards.Shock(2), them); // {1}: colourless

		var asked = Rune(giver, bear, Colorless);

		Assert.That(Offered<ChooseKeywordChoice>(asked).Count(), Nu.EqualTo(6), "colorless too");
		Assert.That(
			TargetSpec.Creature.IsLegal(s, them, shock, bear),
			Nu.False,
			"protection from colorless"
		);
	}

	// ===== Seasoned Dungeoneer =====

	[Test]
	public void SeasonedDungeoneer_TheAttackingWarriorGainsProtectionFromCreatures_AndExplores()
	{
		var dungeoneer = Field(Lsv("Seasoned Dungeoneer"), me); // a Human Warrior: its own target
		var blocker = Field(Bear, them);
		OnTop(Bear); // a nonland: a +1/+1 counter

		AttackWith(Targeting(dungeoneer, yes: false), dungeoneer);

		Assert.That(
			Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(dungeoneer)),
			Nu.False,
			"protection from creatures: it can't be blocked by one"
		);
		Assert.That(s.GetCard(dungeoneer).PlusOneCounters, Nu.EqualTo(1), "it explored a nonland");
	}

	// ===== Winds of Abandon =====

	[Test]
	public void WindsOfAbandon_Overloaded_ExilesEachOfTheirs_TheySearchThatManyBasicsTapped()
	{
		var theirs = new[] { Field(Bear, them), Field(Bear, them) };
		var mine = Field(Bear, me);
		foreach (var _ in Enumerable.Range(0, 3))
			Put(CardRegistry.Get("Plains"), them, ZoneType.Library);
		var lands = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var winds = Hand(Lsv("Winds of Abandon"), me);

		s = s.Act(Casts(winds).Single(c => c.WithoutPayingManaCost));
		Resolve(c =>
			c switch
			{
				ChooseFromLibraryChoice => c.Options.Select(o => o.Id).Take(c.MaxChoices),
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);

		Assert.That(theirs.All(InExile), "each creature I don't control");
		Assert.That(Alive(mine));
		var after = s.CardsIn(them, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList();
		Assert.That(after.Count - lands, Nu.EqualTo(2), "a basic for each creature exiled");
		Assert.That(after.Count(c => c.IsTapped), Nu.EqualTo(2), "tapped");
	}

	// ===== Jennifer Walters // The Sensational She-Hulk =====

	[Test]
	public void SheHulk_ACreatureOfMineIsDealtDamage_SheDealsThatMuch_OnceEachTurn()
	{
		var hulk = Field(Lsv(Jennifer), me);
		s = s.UpdateObject(hulk, s.GetCard(hulk).BackFaceUp()); // TEST SETUP: transformed
		var bear = Field(TestGames.Creature("Ox", "1/5"), me);
		var life = Life(them);

		var shock2 = Hand(TestCards.Shock(2), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock2,
				Targets = [bear],
			}
		);
		Resolve(Targeting(them, yes: true));
		Assert.That(Life(them), Nu.EqualTo(life - 2), "that much damage, to any target");

		var shock1 = Hand(TestCards.Shock(1), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock1,
				Targets = [bear],
			}
		);
		Resolve(Targeting(them, yes: true));
		Assert.That(Life(them), Nu.EqualTo(life - 2), "only once each turn");
	}
}
