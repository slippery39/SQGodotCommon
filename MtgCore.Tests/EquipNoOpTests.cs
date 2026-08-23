using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Re-equipping an attachment to the creature already wearing it is a perfect no-op —
/// AttachEquipmentAction strips the boost and re-stamps an identical one, so the resulting state
/// is unchanged. Offering that as a legal action is what hangs a game.
///
/// It stayed invisible for as long as every equip cost mana, because mana bounded the loop.
/// Dropping Swiftfoot Boots to equip {0} removed the bound: 38.5% of games with Boots on the
/// board ended in a draw against a 1.5% base rate, because the AI re-equipped until GameRunner's
/// 200-action-per-turn limit ended the game.
///
/// The AI cannot defend itself here. MultiTurnBeamSearchAiStrategy.PickBestNode requires EndTurn
/// to EXCEED the best other action by EndTurnBias to be chosen, so an action worth exactly zero
/// beats ending the turn every single time it is offered.
///
/// Cards are inline so an equip-cost tweak cannot break these.
/// </summary>
[TestFixture]
public class EquipNoOpTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void FreeEquip_IsNotOfferedAgainstItsOwnWearer()
	{
		var (state, wearer) = AddCreature(_state, "Bear");
		(state, var boots) = AddEquipment(state, equipCost: 0);

		state = Equip(state, boots.Id, wearer.Id);

		var equipTargets = EquipActions(state, boots.Id).Select(a => a.TargetIds[0]).ToList();

		Assert.That(
			equipTargets,
			Does.Not.Contain(wearer.Id),
			"the wearer must drop out of the target list, or a free equip loops forever"
		);
	}

	/// <summary>
	/// The point of equipment is moving it, so the exclusion must remove exactly one target and
	/// not disable the ability. A fix that made equipment unmovable would be worse than the bug.
	///
	/// Asserted on a FRESH equipment rather than by equipping first and looking again: equip is
	/// now capped at once per turn (see PermanentCardBuilder.WithEquip), so a second activation in
	/// the same turn is correctly unavailable and would mask what this test is checking.
	/// </summary>
	[Test]
	public void Equip_OffersTheOtherCreature_NotTheWearer()
	{
		var (state, wearer) = AddCreature(_state, "Bear");
		(state, var other) = AddCreature(state, "Wolf");
		(state, var boots) = AddEquipment(state, equipCost: 0);

		// Attach directly rather than through the ability, so the per-turn cap is untouched.
		state = state.UpdateObject(
			boots.Id,
			boots.WithComponentReplaced(
				boots.GetComponent<EquipmentComponent>()! with
				{
					EquippedToCardId = wearer.Id,
				}
			)
		);

		var equipTargets = EquipActions(state, boots.Id).Select(a => a.TargetIds[0]).ToList();

		Assert.That(equipTargets, Is.EqualTo(new[] { other.Id }));
	}

	/// <summary>
	/// Unequipped equipment must still be offered. Note the generator emits exactly ONE equip
	/// action, not one per creature: MtgActionGenerator.BuildAbilityAction takes
	/// <c>validTargets[0]</c> — the first creature in zone order — for every targeted activated
	/// ability in the game. That is the same one-option-by-zone-order shape as
	/// SacrificeAdditionalCost had, and it is why the no-op mattered so much: while the wearer
	/// sorted first, the ONLY equip action the AI was ever offered was the no-op.
	/// </summary>
	[Test]
	public void UnequippedEquipment_IsStillOffered()
	{
		var (state, a) = AddCreature(_state, "Bear");
		(state, _) = AddCreature(state, "Wolf");
		(state, var boots) = AddEquipment(state, equipCost: 0);

		var equipTargets = EquipActions(state, boots.Id).Select(x => x.TargetIds[0]).ToList();

		Assert.That(equipTargets, Is.EqualTo(new[] { a.Id }));
	}

	/// <summary>
	/// The degenerate case the exclusion creates: one creature, already wearing the equipment,
	/// so there is nothing legal to move it to. The ability must simply not be offered — which is
	/// exactly the state the AI used to burn its whole turn on.
	/// </summary>
	[Test]
	public void SoleWearer_LeavesNoEquipActionAtAll()
	{
		var (state, wearer) = AddCreature(_state, "Bear");
		(state, var boots) = AddEquipment(state, equipCost: 0);

		state = Equip(state, boots.Id, wearer.Id);

		Assert.That(EquipActions(state, boots.Id), Is.Empty);
	}

	/// <summary>
	/// The generator and the validator must agree. If only the generator filtered, a human UI
	/// asking ValidateAdd would still light up the wearer as a legal target.
	/// </summary>
	[Test]
	public void ValidationAlsoRefusesTheCurrentWearer()
	{
		var (state, wearer) = AddCreature(_state, "Bear");
		(state, var boots) = AddEquipment(state, equipCost: 0);

		state = Equip(state, boots.Id, wearer.Id);

		var (_, success) = state.TryAddAction(EquipAction(boots.Id, wearer.Id));

		Assert.That(success, Is.False);
	}

	private List<ActivateAbilityAction> EquipActions(GameState state, int equipmentId) =>
		MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == equipmentId && !a.TargetIds.IsEmpty)
			.ToList();

	private GameState Equip(GameState state, int equipmentId, int targetId)
	{
		var (result, _) = state.AddAction(EquipAction(equipmentId, targetId)).ProcessAllActions();
		return result;
	}

	private ActivateAbilityAction EquipAction(int equipmentId, int targetId) =>
		new()
		{
			CardId = equipmentId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = 0,
			TargetIds = ImmutableList.Create(targetId),
		};

	private (GameState, Card) AddCreature(GameState state, string name) =>
		state.AddObject(
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

	private (GameState, Card) AddEquipment(GameState state, int equipCost)
	{
		var card = CardFactory
			.Artifact("Test Boots", manaCost: 2)
			.WithSubtype("Equipment")
			.WithComponent(new EquipmentComponent { PowerBonus = 1, ToughnessBonus = 1 })
			.WithEquip(equipCost)
			.Build();

		return state.AddObject(
			card with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1BattlefieldId
		);
	}
}
