using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4i1 + i2a, LSVPower — red's trigger and combat options, FIRING against their printed text through real
/// casting and the real stack: Fury's divided trigger, Ivora's per-card discard and Blood, Dragon's Rage Channeler's
/// delirium and "attacks each combat if able", Caves of Chaos Adventurer's completed dungeon. (i1's six are tier 1:
/// tested pieces, read on the card sheet.)
/// </summary>
public class LsvBatch2iTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static IEnumerable<int> Defaults(ChoiceAction c) =>
		c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>A 1/5 of theirs: it shows the damage and lives.</summary>
	int Ox(string name) => Field(TestGames.Creature(name, "1/5"), them);

	static CardDefinition DiscardTwo =>
		TestGames.Inline("Purge", "{1}", "Sorcery", null, new Discard(Ref.You, 2));

	/// <summary>TEST SETUP: four card types in my graveyard — creature, sorcery, artifact, land: delirium.</summary>
	void Delirium()
	{
		Put(Bear, me, ZoneType.Graveyard);
		Put(TestCards.Fodder, me, ZoneType.Graveyard);
		Put(TestCards.Trinket, me, ZoneType.Graveyard);
		Put(TestGames.Card("Forest"), me, ZoneType.Graveyard);
	}

	// ===== Fury: a triggered ability's divided targets =====

	List<ChoiceAction> CastFury(int[] targets, string? split = null) =>
		CastAsking(
			Hand(Lsv("Fury"), me),
			c =>
				c switch
				{
					ChooseTargetChoice t => t.Index < targets.Length ? [targets[t.Index]] : [],
					ChooseKeywordChoice ways when split is not null =>
					[
						ways.Keywords.IndexOf(split) + 1,
					],
					_ => Defaults(c),
				}
		);

	[Test]
	public void Fury_Enters_FourDamageDividedAsChosenAmongTwo_TheSplitAskedAfterTheTargets()
	{
		var a = Ox("A");
		var b = Ox("B");
		var asked = CastFury([a, b], "3 to A, 1 to B");
		Assert.That(
			asked.OfType<ChooseTargetChoice>().Count(),
			Nu.EqualTo(4),
			"any number: four words, one at a time"
		);
		var ways = asked.OfType<ChooseKeywordChoice>().Single();
		Assert.That(
			ways.Keywords,
			Nu.EqualTo(new[] { "3 to A, 1 to B", "2 to A, 2 to B", "1 to A, 3 to B" })
		);
		Assert.That(
			(s.GetCard(a).Damage, s.GetCard(b).Damage),
			Nu.EqualTo((3, 1)),
			"divided as I chose"
		);
	}

	[Test]
	public void Fury_OneTarget_TakesAllFour_NoSplitAsked()
	{
		var a = Ox("A");
		var asked = CastFury([a]);
		Assert.That(asked.OfType<ChooseKeywordChoice>(), Nu.Empty);
		Assert.That(s.GetCard(a).Damage, Nu.EqualTo(4));
	}

	// ===== Ivora: one discard event per card; the Blood token =====

	[Test]
	public void Ivora_DiscardingTwoCards_TwoCounters()
	{
		var ivora = Field(Lsv("Ivora, Insatiable Heir"), me);
		var purge = Hand(DiscardTwo, me);
		Hand(TestCards.Fodder, me);
		Hand(TestCards.Fodder, me);
		Cast(purge, []);
		Assert.That(s.GetCard(ivora).PlusOneCounters, Nu.EqualTo(2), "whenever you discard A card");
	}

	[Test]
	public void Tinybones_WheelOfFortune_EachPlayersDiscardTriggersIt_OncePerPlayer()
	{
		Field(CardRegistry.Get("Tinybones, Pocket Nuisance"), me);
		var wheel = Hand(Lsv("Wheel of Fortune"), me);
		Hand(TestCards.Fodder, me);
		Hand(TestCards.Fodder, me);
		Hand(TestCards.Fodder, them);
		Hand(TestCards.Fodder, them);
		var life = Life(them);
		Cast(wheel, []);
		Assert.That(
			Life(them),
			Nu.EqualTo(life - 2),
			"each player discards one or more cards: one trigger each (CR 603.2c)"
		);
	}

	[Test]
	public void Ivora_Enters_ABlood_WhoseDiscardDrawsAndFeedsHer()
	{
		var ivora = Hand(Lsv("Ivora, Insatiable Heir"), me);
		Cast(ivora, []);
		var blood = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Blood");
		Assert.That(blood.Is(CardType.Artifact));
		Hand(TestCards.Fodder, me);
		var hand = HandCount(me);
		s = s.Act(Activations(blood.Id).First());
		Resolve();
		Assert.That(Alive(blood.Id), Nu.False, "sacrificed");
		Assert.That(HandCount(me), Nu.EqualTo(hand - 1 + 1), "discard a card, draw a card");
		Assert.That(s.GetCard(ivora).PlusOneCounters, Nu.EqualTo(1), "the discard feeds Ivora");
	}

	[Test]
	public void Ivora_CombatDamageToAPlayer_ABlood()
	{
		var ivora = Field(Lsv("Ivora, Insatiable Heir"), me);
		AttackWith(Defaults, ivora);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Blood"),
			Nu.EqualTo(1)
		);
	}

	// ===== Dragon's Rage Channeler: delirium, "attacks each combat if able" =====

	DeclareAttackerChoice AskToAttack()
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		return (DeclareAttackerChoice)s.GetPendingChoice()!;
	}

	[Test]
	public void DragonsRageChanneler_NoDelirium_A1x1_ThatMayStayHome()
	{
		var drc = Field(Lsv("Dragon's Rage Channeler"), me);
		Assert.That(
			(s.Power(s.GetCard(drc)), s.Has(s.GetCard(drc), "Flying")),
			Nu.EqualTo((1, false))
		);
		Assert.That(AskToAttack().Options.Select(o => o.Id), Has.Member(CombatOption.None));
	}

	[Test]
	public void DragonsRageChanneler_Delirium_A3x3Flier_ThatMustAttack()
	{
		var drc = Field(Lsv("Dragon's Rage Channeler"), me);
		Delirium();
		var card = s.GetCard(drc);
		Assert.That(
			(s.Power(card), s.Toughness(card), s.Has(card, "Flying")),
			Nu.EqualTo((3, 3, true))
		);
		Assert.That(
			AskToAttack().Options.Select(o => o.Id),
			Nu.Not.Contains(CombatOption.None),
			"attacks each combat if able"
		);
	}

	[Test]
	public void DragonsRageChanneler_ANoncreatureSpell_Surveil1()
	{
		Field(Lsv("Dragon's Rage Channeler"), me);
		Cast(Hand(TestCards.Fodder, me), []);
		Assert.That(s.GetPlayer(me).ThisTurn.Surveils, Nu.EqualTo(1));
	}

	// ===== Caves of Chaos Adventurer: "if you've completed a dungeon" =====

	[Test]
	public void Undercity_RemovedOnItsLastRoom_IsCompleted()
	{
		Ventures(); // Secret Entrance
		Ventures(On<ChooseKeywordChoice>(2)); // Lost Well
		Ventures(On<ChooseKeywordChoice>(2)); // Stash
		Ventures(); // Catacombs
		Assert.That(s.GetPlayer(me).DungeonsCompleted, Nu.EqualTo(0));
		Ventures(); // Throne of the Dead Three
		Assert.That(Undercity.Of(s, me), Nu.Null);
		Assert.That(s.GetPlayer(me).DungeonsCompleted, Nu.EqualTo(1), "completed");
	}

	PlayableFromExile? AttackAndExile(int caves)
	{
		var top = OnTop(TestGames.CreatureCosting("{2}{R}", "Ogre", "3/3"));
		AttackWith(Defaults, caves);
		Assert.That(InExile(top), "exile the top card of your library");
		return s.GetCard(top).GetComponent<PlayableFromExile>();
	}

	[Test]
	public void CavesOfChaosAdventurer_Attacks_NoDungeonCompleted_PlayableThisTurn_Paid()
	{
		var caves = Field(Lsv("Caves of Chaos Adventurer"), me);
		var playable = AttackAndExile(caves);
		Assert.That(playable, Nu.Not.Null, "you may play that card this turn");
		Assert.That(playable!.Free, Nu.False);
	}

	[Test]
	public void CavesOfChaosAdventurer_Attacks_ADungeonCompleted_PlayableFree()
	{
		var caves = Field(Lsv("Caves of Chaos Adventurer"), me);
		s = s.WithPlayer(s.GetPlayer(me) with { DungeonsCompleted = 1 }); // TEST SETUP: one completed earlier
		var playable = AttackAndExile(caves);
		Assert.That(playable?.Free, Nu.True, "without paying its mana cost");
	}
}
