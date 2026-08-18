using System.Collections.Immutable;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The card face as it reads DURING a game, rather than in a draft pack.
///
/// Printed text is covered by CoresetCubeRulesTextTests. Everything here is live state: what a
/// spell has done to a permanent, what an Aura is attached to, what a card actually costs right
/// now. All of it was invisible in play — the player saw a smaller number in the P/T badge, or a
/// five-mana card they had just made cost two, with nothing on the card to explain either.
/// </summary>
[TestFixture]
public class MtgCardLiveStateTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void AnAuraBoost_IsNamedOnTheCreatureItIsAttachedTo()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);
		var attached = CastAura(s, "Sensory Deprivation", victim.Id);

		var text = MtgCardMapper.GetRulesText((Card)attached.GetObject(victim.Id), attached);

		Assert.That(text, Does.Contain("-3/+0"));
		Assert.That(text, Does.Contain("Sensory Deprivation"));
	}

	[Test]
	public void AnAura_SaysWhatItIsAttachedTo()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);
		var attached = CastAura(s, "Sensory Deprivation", victim.Id);
		var aura = attached
			.GetCardsInZone(attached.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield))
			.First(c => c.Name == "Sensory Deprivation");

		Assert.That(
			MtgCardMapper.GetRulesText(aura, attached),
			Does.Contain("Attached to Test Bear")
		);
	}

	[Test]
	public void ATemporaryBuff_IsShownOnTheCreature()
	{
		var (s, creature) = AddCreature(_state, _ids.Player1Id);
		var buffed = s.UpdateObject(
			creature.Id,
			creature with
			{
				Components = creature.Components.Add(
					new StaticPowerToughnessModifier
					{
						PowerBonus = 3,
						ToughnessBonus = 3,
						Duration = ModifierDuration.UntilEndOfTurn,
					}
				),
			}
		);

		var text = MtgCardMapper.GetRulesText((Card)buffed.GetObject(creature.Id), buffed);

		Assert.That(text, Does.Contain("+3/+3 until end of turn"));
	}

	[Test]
	public void AnExhaustedCreature_SaysSo()
	{
		var (s, creature) = AddCreature(_state, _ids.Player1Id);
		var (tapped, _) = s.AddAction(
				new ExhaustCreatureAction { TargetIds = ImmutableList.Create(creature.Id) }
			)
			.ProcessAllActions();

		Assert.That(
			MtgCardMapper.GetRulesText((Card)tapped.GetObject(creature.Id), tapped),
			Does.Contain("Exhausted")
		);
	}

	/// <summary>
	/// The hand's mana badge now reads CostEngine instead of the printed cost, so a Stormwing
	/// Entity discounted to 2 stops claiming it costs 5 and stops being greyed out as
	/// unaffordable. Asserted against CostEngine itself: MtgCardMapper.ToDetails loads artwork
	/// through Godot's ResourceLoader and cannot run outside the engine.
	/// </summary>
	[Test]
	public void ADiscountedCard_CostsLessOnceASpellHasBeenCast()
	{
		var template = CoresetCube.Cards.First(c => c.Name == "Stormwing Entity");
		var (withCard, card) = _state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		Assert.That(
			withCard.ComputeEffectiveCost(card, _ids.Player1Id),
			Is.EqualTo(5),
			"Precondition: no spell cast yet this turn"
		);

		var game = withCard.GetGame();
		var afterSpell = withCard.UpdateObject(game.Id, game with { SpellsCastThisTurn = 1 });

		Assert.That(afterSpell.ComputeEffectiveCost(card, _ids.Player1Id), Is.EqualTo(2));
	}

	private GameState CastAura(GameState state, string cardName, int targetId)
	{
		var template = CoresetCube.Cards.First(c => c.Name == cardName);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var action = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastPermanentAction>()
			.First(a => a.CardId == card.Id && a.TargetIds.Contains(targetId));

		var (final, _) = withCard.AddAction(action).ProcessAllActions();
		return final;
	}

	private static (GameState, Card) AddCreature(GameState state, int ownerId)
	{
		var card = new Card
		{
			Name = "Test Bear",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
					HasSummoningSickness = false,
				}
			),
		};

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
