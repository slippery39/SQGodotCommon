using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// Counterspell traps: cards that sit in hand and fire automatically when the opponent casts a
/// matching spell, paid for with mana the trapper left unspent.
///
/// Cards are defined inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class CounterTrapTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
		// CreateForTesting hands out 99 mana. Trap tests are ABOUT mana, so both players start
		// from a known, small pool instead.
		_state = SetMana(_state, _ids.Player1Id, 10);
		_state = SetMana(_state, _ids.Player2Id, 0);
	}

	// ===== FIRING =====

	[Test]
	public void Trap_Fires_WhenOpponentCastsMatchingSpell()
	{
		var s = GiveTrap(_state, HardCounter(), _ids.Player2Id, mana: 2);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(
			final.GetCardZone(spell.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"The countered spell goes to the graveyard"
		);
		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Its effect must not resolve"
		);
	}

	[Test]
	public void Trap_DoesNotFire_WithoutEnoughManaLeftUp()
	{
		// The mana you declined to spend IS the cost. Nothing held up, nothing happens.
		var s = GiveTrap(_state, HardCounter(), _ids.Player2Id, mana: 1);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17), "The bolt resolved");
	}

	[Test]
	public void Trap_IsSpent_WhenItFires()
	{
		var s = GiveTrap(_state, HardCounter(), _ids.Player2Id, mana: 2);
		var trapId = HandCards(s, _ids.Player2Id).Single().Id;
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetCardZone(trapId).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(
			final.GetPlayer(_ids.Player2Id).CurrentMana,
			Is.EqualTo(0),
			"The trap's own cost is paid from the held mana"
		);
	}

	// ===== TYPE FILTER =====

	[Test]
	public void EssenceScatter_CountersCreatures_NotSpells()
	{
		var scatter = CardFactory
			.Instant("Essence Scatter", manaCost: 2)
			.AsCounterTrap(targetTypes: CardType.Creature)
			.Build();

		var s = GiveTrap(_state, scatter, _ids.Player2Id, mana: 2);
		var (withSpell, bolt) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (afterBolt, _) = Cast(withSpell, bolt.Id, _ids.Player1Id);
		Assert.That(
			afterBolt.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(17),
			"A creature-only trap must ignore an instant"
		);

		var (withBear, bear) = PutInHand(afterBolt, Bear(), _ids.Player1Id);
		var (afterBear, _) = CastCreature(withBear, bear.Id, _ids.Player1Id);

		Assert.That(
			afterBear.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"But it counters the creature"
		);
	}

	[Test]
	public void Negate_CountersNoncreature_NotCreatures()
	{
		var negate = CardFactory
			.Instant("Negate", manaCost: 2)
			.AsCounterTrap(excludeTypes: CardType.Creature)
			.Build();

		var s = GiveTrap(_state, negate, _ids.Player2Id, mana: 2);
		var (withBear, bear) = PutInHand(s, Bear(), _ids.Player1Id);

		var (afterBear, _) = CastCreature(withBear, bear.Id, _ids.Player1Id);
		Assert.That(
			afterBear.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Negate must not touch a creature"
		);

		var (withBolt, bolt) = PutInHand(afterBear, Bolt(), _ids.Player1Id);
		var (afterBolt, _) = Cast(withBolt, bolt.Id, _ids.Player1Id);

		Assert.That(afterBolt.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(20));
	}

	// ===== MANA TAX =====

	[Test]
	public void ManaLeak_Fizzles_WhenCasterCanPayTheTax()
	{
		// Caster starts on 10, bolt costs 1, so 9 remain — more than the {3} tax.
		var s = GiveTrap(_state, ManaLeak(), _ids.Player2Id, mana: 2);
		var trapId = HandCards(s, _ids.Player2Id).Single().Id;
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17), "The bolt resolved");
		Assert.That(
			final.GetPlayer(_ids.Player1Id).CurrentMana,
			Is.EqualTo(6),
			"10 - 1 for the bolt - 3 for the tax"
		);
		Assert.That(
			final.GetCardZone(trapId).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"The trap is spent even when the tax is paid — as a real counterspell would be"
		);
	}

	[Test]
	public void ManaLeak_Counters_WhenCasterCannotPayTheTax()
	{
		var s = SetMana(_state, _ids.Player1Id, 1);
		s = GiveTrap(s, ManaLeak(), _ids.Player2Id, mana: 2);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetCardZone(spell.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(20));
	}

	[Test]
	public void ClashOfWills_TaxesTheTrappersLeftoverMana()
	{
		// Trap costs 1; the trapper held 5, so 4 remain and that is X.
		var clash = CardFactory
			.Instant("Clash of Wills", manaCost: 1)
			.AsCounterTrap(taxAllRemaining: true)
			.Build();

		var s = SetMana(_state, _ids.Player1Id, 3);
		s = GiveTrap(s, clash, _ids.Player2Id, mana: 5);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		// Caster has 3 - 1 = 2 left, which is under X = 4.
		Assert.That(
			final.GetCardZone(spell.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"X = 4 exceeds the caster's remaining 2"
		);
	}

	// ===== HAND ORDER TIEBREAK =====

	[Test]
	public void WhenTwoTrapsMatch_TheEarlierCardInHandFires()
	{
		var first = CardFactory.Instant("First Trap", manaCost: 1).AsCounterTrap().Build();
		var second = CardFactory.Instant("Second Trap", manaCost: 1).AsCounterTrap().Build();

		var s = SetMana(_state, _ids.Player2Id, 5);
		var (s1, firstCard) = PutInHand(s, first, _ids.Player2Id);
		var (s2, secondCard) = PutInHand(s1, second, _ids.Player2Id);
		var (withSpell, spell) = PutInHand(s2, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(
			final.GetCardZone(firstCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"The card drawn first wins the tiebreak"
		);
		Assert.That(
			final.GetCardZone(secondCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"Only one trap fires per spell"
		);
	}

	// ===== VARIANTS =====

	[Test]
	public void Dissipate_ExilesInsteadOfGraveyard()
	{
		var dissipate = CardFactory
			.Instant("Dissipate", manaCost: 3)
			.AsCounterTrap(exileInstead: true)
			.Build();

		var s = GiveTrap(_state, dissipate, _ids.Player2Id, mana: 3);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetCardZone(spell.Id).ZoneType, Is.EqualTo(ZoneType.Exile));
	}

	[Test]
	public void Unsubstantiate_ReturnsTheSpellToHand()
	{
		var unsub = CardFactory
			.Instant("Unsubstantiate", manaCost: 2)
			.AsCounterTrap(returnToHandInstead: true)
			.Build();

		var s = GiveTrap(_state, unsub, _ids.Player2Id, mana: 2);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetCardZone(spell.Id).ZoneType, Is.EqualTo(ZoneType.Hand));
	}

	[Test]
	public void BoneToAsh_DrawsWhenItFires()
	{
		var bone = CardFactory
			.Instant("Bone to Ash", manaCost: 4)
			.AsCounterTrap(targetTypes: CardType.Creature, drawOnCounter: 1)
			.Build();

		var s = GiveTrap(_state, bone, _ids.Player2Id, mana: 4);
		s = StockLibrary(s, _ids.Player2Id, 3);
		var handBefore = HandCards(s, _ids.Player2Id).Count;

		var (withBear, bear) = PutInHand(s, Bear(), _ids.Player1Id);
		var (final, _) = CastCreature(withBear, bear.Id, _ids.Player1Id);

		// -1 for the spent trap, +1 for the draw.
		Assert.That(HandCards(final, _ids.Player2Id).Count, Is.EqualTo(handBefore));
	}

	// ===== INTERACTION =====

	[Test]
	public void CounteredSpell_StillCountsAsCast()
	{
		// A countered spell WAS cast. Prowess and storm must see it, which is why the trap fires
		// after SpellCastEvent and the storm counter rather than before.
		var prowess = CardFactory
			.Creature("Prowess Bear", manaCost: 2, power: 2, toughness: 2)
			.WithTriggeredAbility(
				"Prowess",
				new EventTriggerCondition { EventTypeName = EventTypeNames.SpellCast },
				eb => eb.WithProwessBuff()
			)
			.Build();

		var s = GiveTrap(_state, HardCounter(), _ids.Player2Id, mana: 2);
		var battlefieldId = s.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (withProwess, prowessCard) = s.AddObject(
			prowess with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		var (withSpell, spell) = PutInHand(withProwess, Bolt(), _ids.Player1Id);
		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetCardZone(spell.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		Assert.That(
			final.GetEffectivePower(prowessCard.Id),
			Is.EqualTo(3),
			"Prowess triggers on a spell that was countered"
		);
	}

	[Test]
	public void Trap_IsNeverOfferedAsACastableCard()
	{
		// It has a SpellComponent, so without an explicit guard the generator would offer it as
		// a blank spell at full price — and the AI would cast it.
		var s = GiveTrap(_state, HardCounter(), _ids.Player1Id, mana: 10);
		var trapId = HandCards(s, _ids.Player1Id).Single().Id;

		var actions = MtgActionGenerator.GetLegalActions(s, _ids.Player1Id);

		Assert.That(actions.OfType<CastSpellAction>().Any(a => a.CardId == trapId), Is.False);

		var (_, success) = s.TryAddAction(
			new CastSpellAction { CardId = trapId, CastingPlayerId = _ids.Player1Id }
		);
		Assert.That(success, Is.False, "And casting it directly is rejected");
	}

	[Test]
	public void Trap_DoesNotFireOnItsOwnControllersSpell()
	{
		// The trapper is always the non-active player, so your own traps never hit your own cards.
		var s = GiveTrap(_state, HardCounter(), _ids.Player1Id, mana: 10);
		var (withSpell, spell) = PutInHand(s, Bolt(), _ids.Player1Id);

		var (final, _) = Cast(withSpell, spell.Id, _ids.Player1Id);

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
	}

	// ===== HELPERS =====

	private static Card HardCounter() =>
		CardFactory.Instant("Cancel", manaCost: 2).AsCounterTrap().Build();

	private static Card ManaLeak() =>
		CardFactory.Instant("Mana Leak", manaCost: 2).AsCounterTrap(manaTax: 3).Build();

	private static Card Bolt() =>
		CardFactory
			.Instant("Bolt", manaCost: 1)
			.WithDamage(3)
			.WithTarget(Single().Opponent())
			.Build();

	private static Card Bear() =>
		CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2).Build();

	private static GameState SetMana(GameState state, int playerId, int mana)
	{
		var player = state.GetPlayer(playerId);
		return state.UpdateObject(playerId, player with { CurrentMana = mana, MaxMana = mana });
	}

	private static GameState GiveTrap(GameState state, Card trap, int playerId, int mana)
	{
		var (withTrap, _) = PutInHand(state, trap, playerId);
		return SetMana(withTrap, playerId, mana);
	}

	private static (GameState, Card) PutInHand(GameState state, Card template, int playerId) =>
		state.AddObject(
			template with
			{
				OwnerId = playerId,
				ControllerId = playerId,
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Hand)
		);

	private static GameState StockLibrary(GameState state, int playerId, int count)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		for (var i = 0; i < count; i++)
			(state, _) = state.AddObject(
				Bear() with
				{
					OwnerId = playerId,
					ControllerId = playerId,
				},
				parentId: libraryId
			);
		return state;
	}

	private static List<Card> HandCards(GameState state, int playerId) =>
		state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Hand)).ToList();

	private static (GameState, ImmutableList<GameEvent>) Cast(
		GameState state,
		int cardId,
		int playerId
	) =>
		state
			.AddAction(
				new CastSpellAction
				{
					CardId = cardId,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(
							playerId == state.GetWellKnownId(MtgObjectKeys.Player1)
								? state.GetWellKnownId(MtgObjectKeys.Player2)
								: state.GetWellKnownId(MtgObjectKeys.Player1)
						)
					),
				}
			)
			.ProcessAllActions();

	private static (GameState, ImmutableList<GameEvent>) CastCreature(
		GameState state,
		int cardId,
		int playerId
	) =>
		state
			.AddAction(new CastCreatureAction { CardId = cardId, CastingPlayerId = playerId })
			.ProcessAllActions();
}
