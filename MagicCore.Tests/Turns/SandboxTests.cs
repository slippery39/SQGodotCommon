using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Tests.Turns;

public class SandboxTests
{
	static readonly CardDefinition Healer =
		new(
			TestGames.Creature("Healer", "1/1").Data,
			[new TriggeredAbilityComponent(TriggerEvent.SelfEnters, [new GainLife(Ref.You, 3)])]
		);

	static int Find(GameState s, int player, ZoneType zone) =>
		s.CardsIn(player, zone).Single(c => c.Name == "Healer").Id;

	[Test]
	public void OntoTheBattlefield_ItEnters_AndItsTriggerGoesOnTheStack()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;

		s = Sandbox.Put(s, me, Healer, ZoneType.Battlefield);
		var healer = Find(s, me, ZoneType.Battlefield);

		Assert.That(s.IsIn(healer, me, ZoneType.Battlefield));
		Assert.That(s.GetCard(healer).EnteredOnTurn, Is.EqualTo(s.GetGame().TurnNumber));
		Assert.That(s.StackIsEmpty(), Is.False, "its 'enters' trigger");
		var life = s.GetPlayer(me).Life;
		s = TestGames.PassUntil(s, x => x.StackIsEmpty());
		Assert.That(s.GetPlayer(me).Life, Is.EqualTo(life + 3));
	}

	[Test]
	public void IntoTheLibrary_ItGoesOnTop()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;

		s = Sandbox.Put(s, me, Healer, ZoneType.Library);
		var healer = Find(s, me, ZoneType.Library);

		Assert.That(s.CardsIn(me, ZoneType.Library).First().Id, Is.EqualTo(healer));
	}

	[Test]
	public void IntoAHand_OrAnOpponentsGraveyard()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);

		s = Sandbox.Put(s, me, Healer, ZoneType.Hand);
		s = Sandbox.Put(s, them, Healer, ZoneType.Graveyard);
		var mine = Find(s, me, ZoneType.Hand);
		var theirs = Find(s, them, ZoneType.Graveyard);

		Assert.That(s.IsIn(mine, me, ZoneType.Hand));
		Assert.That(s.IsIn(theirs, them, ZoneType.Graveyard));
		Assert.That(s.StackIsEmpty(), "nothing entered, nothing triggered");
	}

	[Test]
	public void AnAura_ChoosesItsHostAsItEnters_InsteadOfFallingOff()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		s = Sandbox.Put(s, me, Healer, ZoneType.Battlefield);
		s = TestGames.PassUntil(s, x => x.StackIsEmpty());
		var healer = Find(s, me, ZoneType.Battlefield);
		var aura = global::MagicCore.Sets.Fra.FraCards.Get("Ferocity of the Hunt");

		s = Sandbox.Put(s, me, aura, ZoneType.Battlefield);
		if (s.GetPendingChoice() is { } host)
			s = s.Choose([healer]);

		var placed = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == aura.Data.Name);
		Assert.That(global::MagicCore.Rules.Attachments.HostOf(s, placed)?.Id, Is.EqualTo(healer));
	}

	[Test]
	public void ATriggerItPutsOnTheStack_GivesTheOtherPlayerTheirChance()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		s = s.Act(new global::MagicCore.Actions.PassPriorityAction { PlayerId = me }); // one pass already counted

		s = Sandbox.Put(s, them, Healer, ZoneType.Battlefield);
		s = s.Act(new global::MagicCore.Actions.PassPriorityAction { PlayerId = them });

		Assert.That(s.StackIsEmpty(), Is.False, "not resolved on a stale pass count");
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));
	}

	[Test]
	public void OnlyIntoItsZones()
	{
		var s = TestGames.InFirstMain();

		Assert.That(
			() => Sandbox.Put(s, s.GetGame().StartingPlayerId, Healer, ZoneType.Command),
			Throws.Exception
		);
	}

	[Test]
	public void NotWhileAChoiceIsPending()
	{
		var s = TestGames.New(); // the mulligan: nobody holds priority

		Assert.That(() => Sandbox.Put(s, s.Player1Id(), Healer, ZoneType.Hand), Throws.Exception);
	}
}
