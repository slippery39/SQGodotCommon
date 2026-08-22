using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The human's casting path, which is a SEPARATE code path from the AI's.
///
/// The AI plays through MtgActionGenerator, which enumerates targets and works out additional
/// cost payments. MtgGameManager hand-built its actions instead, so anything needing either was
/// uncastable for a human while the AI played it perfectly — the exact asymmetry reported in QA
/// for Auras and for Despoiler of Souls. Nothing threw; the card just sprang back to hand.
///
/// The rule these lock in is the one already stated in MtgCore/CLAUDE.md for attack targets: a
/// presentation layer must ASK the engine, never re-derive its rules.
/// </summary>
[TestFixture]
public class MtgGameManagerCastingTests
{
	private static MtgGameManager ManagerWith(params string[] cardNames)
	{
		var cards = cardNames.Select(n => CoresetCube.Cards.Single(c => c.Name == n)).ToList();

		// Padding so both libraries can deal an opening hand and draw without decking.
		var filler = CoresetCube.Cards.Take(40).ToList();

		var manager = new MtgGameManager(
			new DeckSetupData(
				new DeckChoice.Drafted(cards.Concat(filler).ToList()),
				new DeckChoice.Drafted(filler)
			)
		);
		manager.StartGame();
		return manager;
	}

	/// <summary>Puts a named card straight into the human's hand and gives them the mana for it.</summary>
	private static int PlaceInHand(MtgGameManager manager, string name)
	{
		var card = CoresetCube.Cards.Single(c => c.Name == name);
		var handId = manager.State.GetWellKnownId(MtgObjectKeys.Player1Hand);

		var (state, placed) = manager.State.AddObject(
			card with
			{
				OwnerId = manager.HumanPlayerId,
				ControllerId = manager.HumanPlayerId,
			},
			parentId: handId
		);

		var player = state.GetPlayer(manager.HumanPlayerId);
		state = state.UpdateObject(
			manager.HumanPlayerId,
			player with
			{
				CurrentMana = 20,
				MaxMana = 20,
			}
		);

		manager.DebugSetState(state);
		return placed.Id;
	}

	private static int PlaceCreatureOnBoard(MtgGameManager manager, int controllerId)
	{
		var battlefieldId = manager.State.GetWellKnownId(
			controllerId == manager.HumanPlayerId
				? MtgObjectKeys.Player1Battlefield
				: MtgObjectKeys.Player2Battlefield
		);

		var (state, bear) = manager.State.AddObject(
			new Card
			{
				Name = "Test Bear",
				ManaCost = 2,
				OwnerId = controllerId,
				ControllerId = controllerId,
				Types = CardType.Creature,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 }
				),
			},
			parentId: battlefieldId
		);

		manager.DebugSetState(state);
		return bear.Id;
	}

	[Test]
	public void Aura_IsReportedAsNeedingATargetAndIsCastable()
	{
		var manager = ManagerWith();
		var auraId = PlaceInHand(manager, "Mark of the Vampire");
		var bearId = PlaceCreatureOnBoard(manager, manager.HumanPlayerId);

		Assert.That(
			manager.PermanentNeedsTarget(auraId),
			Is.True,
			"An Aura must be reported as needing a target, or the UI casts it with none"
		);
		Assert.That(
			manager.GetPermanentValidTargets(auraId),
			Does.Contain(bearId),
			"and the creature it can enchant must be offered"
		);

		var (success, _) = manager.CastPermanent(auraId, bearId);

		Assert.That(success, Is.True, "Casting the Aura at a legal target must succeed");
		Assert.That(manager.State.GetCardZone(auraId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	/// <summary>
	/// The original failure exactly: no target supplied, so the action never validated.
	/// </summary>
	[Test]
	public void Aura_WithNoTarget_DoesNotSilentlySucceed()
	{
		var manager = ManagerWith();
		var auraId = PlaceInHand(manager, "Mark of the Vampire");
		PlaceCreatureOnBoard(manager, manager.HumanPlayerId);

		var (success, _) = manager.CastPermanent(auraId);

		Assert.That(success, Is.False, "An untargeted Aura must fail loudly, not half-resolve");
	}

	/// <summary>
	/// A cost that wants TWO cards has to report that it wants two.
	///
	/// The scene walked costs one at a time and recorded a single payment per cost before moving
	/// on, while Validate demands the exact count — so Grim Lavamancer's "exile two cards from
	/// your graveyard" was unpayable and its ability simply could not be activated, with no error.
	/// The AI was fine, because MtgActionGenerator reads RequiredPaymentCount. The scene itself is
	/// Godot and untestable here; this pins the manager API it now asks instead of assuming 1.
	/// </summary>
	[Test]
	public void AbilityCostWantingTwoCards_ReportsThatItWantsTwo()
	{
		var manager = ManagerWith();
		var battlefieldId = manager.State.GetWellKnownId(MtgObjectKeys.Player1Battlefield);

		var lavamancer = CoresetCube.Cards.Single(c => c.Name == "Grim Lavamancer");
		var (state, card) = manager.State.AddObject(
			lavamancer with
			{
				OwnerId = manager.HumanPlayerId,
				ControllerId = manager.HumanPlayerId,
			},
			parentId: battlefieldId
		);
		manager.DebugSetState(state);

		Assert.That(
			manager.AbilityHasAdditionalCostSelection(card.Id, 0),
			Is.True,
			"precondition: the ability has a selection cost"
		);
		Assert.That(
			manager.GetAbilityAdditionalCostRequiredPayments(card.Id, 0, 0),
			Is.EqualTo(2),
			"exiling TWO cards is one cost needing two selections, not one"
		);
	}

	/// <summary>
	/// Despoiler of Souls exiles two creature cards from your graveyard to come back. The manager
	/// supplied no AdditionalCostPayments, so the action failed validation every time and the
	/// card could never be recurred by a human.
	/// </summary>
	[Test]
	public void GraveyardRecursion_PaysItsAdditionalCost()
	{
		var manager = ManagerWith();
		var graveyardId = manager.State.GetWellKnownId(MtgObjectKeys.Player1Graveyard);
		var state = manager.State;

		var despoiler = CoresetCube.Cards.Single(c => c.Name == "Despoiler of Souls");
		var (withDespoiler, despoilerCard) = state.AddObject(
			despoiler with
			{
				OwnerId = manager.HumanPlayerId,
				ControllerId = manager.HumanPlayerId,
			},
			parentId: graveyardId
		);
		state = withDespoiler;

		for (var i = 0; i < 2; i++)
		{
			(state, _) = state.AddObject(
				new Card
				{
					Name = $"Dead Bear {i}",
					ManaCost = 2,
					OwnerId = manager.HumanPlayerId,
					ControllerId = manager.HumanPlayerId,
					Types = CardType.Creature,
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent { Power = 2, Toughness = 2 }
					),
				},
				parentId: graveyardId
			);
		}

		var player = state.GetPlayer(manager.HumanPlayerId);
		state = state.UpdateObject(
			manager.HumanPlayerId,
			player with
			{
				CurrentMana = 20,
				MaxMana = 20,
			}
		);
		manager.DebugSetState(state);

		var (success, _) = manager.CastFromGraveyard(
			despoilerCard.Id,
			ImmutableDictionary<int, ImmutableList<int>>.Empty
		);

		Assert.That(success, Is.True, "Despoiler should be castable from the graveyard");
		Assert.That(
			manager.State.GetCardZone(despoilerCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield)
		);
	}
}
