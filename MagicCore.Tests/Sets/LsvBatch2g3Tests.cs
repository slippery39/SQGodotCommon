using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g3, LSVPower — dungeons and the initiative (CR 309, 701.49, 725): Feywild Caretaker, and Undercity's rooms,
/// each FIRING against its printed text through real casting, the real stack and real combat.
/// </summary>
public class LsvBatch2g3Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	int Named(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Name == name);

	[Test]
	public void FeywildCaretaker_EnteringTakesTheInitiative_AndVenturesIntoSecretEntrance()
	{
		var caretaker = Hand(Lsv("Feywild Caretaker"), me);
		int Basics() => s.CardsIn(me, ZoneType.Hand).Count(c => c.Is(Supertype.Basic));
		Put(TestGames.Card("Forest"), me, ZoneType.Library);
		var basics = Basics();

		CastAsking(
			caretaker,
			c => c.Options.Take(c is ChooseFromLibraryChoice ? 1 : c.MinChoices).Select(o => o.Id)
		);

		Assert.That(InitiativeComponent.HolderOf(s), Nu.EqualTo(me), "I have the initiative");
		Assert.That(Room(me), Nu.EqualTo("Secret Entrance"), "Undercity, its first room");
		Assert.That(Basics(), Nu.EqualTo(basics + 1), "Secret Entrance: a basic land card into my hand");
	}

	[Test]
	public void FeywildCaretaker_AtMyEndStep_AFaerieDragon_WhileIHaveTheInitiative()
	{
		Cast(Hand(Lsv("Feywild Caretaker"), me), []);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);

		var dragon = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Faerie Dragon");
		Assert.That(
			(s.Power(dragon), s.Toughness(dragon), s.Has(dragon, "Flying")),
			Nu.EqualTo((1, 1, true))
		);
	}

	[Test]
	public void FeywildCaretaker_WithoutTheInitiative_NoDragon()
	{
		Cast(Hand(Lsv("Feywild Caretaker"), me), []);
		s = InitiativeComponent.Take(s, them); // TEST SETUP: they took it

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);

		Assert.That(Named(me, "Faerie Dragon"), Nu.EqualTo(0));
	}

	[Test]
	public void TheInitiative_CombatDamageToItsHolder_TakesIt_AndTheTakerVentures()
	{
		s = InitiativeComponent.Take(s, them); // TEST SETUP: they have it, no dungeon of mine
		var bear = Field(Bear, me);

		AttackWith(c => c.Options.Take(c.MinChoices).Select(o => o.Id), bear);

		Assert.That(InitiativeComponent.HolderOf(s), Nu.EqualTo(me), "my creature hit them");
		Assert.That(
			s.CardsIn(them, ZoneType.Command).Any(c => c.HasComponent<InitiativeComponent>()),
			Nu.False,
			"one holder: theirs is gone"
		);
		Assert.That(Room(me), Nu.EqualTo("Secret Entrance"), "taking it, I ventured");
	}

	[Test]
	public void TheInitiative_AtItsHoldersUpkeep_TheyVenture_ABranchIsOneChoice()
	{
		var caretaker = Hand(Lsv("Feywild Caretaker"), me);
		Cast(caretaker, []);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Draw && x.GetGame().ActivePlayerId == them
		);
		Assert.That(Room(me), Nu.EqualTo("Secret Entrance"), "their upkeep: no venture of mine");


		s = TestGames.PassUntil(
			s,
			x =>
				x.GetPendingChoice() is ChooseKeywordChoice
				|| x.GetGame().Step == Step.Draw && x.GetGame().ActivePlayerId == me
		);
		var branch = (ChooseKeywordChoice)s.GetPendingChoice()!;
		Assert.That(branch.Keywords, Nu.EqualTo(new[] { "Forge", "Lost Well" }), "one choice, two rooms");
		Resolve(c => c is ChooseKeywordChoice ? [1] : c is ChooseTargetChoice ? [caretaker] : c.Options.Take(c.MinChoices).Select(o => o.Id));

		Assert.That(Room(me), Nu.EqualTo("Forge"));
		Assert.That(s.GetCard(caretaker).PlusOneCounters, Nu.EqualTo(2), "Forge: two +1/+1 counters");
	}

	[Test]
	public void Undercity_LostWell_Stash_Catacombs_Throne_ThenItIsCompleted()
	{
		Ventures(); // Secret Entrance
		Ventures(On<ChooseKeywordChoice>(2)); // Lost Well: scry 2
		Assert.That(Room(me), Nu.EqualTo("Lost Well"));

		Ventures(On<ChooseKeywordChoice>(2));
		Assert.That(Room(me), Nu.EqualTo("Stash"));
		Assert.That(Named(me, "Treasure"), Nu.EqualTo(1), "Stash: a Treasure");

		Ventures();
		var skeleton = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Skeleton");
		Assert.That(
			(s.Power(skeleton), s.Toughness(skeleton), s.Has(skeleton, "Menace")),
			Nu.EqualTo((4, 1, true)),
			"Catacombs: a 4/1 Skeleton with menace"
		);

		var bear = OnTop(Bear);
		Ventures();
		Assert.That(Alive(bear), "Throne: a creature from the top ten onto the battlefield");
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(3), "with three +1/+1 counters");
		Assert.That(s.Has(s.GetCard(bear), "Hexproof"), "it gains hexproof");
		Assert.That(Undercity.Of(s, me), Nu.Null, "the last room's ability resolved: completed, removed");

		Ventures();
		Assert.That(Room(me), Nu.EqualTo("Secret Entrance"), "a new Undercity");
	}

	[Test]
	public void Undercity_TheLastRoomStaysWhileItsAbilityIsOnTheStack()
	{
		for (var i = 0; i < 4; i++)
			Ventures();
		var last = Hand(VentureSpell, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = last });
		for (var i = 0; i < 10 && Room(me) != "Throne of the Dead Three"; i++)
			s = TestGames.PassOrChoose(s); // (a PassUntil predicate would read the old s through Room)
		Assert.That(s.StackIsEmpty(), Nu.False, "Throne's ability waits on the stack");

		s = StateBasedActions.Check(s);

		Assert.That(Undercity.Of(s, me), Nu.Not.Null, "not removed before its ability has left the stack");
	}

	[Test]
	public void Undercity_VenturingOnTheLastRoom_CompletesIt_AndBeginsANewOne()
	{
		for (var i = 0; i < 4; i++)
			Ventures();
		var last = Hand(VentureSpell, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = last });
		for (var i = 0; i < 10 && Room(me) != "Throne of the Dead Three"; i++)
			s = TestGames.PassOrChoose(s);
		Assert.That(s.StackIsEmpty(), Nu.False, "Throne's ability on the stack");

		var again = Hand(VentureSpell, me); // an instant, in response
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = again });
		for (var i = 0; i < 10 && Room(me) == "Throne of the Dead Three"; i++)
			s = TestGames.PassOrChoose(s);

		Assert.That(Room(me), Nu.EqualTo("Secret Entrance"), "completed, and a new Undercity begun");
	}

	[Test]
	public void Undercity_Forge_Trap_Archives()
	{
		var bear = Field(Bear, me);
		Ventures();
		Ventures(c => c is ChooseTargetChoice ? [bear] : c.Options.Take(c.MinChoices).Select(o => o.Id));
		Assert.That(s.GetCard(bear).PlusOneCounters, Nu.EqualTo(2), "Forge");

		var life = Life(them);
		Ventures(c => c is ChooseTargetChoice ? [them] : c.Options.Take(c.MinChoices).Select(o => o.Id));
		Assert.That(Room(me), Nu.EqualTo("Trap!"));
		Assert.That(Life(them), Nu.EqualTo(life - 5), "Trap!: target player loses 5 life");

		var hand = HandCount(me);
		Ventures();
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "Archives: draw a card");
	}

	[Test]
	public void Undercity_Arena_GoadedCreatureMustAttack_TheAiToo()
	{
		var theirs = Field(Bear, them);
		Ventures();
		Ventures(On<ChooseKeywordChoice>(2)); // Lost Well
		Ventures(c => c is ChooseTargetChoice ? [theirs] : c.Options.Take(c.MinChoices).Select(o => o.Id));
		Assert.That(Room(me), Nu.EqualTo("Arena"));

		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		var ask = (DeclareAttackerChoice)s.GetPendingChoice()!;
		Assert.That(ask.CreatureId, Nu.EqualTo(theirs));
		Assert.That(
			ask.Options.Select(o => o.Id),
			Nu.Not.Contains(CombatOption.None),
			"goaded: no \"Don't attack\""
		);
		Assert.That(
			Lookahead.QuickAnswer(s, ask),
			Nu.EqualTo(new[] { me }),
			"the look-ahead's quick answer attacks with it too"
		);
		var stripped = s.GetCard(theirs).WithComponent(new UntilEndOfTurn { LosesAllAbilities = true });
		s = s.UpdateObject(theirs, stripped); // TEST SETUP: Flourishing Grapple's "loses all abilities"
		Assert.That(
			s.GetPendingChoice()!.Options.Select(o => o.Id),
			Nu.Not.Contains(CombatOption.None),
			"goad is an effect, not an ability: losing all abilities doesn't end it"
		);
		var declared = WholeAttacks.Declare(s, them, new(System.Collections.Immutable.ImmutableDictionary<int, int>.Empty, []));
		Assert.That(declared.GetCard(theirs).AttackingId, Nu.EqualTo(me), "the AI's no-attack plan still attacks with it");
	}
}
