using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Tests.Turns;

public class StackTests
{
	static readonly CardDefinition Bear = TestGames.CreatureCosting("{1}{G}", "Bear", "2/2");
	static readonly CardDefinition FlashBear = TestGames.CreatureCosting(
		"{1}{G}",
		"Flash Bear",
		"2/2",
		"Flash"
	);
	static readonly CardDefinition Growth = TestGames.Inline(
		"Growth",
		"{G}",
		"Sorcery",
		null,
		new GainLife(Ref.You, 1)
	);
	static readonly CardDefinition Heal = TestGames.Inline(
		"Heal",
		"{G}",
		"Instant",
		null,
		new GainLife(Ref.You, 3)
	);
	static readonly CardDefinition Drain = TestGames.Inline(
		"Drain",
		"{G}",
		"Instant",
		null,
		new LoseLife(Ref.EachOpponent, 2)
	);

	/// <summary>Both players get two Forests; the starting player is in their first main phase.</summary>
	static (GameState S, int Me, int Them) Board()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		foreach (var p in new[] { me, them, me, them })
			(s, _) = TestGames.Put(s, p, TestGames.Card("Forest"), ZoneType.Battlefield);
		return (s, me, them);
	}

	static GameState Pass(GameState s) =>
		s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });

	[Test]
	public void Creature_GoesOnTheStack_CasterKeepsPriority_ResolvesOntoTheBattlefield()
	{
		var (s, me, them) = Board();
		int bear;
		(s, bear) = TestGames.Put(s, me, Bear, ZoneType.Hand);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = bear });
		Assert.That(s.GetParent(bear), Is.EqualTo(s.GetWellKnownId(MagicKeys.Stack)));
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsTapped), Is.EqualTo(2));

		s = Pass(Pass(s));

		Assert.That(s.IsIn(bear, me, ZoneType.Battlefield));
		Assert.That(s.GetCard(bear).HasComponent<OnStackComponent>(), Is.False);
		Assert.That(
			s.GetGame().Step,
			Is.EqualTo(Step.PreCombatMain),
			"resolving does not end the step"
		);
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));
	}

	[Test]
	public void LastIn_FirstOut_AndTheActivePlayerGetsPriorityAfterEachResolution()
	{
		var (s, me, them) = Board();
		int heal,
			drain;
		(s, heal) = TestGames.Put(s, me, Heal, ZoneType.Hand);
		(s, drain) = TestGames.Put(s, them, Drain, ZoneType.Hand);
		var startingLife = s.GetPlayer(me).Life;

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = heal });
		s = Pass(s);
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(them));
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = drain });

		s = Pass(Pass(s));
		Assert.That(
			s.GetPlayer(me).Life,
			Is.EqualTo(startingLife - 2),
			"Drain, cast last, resolved first"
		);
		Assert.That(s.IsIn(drain, them, ZoneType.Graveyard));
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(me));

		s = Pass(Pass(s));
		Assert.That(s.GetPlayer(me).Life, Is.EqualTo(startingLife - 2 + 3));
		Assert.That(s.StackIsEmpty());
	}

	[Test]
	public void SorcerySpeed_NotOnTheStack_NotInUpkeep_NotOnTheOpponentsTurn()
	{
		var (s, me, them) = Board();
		int growth,
			heal,
			theirBear;
		(s, growth) = TestGames.Put(s, me, Growth, ZoneType.Hand);
		(s, heal) = TestGames.Put(s, me, Heal, ZoneType.Hand);
		(s, theirBear) = TestGames.Put(s, them, Bear, ZoneType.Hand);

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = heal });
		Assert.That(
			s.TryAddAction(new CastSpellAction { PlayerId = me, CardId = growth }).Success,
			Is.False
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 2 && x.GetGame().PriorityPlayerId == them
		);
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
		Assert.That(
			s.TryAddAction(new CastSpellAction { PlayerId = them, CardId = theirBear }).Success,
			Is.False
		);
		s = Pass(s);
		Assert.That(
			s.TryAddAction(new CastSpellAction { PlayerId = me, CardId = growth }).Success,
			Is.False
		);
	}

	[Test]
	public void FlashAndInstants_CastOnTheOpponentsTurn()
	{
		var (s, me, them) = Board();
		int flashBear;
		(s, flashBear) = TestGames.Put(s, them, FlashBear, ZoneType.Hand);

		s = Pass(s);
		Assert.That(s.GetGame().PriorityPlayerId, Is.EqualTo(them));
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = flashBear });
		s = Pass(Pass(s));

		Assert.That(s.IsIn(flashBear, them, ZoneType.Battlefield));
	}

	[Test]
	public void AutoPass_SkipsEveryStepWhereNobodyHasAChoice()
	{
		var s = TestGames.Started().AutoPass();

		Assert.That(s.GetGame().Step, Is.EqualTo(Step.PreCombatMain));
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(s.GetGame().StartingPlayerId));
		Assert.That(s.LegalActions().OfType<PlayLandAction>(), Is.Not.Empty);
	}
}
