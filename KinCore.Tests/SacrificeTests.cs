using System.Collections.Immutable;
using KinCore;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// Sacrifice, Devour, and a rite that targets the lane you drop it on.
///
/// Cards are defined INLINE, never loaded from content, and **every test asserts the CONSEQUENCE**
/// — that something DIED, that a reader was paid, that the lane you chose is the one that emptied.
/// "The effect was declared" is exactly what a silent no-op also looks like.
/// </summary>
public class SacrificeTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static KinEffect OnPlay(KinTarget target, GameAction template) =>
		new()
		{
			Trigger = EffectTrigger.OnPlay,
			Target = target,
			Template = template,
		};

	private static (GameState, int) AddRite(GameState state, params KinEffect[] effects)
	{
		var (s, added) = state.AddObject(
			new KinCard
			{
				Name = "Rite",
				Cost = 0,
				RunCardId = 1,
				Effects = [.. effects],
			},
			state.ZoneId(ZoneType.Hand)
		);
		return (s, added.Id);
	}

	private static (GameState, int) AddUnit(
		GameState state,
		int lane,
		ZoneType zone = ZoneType.Field,
		bool devours = false,
		params KinEffect[] effects
	)
	{
		var card = new KinCard
		{
			Name = "Body",
			Cost = 0,
			RunCardId = 2,
			Devours = devours,
			Effects = [.. effects],
		};

		card = (KinCard)
			card.WithComponent(
				new UnitComponent
				{
					Power = 2,
					Toughness = 4,
					Lane = lane,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(zone));
		return (s, added.Id);
	}

	private static GameState Battle() =>
		KinBattleFactory.Create(opponentHealth: 500);

	[Test]
	public void SacrificingAUnitKillsItRatherThanRemovingIt()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 2);
		(state, var riteId) = AddRite(
			state,
			OnPlay(KinTarget.UnitInSourceLane, new DestroyAction())
		);

		(state, _) = state.BeginBattle();
		(state, var events) = Do(state, new PlayCardAction { CardId = riteId, Lane = 2 });

		Assert.Multiple(() =>
		{
			Assert.That(state.UnitInLane(2), Is.Null, "the lane it was standing in is empty");
			Assert.That(
				state.GetBattle().DiedThisTurnRunCardIds,
				Has.Count.EqualTo(1),
				"it DIED — withdrawing or being discarded would record nothing, and every Loss "
					+ "reader in the pool would go unpaid"
			);
			Assert.That(events.OfType<UnitDiedEvent>().Any(), Is.True);
		});
	}

	/// <summary>
	/// The reason `CountOf.DiedThisTurn` exists: a sacrifice has to pay in the turn you make it,
	/// or every sacrifice card is a setup for next turn and the combo never happens.
	/// </summary>
	[Test]
	public void ASacrificePaysAReaderInTheSameTurn()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 0);
		(state, var riteId) = AddRite(
			state,
			OnPlay(KinTarget.UnitInSourceLane, new DestroyAction()),
			OnPlay(
				KinTarget.Opponent,
				new DealDamageAction { Amount = 7, PerEach = CountOf.DiedThisTurn }
			)
		);

		(state, _) = state.BeginBattle();
		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 0 });

		Assert.That(
			before - state.GetOpponent().Health,
			Is.EqualTo(7),
			"the destroy resolves before the payoff reads the count — if it did not, this is 0 "
				+ "and the card looks exactly like one that worked"
		);
	}

	[Test]
	public void ADrawScalesWithWhatDied()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 0);
		(state, _) = AddUnit(state, lane: 1);
		(state, var riteId) = AddRite(
			state,
			OnPlay(KinTarget.YourUnits, new DestroyAction()),
			OnPlay(
				KinTarget.Player,
				new DrawCardsAction { Amount = 1, PerEach = CountOf.DiedThisTurn }
			)
		);

		(state, _) = state.BeginBattle();
		var before = state.CardsIn(ZoneType.Hand).Count();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 0 });

		Assert.That(
			state.CardsIn(ZoneType.Hand).Count() - (before - 1),
			Is.EqualTo(2),
			"two died, so two are drawn — DrawCardsAction ignored PerEach entirely until this"
		);
	}

	/// <summary>
	/// The lane-targeting unlock: a rite dropped on a lane acts on THAT lane. Two units are on the
	/// board and only the one you chose may die, or "the lane is the choice" is a fiction.
	/// </summary>
	[Test]
	public void ARiteActsOnTheLaneItWasDroppedOn()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 0);
		(state, _) = AddUnit(state, lane: 3);
		(state, var riteId) = AddRite(
			state,
			OnPlay(KinTarget.UnitInSourceLane, new DestroyAction())
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 3 });

		Assert.Multiple(() =>
		{
			Assert.That(state.UnitInLane(3), Is.Null, "the lane you dropped it on");
			Assert.That(state.UnitInLane(0), Is.Not.Null, "and nothing else");
		});
	}

	[Test]
	public void TheCompanionCannotBeSacrificed()
	{
		var run = StarterContent.NewRun();
		var (state, _) = run.StartBattle(
			enemies: [],
			opponentHealth: 500
		);
		(state, var riteId) = AddRite(state, OnPlay(KinTarget.YourUnits, new DestroyAction()));

		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 0 });

		Assert.That(
			state.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"the one thing no doom can take is not taken by your own card either"
		);
	}

	/// <summary>
	/// Devour is one bit of difference from an ordinary overwrite — whether a death happened — and
	/// that bit is the entire keyword. Asserted by an OnDeath effect firing, because "the card is
	/// no longer in the lane" is true of both cases.
	/// </summary>
	[Test]
	public void DevourKillsTheUnitItReplaces()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 1, effects: OnDeathHits());
		(state, var eaterId) = AddUnit(state, lane: 1, zone: ZoneType.Hand, devours: true);

		(state, _) = state.BeginBattle();
		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new PlayCardAction { CardId = eaterId, Lane = 1 });

		Assert.Multiple(() =>
		{
			Assert.That(
				before - state.GetOpponent().Health,
				Is.EqualTo(9),
				"its death fired — a discard would have fired nothing"
			);
			Assert.That(state.GetBattle().DiedThisTurnRunCardIds, Has.Count.EqualTo(1));
			Assert.That(state.UnitInLane(1)!.Id, Is.EqualTo(eaterId), "and the eater is standing");
		});
	}

	[Test]
	public void AnOrdinaryOverwriteIsStillNotADeath()
	{
		var state = Battle();
		(state, _) = AddUnit(state, lane: 1, effects: OnDeathHits());
		(state, var plainId) = AddUnit(state, lane: 1, zone: ZoneType.Hand);

		(state, _) = state.BeginBattle();
		var before = state.GetOpponent().Health;
		(state, _) = Do(state, new PlayCardAction { CardId = plainId, Lane = 1 });

		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetOpponent().Health,
				Is.EqualTo(before),
				"replaced is not dead — the rule Devour exists to break"
			);
			Assert.That(state.GetBattle().DiedThisTurnRunCardIds, Is.Empty);
		});
	}

	[Test]
	public void AUnitThatReturnsOnDeathIsBackInYourHand()
	{
		var state = Battle();
		(state, _) = AddUnit(
			state,
			lane: 4,
			effects: new KinEffect
			{
				Trigger = EffectTrigger.OnDeath,
				Target = KinTarget.Self,
				Template = new ReturnToHandAction(),
			}
		);
		(state, var riteId) = AddRite(
			state,
			OnPlay(KinTarget.UnitInSourceLane, new DestroyAction())
		);

		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new PlayCardAction { CardId = riteId, Lane = 4 });

		Assert.That(
			state.CardsIn(ZoneType.Hand).Any(c => c.Name == "Body"),
			Is.True,
			"sacrificed, and back in hand to be spent again"
		);
	}

	private static KinEffect OnDeathHits() =>
		new()
		{
			Trigger = EffectTrigger.OnDeath,
			Target = KinTarget.Opponent,
			Template = new DealDamageAction { Amount = 9 },
		};
}
