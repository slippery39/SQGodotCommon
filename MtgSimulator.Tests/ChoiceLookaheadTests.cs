using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgSimulator.Tests;

/// <summary>
/// Tests that ResolveChoice uses LookaheadScore to see downstream value —
/// i.e. keeping Reanimate to cast on a discarded creature, discarding Bloodghast
/// to return it via land play, and picking a creature over a land via Sleight of Hand.
/// </summary>
[TestFixture]
public class ChoiceLookaheadTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private BeamSearchAiStrategy _ai;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		// Board built by hand, so both libraries are empty. Without this the decking rule
		// decides these games: EndTurn decks the opponent and wins outright.
		_state = _state.WithoutDeckingLoss();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));
	}

	// ===== REANIMATE =====

	/// <summary>
	/// Careful Study draws 2 then discards 2. With a BigCreature in hand (too expensive
	/// to cast) and Reanimate in hand (no valid target yet), the AI should cast Careful
	/// Study, then during the discard choice discard the BigCreature — putting it in the
	/// graveyard where Reanimate can target it. LookaheadScore sees this chain.
	/// </summary>
	[Test]
	public void CarefulStudy_DiscardsExpensiveCreature_ToEnableReanimate()
	{
		// 2 mana: 1 for Careful Study, 1 left over for Reanimate
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 2, CurrentMana = 2 });

		// BigCreature (5/5, cost 5) is in hand — too expensive to cast, but a great Reanimate target
		// Reanimate has no valid target yet (graveyard empty), so it cannot be cast first
		var bigCreature = new Card
		{
			Name = "BigCreature",
			ManaCost = 5,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 5, Toughness = 5 }
			),
		};
		// Inlined so card balance tweaks in CardLibrary don't break this test.
		// Careful Study: draw 2, then discard 2 (cost 1).
		var carefulStudy = new Card
		{
			Name = "Careful Study",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new DrawCardsAction
									{
										Amount = 2,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardsFromHandAction
									{
										Prompt = "Choose 2 cards to discard",
										MinChoices = 2,
										MaxChoices = 2,
										OutputKey = ContextKeys.SelectedCardIds,
									},
									new DiscardCardsAction
									{
										TargetContextKey = ContextKeys.SelectedCardIds,
									}
								),
							},
						}
					),
				}
			),
		};
		// Reanimate: put a creature from your graveyard onto the battlefield (cost 1).
		// Inlined at cost 1 — this test requires CS (1) + Reanimate (1) = 2 total mana.
		var reanimate = CardFactory
			.Spell("Reanimate", manaCost: 1)
			.WithAction(new PutIntoBattlefieldAction(), Single().CreatureInYourGraveyard())
			.Build() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedCS) = _state.AddObject(carefulStudy, parentId: _ids.Player1HandId);
		(_state, var addedRe) = _state.AddObject(reanimate, parentId: _ids.Player1HandId);
		(_state, var addedBig) = _state.AddObject(bigCreature, parentId: _ids.Player1HandId);

		// Library: exactly 2 junk cards — Careful Study draws both
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "Drawn1"),
			parentId: _ids.Player1LibraryId
		);
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "Drawn2"),
			parentId: _ids.Player1LibraryId
		);

		// Step 1: Reanimate has no graveyard target; BigCreature costs 5. AI must cast Careful Study.
		var action1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action1, Is.InstanceOf<CastSpellAction>());
		Assert.That(((CastSpellAction)action1).CardId, Is.EqualTo(addedCS.Id));
		_state = Execute(_state, action1);

		// Must discard 2 of [Reanimate, BigCreature, Drawn1, Drawn2]
		Assert.That(_state.IsWaitingForChoice, Is.True);
		var choice = _state.GetPendingChoice()!;
		var discardIds = _ai.ResolveChoice(_state, choice, _ids.Player1Id);

		Assert.That(
			discardIds,
			Does.Not.Contain(addedRe.Id),
			"AI should keep Reanimate — it enables the BigCreature once discarded"
		);
		Assert.That(
			discardIds,
			Does.Contain(addedBig.Id),
			"AI should discard BigCreature — LookaheadScore sees Reanimate puts it straight onto battlefield"
		);

		// Complete the discard then verify AI casts Reanimate next
		(_state, _) = _state.ResolveChoice(discardIds);
		var action2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action2, Is.InstanceOf<CastSpellAction>());
		Assert.That(
			((CastSpellAction)action2).CardId,
			Is.EqualTo(addedRe.Id),
			"AI should immediately follow up by casting Reanimate on the discarded BigCreature"
		);
	}

	// ===== BLOODGHAST =====

	/// <summary>
	/// Faithless Looting draws 3 then discards 3. When Plains is drawn alongside
	/// Bloodghast, the AI should discard Bloodghast (not the Plains) so it can play
	/// the Plains and trigger Bloodghast's landfall return from the graveyard.
	/// </summary>
	[Test]
	public void FaithlessLooting_DiscardsBloodghast_ToReturnViaLandPlay()
	{
		// 1 mana: just enough to cast Faithless Looting
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 1, CurrentMana = 1 });

		// Hand: Faithless Looting + Bloodghast — no Plains in hand so land-first override won't fire
		var faithlessLooting = CardLibrary.GetByName("Faithless Looting") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var bloodghast = CardLibrary.GetByName("Bloodghast") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedFL) = _state.AddObject(faithlessLooting, parentId: _ids.Player1HandId);
		(_state, var addedBG) = _state.AddObject(bloodghast, parentId: _ids.Player1HandId);

		// Library: Plains + 2 junk — all 3 drawn by Faithless Looting
		var plains = CardLibrary.Plains() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedPlains) = _state.AddObject(plains, parentId: _ids.Player1LibraryId);
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "Drawn1"),
			parentId: _ids.Player1LibraryId
		);
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "Drawn2"),
			parentId: _ids.Player1LibraryId
		);

		// Step 1: No Plains in hand — land-first override skips; AI casts Faithless Looting
		var action1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action1, Is.InstanceOf<CastSpellAction>());
		Assert.That(((CastSpellAction)action1).CardId, Is.EqualTo(addedFL.Id));
		_state = Execute(_state, action1);

		// Must discard 3 of [Bloodghast, Plains, Drawn1, Drawn2]
		Assert.That(_state.IsWaitingForChoice, Is.True);
		var choice = _state.GetPendingChoice()!;
		var discardIds = _ai.ResolveChoice(_state, choice, _ids.Player1Id);

		Assert.That(
			discardIds,
			Does.Contain(addedBG.Id),
			"AI should discard Bloodghast — LookaheadScore sees Plains play returns it from graveyard"
		);
		Assert.That(
			discardIds,
			Does.Not.Contain(addedPlains.Id),
			"AI should keep Plains to trigger Bloodghast's landfall return"
		);

		// Complete the discard — land-first override fires, AI plays Plains
		(_state, _) = _state.ResolveChoice(discardIds);
		var action2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action2, Is.InstanceOf<PlayLandAction>(), "AI should play Plains next");
		_state = Execute(_state, action2);

		// Bloodghast should be back on the battlefield via landfall trigger
		var battlefield = _state.GetCardsInZone(_ids.Player1BattlefieldId);
		Assert.That(
			battlefield.Any(c => c.Id == addedBG.Id),
			Is.True,
			"Bloodghast should return to battlefield after landfall trigger"
		);
	}

	// ===== SLEIGHT OF HAND =====

	/// <summary>
	/// Sleight of Hand looks at top 2 cards and keeps one. When one option is a
	/// castable creature and the other is a land, the AI should pick the creature —
	/// LookaheadScore sees casting it provides immediate board presence.
	/// </summary>
	[Test]
	public void SleightOfHand_PicksCreatureOverLand_WhenCreatureIsCastable()
	{
		// 3 mana: 1 for Sleight of Hand, 2 remaining to cast Grizzly Bears
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 3, CurrentMana = 3 });

		// Hand: only Sleight of Hand
		var sleightOfHand = CardLibrary.SleightOfHand() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedSoH) = _state.AddObject(sleightOfHand, parentId: _ids.Player1HandId);

		// Library top 2: Grizzly Bears (2/2, costs 2) and Plains
		var bears = CardLibrary.GrizzlyBears() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var plains = CardLibrary.Plains() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedBears) = _state.AddObject(bears, parentId: _ids.Player1LibraryId);
		(_state, var addedPlains) = _state.AddObject(plains, parentId: _ids.Player1LibraryId);

		// Step 1: AI casts Sleight of Hand
		var action1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action1, Is.InstanceOf<CastSpellAction>());
		Assert.That(((CastSpellAction)action1).CardId, Is.EqualTo(addedSoH.Id));
		_state = Execute(_state, action1);

		// Choice: which of the top 2 cards to keep
		Assert.That(_state.IsWaitingForChoice, Is.True);
		var choice = _state.GetPendingChoice()!;
		var selectedIds = _ai.ResolveChoice(_state, choice, _ids.Player1Id);

		Assert.That(
			selectedIds,
			Does.Contain(addedBears.Id),
			"AI should pick Grizzly Bears — LookaheadScore sees it can be cast for immediate board presence"
		);
	}

	// ===== PATH TO EXILE =====

	/// <summary>
	/// Faithless Looting draws 3 then discards 3. When a 10/10 threatens lethal next turn
	/// and Path to Exile (cost 1) is in hand with mana available, the AI must keep Path to
	/// Exile and discard something else — LookaheadScore sees casting it exiles the threat.
	/// </summary>
	[Test]
	public void FaithlessLooting_KeepsPathToExile_WhenDrawnByLooting()
	{
		// 2 mana: 1 for Faithless Looting, 1 left for Path to Exile
		var p1 = _state.GetPlayer(_ids.Player1Id);
		_state = _state.UpdateObject(_ids.Player1Id, p1 with { MaxMana = 2, CurrentMana = 2 });

		// P2 battlefield: 10/10 — lethal threat, clearable by Path to Exile
		var bigThreat = new Card
		{
			Name = "HuntedDragon",
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 10, Toughness = 10 }
			),
		};
		(_state, _) = _state.AddObject(bigThreat, parentId: _ids.Player2BattlefieldId);

		// P1 hand: only Faithless Looting + one junk — no PtE or AR in hand yet.
		// AR and PtE are in the library so FL draws them; this forces FL as the first action
		// and isolates the discard choice: keep PtE (exile 10/10) vs keep AR (draw 3 from empty library).
		var faithlessLooting = CardLibrary.GetByName("Faithless Looting") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, var addedFL) = _state.AddObject(faithlessLooting, parentId: _ids.Player1HandId);
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "HandJunk"),
			parentId: _ids.Player1HandId
		);

		// Library top 3 drawn by FL: AR + Path to Exile + junk
		// After drawing, discard choice is [HandJunk, AR, PtE, LibJunk] — keep 1.
		// Keeping PtE → cast it → exile 10/10 → score +23
		// Keeping AR  → cast it → draw 0 (library now empty) → score ≈ baseline
		var ancestralRecall = CardLibrary.AncestralRecall() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var pathToExile = CardLibrary.PathToExile() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(ancestralRecall, parentId: _ids.Player1LibraryId);
		(_state, var addedPtE) = _state.AddObject(pathToExile, parentId: _ids.Player1LibraryId);
		(_state, _) = _state.AddObject(
			MakeJunk(_ids.Player1Id, "LibJunk"),
			parentId: _ids.Player1LibraryId
		);

		// Step 1: FL is the only castable spell — AI must cast it
		var action1 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action1, Is.InstanceOf<CastSpellAction>());
		Assert.That(((CastSpellAction)action1).CardId, Is.EqualTo(addedFL.Id));
		_state = Execute(_state, action1);

		// Discard choice: [HandJunk, AR, PtE, LibJunk] — keep 1 (discard 3)
		Assert.That(_state.IsWaitingForChoice, Is.True);
		var choice = _state.GetPendingChoice()!;
		var discardIds = _ai.ResolveChoice(_state, choice, _ids.Player1Id);

		Assert.That(
			discardIds,
			Does.Not.Contain(addedPtE.Id),
			"AI should keep Path to Exile — removing the 10/10 (score +23) beats any alternative"
		);

		// After keeping PtE, AI should cast it to exile the 10/10
		(_state, _) = _state.ResolveChoice(discardIds);
		var action2 = _ai.SelectAction(_state, _ids, _ids.Player1Id);
		Assert.That(action2, Is.InstanceOf<CastSpellAction>());
		Assert.That(
			((CastSpellAction)action2).CardId,
			Is.EqualTo(addedPtE.Id),
			"AI should immediately cast Path to Exile to remove the lethal threat"
		);
	}

	// ===== HELPERS =====

	private static Card MakeJunk(int ownerId, string name) =>
		new()
		{
			Name = name,
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	private static GameState Execute(GameState state, GameAction action)
	{
		var (stateWithAction, success) = state.TryAddAction(action);
		Assert.That(success, Is.True, $"TryAddAction failed for {action.GetType().Name}");
		var (finalState, _) = stateWithAction.ProcessAllActions();
		return finalState;
	}
}
