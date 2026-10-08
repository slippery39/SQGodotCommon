using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Tests.Turns;

public class CombatTests
{
	GameState s = null!;
	int me,
		them,
		startingLife;

	[SetUp]
	public void Board()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		startingLife = s.GetPlayer(them).Life;
	}

	int Put(int player, string pt, params string[] keywords)
	{
		int id;
		(s, id) = TestGames.Put(
			s,
			player,
			TestGames.Creature($"C{s.NextId}", pt, keywords),
			ZoneType.Battlefield
		);
		return id;
	}

	/// <summary>Passes to declare attackers and attacks with exactly these creatures.</summary>
	void Attack(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice choice)
			s = s.Choose([attackers.Contains(choice.CreatureId) ? them : CombatOption.None]);
	}

	void Block(params (int Blocker, int Attacker)[] blocks)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareBlockerChoice choice)
			s = s.Choose([blocks.FirstOrDefault(b => b.Blocker == choice.CreatureId).Attacker]);
	}

	void ToSecondMain() => s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

	bool Alive(int card) =>
		s.GetParent(card) == s.ZoneId(s.GetCard(card).ControllerId, ZoneType.Battlefield);

	// An attacker stays tapped through EVERY step of the opponent's turn — so it is never asked to
	// block their attack — and untaps only in its controller's next untap step.
	[Test]
	public void AnAttacker_StaysTappedThroughTheOpponentsWholeTurn_AndCannotBlock()
	{
		var bear = Put(me, "2/2");
		var raider = Put(them, "1/1");
		Attack(bear);
		ToSecondMain();

		var askedToBlock = new List<int>();
		while (s.GetGame().TurnNumber < 3)
		{
			if (s.GetGame().TurnNumber == 2)
				Assert.That(s.GetCard(bear).IsTapped, $"untapped during {s.GetGame().Step}");
			if (s.GetPendingChoice() is DeclareAttackerChoice)
				s = s.Choose([me]); // they attack with the raider
			else if (s.GetPendingChoice() is DeclareBlockerChoice block)
			{
				askedToBlock.Add(block.CreatureId);
				s = s.Choose([CombatOption.None]);
			}
			else
				s = TestGames.PassOrChoose(s);
		}

		Assert.That(askedToBlock, Does.Not.Contain(bear), "a tapped creature cannot block");
		Assert.That(s.GetCard(raider).AttackingId, Is.Zero, "their attack happened and ended");
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Upkeep);
		Assert.That(s.GetCard(bear).IsTapped, Is.False, "untapped in its controller's untap step");
	}

	[Test]
	public void Unblocked_DamagesTheDefender_AttackingTaps_VigilanceDoesNot()
	{
		var bear = Put(me, "2/2");
		var watcher = Put(me, "1/1", "Vigilance");

		Attack(bear, watcher);
		Assert.That(s.GetCard(bear).IsTapped);
		Assert.That(s.GetCard(watcher).IsTapped, Is.False);
		ToSecondMain();

		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 3));
		Assert.That(s.GetCard(bear).AttackingId, Is.Zero, "removed from combat");
	}

	[Test]
	public void Defender_IsNeverAskedToAttack()
	{
		var wall = Put(me, "0/4", "Defender");
		var bear = Put(me, "2/2");

		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);

		Assert.That(((DeclareAttackerChoice)s.GetPendingChoice()!).CreatureId, Is.EqualTo(bear));
		Assert.That(Combat.CanAttack(s, s.GetCard(wall)), Is.False);
	}

	[Test]
	public void Choose_RejectsAnUnofferedAnswer()
	{
		var bird = Put(me, "1/1", "Flying");
		Put(them, "2/2");
		var spider = Put(them, "1/3", "Reach");
		Attack(bird);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);

		// The spider is asked; answering "block" with an id that is not an attacker must fail.
		Assert.That(((DeclareBlockerChoice)s.GetPendingChoice()!).CreatureId, Is.EqualTo(spider));
		Assert.That(() => s.Choose([spider]), Throws.InvalidOperationException);
	}

	[Test]
	public void SummoningSickness_HasteIgnoresIt_GoneOnYourNextTurn()
	{
		int sick,
			hasty;
		(s, sick) = TestGames.Put(s, me, TestGames.Creature("Sick", "2/2"), ZoneType.Hand);
		(s, hasty) = TestGames.Put(
			s,
			me,
			TestGames.Creature("Hasty", "2/2", "Haste"),
			ZoneType.Hand
		);
		s = s.EnterBattlefield(sick, me).EnterBattlefield(hasty, me);

		Assert.That(Combat.CanAttack(s, s.GetCard(sick)), Is.False);
		Assert.That(Combat.CanAttack(s, s.GetCard(hasty)));

		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == 3);
		Assert.That(Combat.CanAttack(s, s.GetCard(sick)));
	}

	[Test]
	public void Blocked_TradeDamage_LethalDies_SurvivorHealsInCleanup()
	{
		var attacker = Put(me, "2/3");
		var blocker = Put(them, "2/2");

		Attack(attacker);
		Block((blocker, attacker));
		ToSecondMain();

		Assert.That(Alive(blocker), Is.False);
		Assert.That(s.IsIn(blocker, them, ZoneType.Graveyard));
		Assert.That(s.GetCard(attacker).Damage, Is.EqualTo(2));
		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife), "blocked, no trample");

		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == 2);
		Assert.That(s.GetCard(attacker).Damage, Is.Zero);
	}

	[Test]
	public void Flying_OnlyFlyingOrReachMayBlock()
	{
		var bird = Put(me, "1/1", "Flying");
		var bear = Put(them, "2/2");
		var spider = Put(them, "1/3", "Reach");

		Attack(bird);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);

		var choices = new List<int>();
		while (s.GetPendingChoice() is DeclareBlockerChoice choice)
		{
			choices.Add(choice.CreatureId);
			s = s.Choose([CombatOption.None]);
		}
		Assert.That(choices, Is.EqualTo(new[] { spider }), "the bear is never asked");
	}

	[Test]
	public void Menace_ALoneBlockerIsDropped()
	{
		var brute = Put(me, "3/3", "Menace");
		var bear = Put(them, "2/2");

		Attack(brute);
		Block((bear, brute));
		ToSecondMain();

		Assert.That(Alive(bear));
		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 3));
	}

	[Test]
	public void DoubleBlock_AttackerSplitsLethalDamage_AndTakesBoth()
	{
		var attacker = Put(me, "3/3");
		var first = Put(them, "2/2");
		var second = Put(them, "2/2");

		Attack(attacker);
		Block((first, attacker), (second, attacker));
		ToSecondMain();

		Assert.That(Alive(attacker), Is.False);
		Assert.That(Alive(first), Is.False, "took lethal 2");
		Assert.That(s.GetCard(second).Damage, Is.EqualTo(1), "the remaining 1");
	}

	[Test]
	public void Trample_ExcessOverLethalHitsThePlayer()
	{
		var wurm = Put(me, "5/5", "Trample");
		var bear = Put(them, "2/2");

		Attack(wurm);
		Block((bear, wurm));
		ToSecondMain();

		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 3));
	}

	[Test]
	public void Deathtouch_AnyDamageKills_AndOneIsLethalForTrample()
	{
		var giant = Put(me, "5/5");
		var snake = Put(them, "1/1", "Deathtouch");
		var killer = Put(me, "3/3", "Deathtouch", "Trample");
		var wall = Put(them, "0/4");

		Attack(giant, killer);
		Block((snake, giant), (wall, killer));
		ToSecondMain();

		Assert.That(Alive(giant), Is.False);
		Assert.That(Alive(wall), Is.False);
		Assert.That(
			s.GetPlayer(them).Life,
			Is.EqualTo(startingLife - 2),
			"3 power, 1 is lethal to the wall, 2 tramples over"
		);
	}

	[Test]
	public void FirstStrike_KillsBeforeTheBlockerStrikesBack_DoubleStrikeHitsTwice()
	{
		var knight = Put(me, "2/2", "First strike");
		var duelist = Put(me, "2/2", "Double strike");
		var bear = Put(them, "2/2");

		Attack(knight, duelist);
		Block((bear, knight));
		ToSecondMain();

		Assert.That(Alive(bear), Is.False);
		Assert.That(s.GetCard(knight).Damage, Is.Zero);
		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 4), "double strike: 2 + 2");
	}

	[Test]
	public void Lifelink_ControllerGainsTheDamageDealt()
	{
		var cleric = Put(me, "3/3", "Lifelink");

		Attack(cleric);
		ToSecondMain();

		Assert.That(s.GetPlayer(me).Life, Is.EqualTo(startingLife + 3));
		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 3));
	}

	[Test]
	public void LethalCombatDamage_EndsTheGame()
	{
		var giant = Put(me, "20/20");

		Attack(giant);
		s = TestGames.PassUntil(s, x => x.IsOver());

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	[Test]
	public void ABlockerWhoseAttackerNoLongerExists_DealsNoDamageAndNothingThrows()
	{
		var token = Put(me, "1/1");
		s = s.UpdateObject(token, s.GetCard(token) with { IsToken = true }); // TEST SETUP
		var other = Put(me, "1/1"); // still attacking, so the regular damage step happens
		var striker = Put(them, "2/2", "Double strike");

		Attack(token, other);
		Block((striker, token));
		ToSecondMain(); // first strike kills the token; it ceases to exist before regular damage

		Assert.That(s.HasObject(token), Is.False);
		Assert.That(s.GetPlayer(them).Life, Is.EqualTo(startingLife - 1), "the other attacker hit");
	}
}
