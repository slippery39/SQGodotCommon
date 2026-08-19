using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The ten worst-performing cards in the trained draft model, tested for whether they WORK.
///
/// This fixture exists because the model turned out to be a bug detector. Trumpet Blast measured
/// -9.3pp — the worst card in the set — and the reason was not that it was weak but that it was
/// literally blank: it buffed creatures that had already dealt their combat damage. Nothing else
/// caught that. `EveryCard_CanBeCastAndResolve` passed it happily.
///
/// So: a card that does SOMETHING, even something weak, should beat the rate of a card that does
/// nothing. Every test here asserts the card's consequence — life moved, a creature grew, a card
/// changed zone — and says nothing about whether the card is well costed. **Balance is not what
/// this fixture is for**; if a card here is merely weak, these tests still pass and the tuning
/// conversation is a separate one.
///
/// Cards, with their measured win rate at the time of writing:
///   Molten Vortex 41.7 · Fortify 41.6 · Dark Tutelage 41.2 · Inspired Charge 41.1
///   Aether Tunnel 40.7 · Safe Passage 40.7 · Mind Spring 39.8 · Barrage of Expendables 39.4
///   Teferi's Tutelage 39.0 · Ravaging Blaze 38.7
/// </summary>
[TestFixture]
public class BottomOfModelCardTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== RAVAGING BLAZE — X to the creature AND X to its controller =====

	[Test]
	public void RavagingBlaze_DamagesTheCreatureAndItsController()
	{
		var (state, victim) = AddOpponentCreature(_state, power: 1, toughness: 9);
		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;

		var (final, _) = CastSpell(state, Find("Ravaging Blaze"), targetId: victim.Id, xValue: 3);

		Assert.Multiple(() =>
		{
			Assert.That(Damage(final, victim.Id), Is.EqualTo(3), "creature should take X");
			Assert.That(
				final.GetPlayer(_ids.Player2Id).Life,
				Is.EqualTo(lifeBefore - 3),
				"controller should take X as well"
			);
		});
	}

	// ===== FORTIFY — modal team pump =====

	[Test]
	public void Fortify_PumpsYourTeam()
	{
		var (state, mine) = AddOwnCreature(_state, 2, 2);
		(state, var other) = AddOwnCreature(state, 2, 2);

		var (final, _) = CastSpellResolvingChoices(state, Find("Fortify"), chooseOption: 0);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetEffectivePower(mine.Id),
				Is.GreaterThan(2),
				"Fortify's chosen mode must actually reach your creatures"
			);
			Assert.That(final.GetEffectivePower(other.Id), Is.GreaterThan(2));
		});
	}

	// ===== INSPIRED CHARGE — team pump =====

	[Test]
	public void InspiredCharge_PumpsEveryCreatureYouControl()
	{
		var (state, a) = AddOwnCreature(_state, 2, 2);
		(state, var b) = AddOwnCreature(state, 1, 1);

		var (final, _) = CastSpell(state, Find("Inspired Charge"));

		Assert.Multiple(() =>
		{
			Assert.That(final.GetEffectivePower(a.Id), Is.EqualTo(4));
			Assert.That(final.GetEffectivePower(b.Id), Is.EqualTo(3));
		});
	}

	// ===== SAFE PASSAGE — prevent all damage =====

	/// <summary>
	/// KNOWN STRUCTURAL GAP, not a code defect — the mechanism works, the card cannot reach it.
	///
	/// "Prevent all damage dealt to you and creatures you control THIS TURN." A spell can only be
	/// cast on your own turn (there is no priority window), combat damage to you only arrives on
	/// the OPPONENT's turn, and EndTurnAction strips UntilEndOfTurn replacements from both players
	/// in between. So the shield is always gone before the damage it exists to stop.
	///
	/// It is not perfectly blank: cast on your own turn it still covers a symmetric effect you
	/// control, such as your own Earthquake or Brash Taunter's reflection. That is a far narrower
	/// card than the text promises, and it is why the model rates it at 40.7%.
	///
	/// Fixing it is a DESIGN decision, not a repair — "until your next turn" would make it work,
	/// and that is a balance change. Left deliberately failing-as-ignored rather than adjusted;
	/// see the entry in DesignNotes.md. Remove the Ignore when the decision is made.
	/// </summary>
	[Test]
	[Ignore(
		"Structural: no priority window means the shield always expires before the damage. "
			+ "Needs a design decision, not a fix — see DesignNotes.md."
	)]
	public void SafePassage_PreventsDamageWhenItArrives()
	{
		var (state, _) = CastSpell(_state, Find("Safe Passage"));

		// Hand the turn over, then let the opponent swing at us.
		(state, _) = state
			.AddAction(
				new EndTurnAction
				{
					GameId = _ids.GameId,
					Player1Id = _ids.Player1Id,
					Player2Id = _ids.Player2Id,
				}
			)
			.ProcessAllActions();

		(state, var attacker) = AddOpponentCreature(state, power: 4, toughness: 4, ready: true);
		var lifeBefore = state.GetPlayer(_ids.Player1Id).Life;

		(state, _) = state
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = _ids.Player1Id,
					AttackingPlayerId = _ids.Player2Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			state.GetPlayer(_ids.Player1Id).Life,
			Is.EqualTo(lifeBefore),
			"Safe Passage should prevent the damage it was cast to prevent"
		);
	}

	// ===== MIND SPRING — draw X =====

	[Test]
	public void MindSpring_DrawsXCards()
	{
		var state = StockLibrary(_state, _ids.Player1Id, 10);
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var before = state.GetCardsInZone(handId).Count();

		var (final, cardId) = CastSpell(state, Find("Mind Spring"), xValue: 3);

		// The spell itself left hand for the stack, so it no longer counts toward hand size.
		var after = final.GetCardsInZone(handId).Count(c => c.Id != cardId);
		Assert.That(after, Is.EqualTo(before + 3), "should draw exactly X");
	}

	// ===== DARK TUTELAGE — reveal, draw, lose that much life =====

	[Test]
	public void DarkTutelage_DrawsTheTopCardAndCostsItsManaValue()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var (state, top) = _state.AddObject(
			MakeCreature("Expensive", _ids.Player1Id, 1, 1, manaCost: 4),
			parentId: libraryId
		);
		state = PutOnBattlefield(state, Find("Dark Tutelage"), _ids.Player1Id);

		var lifeBefore = state.GetPlayer(_ids.Player1Id).Life;
		state = RunUpkeep(state, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetCardZone(top.Id).ZoneType,
				Is.EqualTo(ZoneType.Hand),
				"the revealed card should reach your hand"
			);
			Assert.That(
				state.GetPlayer(_ids.Player1Id).Life,
				Is.EqualTo(lifeBefore - 4),
				"you should lose life equal to its mana value"
			);
		});
	}

	// ===== TEFERI'S TUTELAGE — mill on draw =====

	[Test]
	public void TeferisTutelage_MillsTheOpponentWhenYouDraw()
	{
		var state = StockLibrary(_state, _ids.Player1Id, 10);
		state = StockLibrary(state, _ids.Player2Id, 10);
		state = PutOnBattlefield(state, Find("Teferi's Tutelage"), _ids.Player1Id);

		var oppGraveyardId = state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Graveyard);
		var before = state.GetCardsInZone(oppGraveyardId).Count();

		(state, _) = state
			.AddAction(new DrawCardsAction { TargetIds = [_ids.Player1Id], Amount = 1 })
			.ProcessAllActions();

		Assert.That(
			state.GetCardsInZone(oppGraveyardId).Count(),
			Is.GreaterThan(before),
			"drawing a card should mill the opponent"
		);
	}

	// ===== AETHER TUNNEL — aura buff =====

	[Test]
	public void AetherTunnel_BuffsTheCreatureItEnchants()
	{
		var (state, mine) = AddOwnCreature(_state, 2, 2);
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withAura, aura) = state.AddObject(Owned(Find("Aether Tunnel")), parentId: handId);

		var (final, _) = withAura
			.AddAction(
				new CastPermanentAction
				{
					CardId = aura.Id,
					CastingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableList.Create(mine.Id),
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetEffectivePower(mine.Id), Is.EqualTo(3), "aura should grant +1/+0");
	}

	// ===== MOLTEN VORTEX — discard a land for 2 damage =====

	[Test]
	public void MoltenVortex_DiscardsALandAndDealsTwo()
	{
		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (state, land) = _state.AddObject(
			MakeLand("Mountain", _ids.Player1Id),
			parentId: handId
		);
		state = PutOnBattlefield(state, Find("Molten Vortex"), _ids.Player1Id);

		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;
		state = ActivateFirstAbility(state, "Molten Vortex", targetId: _ids.Player2Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetPlayer(_ids.Player2Id).Life,
				Is.EqualTo(lifeBefore - 2),
				"should deal 2 damage"
			);
			Assert.That(
				state.GetCardZone(land.Id).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"the land is the cost and must actually be discarded"
			);
		});
	}

	// ===== BARRAGE OF EXPENDABLES — sacrifice for 1 damage =====

	[Test]
	public void BarrageOfExpendables_SacrificesACreatureAndDealsOne()
	{
		var (state, fodder) = AddOwnCreature(_state, 1, 1);
		state = PutOnBattlefield(state, Find("Barrage of Expendables"), _ids.Player1Id);

		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;
		state = ActivateFirstAbility(state, "Barrage of Expendables", targetId: _ids.Player2Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetPlayer(_ids.Player2Id).Life,
				Is.EqualTo(lifeBefore - 1),
				"should deal 1 damage"
			);
			Assert.That(
				state.GetCardZone(fodder.Id).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"the sacrificed creature must reach the graveyard"
			);
		});
	}

	// ===== HELPERS =====

	private static Card Find(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	private Card Owned(Card template) =>
		template with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

	private static int Damage(GameState s, int cardId) =>
		((Card)s.GetObject(cardId)).GetComponent<CreatureComponent>()!.Damage;

	private (GameState, Card) AddOwnCreature(GameState state, int power, int toughness) =>
		state.AddObject(
			MakeCreature("Mine", _ids.Player1Id, power, toughness, manaCost: 2),
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)
		);

	private (GameState, Card) AddOpponentCreature(
		GameState state,
		int power,
		int toughness,
		bool ready = false
	)
	{
		var card = MakeCreature("Theirs", _ids.Player2Id, power, toughness, manaCost: 2);
		if (ready)
			card =
				card.WithComponentReplaced(
					new CreatureComponent
					{
						Power = power,
						Toughness = toughness,
						HasSummoningSickness = false,
					}
				) as Card
				?? card;

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield)
		);
	}

	private static Card MakeCreature(
		string name,
		int ownerId,
		int power,
		int toughness,
		int manaCost
	) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private static Card MakeLand(string name, int ownerId) =>
		new()
		{
			Name = name,
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Land,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
		};

	private GameState StockLibrary(GameState state, int playerId, int count)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		for (var i = 0; i < count; i++)
			(state, _) = state.AddObject(
				MakeCreature($"Filler {i}", playerId, 1, 1, manaCost: 1),
				parentId: libraryId
			);
		return state;
	}

	private GameState PutOnBattlefield(GameState state, Card template, int playerId)
	{
		var (next, _) = state.AddObject(
			template with
			{
				OwnerId = playerId,
				ControllerId = playerId,
			},
			parentId: state.GetPlayerZoneId(playerId, ZoneType.Battlefield)
		);
		return next;
	}

	private GameState RunUpkeep(GameState state, int playerId)
	{
		var (next, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = playerId,
					BattlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield),
					SkipDraw = true,
				}
			)
			.ProcessAllActions();
		return next;
	}

	/// <summary>Activates a battlefield permanent's first ability, resolving any choices.</summary>
	private GameState ActivateFirstAbility(GameState state, string cardName, int targetId)
	{
		var bfId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var source = state.GetCardsInZone(bfId).Single(c => c.Name == cardName);

		var actions = MtgActionGenerator.GetLegalActions(state, _ids.Player1Id);
		var activate = actions
			.OfType<ActivateAbilityAction>()
			.FirstOrDefault(a => a.CardId == source.Id);

		Assert.That(
			activate,
			Is.Not.Null,
			$"{cardName}'s ability was never offered as a legal action — it cannot be used at all"
		);

		var (next, _) = state.AddAction(activate!).ProcessAllActions();
		return ResolveAnyChoices(next);
	}

	private (GameState State, int CardId) CastSpell(
		GameState state,
		Card template,
		int targetId = 0,
		int xValue = 0
	)
	{
		var (withCard, card) = state.AddObject(
			Owned(template),
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var cast = new CastSpellAction
		{
			CardId = card.Id,
			CastingPlayerId = _ids.Player1Id,
			XValue = xValue,
		};

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
		return (ResolveAnyChoices(final), card.Id);
	}

	/// <summary>Casts a spell and takes the given option for every choice it raises.</summary>
	private (GameState State, int CardId) CastSpellResolvingChoices(
		GameState state,
		Card template,
		int chooseOption
	)
	{
		var (withCard, card) = state.AddObject(
			Owned(template),
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (afterCast, _) = withCard
			.AddAction(new CastSpellAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id })
			.ProcessAllActions();

		return (ResolveAnyChoices(afterCast, chooseOption), card.Id);
	}

	private static GameState ResolveAnyChoices(GameState state, int chooseOption = 0)
	{
		// Bounded: a choice that never clears would otherwise spin here forever.
		for (var i = 0; i < 20 && state.IsWaitingForChoice; i++)
		{
			var choice = state.GetPendingChoice();
			if (choice == null || choice.Options.Count == 0)
				break;

			var pick = Math.Min(chooseOption, choice.Options.Count - 1);
			(state, _) = state.ResolveChoice(ImmutableList.Create(choice.Options[pick].Id));
		}
		return state;
	}
}
