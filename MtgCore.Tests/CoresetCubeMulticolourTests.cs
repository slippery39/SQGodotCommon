using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The Core Set Cube's multicolour section — the last 30 cards, and the ones with the highest
/// density of "builds, casts, resolves and does nothing" risk in the whole cube.
///
/// Two failure modes drive what is tested here, and both were found while writing these cards:
///
/// 1. SpellCardBuilder.WithTarget binds only the PENDING effect, not every effect built so far.
///    Chaining two effects and putting one WithTarget at the end silently leaves the first on its
///    default strategy — on Heroic Reinforcements that made the +1/+1 a single-target buff while
///    only the haste went team-wide, which looks completely fine on the card until you count.
/// 2. WithSelfBuff is PERMANENT duration. Used for an "until end of turn" pump it stacks every
///    activation into an unbounded creature.
///
/// Neither errors, neither shows up in a cast-and-resolve test, and both were caught by asserting
/// how many creatures ended up buffed and for how long.
/// </summary>
[TestFixture]
public class CoresetCubeMulticolourTests
{
	private static readonly IReadOnlyList<Card> All =
	[
		.. CoresetCubeMulticolour.Cards,
		.. CoresetCubeMulticolourSpells.Cards,
	];

	private static Card Find(string name) =>
		All.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

	// ===== STRUCTURE =====

	[Test]
	public void SectionCounts_MatchTheCube()
	{
		Assert.Multiple(() =>
		{
			Assert.That(CoresetCubeMulticolour.Cards, Has.Count.EqualTo(27), "27 creatures");
			Assert.That(
				CoresetCubeMulticolourSpells.Cards,
				Has.Count.EqualTo(3),
				"2 sorceries + Garruk"
			);
		});
	}

	[Test]
	public void Sorceries_DeclareTheirCardType()
	{
		var undeclared = CoresetCubeMulticolourSpells
			.Cards.Where(c => !c.HasComponent<PlaneswalkerComponent>())
			.Where(c => !c.HasType(CardType.Sorcery))
			.Select(c => c.Name)
			.ToList();

		Assert.That(undeclared, Is.Empty, "spell mastery cannot see an undeclared spell");
	}

	/// <summary>
	/// The pairs are flavour, so these are costed as monocolour cards. A gold rate would make the
	/// whole section strictly better than the five colour sections, since nothing here is harder
	/// to cast than a mono card of the same mana value.
	/// </summary>
	[Test]
	public void NothingIsCostedBelowATwoColourRate()
	{
		var suspicious = CoresetCubeMulticolour
			.Cards.Where(c => c.GetComponent<CreatureComponent>() is { } cc && c.ManaCost <= 2)
			.Where(c => c.GetComponent<CreatureComponent>()!.Power >= 3)
			.Select(c => c.Name)
			.ToList();

		Assert.That(
			suspicious,
			Is.Empty,
			$"Two-mana 3-power creatures: {string.Join(", ", suspicious)}"
		);
	}

	// ===== BEHAVIOUR =====

