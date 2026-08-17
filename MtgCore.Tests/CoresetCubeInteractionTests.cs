using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Multi-card interactions in the white section — specifically the two combinations that could
/// hang or double-count a real game.
///
/// These use the actual set cards, because the point is that THESE cards work together. The
/// single-mechanic tests use inline definitions.
/// </summary>
[TestFixture]
public class CoresetCubeInteractionTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void LifeGainPackage_Terminates_AndCountsEachGainOnce()
	{
		// The loop-risk board: Angel of Vitality replaces life gain with "+1 more", Soul Warden
		// gains life whenever ANOTHER creature enters, Ajani's Pridemate and Archangel of Thune
		// both trigger on life gain. If the replacement had been written as a trigger, this hangs.
		//
		// Soul Warden is played FIRST so every later arrival triggers it, and the life gained
		// during setup is measured rather than assumed.
		var s = Put(_state, "Soul Warden");
		s = Put(s, "Angel of Vitality");
		s = Put(s, "Ajani's Pridemate");
		s = Put(s, "Archangel of Thune");

		var pridemate = FindOnBattlefield(s, "Ajani's Pridemate");
		var lifeBeforeBear = s.GetPlayer(_ids.Player1Id).Life;
		var pridematePowerBefore = s.GetEffectivePower(pridemate.Id);

		// Something enters -> Soul Warden gains 1, replaced up to 2.
		var bear = MtgCore
			.Cards.Builders.CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2)
			.Build();

		GameState final = default!;
		Assert.DoesNotThrow(() =>
		{
			var (result, _) = s.AddAction(
					new CreateCardAction { CardTemplate = bear, ControllerId = _ids.Player1Id }
				)
				.ProcessAllActions();
			final = result;
		});

		Assert.That(
			final.GetPlayer(_ids.Player1Id).Life - lifeBeforeBear,
			Is.EqualTo(2),
			"Soul Warden's 1 life, replaced to 2 by Angel of Vitality — once, not repeatedly"
		);

		// One life-gain event: +1/+1 from the Pridemate's own trigger, +1/+1 from Archangel of
		// Thune. Four total would mean the gain fired twice.
		Assert.That(final.GetEffectivePower(pridemate.Id) - pridematePowerBefore, Is.EqualTo(2));
	}

	[Test]
	public void LifeGainPackage_HasNoPendingActionsLeft()
	{
		// A trigger cascade that never settles would leave actions queued.
		var s = Put(_state, "Angel of Vitality");
		s = Put(s, "Soul Warden");
		s = Put(s, "Archangel of Thune");

		var lifeBefore = s.GetPlayer(_ids.Player1Id).Life;

		var (final, _) = s.AddAction(
				new GainLifeAction { Amount = 2, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		Assert.That(final.HasPendingActions, Is.False);
		Assert.That(
			final.GetPlayer(_ids.Player1Id).Life - lifeBefore,
			Is.EqualTo(3),
			"2 + 1 from Angel of Vitality, and nothing re-triggers it"
		);
	}

	[Test]
	public void TapperPackage_LawkeeperExhausts_AndAvengerGrows()
	{
		// Gideon's Lawkeeper exhausts an opposing creature; Gideon's Avenger sees the
		// CreatureExhaustedEvent and grows. Both halves of the tapper theme in one line.
		var s = Put(_state, "Gideon's Lawkeeper");
		s = Put(s, "Gideon's Avenger");

		var lawkeeper = FindOnBattlefield(s, "Gideon's Lawkeeper");
		var avenger = FindOnBattlefield(s, "Gideon's Avenger");

		var bear = MtgCore
			.Cards.Builders.CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2)
			.Build();
		var (withBear, victim) = s.AddObject(
			bear with
			{
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: s.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield)
		);

		var (final, _) = withBear
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = lawkeeper.Id,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
					TargetIds = ImmutableList.Create(victim.Id),
				}
			)
			.ProcessAllActions();

		Assert.That(
			((Card)final.GetObject(victim.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True
		);
		Assert.That(
			final.GetEffectivePower(avenger.Id),
			Is.EqualTo(3),
			"Gideon's Avenger should grow when an opponent's creature becomes exhausted"
		);
		Assert.That(
			((Card)final.GetObject(lawkeeper.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True,
			"The tap cost should exhaust the Lawkeeper itself"
		);
	}

	[Test]
	public void CrusaderOfOdric_GrowsWithTheBoard()
	{
		var s = Put(_state, "Crusader of Odric");
		var crusader = FindOnBattlefield(s, "Crusader of Odric");

		Assert.That(s.GetEffectivePower(crusader.Id), Is.EqualTo(1), "Counts itself");

		s = Put(s, "Fencing Ace");
		s = Put(s, "Stormfront Pegasus");

		Assert.That(s.GetEffectivePower(crusader.Id), Is.EqualTo(3));
		Assert.That(s.GetEffectiveToughness(crusader.Id), Is.EqualTo(3));
	}

	[Test]
	public void VeteranSwordsmith_BuffsOtherSoldiersOnly()
	{
		var s = Put(_state, "Veteran Swordsmith");
		s = Put(s, "Fencing Ace"); // Human Soldier
		s = Put(s, "Stormfront Pegasus"); // Pegasus

		var smith = FindOnBattlefield(s, "Veteran Swordsmith");
		var ace = FindOnBattlefield(s, "Fencing Ace");
		var pegasus = FindOnBattlefield(s, "Stormfront Pegasus");

		Assert.That(s.GetEffectivePower(ace.Id), Is.EqualTo(2), "1/1 Soldier gets +1/+0");
		Assert.That(s.GetEffectivePower(pegasus.Id), Is.EqualTo(2), "Pegasus is unaffected");
		Assert.That(s.GetEffectivePower(smith.Id), Is.EqualTo(3), "Does not buff itself");
	}

	// ===== HELPERS =====

	/// <summary>
	/// Casts the named set card onto Player 1's battlefield through the real action pipeline, so
	/// ETB triggers and static abilities fire exactly as they would in a game.
	///
	/// Summoning sickness is cleared afterwards — these tests are about card interactions, not
	/// about waiting a turn, and a sick creature cannot use a tap ability.
	/// </summary>
	private GameState Put(GameState state, string name)
	{
		var template = CoresetCubeWhite.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (resolved, _) = withCard
			.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var onBattlefield = (Card)resolved.GetObject(card.Id);
		return resolved.UpdateObject(
			card.Id,
			onBattlefield.WithComponentReplaced(
				onBattlefield.GetComponent<CreatureComponent>()! with
				{
					HasSummoningSickness = false,
				}
			)
		);
	}

	private Card FindOnBattlefield(GameState state, string name) =>
		state
			.GetCardsInZone(state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield))
			.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
