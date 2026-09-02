using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// <see cref="UnexhaustCreatureAction"/> — the engine's untap effect, and the primitive that makes
/// every untap-trading combo expressible. Before it existed, only <see cref="StartTurnAction"/>
/// could clear <c>IsExhausted</c>, so Splinter Twin, Kiki-Jiki and "untap your mana creature" were
/// all structurally unreachable and Manifold Key's ability was cut as unimplementable.
///
/// Cards are built inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class UnexhaustTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void ItReadiesAnExhaustedCreature()
	{
		var (s, bear) = AddCreature(_state, "Bear");
		s = Exhaust(s, bear.Id);
		Assert.That(IsExhausted(s, bear.Id), Is.True, "precondition: it was exhausted");

		var after = Unexhaust(s, bear.Id);

		Assert.That(IsExhausted(after, bear.Id), Is.False);
	}

	/// <summary>
	/// **A frozen creature does not ready, and this is the assertion that matters most.**
	///
	/// The freeze rule lives on <see cref="CreatureComponent.IsFrozen"/> rather than being restated
	/// in the action, because an untap effect that ignored it would silently undo Dungeon Geists
	/// and every "doesn't ready" clause in the game — with nothing erroring and no visible symptom
	/// beyond a creature that quietly refuses to stay down.
	/// </summary>
	[Test]
	public void ItDoesNotReadyAFrozenCreature()
	{
		var (s, bear) = AddCreature(_state, "Bear");
		s = Exhaust(s, bear.Id, freezeTurns: 1);

		var after = Unexhaust(s, bear.Id);

		Assert.That(IsExhausted(after, bear.Id), Is.True);
	}

	[Test]
	public void ItDoesNotReadyACreatureFrozenBySource()
	{
		var (s, geists) = AddCreature(_state, "Geists");
		var (s2, bear) = AddCreature(s, "Bear");
		s2 = Exhaust(s2, bear.Id, freezeBySourceId: geists.Id);

		var after = Unexhaust(s2, bear.Id);

		Assert.That(IsExhausted(after, bear.Id), Is.True);
	}

	/// <summary>
	/// Readying is not vigilance. A creature that has already swung stays spent, so a mass-ready
	/// effect cannot be a second combat step by implication — the same separation
	/// <see cref="CreatureComponent.IsExhausted"/> keeps from <c>HasAttacked</c>.
	/// </summary>
	[Test]
	public void ItDoesNotClearHasAttacked()
	{
		var (s, bear) = AddCreature(_state, "Bear");
		var card = (Card)s.GetObject(bear.Id);
		var creature = card.GetComponent<CreatureComponent>()!;
		s = s.UpdateObject(
			bear.Id,
			card.WithComponentReplaced(creature with { IsExhausted = true, HasAttacked = true })
		);

		var after = Unexhaust(s, bear.Id);
		var result = ((Card)after.GetObject(bear.Id)).GetComponent<CreatureComponent>()!;

		Assert.Multiple(() =>
		{
			Assert.That(result.IsExhausted, Is.False, "it readied");
			Assert.That(result.HasAttacked, Is.True, "but it has still attacked this turn");
		});
	}

	/// <summary>
	/// **The combo shape this primitive exists for**, asserted end to end: a creature with a
	/// once-per-turn tap ability can be readied and used again in the same turn. Without the
	/// action there is no way to reach the second activation at all.
	/// </summary>
	[Test]
	public void AReadiedCreatureCanUseItsTapAbilityAgainInTheSameTurn()
	{
		var (s, dork) = AddCreature(_state, "Mana Dork");

		// Standing in for "exhaust: add mana" — the state a tap cost leaves behind.
		s = Exhaust(s, dork.Id);
		Assert.That(IsExhausted(s, dork.Id), Is.True, "precondition: the ability was used");

		s = Unexhaust(s, dork.Id);
		Assert.That(IsExhausted(s, dork.Id), Is.False, "it can pay the tap cost again");

		s = Exhaust(s, dork.Id);
		Assert.That(IsExhausted(s, dork.Id), Is.True, "and paying it exhausts it once more");
	}

	// ===== helpers =====

	private static bool IsExhausted(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()!.IsExhausted;

	private static GameState Exhaust(
		GameState state,
		int cardId,
		int freezeTurns = 0,
		int freezeBySourceId = 0
	)
	{
		var action = new ExhaustCreatureAction
		{
			TargetIds = ImmutableList.Create(cardId),
			FreezeTurns = freezeTurns,
			FreezeWhileSourceRemains = freezeBySourceId != 0,
		};

		if (freezeBySourceId != 0)
			action = action with
			{
				InputContext = action.InputContext.SetItem(
					ContextKeys.SourceCardId,
					freezeBySourceId
				),
			};

		var (next, _) = state.AddAction(action).ProcessAllActions();
		return next;
	}

	private static GameState Unexhaust(GameState state, int cardId)
	{
		var (next, _) = state
			.AddAction(new UnexhaustCreatureAction { TargetIds = ImmutableList.Create(cardId) })
			.ProcessAllActions();
		return next;
	}

	private (GameState, Card) AddCreature(GameState state, string name)
	{
		var (next, placed) = state.AddObject(
			new Card
			{
				Name = name,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 2,
						Toughness = 2,
						HasSummoningSickness = false,
					}
				),
			},
			parentId: _ids.Player1BattlefieldId
		);
		return (next, (Card)placed);
	}
}
