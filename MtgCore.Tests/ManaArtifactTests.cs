using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests for Phase 2 mana artifacts: Mox and Sol Ring.
///
/// Both cards are non-creature permanents that produce mana via a once-per-turn
/// activated ability (tap proxied as HasActivated). Key behaviors:
///   - Cards enter the battlefield via CastPermanentAction
///   - Activated ability adds the correct amount of temporary mana
///   - Ability can only be used once per turn (HasActivated gate)
///   - HasActivated resets at the start of the controller's next turn
///   - No summoning sickness restriction (no CreatureComponent)
/// </summary>
[TestFixture]
public class ManaArtifactTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== MOX =====

	[Test]
	public void Mox_EntersBattlefield()
	{
		var (state, moxId) = CastFromHand(CardLibrary.Mox());

		Assert.That(state.GetCardZone(moxId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Mox_AddsOneMana()
	{
		var (state, moxId) = CastFromHand(CardLibrary.Mox());
		var manaBefore = state.GetPlayer(_ids.Player1Id).CurrentMana;

		var (finalState, _) = state.AddAction(MakeActivate(moxId)).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(manaBefore + 1));
	}

	[Test]
	public void Mox_CanOnlyActivateOncePerTurn()
	{
		var (state, moxId) = CastFromHand(CardLibrary.Mox());
		var (afterFirst, _) = state.AddAction(MakeActivate(moxId)).ProcessAllActions();

		var (_, success) = afterFirst.TryAddAction(MakeActivate(moxId));

		Assert.That(success, Is.False);
	}

	[Test]
	public void Mox_ResetsAfterTurnEnd()
	{
		var (state, moxId) = CastFromHand(CardLibrary.Mox());
		var (afterActivation, _) = state.AddAction(MakeActivate(moxId)).ProcessAllActions();
		var afterTurn = SimulateNextTurn(afterActivation);

		var (_, success) = afterTurn.TryAddAction(MakeActivate(moxId));

		Assert.That(success, Is.True);
	}

	[Test]
	public void Mox_ActivationAvailableImmediately_NoSummoningSickness()
	{
		var (state, moxId) = CastFromHand(CardLibrary.Mox());

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, _ids.Player1Id);

		Assert.That(actions.OfType<ActivateAbilityAction>().Any(a => a.CardId == moxId), Is.True);
	}

	// ===== SOL RING =====

	[Test]
	public void SolRing_EntersBattlefield()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());

		Assert.That(state.GetCardZone(solRingId).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void SolRing_AddsTwoMana()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());
		var manaBefore = state.GetPlayer(_ids.Player1Id).CurrentMana;

		var (finalState, _) = state.AddAction(MakeActivate(solRingId)).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(manaBefore + 2));
	}

	[Test]
	public void SolRing_CanOnlyActivateOncePerTurn()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());
		var (afterFirst, _) = state.AddAction(MakeActivate(solRingId)).ProcessAllActions();

		var (_, success) = afterFirst.TryAddAction(MakeActivate(solRingId));

		Assert.That(success, Is.False);
	}

	[Test]
	public void SolRing_ResetsAfterTurnEnd()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());
		var (afterActivation, _) = state.AddAction(MakeActivate(solRingId)).ProcessAllActions();
		var afterTurn = SimulateNextTurn(afterActivation);

		var (_, success) = afterTurn.TryAddAction(MakeActivate(solRingId));

		Assert.That(success, Is.True);
	}

	[Test]
	public void SolRing_ActivationAvailableImmediately_NoSummoningSickness()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, _ids.Player1Id);

		Assert.That(
			actions.OfType<ActivateAbilityAction>().Any(a => a.CardId == solRingId),
			Is.True
		);
	}

	[Test]
	public void SolRing_ManaIsTemporary_DoesNotAffectMaxMana()
	{
		var (state, solRingId) = CastFromHand(CardLibrary.SolRing());
		var maxManaBefore = state.GetPlayer(_ids.Player1Id).MaxMana;

		var (finalState, _) = state.AddAction(MakeActivate(solRingId)).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).MaxMana, Is.EqualTo(maxManaBefore));
	}

	// ===== HELPERS =====

	private (GameState state, int cardId) CastFromHand(Card template)
	{
		var card = template with { OwnerId = _ids.Player1Id, ControllerId = _ids.Player1Id };
		var (stateWithCard, added) = _state.AddObject(card, parentId: _ids.Player1HandId);
		var (resolved, _) = stateWithCard
			.AddAction(
				new CastPermanentAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();
		return (resolved, added.Id);
	}

	private ActivateAbilityAction MakeActivate(int cardId) =>
		new()
		{
			CardId = cardId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = 0,
		};

	/// <summary>
	/// Advances past the current turn so HasActivated flags are cleared.
	/// Runs StartTurnAction for Player 1 with SkipDraw to avoid library issues.
	/// </summary>
	private GameState SimulateNextTurn(GameState state)
	{
		var (next, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = _ids.Player1BattlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();
		return next;
	}
}
