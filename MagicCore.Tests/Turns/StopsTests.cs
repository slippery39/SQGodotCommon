using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Tests.Turns;

public class StopsTests
{
	static readonly CardDefinition Heal = TestGames.Inline(
		"Heal",
		"{G}",
		"Instant",
		null,
		new GainLife(Ref.You, 3)
	);

	/// <summary>The starting player's first upkeep, holding an instant they can cast — a stop is possible anywhere.</summary>
	static (GameState S, int Me, int Them) UpkeepWithAnInstant()
	{
		var s = TestGames.Started();
		var me = s.GetGame().StartingPlayerId;
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, Heal, ZoneType.Hand);
		return (s, me, s.OpponentOf(me));
	}

	[Test]
	public void ArenaStops_PassTheUpkeep_StopInTheMainPhase()
	{
		var (s, me, _) = UpkeepWithAnInstant();
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
		Assert.That(s.LegalActions().OfType<CastSpellAction>(), Is.Not.Empty, "precondition");
		Assert.That(Stops.Arena.StopsAt(s, me), Is.False);

		for (var i = 0; i < 20 && !(s.DecidingPlayerId() == me && Stops.Arena.StopsAt(s, me)); i++)
			s =
				s.DecidingPlayerId() == me
					? Stops.Arena.PassUntilStop(s, me)
					: TestGames.PassOrChoose(s);

		Assert.That(s.GetGame().Step, Is.EqualTo(Step.PreCombatMain));
		Assert.That(s.GetGame().ActivePlayerId, Is.EqualTo(me));
	}

	[Test]
	public void FullControl_StopsInTheUpkeep()
	{
		var (s, me, _) = UpkeepWithAnInstant();

		Assert.That((Stops.Arena with { FullControl = true }).StopsAt(s, me), Is.True);
	}

	[Test]
	public void NoStop_WherePassIsTheOnlyMove_EvenInFullControl()
	{
		var s = TestGames.Started();
		var me = s.GetGame().StartingPlayerId;
		Assert.That(
			s.LegalActions(),
			Is.EquivalentTo(new[] { new PassPriorityAction { PlayerId = me } })
		);

		Assert.That((Stops.Arena with { FullControl = true }).StopsAt(s, me), Is.False);
	}

	[Test]
	public void ACombatStepStop_IsSkipped_WhenNothingAttacks()
	{
		var (s, me, _) = UpkeepWithAnInstant();
		var combat = new Stops([Step.DeclareAttackers], []);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.DeclareAttackers);
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(me), "precondition");
		Assert.That(global::MagicCore.Turns.Combat.Attackers(s), Is.Empty, "precondition");
		Assert.That(s.LegalActions().OfType<CastSpellAction>(), Is.Not.Empty, "precondition");

		Assert.That(combat.StopsAt(s, me), Is.False);
	}

	[Test]
	public void OpponentsSpellOnTop_Stops_YourOwnDoesNot()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		foreach (var p in new[] { me, me, them })
			(s, _) = TestGames.Put(s, p, TestGames.Card("Forest"), ZoneType.Battlefield);
		int heal;
		(s, heal) = TestGames.Put(s, me, Heal, ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, Heal, ZoneType.Hand);
		(s, _) = TestGames.Put(s, them, Heal, ZoneType.Hand);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = heal });
		Assert.That(s.LegalActions().OfType<CastSpellAction>(), Is.Not.Empty, "precondition");
		Assert.That(Stops.Arena.StopsAt(s, me), Is.False, "Arena passes so your spell resolves");

		s = s.Act(new PassPriorityAction { PlayerId = me });
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(them));
		Assert.That(Stops.Arena.Theirs, Does.Not.Contain(Step.PreCombatMain), "precondition");
		Assert.That(Stops.Arena.StopsAt(s, them), Is.True, "a response is possible");
	}
}