	/// <summary>
	/// Failure mode 1, plus the engine constraint it uncovered.
	///
	/// The +1/+1 must reach EVERY pre-existing creature, not one: a single WithTarget at the end of
	/// the chain leaves the boost on WithBoost's default and the card silently becomes a trick.
	///
	/// It must NOT reach the Soldiers this spell just made, and that is not a bug to fix here —
	/// ResolveEffectAction resolves every effect's targets before any of them executes, so the
	/// AllValid list is fixed while the tokens are still unmade. The tokens carry haste natively
	/// instead. Asserting the token is a 1/1 pins the constraint, so if the engine ever resolves
	/// targets lazily this test says so rather than quietly passing.
	/// </summary>
	[Test]
	public void HeroicReinforcements_BuffsTheWholeTeam_IncludingItsOwnTokens()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withBear, bearId) = PutOnBattlefield(state, Bear(), ids.Player1Id);

		var final = Resolve(withBear, Find("Heroic Reinforcements"), ids.Player1Id);

		var battlefieldId = final.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var soldiers = final.GetCardsInZone(battlefieldId).Where(c => c.Name == "Soldier").ToList();

		Assert.Multiple(() =>
		{
			Assert.That(soldiers, Has.Count.EqualTo(2), "two Soldiers");
			Assert.That(final.GetEffectivePower(bearId), Is.EqualTo(3), "the Bear is buffed too");
			foreach (var soldier in soldiers)
			{
				Assert.That(
					final.GetEffectivePower(soldier.Id),
					Is.EqualTo(1),
					"targets are resolved before any effect runs, so the tokens miss the buff"
				);
				Assert.That(
					final.GetEffectiveStats(soldier.Id).HasHaste,
					Is.True,
					"which is why the haste is built into the token instead"
				);
			}
		});
	}

	/// <summary>
	/// Failure mode 2. Brawl-Bash Ogre's pump is "until end of turn". WithSelfBuff would make it
	/// permanent, so two activations across two turns would leave a 7/7 rather than a 5/5.
	/// </summary>
	[Test]
	public void BrawlBashOgre_PumpsItselfUntilEndOfTurnOnly()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withOgre, ogreId) = PutOnBattlefield(state, Find("Brawl-Bash Ogre"), ids.Player1Id);
		var (withFodder, fodderId) = PutOnBattlefield(withOgre, Bear(), ids.Player1Id);

		var brawl = AbilityIndex(Find("Brawl-Bash Ogre"), "Brawl");
		var pumped = Run(
			withFodder,
			Activate(ogreId, ids.Player1Id, brawl) with
			{
				AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					[fodderId]
				),
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(pumped.GetEffectivePower(ogreId), Is.EqualTo(5), "3 + 2");
			Assert.That(
				pumped.GetCardZone(fodderId).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"the sacrifice must really happen"
			);
		});

		var nextTurn = Run(
			pumped,
			new StartTurnAction
			{
				ActivePlayerId = ids.Player1Id,
				BattlefieldId = pumped.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield),
				SkipDraw = true,
			}
		);

		Assert.That(
			nextTurn.GetEffectivePower(ogreId),
			Is.EqualTo(3),
			"a permanent-duration buff would stack every activation into an unbounded creature"
		);
	}

	/// <summary>
	/// Enigma Drake counts instants and sorceries only, and is a */4 — the two fields that
	/// GraveyardCountComponent grew for it. A creature card in the yard must not grow its power,
	/// and nothing may grow its toughness.
	/// </summary>
	[Test]
	public void EnigmaDrake_CountsSpellsForPowerOnly()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withDrake, drakeId) = PutOnBattlefield(state, Find("Enigma Drake"), ids.Player1Id);

		Assert.That(withDrake.GetEffectivePower(drakeId), Is.Zero, "empty graveyard");

		withDrake = AddToGraveyard(withDrake, Spell("Bolt"), ids.Player1Id);
		withDrake = AddToGraveyard(withDrake, Spell("Counterspell"), ids.Player1Id);
		withDrake = AddToGraveyard(withDrake, Bear(), ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(withDrake.GetEffectivePower(drakeId), Is.EqualTo(2), "spells only");
			Assert.That(withDrake.GetEffectiveToughness(drakeId), Is.EqualTo(4), "*/4, not */*");
		});
	}

	/// <summary>
	/// Experimental Overload's Weird is a LIVE X/X, not a snapshot — nothing can create a token
	/// with a runtime P/T, so it carries the same component the Drake does. A 0/0 that arrived
	/// with a power-only modifier would die to the zero-toughness rule on the spot.
	/// </summary>
	[Test]
	public void ExperimentalOverload_MakesAWeirdThatSurvivesAndScales()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		state = AddToGraveyard(state, Spell("Bolt"), ids.Player1Id);
		state = AddToGraveyard(state, Spell("Shock"), ids.Player1Id);

		var final = Resolve(state, Find("Experimental Overload"), ids.Player1Id);
		final = Run(final, StateBasedCheck(final, ids));

		var battlefieldId = final.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var weird = final.GetCardsInZone(battlefieldId).SingleOrDefault(c => c.Name == "Weird");

		Assert.That(weird, Is.Not.Null, "a 0/0 with a power-only modifier dies on arrival");
		Assert.Multiple(() =>
		{
			Assert.That(final.GetEffectivePower(weird!.Id), Is.EqualTo(2));
			Assert.That(final.GetEffectiveToughness(weird.Id), Is.EqualTo(2), "X/X, not X/0");
		});
	}

	/// <summary>
	/// Blood-Cursed Knight is the card that showed the "conditional static abilities" deferral is
	/// narrower than it reads: a condition on a single creature buffing itself is a live modifier,
	/// not a push-model anthem. Both directions matter, and it must go BACK off when the
	/// enchantment leaves — a push model would leave the buff stranded.
	/// </summary>
	[Test]
	public void BloodCursedKnight_TracksEnchantmentsLive()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withKnight, knightId) = PutOnBattlefield(
			state,
			Find("Blood-Cursed Knight"),
			ids.Player1Id
		);

		Assert.Multiple(() =>
		{
			Assert.That(withKnight.GetEffectivePower(knightId), Is.EqualTo(3), "no enchantment");
			Assert.That(withKnight.GetEffectiveStats(knightId).HasLifelink, Is.False);
		});

		var (withEnchantment, enchantmentId) = PutOnBattlefield(
			withKnight,
			Enchantment("Omen"),
			ids.Player1Id
		);

		Assert.Multiple(() =>
		{
			Assert.That(withEnchantment.GetEffectivePower(knightId), Is.EqualTo(4));
			Assert.That(withEnchantment.GetEffectiveStats(knightId).HasLifelink, Is.True);
		});

		var graveyardId = withEnchantment.GetPlayerZoneId(ids.Player1Id, ZoneType.Graveyard);
		var without = withEnchantment.MoveCardTracked(enchantmentId, graveyardId);

		Assert.That(
			without.GetEffectivePower(knightId),
			Is.EqualTo(3),
			"live evaluation must fall back off, not strand the buff"
		);
	}

	/// <summary>
	/// Conclave Mentor's replacement must apply ONCE per placement and must emit a single event
	/// carrying the final number. A trigger-shaped implementation loops forever instead.
	/// </summary>
	[Test]
	public void ConclaveMentor_AddsOneExtraCounter()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withMentor, _) = PutOnBattlefield(state, Find("Conclave Mentor"), ids.Player1Id);
		var (withTarget, targetId) = PutOnBattlefield(withMentor, Bear(), ids.Player1Id);

		var final = Run(withTarget, new AddCountersAction { Amount = 2, TargetIds = [targetId] });

		Assert.That(
			((Card)final.GetObject(targetId)).GetComponent<PlusOneCounterComponent>()?.Count,
			Is.EqualTo(3),
			"2 plus one, not 2 plus 2"
		);
	}

	/// <summary>
	/// Radha's top-of-library clause, end to end. The predicate and the generator must agree, or
	/// the AI plays lands off the top and the human cannot.
	/// </summary>
	[Test]
	public void Radha_LetsYouPlayLandsOffTheTop()
	{
		var (state, ids) = MtgGameFactory.Create();
		var libraryId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Library);

		// Put a land on top by moving it to the front of an already-populated library.
		var (withLand, land) = state.AddObject(
			Land("Forest") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: libraryId
		);
		var topFirst = withLand.MoveObject(land.Id, 0).MoveObject(land.Id, libraryId);
		foreach (
			var other in topFirst.GetChildrenIds(libraryId).Where(id => id != land.Id).ToList()
		)
			topFirst = topFirst.MoveObject(other, 0).MoveObject(other, libraryId);

		Assert.That(
			topFirst.GetChildrenIds(libraryId).First(),
			Is.EqualTo(land.Id),
			"test setup: the land must be on top"
		);

		Assert.That(
			topFirst.IsInCastableZone(land.Id, ids.Player1Id),
			Is.False,
			"no Radha, no play"
		);

		var (withRadha, _) = PutOnBattlefield(
			topFirst,
			Find("Radha, Heart of Keld"),
			ids.Player1Id
		);

		Assert.Multiple(() =>
		{
			Assert.That(withRadha.IsInCastableZone(land.Id, ids.Player1Id), Is.True);
			Assert.That(
				MtgActionGenerator
					.GetLegalActions(withRadha, ids, ids.Player1Id)
					.OfType<PlayLandAction>()
					.Select(a => a.CardId),
				Does.Contain(land.Id),
				"the generator must offer what the predicate allows"
			);
		});
	}

	/// <summary>
	/// Skyrider Patrol's counter and its flying must land on the SAME creature. Two separate
	/// user-select strategies would let the engine pick differently for each half.
	/// </summary>
	[Test]
	public void SkyriderPatrol_PutsBothHalvesOnOneCreature()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withPatrol, patrolId) = PutOnBattlefield(
			state,
			Find("Skyrider Patrol"),
			ids.Player1Id
		);
		var (withBear, bearId) = PutOnBattlefield(withPatrol, Bear(), ids.Player1Id);

		var lift = AbilityIndex(Find("Skyrider Patrol"), "Lift");
		var final = Run(
			withBear,
			Activate(patrolId, ids.Player1Id, lift) with
			{
				TargetIds = [bearId],
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				((Card)final.GetObject(bearId)).GetComponent<PlusOneCounterComponent>()?.Count,
				Is.EqualTo(1)
			);
			Assert.That(final.GetEffectiveStats(bearId).HasFlying, Is.True, "same creature");
		});
	}

	/// <summary>
	/// The flying anthems must buff flyers only, and must not buff themselves. Two cards share
	/// this shape, so a filter mistake would hit both.
	/// </summary>
	[TestCase("Empyrean Eagle")]
	[TestCase("Thunderclap Wyvern")]
	public void FlyingAnthem_BuffsOtherFlyersOnly(string name)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withFlyer, flyerId) = PutOnBattlefield(state, Flyer(), ids.Player1Id);
		var (withGround, groundId) = PutOnBattlefield(withFlyer, Bear(), ids.Player1Id);
		var (withAnthem, anthemId) = PutOnBattlefield(withGround, Find(name), ids.Player1Id);

		var final = StaticAbilityEngine.ProcessPermanentEntered(withAnthem, anthemId, ids.GameId);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetEffectivePower(flyerId), Is.EqualTo(3), "2 + 1");
			Assert.That(final.GetEffectivePower(groundId), Is.EqualTo(2), "grounded, unbuffed");
			Assert.That(
				final.GetEffectivePower(anthemId),
				Is.EqualTo(2),
				"'other' excludes itself"
			);
		});
	}

	/// <summary>
	/// Poison-Tip Archer drains on ANOTHER creature dying, and drains no life back — the printed
	/// card gains none. Both halves are easy to get wrong: WithDrain would gain life, and dropping
	/// IsNotSelfSpecification would fire an extra time on the Archer's own death.
	/// </summary>
	[Test]
	public void PoisonTipArcher_DrainsOnOtherDeaths_WithoutGainingLife()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withArcher, _) = PutOnBattlefield(state, Find("Poison-Tip Archer"), ids.Player1Id);
		var (withVictim, victimId) = PutOnBattlefield(withArcher, Bear(), ids.Player2Id);

		var myLifeBefore = ((MtgPlayer)withVictim.GetObject(ids.Player1Id)).Life;
		var theirLifeBefore = ((MtgPlayer)withVictim.GetObject(ids.Player2Id)).Life;

		var final = Run(withVictim, new DestroyCreatureAction { TargetIds = [victimId] });
		final = Run(final, StateBasedCheck(final, ids));

		Assert.Multiple(() =>
		{
			Assert.That(
				((MtgPlayer)final.GetObject(ids.Player2Id)).Life,
				Is.EqualTo(theirLifeBefore - 1)
			);
			Assert.That(
				((MtgPlayer)final.GetObject(ids.Player1Id)).Life,
				Is.EqualTo(myLifeBefore),
				"the printed card gains no life"
			);
		});
	}

	// ===== HELPERS =====

	private static GameState Resolve(GameState state, Card template, int playerId)
	{
		var handId = state.GetPlayerZoneId(playerId, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = playerId,
				ControllerId = playerId,
			},
			parentId: handId
		);

		return Run(withCard, new CastSpellAction { CardId = card.Id, CastingPlayerId = playerId });
	}

	private static ActivateAbilityAction Activate(int cardId, int playerId, int index) =>
		new()
		{
			CardId = cardId,
			ActivatingPlayerId = playerId,
			AbilityIndex = index,
		};

	private static int AbilityIndex(Card card, string name) =>
		card.GetComponents<ActivatedAbilityComponent>().ToList().FindIndex(a => a.Name == name);

	private static Card Bear() => MakeCreature("Bear", 2, 2, flying: false);

	private static Card Flyer() => MakeCreature("Hawk", 2, 2, flying: true);

	private static Card MakeCreature(string name, int power, int toughness, bool flying) =>
		new()
		{
			Name = name,
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasFlying = flying,
				}
			),
		};

	private static Card Enchantment(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Enchantment,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Enchantment"),
			Components = ImmutableArray.Create<GameComponent>(new PermanentComponent()),
		};

	private static Card Spell(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Instant,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
		};

	private static Card Land(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Land,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
			Components = ImmutableArray<GameComponent>.Empty,
		};

	private static GameAction StateBasedCheck(GameState state, MtgGameIds ids) =>
		new CheckStateBasedEffectsAction
		{
			GameId = ids.GameId,
			Player1Id = ids.Player1Id,
			Player2Id = ids.Player2Id,
			Player1BattlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield),
			Player2BattlefieldId = state.GetPlayerZoneId(ids.Player2Id, ZoneType.Battlefield),
			Player1GraveyardId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Graveyard),
			Player2GraveyardId = state.GetPlayerZoneId(ids.Player2Id, ZoneType.Graveyard),
		};

	private static (GameState State, int CardId) PutOnBattlefield(
		GameState state,
		Card template,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: battlefieldId
		);
		return (withCard, card.Id);
	}

	private static GameState AddToGraveyard(GameState state, Card template, int ownerId)
	{
		var graveyardId = state.GetPlayerZoneId(ownerId, ZoneType.Graveyard);
		var (withCard, _) = state.AddObject(
			template with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: graveyardId
		);
		return withCard;
	}

	private static GameState Run(GameState state, GameAction action)
	{
		var (final, _) = state.AddAction(action).ProcessAllActions();
		return final;
	}
}
