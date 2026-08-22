using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// Persistent freeze, conditional Taunt, clone, extra turns, gain control and "becomes a 1/1".
/// Cards are defined inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class BlueMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== FREEZE =====

	[Test]
	public void PlainExhaust_ClearsAtTheOwnersNextTurn()
	{
		// The control for the freeze tests below — proves the extra turn comes from FrozenTurns.
		var (s, victim) = AddCreature(_state, "Bear", 2, 2, _ids.Player2Id);

		var (exhausted, _) = s.AddAction(
				new ExhaustCreatureAction { TargetIds = ImmutableList.Create(victim.Id) }
			)
			.ProcessAllActions();

		var afterUntap = StartTurnFor(exhausted, _ids.Player2Id);

		Assert.That(IsExhausted(afterUntap, victim.Id), Is.False);
	}

	/// <summary>
	/// A card whose text is "exhaust target creature" has to cost the opponent an attack.
	///
	/// Plain exhaust — the ACTION above — is correct as a COST (convoke, tap-to-activate) but is a
	/// complete no-op as an EFFECT, and the builder verb only ever aims at opponents' creatures.
	/// You can act only on your own turn (no priority, no instant speed), and the exhaustion
	/// clears at the start of theirs, which is before they attack. So it costs them nothing at
	/// all. Real Magic gets away with a plain tapper because you cast it during THEIR turn.
	///
	/// Every one of the eight cards built on WithExhaust documented the opposite in its comment —
	/// Gideon's Lawkeeper "costs its controller exactly one attack", Gideon Jura "cannot attack at
	/// all next turn" — and none of them did it. Missing one untap step is what that sentence
	/// means here.
	/// </summary>
	[Test]
	public void ExhaustEffect_CostsTheOpponentTheirNextAttack()
	{
		var (s, victim) = AddCreature(_state, "Bear", 2, 2, _ids.Player2Id);

		var tapper = CardFactory.Sorcery("Test Tapper", manaCost: 1).WithExhaust().Build();
		var handId = s.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = s.AddObject(
			tapper with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var (cast, _) = withCard
			.AddAction(
				new CastSpellAction
				{
					CardId = card.Id,
					CastingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(victim.Id)
					),
				}
			)
			.ProcessAllActions();

		Assert.That(IsExhausted(cast, victim.Id), Is.True, "precondition: it was exhausted");

		// Their turn begins — the untap step the effect is supposed to make it miss.
		var afterTheirUntap = StartTurnFor(cast, _ids.Player2Id);

		Assert.That(
			IsExhausted(afterTheirUntap, victim.Id),
			Is.True,
			"an exhaust EFFECT must survive the opponent's untap step or it does nothing"
		);
	}

	[Test]
	public void Freeze_SurvivesExactlyOneUntapStep()
	{
		var (s, victim) = AddCreature(_state, "Bear", 2, 2, _ids.Player2Id);

		var (frozen, _) = s.AddAction(
				new ExhaustCreatureAction
				{
					FreezeTurns = 1,
					TargetIds = ImmutableList.Create(victim.Id),
				}
			)
			.ProcessAllActions();

		var afterFirst = StartTurnFor(frozen, _ids.Player2Id);
		Assert.That(
			IsExhausted(afterFirst, victim.Id),
			Is.True,
			"Doesn't untap during its controller's NEXT untap step"
		);

		var afterSecond = StartTurnFor(afterFirst, _ids.Player2Id);
		Assert.That(IsExhausted(afterSecond, victim.Id), Is.False, "But it untaps the turn after");
	}

	[Test]
	public void SourceLinkedFreeze_HoldsUntilTheSourceLeaves()
	{
		var (s1, source) = AddCreature(_state, "Dungeon Geists", 3, 3, _ids.Player1Id);
		var (s2, victim) = AddCreature(s1, "Bear", 2, 2, _ids.Player2Id);

		var (frozen, _) = s2.AddAction(
				new ExhaustCreatureAction
				{
					FreezeWhileSourceRemains = true,
					TargetIds = ImmutableList.Create(victim.Id),
					InputContext = ImmutableDictionary<string, object>.Empty.Add(
						ContextKeys.SourceCardId,
						source.Id
					),
				}
			)
			.ProcessAllActions();

		var afterTwoTurns = StartTurnFor(StartTurnFor(frozen, _ids.Player2Id), _ids.Player2Id);
		Assert.That(
			IsExhausted(afterTwoTurns, victim.Id),
			Is.True,
			"A source-linked freeze has no turn limit"
		);

		// Kill the source; the lock lifts.
		var (released, _) = afterTwoTurns
			.AddAction(new DestroyPermanentAction { TargetIds = ImmutableList.Create(source.Id) })
			.ProcessAllActions();

		var afterRelease = StartTurnFor(released, _ids.Player2Id);
		Assert.That(
			IsExhausted(afterRelease, victim.Id),
			Is.False,
			"Killing the source frees the creature at its next untap"
		);
	}

	// ===== FOG BANK'S CONDITIONAL TAUNT =====

	[Test]
	public void TauntUntilAttacked_DropsTauntAfterTheFirstAttack()
	{
		// Otherwise a damage-immune Taunt wall is unanswerable: every attack is compelled into it
		// forever and there is no going wide in this engine.
		var (s1, wall) = AddCreature(_state, "Fog Bank", 0, 2, _ids.Player2Id, fogBank: true);
		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);
		var (s3, second) = AddCreature(s2, "Second Bear", 2, 2, _ids.Player1Id);

		Assert.That(s3.GetEffectiveStats(wall.Id).HasTaunt, Is.True, "Taunt is on before combat");

		var (afterAttack, _) = s3.AddAction(Attack(attacker.Id, wall.Id)).ProcessAllActions();

		Assert.That(
			afterAttack.GetEffectiveStats(wall.Id).HasTaunt,
			Is.False,
			"Taunt lapses once it has soaked an attack"
		);

		// The second creature can now attack the player directly.
		var (_, success) = afterAttack.TryAddAction(Attack(second.Id, _ids.Player2Id));
		Assert.That(success, Is.True);
	}

	[Test]
	public void FogBank_TakesAndDealsNoCombatDamage()
	{
		var (s1, wall) = AddCreature(_state, "Fog Bank", 0, 2, _ids.Player2Id, fogBank: true);
		var (s2, attacker) = AddCreature(s1, "Giant", 5, 5, _ids.Player1Id);

		var (final, _) = s2.AddAction(Attack(attacker.Id, wall.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(wall.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"5 damage does not kill a damage-immune wall"
		);
		Assert.That(Damage(final, wall.Id), Is.EqualTo(0));
	}

	[Test]
	public void TauntComesBack_TheFollowingTurn()
	{
		var (s1, wall) = AddCreature(_state, "Fog Bank", 0, 2, _ids.Player2Id, fogBank: true);
		var (s2, attacker) = AddCreature(s1, "Bear", 2, 2, _ids.Player1Id);

		var (afterAttack, _) = s2.AddAction(Attack(attacker.Id, wall.Id)).ProcessAllActions();
		var nextTurn = StartTurnFor(afterAttack, _ids.Player2Id);

		Assert.That(
			nextTurn.GetEffectiveStats(wall.Id).HasTaunt,
			Is.True,
			"It soaks one attack EACH turn, not one ever"
		);
	}

	// ===== CLONE =====

	[Test]
	public void Clone_CopiesTheBiggestCreatureOnTheBoard()
	{
		var (s1, _) = AddCreature(_state, "Small", 1, 1, _ids.Player1Id);
		var (s2, _) = AddCreature(s1, "Huge", 6, 6, _ids.Player2Id);

		var clone = CardFactory
			.Creature("Clone", manaCost: 4, power: 0, toughness: 0)
			.WithComponent(new CopyOnEnterComponent())
			.Build();

		var (s3, cloneCard) = s2.AddObject(
			clone with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: s2.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (final, _) = s3.AddAction(
				new CastCreatureAction { CardId = cloneCard.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var copied = (Card)final.GetObject(cloneCard.Id);

		Assert.That(copied.Name, Is.EqualTo("Huge"));
		Assert.That(final.GetEffectivePower(cloneCard.Id), Is.EqualTo(6));
		Assert.That(
			copied.ControllerId,
			Is.EqualTo(_ids.Player1Id),
			"A Clone of the opponent's creature is still yours"
		);
	}

	[Test]
	public void Clone_WithNothingToCopy_DiesToZeroToughness()
	{
		// Printed 0/0, so an empty board kills it — exactly what the real card does.
		var clone = CardFactory
			.Creature("Clone", manaCost: 4, power: 0, toughness: 0)
			.WithComponent(new CopyOnEnterComponent())
			.Build();

		var (s, cloneCard) = _state.AddObject(
			clone with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (final, _) = s.AddAction(
				new CastCreatureAction { CardId = cloneCard.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.That(final.GetCardZone(cloneCard.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	// ===== EXTRA TURNS =====

	[Test]
	public void ExtraTurn_DoesNotPassPlayToTheOpponent()
	{
		var (queued, _) = _state.AddAction(new TakeExtraTurnAction()).ProcessAllActions();

		var (afterEnd, _) = queued.AddAction(EndTurn()).ProcessAllActions();

		Assert.That(
			afterEnd.TryGetGame()!.ActivePlayerId,
			Is.EqualTo(_ids.Player1Id),
			"The same player takes another turn"
		);
		Assert.That(afterEnd.TryGetGame()!.ExtraTurnsQueued, Is.EqualTo(0), "And it is spent");
	}

	[Test]
	public void WithoutAnExtraTurn_PlayPassesNormally()
	{
		var (afterEnd, _) = _state.AddAction(EndTurn()).ProcessAllActions();

		Assert.That(afterEnd.TryGetGame()!.ActivePlayerId, Is.EqualTo(_ids.Player2Id));
	}

	[Test]
	public void ExtraTurns_AreCapped()
	{
		// An AI that overvalues extra turns could otherwise chain them past the simulator's
		// per-turn action cutoff, and a game lost to the loop detector looks like a bug.
		var s = _state;
		for (var i = 0; i < 10; i++)
			(s, _) = s.AddAction(new TakeExtraTurnAction()).ProcessAllActions();

		Assert.That(s.TryGetGame()!.ExtraTurnsQueued, Is.EqualTo(TakeExtraTurnAction.MaxQueued));
	}

	// ===== GAIN CONTROL =====

	[Test]
	public void GainControl_MovesThePermanentAndKeepsItsOwner()
	{
		var (s, victim) = AddCreature(_state, "Stolen Bear", 2, 2, _ids.Player2Id);

		var (final, _) = s.AddAction(
				new GainControlAction
				{
					TargetIds = ImmutableList.Create(victim.Id),
					InputContext = ImmutableDictionary<string, object>.Empty.Add(
						ContextKeys.CastingPlayerId,
						_ids.Player1Id
					),
				}
			)
			.ProcessAllActions();

		var stolen = (Card)final.GetObject(victim.Id);

		Assert.That(stolen.ControllerId, Is.EqualTo(_ids.Player1Id));
		Assert.That(stolen.OwnerId, Is.EqualTo(_ids.Player2Id), "Ownership never changes");
		Assert.That(
			final.GetCardZoneId(victim.Id),
			Is.EqualTo(final.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)),
			"It must physically move — every battlefield scan works from the zone"
		);
		Assert.That(
			stolen.GetComponent<CreatureComponent>()!.HasSummoningSickness,
			Is.True,
			"A stolen creature cannot be swung with the same turn"
		);
	}

	// ===== BECOMES A 1/1 =====

	[Test]
	public void BecomesVanilla_OverridesStatsAndStripsKeywords()
	{
		var (s, victim) = AddCreature(_state, "Dragon", 6, 6, _ids.Player2Id, flying: true);

		Assert.That(s.GetEffectiveStats(victim.Id).HasFlying, Is.True);

		var (final, _) = s.AddAction(
				new AddCustomModifierAction
				{
					Modifier = new BecomesBaseCreatureComponent
					{
						Power = 1,
						Toughness = 1,
						Duration = ModifierDuration.UntilEndOfTurn,
					},
					TargetIds = ImmutableList.Create(victim.Id),
				}
			)
			.ProcessAllActions();

		var stats = final.GetEffectiveStats(victim.Id);
		Assert.That(stats.Power, Is.EqualTo(1));
		Assert.That(stats.Toughness, Is.EqualTo(1));
		Assert.That(stats.HasFlying, Is.False, "Loses all abilities");
	}

	// ===== HELPERS =====

	private AttackAction Attack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	private EndTurnAction EndTurn() =>
		new()
		{
			GameId = _ids.GameId,
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
		};

	private GameState StartTurnFor(GameState state, int playerId)
	{
		var (result, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = playerId,
					BattlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield),
					SkipDraw = true,
				}
			)
			.ProcessAllActions();
		return result;
	}

	private static bool IsExhausted(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()!.IsExhausted;

	private static int Damage(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()!.Damage;

	private static (GameState, Card) AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId,
		bool flying = false,
		bool fogBank = false
	)
	{
		var components = ImmutableArray.Create<GameComponent>(
			new PermanentComponent(),
			new CreatureComponent
			{
				Power = power,
				Toughness = toughness,
				HasSummoningSickness = false,
				HasFlying = flying,
				HasTaunt = fogBank,
			}
		);

		if (fogBank)
			components = components
				.Add(new TauntUntilAttackedComponent())
				.Add(new PreventsCombatDamageComponent());

		var card = new Card
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = components,
		};

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
