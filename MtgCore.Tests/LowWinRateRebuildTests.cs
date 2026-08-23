using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The four cards reworked after the 140 000-game run: Platinum Angel narrowed, Barrage of
/// Expendables and Evolutionary Leap converted from sacrifice outlets to death payoffs, and
/// Molten Vortex re-rated.
///
/// Barrage and Leap were bottom-of-model cards, and the cause was never the rate.
/// MtgActionGenerator offers exactly ONE sacrifice payment per cost, so both cards asked the AI to
/// give up a creature — which StateEvaluator prices at 3.0 plus 2.0 per power plus race pressure —
/// for an effect worth far less. The search correctly refused, forever, and both measured as
/// blanks. Converting them to triggers removes the decision the AI was bad at.
///
/// Every test asserts the CONSEQUENCE on the board or in a zone. "It resolved without throwing" is
/// exactly what nine inert white cards once passed.
/// </summary>
[TestFixture]
public class LowWinRateRebuildTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static Card Find(string name) =>
		CoresetCube.Cards.First(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	// ===== PLATINUM ANGEL — life only, decking still kills =====

	[Test]
	public void PlatinumAngel_StopsTheLifeLoss()
	{
		var state = WithAngel(_state, _ids.Player1Id);
		state = SetLife(state, _ids.Player1Id, -5);

		state = Settle(state);

		Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.False);
	}

	/// <summary>
	/// The narrowing, and the whole reason for the change. A blanket "you can't lose" left an
	/// unanswered Angel with no way to lose at all, so those games ran to the harness cutoff —
	/// 11.8% of games with it on board were flagged draws against a 4.0% base rate. Decking is the
	/// one clock the board cannot interact with.
	/// </summary>
	[Test]
	public void PlatinumAngel_DoesNotStopDecking()
	{
		// CreateForTesting disables the decking rule (hand-built states leave libraries empty, and
		// with it live that alone would decide every test). This test is about decking, so it has
		// to be switched back on.
		var game = _state.GetGame(_ids.GameId);
		var state = _state.UpdateObject(game.Id, game with { DeckingLossEnabled = true });
		state = WithAngel(state, _ids.Player1Id);

		var player = state.GetPlayer(_ids.Player1Id);
		state = state.UpdateObject(
			_ids.Player1Id,
			player with
			{
				AttemptedDrawFromEmptyLibrary = true,
			}
		);

		state = Settle(state);

		Assert.That(
			state.GetPlayer(_ids.Player1Id).HasLost,
			Is.True,
			"decking must still kill, or the game has no guaranteed ending"
		);
	}

	[Test]
	public void PlatinumAngel_LifeLossLandsOnceTheAngelLeaves()
	{
		var state = WithAngel(_state, _ids.Player1Id, out var angelId);
		state = SetLife(state, _ids.Player1Id, -5);
		state = Settle(state);

		Assert.That(state.GetPlayer(_ids.Player1Id).HasLost, Is.False, "suppressed while it lives");

		var graveyard = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		state = state.MoveCardTracked(angelId, graveyard);
		state = Settle(state);

		Assert.That(
			state.GetPlayer(_ids.Player1Id).HasLost,
			Is.True,
			"the waiting loss must land the moment the Angel goes"
		);
	}

	// ===== BARRAGE OF EXPENDABLES — death payoff =====

	[Test]
	public void BarrageOfExpendables_DealsDamageWhenYourCreatureDies()
	{
		var state = PutOnBattlefield(_state, Find("Barrage of Expendables"), _ids.Player1Id);
		(state, var fodder) = AddCreature(state, _ids.Player1Id, 1, 1);

		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;

		(state, _) = state
			.AddAction(new DestroyCreatureAction { TargetIds = ImmutableList.Create(fodder.Id) })
			.ProcessAllActions();

		Assert.That(
			state.GetPlayer(_ids.Player2Id).Life,
			Is.LessThan(lifeBefore),
			"a death must convert into damage with no activation at all"
		);
	}

	[Test]
	public void BarrageOfExpendables_IsATriggerNotAnOutlet()
	{
		var card = Find("Barrage of Expendables");

		Assert.Multiple(() =>
		{
			Assert.That(card.GetComponents<TriggeredAbilityComponent>(), Is.Not.Empty);
			Assert.That(
				card.GetComponents<ActivatedAbilityComponent>(),
				Is.Empty,
				"the sacrifice outlet is what the AI would never use — see the class note"
			);
		});
	}

	// ===== EVOLUTIONARY LEAP — death payoff, strictly cheaper replacement =====

	[Test]
	public void EvolutionaryLeap_PutsACheaperCreatureOntoTheBattlefield()
	{
		var state = PutOnBattlefield(_state, Find("Evolutionary Leap"), _ids.Player1Id);
		(state, var dying) = AddCreature(state, _ids.Player1Id, 3, 3, manaCost: 5);

		// Two candidates under the cap; the more expensive one must be chosen.
		state = AddToLibrary(state, _ids.Player1Id, "Small", manaCost: 1);
		state = AddToLibrary(state, _ids.Player1Id, "Big", manaCost: 4);
		state = AddToLibrary(state, _ids.Player1Id, "TooBig", manaCost: 6);

		(state, _) = state
			.AddAction(new DestroyCreatureAction { TargetIds = ImmutableList.Create(dying.Id) })
			.ProcessAllActions();

		var battlefield = BattlefieldNames(state, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(battlefield, Does.Contain("Big"), "best creature under the cap");
			Assert.That(battlefield, Does.Not.Contain("TooBig"), "6 is not less than 5");
		});
	}

	/// <summary>
	/// An unbounded death payoff next to any sacrifice outlet is the free-repeatable shape that
	/// hangs the search — see PermanentCardBuilder.WithEquip for what that costs.
	/// </summary>
	[Test]
	public void EvolutionaryLeap_TriggersAtMostOncePerTurn()
	{
		var trigger = Find("Evolutionary Leap").GetComponents<TriggeredAbilityComponent>().Single();

		Assert.That(trigger.MaxTriggersPerTurn, Is.EqualTo(1));
	}

	// ===== MOLTEN VORTEX =====

	[Test]
	public void MoltenVortex_DealsThree()
	{
		var state = PutOnBattlefield(_state, Find("Molten Vortex"), _ids.Player1Id);
		state = AddLandToHand(state, _ids.Player1Id);

		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;
		var vortexId = BattlefieldCards(state, _ids.Player1Id)
			.First(c => c.Name == "Molten Vortex")
			.Id;

		var payment = state
			.GetCardsInZone(state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand))
			.First(c => c.HasSubtype("Land"))
			.Id;

		(state, _) = state
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = vortexId,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
					AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(payment)
					),
				}
			)
			.ProcessAllActions();

		Assert.That(state.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(lifeBefore - 3));
	}

	// ===== helpers =====

	private GameState WithAngel(GameState state, int playerId) => WithAngel(state, playerId, out _);

	private GameState WithAngel(GameState state, int playerId, out int angelId)
	{
		var result = PutOnBattlefield(state, Find("Platinum Angel"), playerId);
		angelId = BattlefieldCards(result, playerId).First(c => c.Name == "Platinum Angel").Id;
		return result;
	}

	private GameState PutOnBattlefield(GameState state, Card card, int playerId)
	{
		var (result, _) = state.AddObject(
			card with
			{
				OwnerId = playerId,
				ControllerId = playerId,
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Battlefield)
		);
		return result;
	}

	private static GameState SetLife(GameState state, int playerId, int life) =>
		state.UpdateObject(playerId, state.GetPlayer(playerId) with { Life = life });

	private GameState Settle(GameState state)
	{
		var (result, _) = state
			.AddAction(
				new CheckStateBasedEffectsAction
				{
					GameId = _ids.GameId,
					Player1Id = _ids.Player1Id,
					Player2Id = _ids.Player2Id,
				}
			)
			.ProcessAllActions();
		return result;
	}

	private (GameState, Card) AddCreature(
		GameState state,
		int playerId,
		int power,
		int toughness,
		int manaCost = 2
	) =>
		state.AddObject(
			new Card
			{
				Name = "Fodder",
				ManaCost = manaCost,
				OwnerId = playerId,
				ControllerId = playerId,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = power, Toughness = toughness }
				),
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Battlefield)
		);

	private static GameState AddToLibrary(GameState state, int playerId, string name, int manaCost)
	{
		var (result, _) = state.AddObject(
			new Card
			{
				Name = name,
				ManaCost = manaCost,
				Types = CardType.Creature,
				OwnerId = playerId,
				ControllerId = playerId,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 1, Toughness = 1 }
				),
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Library)
		);
		return result;
	}

	private static GameState AddLandToHand(GameState state, int playerId)
	{
		var (result, _) = state.AddObject(
			new Card
			{
				Name = "Plains",
				OwnerId = playerId,
				ControllerId = playerId,
				Subtypes = ["Land", "Basic"],
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Hand)
		);
		return result;
	}

	private static IEnumerable<Card> BattlefieldCards(GameState state, int playerId) =>
		state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Battlefield));

	private static IEnumerable<string> BattlefieldNames(GameState state, int playerId) =>
		BattlefieldCards(state, playerId).Select(c => c.Name);
}
