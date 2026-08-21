using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// The engine mechanics added for the Core Set Cube's green section.
///
/// Card-agnostic on purpose, like RedMechanicsTests: these assert the mechanic rather than the
/// card that motivated it, so rebalancing Primordial Hydra cannot quietly delete the coverage.
///
/// Several tests deliberately assert TWO things about the same object. That is the point rather
/// than laziness — a counter that P/T does not read and a P/T modifier that cannot be counted are
/// both plausible wrong implementations, and each fails only one half.
/// </summary>
[TestFixture]
public class GreenMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== +1/+1 COUNTERS =====

	/// <summary>
	/// The single assertion that fails for every wrong representation of a counter: the count and
	/// the effective power must move together. A bare number fails the power half; a plain
	/// StaticPowerToughnessModifier fails the count half because nothing can double it.
	/// </summary>
	[Test]
	public void Counters_DriveEffectivePower_AndCanBeDoubled()
	{
		var (state, id) = PutOnBattlefield(MakeCreature("Hydra", 0, 0));

		state = Run(state, new AddCountersAction { Amount = 3, TargetIds = [id] });

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(state, id), Is.EqualTo(3), "counters");
			Assert.That(state.GetEffectivePower(id), Is.EqualTo(3), "power must follow counters");
			Assert.That(state.GetEffectiveToughness(id), Is.EqualTo(3), "toughness");
		});

		state = Run(
			state,
			new AddCountersAction
			{
				Multiplier = 2,
				Amount = 0,
				TargetIds = [id],
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(state, id), Is.EqualTo(6), "doubling must see a real number");
			Assert.That(state.GetEffectivePower(id), Is.EqualTo(6), "power after doubling");
		});
	}

	/// <summary>
	/// Removing counters must not be able to drive the count negative, or a creature would end up
	/// with a NEGATIVE power bonus from a mechanic that only ever adds.
	/// </summary>
	[Test]
	public void RemovingMoreCountersThanExist_FloorsAtZero()
	{
		var (state, id) = PutOnBattlefield(MakeCreature("Troll", 2, 2));

		state = Run(state, new AddCountersAction { Amount = 1, TargetIds = [id] });
		state = Run(state, new AddCountersAction { Amount = -5, TargetIds = [id] });

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(state, id), Is.Zero);
			Assert.That(state.GetEffectivePower(id), Is.EqualTo(2), "back to its printed body");
		});
	}

	/// <summary>
	/// Removing a counter must NOT fire the "counters were put on a creature" trigger, or
	/// Wildwood Scourge would grow every time an opponent shrank something.
	/// </summary>
	[Test]
	public void OnlyAddingCounters_EmitsTheEvent()
	{
		var (state, id) = PutOnBattlefield(MakeCreature("Hydra", 1, 1));

		var added = state.AddAction(new AddCountersAction { Amount = 2, TargetIds = [id] });
		var (afterAdd, addEvents) = added.ProcessAllActions();

		var removed = afterAdd.AddAction(new AddCountersAction { Amount = -1, TargetIds = [id] });
		var (_, removeEvents) = removed.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				addEvents.OfType<CountersAddedEvent>().Sum(e => e.Amount),
				Is.EqualTo(2),
				"adding counters must announce how many"
			);
			Assert.That(
				removeEvents.OfType<CountersAddedEvent>(),
				Is.Empty,
				"removing a counter is not counters being put on a creature"
			);
		});
	}

	/// <summary>
	/// Counters vanish on a zone change, but a card in the graveyard must still remember what it
	/// had — that is the difference between stripping on entry and stripping on exit, and getting
	/// it wrong is what makes Chasm Skulker create zero tokens.
	/// </summary>
	[Test]
	public void Counters_SurviveIntoTheGraveyard_ButNotBackOntoTheBattlefield()
	{
		var (state, id) = PutOnBattlefield(MakeCreature("Hydra", 1, 1));
		state = Run(state, new AddCountersAction { Amount = 4, TargetIds = [id] });

		var graveyardId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		state = state.MoveCardTracked(id, graveyardId);

		Assert.That(
			CounterCount(state, id),
			Is.EqualTo(4),
			"a death trigger reads last-known information — stripping on exit breaks it"
		);

		state = Run(state, new PutIntoBattlefieldAction { TargetIds = [id] });

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(state, id), Is.Zero, "counters must not ride back into play");
			Assert.That(state.GetEffectivePower(id), Is.EqualTo(1), "back to its printed body");
		});
	}

	/// <summary>
	/// A counter-gated keyword must be evaluated LIVE. StaticAbilityEngine only re-stamps on
	/// ETB/LTB, so a pushed implementation would go stale the moment a counter landed and
	/// Primordial Hydra would never actually gain trample.
	/// </summary>
	[Test]
	public void CounterThreshold_TurnsOnTheMomentTheCounterLands()
	{
		var template = MakeCreature("Hydra", 0, 0) with
		{
			Components = MakeCreature("Hydra", 0, 0)
				.Components.Add(
					new ThresholdComponent
					{
						CountSource = ThresholdSource.PlusOneCounters,
						Minimum = 3,
						GrantsTrample = true,
						Duration = ModifierDuration.Permanent,
					}
				),
		};

		var (state, id) = PutOnBattlefield(template);

		state = Run(state, new AddCountersAction { Amount = 2, TargetIds = [id] });
		Assert.That(state.GetEffectiveStats(id).HasTrample, Is.False, "two counters is not three");

		state = Run(state, new AddCountersAction { Amount = 1, TargetIds = [id] });
		Assert.That(
			state.GetEffectiveStats(id).HasTrample,
			Is.True,
			"the third counter must switch it on with no re-stamp"
		);
	}

	// ===== X ON CREATURE SPELLS =====

	/// <summary>
	/// Cost and effect travel completely different paths — CostEngine versus InputContext — and
	/// each has failed independently in this repo before, so both halves are asserted together.
	/// </summary>
	[Test]
	public void XCreature_ChargesTheX_AndEntersWithThatManyCounters()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.UpdateObject(
			ids.Player1Id,
			state.GetPlayer(ids.Player1Id) with
			{
				MaxMana = 6,
				CurrentMana = 6,
			}
		);

		var template = MakeCreature("Hydra", 0, 0) with
		{
			ManaCost = 2,
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
			Components = MakeCreature("Hydra", 0, 0)
				.Components.Add(new XCostComponent())
				.Add(new EntersWithCountersComponent { FromXValue = true }),
		};

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(template, parentId: handId);

		var (final, _) = withCard
			.AddAction(
				new CastCreatureAction
				{
					CardId = card.Id,
					CastingPlayerId = ids.Player1Id,
					XValue = 4,
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetPlayer(ids.Player1Id).CurrentMana,
				Is.Zero,
				"2 base + X=4 should have cost all 6 — X was silently free otherwise"
			);
			Assert.That(
				final.GetEffectivePower(card.Id),
				Is.EqualTo(4),
				"X never reached the ETB — the Hydra arrives as a 0/0 and dies"
			);
		});
	}

	/// <summary>
	/// A reanimated or cloned Hydra was never cast, so it has no X and must arrive at 0. Without
	/// this the entry stamp would read a stale context value from whatever ran last.
	/// </summary>
	[Test]
	public void XCreature_ReanimatedWithoutBeingCast_EntersWithNoCounters()
	{
		var template = MakeCreature("Hydra", 1, 1) with
		{
			Components = MakeCreature("Hydra", 1, 1)
				.Components.Add(new EntersWithCountersComponent { FromXValue = true }),
		};

		var (state, id) = PutOnBattlefield(template);

		Assert.That(CounterCount(state, id), Is.Zero);
	}

	// ===== FIGHT =====

	/// <summary>
	/// The second assertion is the whole primitive — a two-sided fight passes the first one.
	/// </summary>
	[Test]
	public void OneSidedFight_DealsDamageOneWay_AndTakesNoneBack()
	{
		var (state, mine) = PutOnBattlefield(MakeCreature("Mine", 4, 4));
		var (withTheirs, theirs) = AddToBattlefield(
			state,
			MakeCreature("Theirs", 3, 3),
			_ids.Player2Id
		);

		var final = Run(
			withTheirs,
			new FightAction { OneSided = true, TargetIds = [theirs] }.WithInputs(
				_ids.Player1Id,
				mine
			)
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(theirs).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"a 4/4 must kill a 3/3"
			);
			Assert.That(
				DamageOn(final, mine),
				Is.Zero,
				"one-sided means no damage comes back — this is the entire mechanic"
			);
		});
	}

	/// <summary>
	/// A fight cast as a SPELL has a spell as its SourceCardId, which is not a creature. Before
	/// the fallback, FightAction bailed out silently and every fight spell in the engine did
	/// nothing — Hollowmere's Set Upon the Pack shipped in that state.
	/// </summary>
	[Test]
	public void FightFromASpell_FallsBackToYourStrongestCreature()
	{
		var (state, weak) = PutOnBattlefield(MakeCreature("Weak", 1, 1));
		var (withStrong, strong) = AddToBattlefield(
			state,
			MakeCreature("Strong", 5, 5),
			_ids.Player1Id
		);
		var (withTheirs, theirs) = AddToBattlefield(
			withStrong,
			MakeCreature("Theirs", 4, 4),
			_ids.Player2Id
		);

		// No SourceCardId that is a creature — exactly what a spell provides.
		var final = Run(
			withTheirs,
			new FightAction { OneSided = true, TargetIds = [theirs] }.WithInputs(
				_ids.Player1Id,
				sourceCardId: 0
			)
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(theirs).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"the 5/5 should have fought, not nobody"
			);
			Assert.That(DamageOn(final, weak), Is.Zero, "the 1/1 must not have been chosen");
		});
	}

	// ===== BEST TARGETING =====

	/// <summary>
	/// Best must pick by effective POWER, not by base power or mana cost — in a counters set a
	/// cheap creature with counters on it is the strongest thing you control.
	/// </summary>
	[Test]
	public void BestTargeting_PicksHighestEffectivePower_IncludingCounters()
	{
		var (state, small) = PutOnBattlefield(MakeCreature("Small", 1, 1));
		var (withBig, big) = AddToBattlefield(state, MakeCreature("Big", 3, 3), _ids.Player1Id);

		// Five counters make the "small" one a 6/6.
		var counted = Run(withBig, new AddCountersAction { Amount = 5, TargetIds = [small] });

		var picked = counted.PickStrongest([small, big]);

		Assert.That(picked, Is.EqualTo(ImmutableList.Create(small)));
	}

	// ===== CONDITIONS =====

	/// <summary>
	/// The next-turn case is the one people leave out, and an unreset counter passes every other
	/// assertion while making the card unconditionally on from turn two onward.
	/// </summary>
	[Test]
	public void CreatureDiedThisTurn_CountsDeaths_AndResetsEachTurn()
	{
		var condition = new CreatureDiedThisTurnCondition();
		var (state, id) = PutOnBattlefield(MakeCreature("Doomed", 2, 2));

		Assert.That(
			condition.IsSatisfied(state, 0, _ids.Player1Id),
			Is.False,
			"nothing has died yet"
		);

		state = Run(state, new DestroyCreatureAction { TargetIds = [id] });

		Assert.That(
			condition.IsSatisfied(state, 0, _ids.Player1Id),
			Is.True,
			"a creature died this turn"
		);

		var (afterTurn, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield),
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(
			condition.IsSatisfied(afterTurn, 0, _ids.Player1Id),
			Is.False,
			"the counter must reset, or the card is unconditionally on forever"
		);
	}

	/// <summary>
	/// "You control ANOTHER Elf" is expressed as a minimum of two because the card counts itself.
	/// One Elf must not satisfy it, or Dwynen's Elite always makes its token.
	/// </summary>
	[Test]
	public void ControlsSubtype_CountsCorrectly()
	{
		var condition = new ControlsSubtypeCondition { Subtype = "Elf", Minimum = 2 };

		var (state, _) = PutOnBattlefield(MakeCreature("Elf One", 1, 1, "Elf"));
		Assert.That(
			condition.IsSatisfied(state, 0, _ids.Player1Id),
			Is.False,
			"one Elf is not another Elf"
		);

		var (withSecond, _) = AddToBattlefield(
			state,
			MakeCreature("Elf Two", 1, 1, "Elf"),
			_ids.Player1Id
		);
		Assert.That(condition.IsSatisfied(withSecond, 0, _ids.Player1Id), Is.True);
	}

	// ===== COST REDUCTION THAT INSPECTS THE CARD =====

	/// <summary>
	/// Three assertions, three independent ways to get the battlefield scan wrong: no filter,
	/// inverted filter, or scanning the opponent's side as well.
	/// </summary>
	[Test]
	public void CostReductionFromAPermanent_AppliesOnlyToMatchingCardsAndOnlyToYou()
	{
		var reducer = MakeCreature("Reducer", 4, 4) with
		{
			Components = MakeCreature("Reducer", 4, 4)
				.Components.Add(
					new ConditionalCostReductionComponent
					{
						Amount = 2,
						AppliesTo = new IsCardTypeSpecification { Types = CardType.Creature }.And(
							new PowerAtLeastSpecification { Minimum = 4 }
						),
					}
				),
		};

		var (state, _) = PutOnBattlefield(reducer);

		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withBig, big) = state.AddObject(
			MakeCreature("Big", 5, 5) with
			{
				ManaCost = 6,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);
		var (withSmall, small) = withBig.AddObject(
			MakeCreature("Small", 2, 2) with
			{
				ManaCost = 3,
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var theirHandId = withSmall.GetPlayerZoneId(_ids.Player2Id, ZoneType.Hand);
		var (final, theirBig) = withSmall.AddObject(
			MakeCreature("Their Big", 5, 5) with
			{
				ManaCost = 6,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: theirHandId
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.ComputeEffectiveCost((Card)final.GetObject(big.Id), _ids.Player1Id),
				Is.EqualTo(4),
				"a power-5 creature should be discounted"
			);
			Assert.That(
				final.ComputeEffectiveCost((Card)final.GetObject(small.Id), _ids.Player1Id),
				Is.EqualTo(3),
				"a power-2 creature must not be discounted"
			);
			Assert.That(
				final.ComputeEffectiveCost((Card)final.GetObject(theirBig.Id), _ids.Player2Id),
				Is.EqualTo(6),
				"a discount must not cross the table — that is the ComputeTax asymmetry"
			);
		});
	}

	// ===== GREATEST POWER =====

	/// <summary>
	/// The query must emit BOTH the number and the creature list — a mass buff needs both, and a
	/// pipeline cannot receive mass targets any other way.
	/// </summary>
	[Test]
	public void GreatestPower_EmitsTheMaximumAndTheCreatureList()
	{
		var (state, a) = PutOnBattlefield(MakeCreature("A", 2, 2));
		var (withB, b) = AddToBattlefield(state, MakeCreature("B", 5, 1), _ids.Player1Id);
		var (withTheirs, _) = AddToBattlefield(withB, MakeCreature("Theirs", 9, 9), _ids.Player2Id);

		var result = new CountGreatestPowerAction
		{
			PlayerId = _ids.Player1Id,
			OutputKey = "power",
			CreatureIdsOutputKey = "ids",
		}.Execute(withTheirs);

		Assert.Multiple(() =>
		{
			Assert.That(
				result.OutputData["power"],
				Is.EqualTo(5),
				"must be YOUR greatest power, not the opponent's 9"
			);
			Assert.That(result.OutputData["ids"], Is.EquivalentTo(new[] { a, b }));
		});
	}

	// ===== REGRESSIONS FIXED BY THE GREEN WORK =====

	/// <summary>
	/// Chasm Skulker shipped making ZERO tokens. Its counters were a StaticPowerToughnessModifier,
	/// which MoveCardTracked strips on the way to the graveyard — before the death trigger
	/// resolves — so the count measured power 1, applied its -1 offset, and created nothing.
	/// Now that counters are real and survive into the graveyard, it counts them.
	/// </summary>
	[Test]
	public void ChasmSkulker_MakesOneTokenPerCounter_WhenItDies()
	{
		var template = CoresetCubeBlue.Cards.First(c => c.Name == "Chasm Skulker");
		var (state, id) = PutOnBattlefield(template);

		state = Run(state, new AddCountersAction { Amount = 3, TargetIds = [id] });
		Assert.That(CounterCount(state, id), Is.EqualTo(3), "setup");

		var before = CountBattlefield(state, "Squid");
		state = Run(state, new DestroyCreatureAction { TargetIds = [id] });

		Assert.That(
			CountBattlefield(state, "Squid") - before,
			Is.EqualTo(3),
			"a death trigger must still see the counters the creature had"
		);
	}

	/// <summary>
	/// Set Upon the Pack is a fight SPELL, so its SourceCardId is the spell — which has no
	/// CreatureComponent. FightAction bailed out silently, and the card was a complete no-op at
	/// full price for its whole shipped life.
	/// </summary>
	[Test]
	public void SetUponThePack_ActuallyFights()
	{
		var template = Hollowmere.Cards.First(c => c.Name == "Set Upon the Pack");

		var (state, mine) = PutOnBattlefield(MakeCreature("Mine", 5, 5));
		var (withTheirs, theirs) = AddToBattlefield(
			state,
			MakeCreature("Theirs", 2, 2),
			_ids.Player2Id
		);

		var handId = withTheirs.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = withTheirs.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var spell = ((Card)withCard.GetObject(card.Id)).GetComponent<SpellComponent>()!;
		var index = spell.Effects.FindIndex(e => e.TargetingStrategy.RequiresUserSelection);

		var (final, _) = withCard
			.AddAction(
				new CastSpellAction
				{
					CardId = card.Id,
					CastingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						index,
						ImmutableList.Create(theirs)
					),
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(theirs).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"the 5/5 should have killed the 2/2"
			);
			Assert.That(
				DamageOn(final, mine),
				Is.EqualTo(2),
				"and taken 2 back — a fight is mutual"
			);
		});
	}

	// ===== HELPERS =====

	private static int CountBattlefield(GameState state, string name) =>
		state
			.GetCardsInZone(
				state.GetPlayerZoneId(
					state.GetWellKnownId(MtgObjectKeys.Player1),
					ZoneType.Battlefield
				)
			)
			.Count(c => c.Name == name);

	private static int CounterCount(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<PlusOneCounterComponent>()?.Count ?? 0;

	private static int DamageOn(GameState state, int cardId) =>
		state.HasObject(cardId)
			? ((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()?.Damage ?? 0
			: 0;

	private static Card MakeCreature(string name, int power, int toughness, string subtype = "") =>
		new()
		{
			Name = name,
			Types = CardType.Creature,
			Subtypes = string.IsNullOrEmpty(subtype)
				? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.OrdinalIgnoreCase)
				: ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private (GameState State, int CardId) PutOnBattlefield(Card template)
	{
		var (state, id) = AddToBattlefield(_state, template, _ids.Player1Id);
		return (state, id);
	}

	private static (GameState State, int CardId) AddToBattlefield(
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

	private static GameState Run(GameState state, GameAction action)
	{
		var (final, _) = state.AddAction(action).ProcessAllActions();
		return final;
	}
}

internal static class GreenTestExtensions
{
	/// <summary>Seeds the two context keys ResolveEffectAction would normally inject.</summary>
	public static FightAction WithInputs(this FightAction action, int playerId, int sourceCardId)
	{
		var context = ImmutableDictionary<string, object>
			.Empty.SetItem(ContextKeys.CastingPlayerId, playerId)
			.SetItem(ContextKeys.SourceCardId, sourceCardId);

		return action with
		{
			InputContext = context,
		};
	}
}
