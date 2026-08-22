using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The Core Set Cube's colourless section, mirroring CoresetCubeGreenTests and CoresetCubeRedTests.
///
/// Two halves. The structural tests assert shape and wiring — they look cards up by name
/// deliberately, because they test the SET. The behavioural tests assert a board or zone
/// CONSEQUENCE for the cards whose whole point is a mechanic that could silently no-op; the
/// mechanics themselves are covered card-agnostically in ColourlessMechanicsTests, so a balance
/// tweak here cannot delete that coverage.
///
/// The artifact-type test is the one worth reading twice. Card.EffectiveTypes short-circuits on
/// declared Types, so a creature built with only .WithSubtype("Artifact") reports
/// HasType(CardType.Artifact) as false — while affinity and sacrifice costs read the subtype and
/// IsCardTypeSpecification reads the flag. Every legacy artifact creature in CardLibrary has that
/// bug and is invisible to half the engine.
/// </summary>
[TestFixture]
public class CoresetCubeColourlessTests
{
	private static readonly IReadOnlyList<Card> All =
	[
		.. CoresetCubeColourlessCreatures.Cards,
		.. CoresetCubeColourlessArtifacts.Cards,
		.. CoresetCubeColourlessEquipment.Cards,
	];

	private static Card Find(string name) =>
		All.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

	// ===== STRUCTURE =====

	[Test]
	public void SectionCounts_MatchTheCube()
	{
		Assert.Multiple(() =>
		{
			Assert.That(
				CoresetCubeColourlessCreatures.Cards,
				Has.Count.EqualTo(11),
				"11 artifact creatures"
			);
			Assert.That(
				CoresetCubeColourlessArtifacts.Cards,
				Has.Count.EqualTo(18),
				"17 artifacts + Ugin"
			);
			Assert.That(
				CoresetCubeColourlessEquipment.Cards,
				Has.Count.EqualTo(14),
				"14 equipment"
			);
			Assert.That(
				CoresetCube.Cards,
				Has.Count.EqualTo(408),
				"335 coloured + 43 colourless + 30 multicolour; the cube's 42 lands are cut"
			);
		});
	}

	/// <summary>
	/// The trap described in the class summary. Both the flag and the subtype string, on every
	/// card here, or the card is invisible to half the engine.
	/// </summary>
	[Test]
	public void EveryArtifact_DeclaresBothTheTypeAndTheSubtype()
	{
		var broken = All.Where(c => !c.HasType(CardType.Artifact) || !c.HasSubtype("Artifact"))
			.Where(c => !c.HasComponent<PlaneswalkerComponent>())
			.Select(c =>
				$"{c.Name} (type={c.HasType(CardType.Artifact)}, subtype={c.HasSubtype("Artifact")})"
			)
			.ToList();

		Assert.That(broken, Is.Empty, $"Half-declared artifacts: {string.Join("; ", broken)}");
	}

	/// <summary>
	/// Affinity already existed and counts artifact permanents by subtype. These 43 cards are the
	/// cheapest artifacts in the cube, so this section is what turns affinity from a dead keyword
	/// into a deck — if the subtype is on them.
	/// </summary>
	[Test]
	public void TheSectionFeedsAffinity()
	{
		var cheapArtifacts = All.Count(c => c.HasSubtype("Artifact") && c.ManaCost <= 3);

		Assert.That(cheapArtifacts, Is.GreaterThanOrEqualTo(15), "affinity needs a critical mass");
	}

	[Test]
	public void EveryEquipment_HasARepeatableEquipAbility()
	{
		var equipment = All.Where(c => c.GetComponent<EquipmentComponent>() != null).ToList();

		Assert.That(equipment, Has.Count.EqualTo(14));

		foreach (var card in equipment)
		{
			var equip = card.GetComponents<ActivatedAbilityComponent>()
				.FirstOrDefault(a => a.Name == "Equip");

			Assert.That(equip, Is.Not.Null, $"{card.Name} has no equip ability");
			Assert.That(
				equip!.MaxActivationsPerTurn,
				Is.Zero,
				$"{card.Name}'s equip must be repeatable — moving a sword off a dying creature "
					+ "is most of what Equipment does"
			);
		}
	}

	/// <summary>
	/// No Equipment here is an Aura. They ride the same component and differ only by IsAura, which
	/// changes how they attach and what happens when the wearer leaves — a mislabelled Equipment
	/// would attach on ETB and then die with its creature.
	/// </summary>
	[Test]
	public void NoEquipment_IsMarkedAsAnAura()
	{
		var auras = All.Where(c => c.GetComponent<EquipmentComponent>()?.IsAura == true)
			.Select(c => c.Name)
			.ToList();

		Assert.That(auras, Is.Empty);
	}

