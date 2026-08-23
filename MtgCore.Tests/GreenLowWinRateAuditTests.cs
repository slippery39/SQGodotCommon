using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The ten green cards that came back at the bottom of the win-rate table (39–43%), audited one
/// by one for whether they DO anything.
///
/// A blank card lands around 40% in this metric, which is what makes that band worth auditing
/// rather than rebalancing: a card that is merely weak and a card that is inert score the same,
/// and only one of them is a bug. Every test here asserts the CONSEQUENCE on the board or in a
/// zone — "it resolved without throwing" is exactly what nine inert white cards once passed.
///
/// Two real bugs were found this way and are pinned below: WithDig was a query with no consumer,
/// and IsCreatureSpecification is battlefield-only so every library tutor filtered on it matched
/// nothing.
/// </summary>
[TestFixture]
public class GreenLowWinRateAuditTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	private static Card Find(string name) =>
		CoresetCube.Cards.First(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	// ===== THE LIBRARY TUTORS =====

	/// <summary>
	/// Shared Summons must actually put two creature cards into your hand. Filtered on
	/// IsCreatureSpecification, which requires the candidate to be ON THE BATTLEFIELD, it matched
	/// nothing in a library and the spell was a five-mana blank.
	/// </summary>
	[Test]
	public void SharedSummons_PutsTwoCreatureCardsIntoYourHand()
	{
		var state = WithLibrary(_state, "Grizzly One", "Grizzly Two", "Grizzly Three");
		var handBefore = HandCount(state);

		state = CastSpell(state, Find("Shared Summons"));

		Assert.That(
			HandCount(state) - handBefore,
			Is.EqualTo(2),
			"Shared Summons searches for TWO creature cards"
		);
	}

	// Evolutionary Leap's activated ability is gone — it is now a death trigger that puts a
	// strictly cheaper creature onto the battlefield, pinned by LowWinRateRebuildTests.
	//
	// The audit this fixture performs still stands and is worth recording: the card was NOT inert.
	// The tutor worked (once IsCreatureSpecification was replaced with IsCardTypeSpecification —
	// see below). It measured at ~39% because the AI would not pay a creature plus a mana for a
	// card it could not cast until next turn, which MtgActionGenerator made worse by offering
	// exactly one sacrifice payment. An inert card and a card the AI refuses to play look
	// identical in a win-rate table and need opposite fixes.

	/// <summary>
	/// Woodland Bellower's six mana buys a 6/5 AND a free creature. Without the second half it is
	/// an overcosted vanilla.
	/// </summary>
	[Test]
	public void WoodlandBellower_PutsACheapCreatureOntoTheBattlefield()
	{
		var state = WithLibrary(_state, "Grizzly One");
		var before = BattlefieldCount(state);

		state = CastCreature(state, Find("Woodland Bellower"));

		Assert.That(
			BattlefieldCount(state) - before,
			Is.EqualTo(2),
			"the Bellower itself plus the creature it fetched"
		);
	}

	/// <summary>Fauna Shaman turns a dead creature in hand into the one you need.</summary>
	[Test]
	public void FaunaShaman_SearchesYourLibraryForACreature()
	{
		var state = WithLibrary(_state, "Grizzly One", "Grizzly Two");
		var (withShaman, shaman) = AddTo(state, Find("Fauna Shaman"), ZoneType.Battlefield);
		var (withPitch, _) = AddTo(withShaman, MakeCreature("Pitch Me"), ZoneType.Hand);

		// Clear summoning sickness so the tap cost is payable.
		withPitch = ReadyCreature(withPitch, shaman);

		var handBefore = HandCount(withPitch);
		var after = ActivateFirstAbility(withPitch, shaman);

		Assert.That(
			HandCount(after),
			Is.EqualTo(handBefore),
			"discard one, fetch one — a net-zero hand means the fetch happened; -1 means it did not"
		);
	}

	// ===== THE DIG CARDS =====

	/// <summary>
	/// "Look at the top N cards, put one into your hand" must move a card. WithDig built a bare
	/// LookAtTopCardsAction, which writes card IDs into pipeline context and stops — nothing ever
	/// consumed them, so every dig card in the cube drew nothing at all.
	/// </summary>
	[Test]
	public void TrackDown_ActuallyDrawsACard()
	{
		var state = WithLibrary(
			_state,
			"Grizzly One",
			"Grizzly Two",
			"Grizzly Three",
			"Grizzly Four"
		);
		var handBefore = HandCount(state);

		state = CastSpell(state, Find("Track Down"));

		Assert.That(HandCount(state) - handBefore, Is.EqualTo(1), "Track Down is a cantrip");
	}

	/// <summary>
	/// The same card, with a LAND on top — which is what a real library looks like roughly a third
	/// of the time.
	///
	/// The passing test above stocks four creatures and no lands, so it never exercises
	/// SelectFromRevealedAction's AllowLands = false filter. With a land revealed the dig matches
	/// nothing, writes 0, and the mover no-ops: the cantrip silently does not happen. That is the
	/// QA report ("track down didn't put a card in my hand") and no card definition is wrong.
	///
	/// A dig is "put one of them into your hand" — the player takes the land if the land is what
	/// is there. Excluding lands is right for a TUTOR that ranks by mana cost; it is wrong for a
	/// reveal the player is choosing from.
	/// </summary>
	[Test]
	public void TrackDown_StillDrawsWhenTheTopCardIsALand()
	{
		var (state, _) = AddTo(_state, MakeLand("Forest"), ZoneType.Library);
		var handBefore = HandCount(state);

		state = CastSpell(state, Find("Track Down"));

		Assert.That(
			HandCount(state) - handBefore,
			Is.EqualTo(1),
			"a dig must not whiff just because the card revealed is a land"
		);
	}

	[Test]
	public void LlanowarEmpath_PutsACardIntoYourHand()
	{
		var state = WithLibrary(_state, "Grizzly One", "Grizzly Two", "Grizzly Three");
		var handBefore = HandCount(state);

		state = CastCreature(state, Find("Llanowar Empath"));

		Assert.That(HandCount(state) - handBefore, Is.EqualTo(1), "its ETB is its whole value");
	}

	// ===== THE REST =====

	/// <summary>
	/// The mass buff needs both a number and a target LIST out of one pipeline. If either half
	/// fails to cross a step boundary the spell buffs nobody and renders perfectly.
	/// </summary>
	[Test]
	public void OverwhelmingStampede_BuffsEveryCreatureYouControl()
	{
		var (withBig, big) = AddTo(_state, MakeCreature("Big", 5, 5), ZoneType.Battlefield);
		var (withSmall, small) = AddTo(withBig, MakeCreature("Small", 1, 1), ZoneType.Battlefield);

		var after = CastSpell(withSmall, Find("Overwhelming Stampede"));

		Assert.Multiple(() =>
		{
			Assert.That(after.GetEffectivePower(small), Is.EqualTo(6), "1 + greatest power 5");
			Assert.That(after.GetEffectivePower(big), Is.EqualTo(10), "5 + 5");
			Assert.That(after.GetEffectiveStats(small).HasTrample, Is.True, "and trample");
		});
	}

	/// <summary>
	/// Two different targets on one spell: the engine picks yours, the player picks theirs. Both
	/// halves must land, and the buff must apply BEFORE the fight or it changes nothing.
	/// </summary>
	[Test]
	public void WildInstincts_BuffsYourCreatureThenFights()
	{
		var (withMine, mine) = AddTo(_state, MakeCreature("Mine", 2, 2), ZoneType.Battlefield);
		var (withTheirs, theirs) = AddToPlayer(
			withMine,
			MakeCreature("Theirs", 4, 4),
			ZoneType.Battlefield,
			_ids.Player2Id
		);

		var after = CastSpell(withTheirs, Find("Wild Instincts"), theirs);

		Assert.Multiple(() =>
		{
			Assert.That(
				after.GetCardZone(theirs).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"a 2/2 pumped to 4/4 kills a 4/4 — without the buff it bounces off"
			);
			Assert.That(
				after.GetCardZone(mine).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"and dies to the 4 coming back — Wild Instincts is a real fight, not one-sided"
			);
		});
	}

	/// <summary>Return to Nature must be able to destroy an artifact.</summary>
	[Test]
	public void ReturnToNature_DestroysAnArtifact()
	{
		var (withArtifact, artifact) = AddToPlayer(
			_state,
			Find("Wolfrider's Saddle"),
			ZoneType.Battlefield,
			_ids.Player2Id
		);

		var after = CastSpell(withArtifact, Find("Return to Nature"), artifact);

		Assert.That(
			after.GetCardZone(artifact).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"mode 1 is 'destroy target artifact'"
		);
	}

	/// <summary>
	/// Master of the Wild Hunt's tap ability scales with Wolves. With no Wolf it deals zero, so
	/// the upkeep token must actually arrive for the card to do anything at all.
	/// </summary>
	[Test]
	public void MasterOfTheWildHunt_MakesAWolfOnUpkeep()
	{
		var (state, _) = AddTo(_state, Find("Master of the Wild Hunt"), ZoneType.Battlefield);

		var (after, _) = state
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
			after
				.GetCardsInZone(after.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield))
				.Count(c => c.Name == "Wolf"),
			Is.EqualTo(1)
		);
	}

	/// <summary>
	/// The AI enumerates one cast action per affordable X. If the beam search collapses those to
	/// one by card id, a Hydra is only ever cast for the FIRST X offered — which is 0, and a 0/0
	/// dies the instant it arrives. That is indistinguishable from a blank card.
	/// </summary>
	[Test]
	public void Hydras_AreOfferedAtEveryAffordableX_NotJustZero()
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

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var (withHydra, hydra) = state.AddObject(
			Find("Primordial Hydra") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: handId
		);

		var xValues = MtgActionGenerator
			.GetLegalActions(withHydra, ids, ids.Player1Id)
			.OfType<CastCreatureAction>()
			.Where(a => a.CardId == hydra.Id)
			.Select(a => a.XValue)
			.ToList();

		Assert.That(
			xValues.Count(x => x > 0),
			Is.GreaterThan(0),
			"only X=0 was offered — the Hydra can only ever be cast as a 0/0 that dies on arrival"
		);
	}

	/// <summary>
	/// Wildwood Scourge cast for X must survive its own arrival. A 0/0 with no counters is killed
	/// by the zero-toughness rule before it does anything.
	/// </summary>
	[Test]
	public void WildwoodScourge_CastForX_SurvivesAndHasABody()
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.UpdateObject(
			ids.Player1Id,
			state.GetPlayer(ids.Player1Id) with
			{
				MaxMana = 4,
				CurrentMana = 4,
			}
		);

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			Find("Wildwood Scourge") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: handId
		);

		var (after, _) = withCard
			.AddAction(
				new CastCreatureAction
				{
					CardId = card.Id,
					CastingPlayerId = ids.Player1Id,
					XValue = 3,
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(after.GetCardZone(card.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
			Assert.That(after.GetEffectivePower(card.Id), Is.EqualTo(3));
		});
	}

	// ===== HELPERS =====

	/// <summary>
	/// Drives every pending ChoiceAction to a decision, taking the first MinChoices options.
	///
	/// GameState.ProcessAllActions deliberately STOPS at a choice — it is the AI strategy that
	/// resolves them in a real game (MultiTurnBeamSearchAiStrategy.ResolveAllChoices). A test that
	/// only calls ProcessAllActions therefore leaves any card containing a scry or a mode frozen
	/// mid-pipeline, and every effect after it looks inert. That is a harness artefact, not a card
	/// bug, and it is worth knowing before "fixing" a card that was fine: Track Down, Llanowar
	/// Empath and Return to Nature all looked broken here for exactly this reason.
	///
	/// First-option rather than a greedy heuristic keeps it predictable: mode 0 on a modal card,
	/// and "bottom nothing" on a scry, whose MinChoices is 0.
	/// </summary>
	private static GameState SettleChoices(GameState state)
	{
		for (var i = 0; i < 100 && state.IsWaitingForChoice; i++)
		{
			var choice = state.GetPendingChoice()!;
			var picks = choice.Options.Take(choice.MinChoices).Select(o => o.Id).ToImmutableList();
			(state, _) = state.ResolveChoice(picks);
			(state, _) = state.ProcessAllActions();
		}
		return state;
	}

	private static Card MakeCreature(string name, int power = 2, int toughness = 2) =>
		new()
		{
			Name = name,
			ManaCost = 2,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private GameState WithLibrary(GameState state, params string[] creatureNames)
	{
		foreach (var name in creatureNames)
			(state, _) = AddTo(state, MakeCreature(name), ZoneType.Library);
		return state;
	}

	private static Card MakeLand(string name) =>
		new()
		{
			Name = name,
			ManaCost = 0,
			Types = CardType.Land,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
		};

	private (GameState, int) AddTo(GameState state, Card template, ZoneType zone) =>
		AddToPlayer(state, template, zone, _ids.Player1Id);

	private static (GameState, int) AddToPlayer(
		GameState state,
		Card template,
		ZoneType zone,
		int playerId
	)
	{
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = playerId,
				ControllerId = playerId,
			},
			parentId: state.GetPlayerZoneId(playerId, zone)
		);
		return (withCard, card.Id);
	}

	private int HandCount(GameState state) =>
		state.GetCardsInZone(state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)).Count();

	private int BattlefieldCount(GameState state) =>
		state.GetCardsInZone(state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)).Count();

	private static GameState ReadyCreature(GameState state, int cardId)
	{
		var card = (Card)state.GetObject(cardId);
		var creature = card.GetComponent<CreatureComponent>()!;
		return state.UpdateObject(
			cardId,
			card.WithComponentReplaced(creature with { HasSummoningSickness = false })
		);
	}

	private GameState CastSpell(GameState state, Card template, int targetId = 0)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var cast = new CastSpellAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id };
		if (targetId != 0)
		{
			var spell = ((Card)withCard.GetObject(card.Id)).GetComponent<SpellComponent>()!;
			var index = spell.Effects.FindIndex(e => e.TargetingStrategy.RequiresUserSelection);
			if (index >= 0)
				cast = cast with
				{
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						index,
						ImmutableList.Create(targetId)
					),
				};
		}

		var (final, _) = withCard.AddAction(cast).ProcessAllActions();
		return SettleChoices(final);
	}

	private GameState CastCreature(GameState state, Card template)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var (final, _) = withCard
			.AddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();
		return SettleChoices(final);
	}

	private GameState ActivateFirstAbility(GameState state, int cardId)
	{
		var actions = MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == cardId)
			.ToList();

		Assert.That(actions, Is.Not.Empty, "the ability was not even offered as a legal action");

		var (final, _) = state.AddAction(actions[0]).ProcessAllActions();
		return SettleChoices(final);
	}
}