	[Test]
	public void Ugin_IsAPlaneswalkerAndNotACreature()
	{
		var ugin = Find("Ugin, the Spirit Dragon");

		Assert.Multiple(() =>
		{
			Assert.That(ugin.GetComponent<PlaneswalkerComponent>()?.StartingLoyalty, Is.EqualTo(7));
			Assert.That(
				ugin.HasComponent<CreatureComponent>(),
				Is.False,
				"it would route through CastCreatureAction"
			);
			Assert.That(
				ugin.GetComponents<ActivatedAbilityComponent>().Count(a => a.IsLoyaltyAbility),
				Is.EqualTo(3)
			);
		});
	}

	/// <summary>
	/// Every mana producer in this section is an upkeep TRIGGER, never an activated ability — see
	/// the file headers for why. A rock that slipped through as an activated ability would be
	/// invisible to the AI, which would simply never turn it on.
	/// </summary>
	[Test]
	public void ManaProducers_AreUpkeepTriggers_NotActivatedAbilities()
	{
		foreach (var name in new[] { "Gilded Lotus", "Meteorite", "Scuttlemutt", "Dragon's Hoard" })
		{
			var card = Find(name);

			var activatedMana = card.GetComponents<ActivatedAbilityComponent>()
				.SelectMany(a => a.Effects)
				.Any(e => e.ActionTemplate is AddTemporaryManaAction);

			var triggeredMana = card.GetComponents<TriggeredAbilityComponent>()
				.SelectMany(t => t.Effects)
				.Any(e => e.ActionTemplate is AddTemporaryManaAction);

			Assert.Multiple(() =>
			{
				Assert.That(triggeredMana, Is.True, $"{name} must produce on upkeep");
				Assert.That(activatedMana, Is.False, $"{name} must not need activating");
			});
		}
	}

	// ===== BEHAVIOUR =====
	// One test per card whose whole point is a mechanic that can silently no-op. A card that
	// builds, casts and resolves without erroring while doing nothing scores the same ~40% as a
	// merely weak card, and only one of the two is a bug.

	/// <summary>
	/// Hangarback Walker end to end: it must enter with X counters, grow, and — the part that
	/// breaks — make that many Thopters when it dies. Chasm Skulker made ZERO tokens for exactly
	/// this reason before counters moved to entry-stripping.
	/// </summary>
	[Test]
	public void HangarbackWalker_MakesOneThopterPerCounter_WhenItDies()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withCard, id) = PutOnBattlefield(state, Find("Hangarback Walker"), ids.Player1Id);

		withCard = Run(withCard, new AddCountersAction { Amount = 3, TargetIds = [id] });

		Assert.That(withCard.GetEffectivePower(id), Is.EqualTo(3), "counters drive its body");

		withCard = Run(withCard, new DestroyCreatureAction { TargetIds = [id] });
		withCard = Run(withCard, StateBasedCheck(withCard, ids));

		var battlefieldId = withCard.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var thopters = withCard.GetCardsInZone(battlefieldId).Count(c => c.Name == "Thopter");

		Assert.That(thopters, Is.EqualTo(3), "one per counter, read off the dead card");
	}

	/// <summary>
	/// Solemn Simulacrum's fetch is "onto the battlefield TAPPED", which here means MaxMana rises
	/// and CurrentMana does not. Without Deferred it would be strictly better than printed.
	/// </summary>
	[Test]
	public void SolemnSimulacrum_RampsButNotThisTurn()
	{
		var (state, ids) = MtgGameFactory.Create();
		var before = (MtgPlayer)state.GetObject(ids.Player1Id);

		var (withCard, id) = PutOnBattlefield(state, Find("Solemn Simulacrum"), ids.Player1Id);
		withCard = Run(withCard, new PutIntoBattlefieldAction { TargetIds = [id] });
		withCard = Run(withCard, StateBasedCheck(withCard, ids));

		var after = (MtgPlayer)withCard.GetObject(ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(after.MaxMana, Is.EqualTo(before.MaxMana + 1));
			Assert.That(after.CurrentMana, Is.EqualTo(before.CurrentMana), "tapped");
		});
	}

	/// <summary>
	/// Perilous Vault is the first sweeper in the project that reaches past creatures. If
	/// NonlandPermanents() misses a type, the card is a worse Day of Judgment and nothing errors.
	/// </summary>
	[Test]
	public void PerilousVault_ExilesEveryPermanentTypeOnBothSides()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		var (s1, vaultId) = PutOnBattlefield(state, Find("Perilous Vault"), ids.Player1Id);
		var (s2, creatureId) = PutOnBattlefield(s1, Find("Skyscanner"), ids.Player1Id);
		var (s3, walkerId) = PutOnBattlefield(s2, Find("Ugin, the Spirit Dragon"), ids.Player2Id);
		var (s4, swordId) = PutOnBattlefield(s3, Find("Greatsword"), ids.Player2Id);

		// SacrificeSelf is a SELECTION cost, so the payment has to be supplied — the generator
		// does this for the AI via BuildAdditionalCostPayments, and a hand-built action must too.
		var purgeIndex = AbilityIndex(Find("Perilous Vault"), "Purge");
		var final = Run(
			s4,
			Activate(vaultId, ids.Player1Id, purgeIndex) with
			{
				AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					0,
					[vaultId]
				),
			}
		);

		var battlefield1 = final.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var battlefield2 = final.GetPlayerZoneId(ids.Player2Id, ZoneType.Battlefield);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetChildrenIds(battlefield1), Is.Empty, "your side too");
			Assert.That(final.GetChildrenIds(battlefield2), Is.Empty);
			foreach (var id in new[] { creatureId, walkerId, swordId })
				Assert.That(
					final.GetCardZone(id).ZoneType,
					Is.EqualTo(ZoneType.Exile),
					$"card {id} must be exiled, not merely off the battlefield"
				);
		});
	}

	/// <summary>
	/// The Empires payoff. Both halves must be true at once: the base effect always happens, and
	/// the upgrade happens ONLY with both partners out. A condition that reads true unconditionally
	/// is the plausible wrong implementation and would be invisible in a normal game, because the
	/// trio almost never assembles.
	/// </summary>
	[Test]
	public void ThroneOfEmpires_MakesOneSoldierAlone_AndFiveWithTheSet()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (alone, throneId) = PutOnBattlefield(state, Find("Throne of Empires"), ids.Player1Id);

		var muster = AbilityIndex(Find("Throne of Empires"), "Muster");
		var afterAlone = Run(alone, Activate(throneId, ids.Player1Id, muster));

		Assert.That(SoldierCount(afterAlone, ids.Player1Id), Is.EqualTo(1), "base effect only");

		// Muster is once per turn, so the assembled case branches off the pre-activation board
		// rather than activating a second time.
		var (withCrown, _) = PutOnBattlefield(alone, Find("Crown of Empires"), ids.Player1Id);
		var (withSet, _) = PutOnBattlefield(withCrown, Find("Scepter of Empires"), ids.Player1Id);

		var afterSet = Run(withSet, Activate(throneId, ids.Player1Id, muster));

		Assert.That(
			SoldierCount(afterSet, ids.Player1Id),
			Is.EqualTo(5),
			"1 from the base effect plus 4 more with the set assembled"
		);
	}

	/// <summary>
	/// Dragon's Hoard is the cube's only counter-as-currency card. The trigger must supply
	/// counters, the cost must spend them, and the ability must be unusable without one.
	/// </summary>
	[Test]
	public void DragonsHoard_AccumulatesGoldCountersAndSpendsThem()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withHoard, hoardId) = PutOnBattlefield(state, Find("Dragon's Hoard"), ids.Player1Id);

		var spend = AbilityIndex(Find("Dragon's Hoard"), "Spend the Hoard");

		Assert.That(
			Activate(hoardId, ids.Player1Id, spend).ValidateAdd(withHoard).IsValid,
			Is.False,
			"no counters yet"
		);

		// Golos costs 5, so it clears the "mana value 5 or greater" filter; Skyscanner costs 3
		// and must not.
		var (withCheap, cheapId) = PutOnBattlefield(withHoard, Find("Skyscanner"), ids.Player1Id);
		var afterCheap = Run(withCheap, new PutIntoBattlefieldAction { TargetIds = [cheapId] });
		afterCheap = Run(afterCheap, StateBasedCheck(afterCheap, ids));

		Assert.That(GoldCount(afterCheap, hoardId), Is.Zero, "a 3-drop must not feed the hoard");

		var (withBig, bigId) = PutOnBattlefield(
			afterCheap,
			Find("Golos, Tireless Pilgrim"),
			ids.Player1Id
		);
		var afterBig = Run(withBig, new PutIntoBattlefieldAction { TargetIds = [bigId] });
		afterBig = Run(afterBig, StateBasedCheck(afterBig, ids));

		Assert.That(GoldCount(afterBig, hoardId), Is.EqualTo(1), "a 5-drop must");

		var afterSpend = Run(afterBig, Activate(hoardId, ids.Player1Id, spend));

		Assert.That(GoldCount(afterSpend, hoardId), Is.Zero, "activating must spend the counter");
	}

	/// <summary>
	/// Akroma's Memorial is a static grant from a NON-creature source. Every other
	/// StaticGrantKeywordAbility in the repo sits on a creature, so this is the first card to
	/// exercise the PermanentEnteredBattlefieldEvent arm of the engine.
	/// </summary>
	[Test]
	public void AkromasMemorial_GrantsItsKeywordsFromANonCreatureSource()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withCreature, creatureId) = PutOnBattlefield(
			state,
			Find("Guardian Automaton"),
			ids.Player1Id
		);

		Assert.That(withCreature.GetEffectiveStats(creatureId).HasFlying, Is.False, "precondition");

		var (withMemorial, memorialId) = PutOnBattlefield(
			withCreature,
			Find("Akroma's Memorial"),
			ids.Player1Id
		);
		var final = StaticAbilityEngine.ProcessPermanentEntered(
			withMemorial,
			memorialId,
			ids.GameId
		);

		var stats = final.GetEffectiveStats(creatureId);

		Assert.Multiple(() =>
		{
			Assert.That(stats.HasFlying, Is.True);
			Assert.That(stats.StrikesFirst, Is.True);
			Assert.That(stats.HasTrample, Is.True);
			Assert.That(stats.HasHaste, Is.True);
		});
	}

	/// <summary>
	/// Haunted Plate Mail's animate mode, gated on its own condition. Both directions matter: it
	/// must be unavailable with a creature out, and must actually produce a body without one.
	/// </summary>
	[Test]
	public void HauntedPlateMail_AnimatesOnlyOnAnEmptyBoard()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withMail, mailId) = PutOnBattlefield(state, Find("Haunted Plate Mail"), ids.Player1Id);

		var animate = AbilityIndex(Find("Haunted Plate Mail"), "Animate");

		Assert.That(
			Activate(mailId, ids.Player1Id, animate).ValidateAdd(withMail).IsValid,
			Is.True,
			"no creatures — it may animate"
		);

		var (withCreature, _) = PutOnBattlefield(withMail, Find("Skyscanner"), ids.Player1Id);

		Assert.That(
			Activate(mailId, ids.Player1Id, animate).ValidateAdd(withCreature).IsValid,
			Is.False,
			"a creature on board shuts it off"
		);

		var animated = Run(withMail, Activate(mailId, ids.Player1Id, animate));

		Assert.Multiple(() =>
		{
			Assert.That(
				((Card)animated.GetObject(mailId)).HasComponent<CreatureComponent>(),
				Is.True
			);
			Assert.That(animated.GetEffectivePower(mailId), Is.EqualTo(4));
		});
	}

	/// <summary>
	/// The Rings' upkeep counter is the entire reason the five are not near-identical vanilla
	/// equipment. It must land on the WEARER, and must not fire while the Ring is unattached.
	/// </summary>
	[Test]
	public void RingUpkeep_GrowsTheWearerOnly()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withCreature, creatureId) = PutOnBattlefield(
			state,
			Find("Guardian Automaton"),
			ids.Player1Id
		);
		var (withBystander, bystanderId) = PutOnBattlefield(
			withCreature,
			Find("Skyscanner"),
			ids.Player1Id
		);
		var (withRing, ringId) = PutOnBattlefield(
			withBystander,
			Find("Ring of Thune"),
			ids.Player1Id
		);

		var equip = AbilityIndex(Find("Ring of Thune"), "Equip");
		var attached = Run(
			withRing,
			Activate(ringId, ids.Player1Id, equip) with
			{
				TargetIds = [creatureId],
			}
		);

		var upkeep = Run(
			attached,
			new StartTurnAction
			{
				ActivePlayerId = ids.Player1Id,
				BattlefieldId = attached.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield),
				SkipDraw = true,
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(upkeep, creatureId), Is.EqualTo(1), "the wearer grows");
			Assert.That(CounterCount(upkeep, bystanderId), Is.Zero, "and nobody else does");
		});
	}

	// ===== HELPERS =====

	private static ActivateAbilityAction Activate(int cardId, int playerId, int index) =>
		new()
		{
			CardId = cardId,
			ActivatingPlayerId = playerId,
			AbilityIndex = index,
		};

	private static int AbilityIndex(Card card, string name) =>
		card.GetComponents<ActivatedAbilityComponent>().ToList().FindIndex(a => a.Name == name);

	private static int CounterCount(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<PlusOneCounterComponent>()?.Count ?? 0;

	private static int GoldCount(GameState state, int cardId) =>
		((Card)state.GetObject(cardId))
			.GetComponents<ChargeCounterComponent>()
			.FirstOrDefault(c => c.IsKind("gold"))
			?.Count ?? 0;

	private static int SoldierCount(GameState state, int playerId) =>
		state
			.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Battlefield))
			.Count(c => c.Name == "Soldier");

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

		// A planeswalker's loyalty is stamped as it enters play; a hand-placed one would sit at 0
		// and die on the next state-based check.
		if (card.HasComponent<PlaneswalkerComponent>())
			withCard = withCard.StampPlaneswalkerEntry(card.Id);

		return (withCard, card.Id);
	}

	private static GameState Run(GameState state, GameAction action)
	{
		var (final, _) = state.AddAction(action).ProcessAllActions();
		return final;
	}
}
