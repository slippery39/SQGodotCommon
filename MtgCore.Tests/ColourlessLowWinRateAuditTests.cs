using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The bottom of the colourless/multicolour win-rate band, audited card by card.
///
/// A BLANK CARD AND A MERELY WEAK CARD BOTH SCORE ABOUT 40%, and only one of the two is a bug.
/// Green's pass found seven cards in that band that did literally nothing; the technique that
/// works is auditing the low band individually and asserting the board or zone consequence, not
/// rebalancing it.
///
/// After the 8400-game retrain the new section's floor is Whispersilk Cloak at 38.8%, and the
/// twenty lowest cards in the whole set are a mix of new and pre-existing ones rather than a
/// cluster of new arrivals — which is the shape you want. These pin the handful where "inert" was
/// a plausible explanation for the number, so that a future regression is distinguishable from a
/// card that is simply not very good.
///
/// Note the harness trap that cost real time in green: GameState.ProcessAllActions deliberately
/// STOPS at a ChoiceAction, so anything containing a scry or a mode freezes mid-pipeline and looks
/// inert. None of the cards here contain one; if you add an audit for one that does, settle the
/// choice first.
/// </summary>
[TestFixture]
public class ColourlessLowWinRateAuditTests
{
	/// <summary>
	/// Whispersilk Cloak is the lowest-scoring card in the entire set at 38.8%, so it is the first
	/// place to look for a no-op. It is NOT one: the shroud really lands. The card is genuinely
	/// awkward, and deliberately so — shroud cuts both ways, so the wearer cannot be targeted by
	/// its own controller either, which is the cost of the protection and the reason the printed
	/// "can't be blocked" half mattered. With no blocking there is nothing to give it back.
	///
	/// If this ever needs rebalancing the answer is a different keyword, not a bug fix.
	/// </summary>
	[Test]
	public void WhispersilkCloak_ReallyGrantsShroud()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withBear, bearId) = AddToBattlefield(state, Bear(), ids.Player1Id);
		var (withCloak, cloakId) = AddToBattlefield(
			withBear,
			Find("Whispersilk Cloak"),
			ids.Player1Id
		);

		Assert.That(withCloak.GetEffectiveStats(bearId).HasShroud, Is.False, "precondition");

		var equipped = Run(
			withCloak,
			Equip(Find("Whispersilk Cloak"), cloakId, bearId, ids.Player1Id)
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				equipped.GetEffectiveStats(bearId).HasShroud,
				Is.True,
				"the card's only ability"
			);
			Assert.That(
				new IsCreatureSpecification().IsSatisfiedBy(
					bearId,
					new TargetingContext
					{
						GameState = equipped,
						CastingPlayerId = ids.Player2Id,
						SourceCardId = 0,
					}
				),
				Is.False,
				"shroud is enforced in IsCreatureSpecification — it must actually block targeting"
			);
		});
	}

	/// <summary>
	/// Gilded Lotus at 42.2% is the other plausible no-op: a five-mana rock whose mana arrives via
	/// an upkeep trigger rather than an activated ability, so if the trigger never fired the card
	/// would be a blank five-drop and score exactly like one.
	///
	/// It fires. The mana is additive on top of StartTurnAction's refill, which is the whole point
	/// of the upkeep pattern — and it is temporary, so it correctly vanishes if the Lotus dies.
	/// The low score is the delay: a rock cast on turn five produces nothing until turn six.
	/// </summary>
	[Test]
	public void GildedLotus_ReallyProducesManaOnUpkeep()
	{
		var (state, ids) = MtgGameFactory.Create();
		var (withLotus, _) = AddToBattlefield(state, Find("Gilded Lotus"), ids.Player1Id);

		// Give the player a real mana base so the refill has something to refill to.
		withLotus = withLotus.UpdateObject(
			ids.Player1Id,
			((MtgPlayer)withLotus.GetObject(ids.Player1Id)) with
			{
				MaxMana = 5,
				CurrentMana = 0,
			}
		);

		var afterUpkeep = Run(
			withLotus,
			new StartTurnAction
			{
				ActivePlayerId = ids.Player1Id,
				BattlefieldId = withLotus.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield),
				SkipDraw = true,
			}
		);

		var player = (MtgPlayer)afterUpkeep.GetObject(ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(player.CurrentMana, Is.EqualTo(8), "5 refilled + 3 from the Lotus");
			Assert.That(
				player.MaxMana,
				Is.EqualTo(5),
				"temporary — it must vanish if the Lotus dies"
			);
		});
	}

	/// <summary>
	/// Arcane Encyclopedia at 42.2%. A repeatable draw engine that scores below average is
	/// plausible on a fast board, but a draw ability that silently does nothing scores the same.
	/// It draws.
	/// </summary>
	[Test]
	public void ArcaneEncyclopedia_ReallyDraws()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withBook, bookId) = AddToBattlefield(
			state,
			Find("Arcane Encyclopedia"),
			ids.Player1Id
		);

		// CreateForTesting leaves libraries empty, and drawing from an empty library is a loss
		// rather than a draw — stock it before asking the card to work.
		var libraryId = withBook.GetPlayerZoneId(ids.Player1Id, ZoneType.Library);
		for (var i = 0; i < 3; i++)
			(withBook, _) = withBook.AddObject(
				Bear() with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: libraryId
			);

		var handId = withBook.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var before = withBook.GetChildrenIds(handId).Count();

		var index = AbilityIndex(Find("Arcane Encyclopedia"), "Consult");
		var after = Run(
			withBook,
			new ActivateAbilityAction
			{
				CardId = bookId,
				ActivatingPlayerId = ids.Player1Id,
				AbilityIndex = index,
			}
		);

		Assert.That(after.GetChildrenIds(handId).Count(), Is.EqualTo(before + 1));
	}

	/// <summary>
	/// Risen Reef at 42.9% is the most heavily reskinned card in the section — its land-or-hand
	/// branch collapsed to a draw — so it is worth confirming the survivor actually happens, and
	/// that the once-per-turn cap really caps it. Uncapped, a go-wide deck would draw its whole
	/// library.
	/// </summary>
	[Test]
	public void RisenReef_DrawsOncePerTurn_NotOncePerCreature()
	{
		var (state, ids) = MtgGameFactory.Create();
		var (withReef, _) = AddToBattlefield(state, Find("Risen Reef"), ids.Player1Id);

		// Stock the library first. MtgGameFactory.Create leaves it empty until BeginGame deals,
		// and drawing from an empty library is a LOSS rather than a draw — so an unstocked test
		// reads as "the card did nothing" when what actually happened is that it worked and
		// decked the player. That is how Risen Reef looked inert on the first pass.
		var libraryId = withReef.GetPlayerZoneId(ids.Player1Id, ZoneType.Library);
		var stocked = withReef;
		for (var i = 0; i < 5; i++)
			(stocked, _) = stocked.AddObject(
				Bear() with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: libraryId
			);
		withReef = stocked;

		var handId = withReef.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var before = withReef.GetChildrenIds(handId).Count();

		var current = withReef;
		for (var i = 0; i < 3; i++)
		{
			var (withCreature, creatureId) = AddToBattlefield(current, Bear(), ids.Player1Id);
			current = Run(withCreature, new PutIntoBattlefieldAction { TargetIds = [creatureId] });
			current = Run(current, StateBasedCheck(current, ids));
		}

		Assert.That(
			current.GetChildrenIds(handId).Count(),
			Is.EqualTo(before + 1),
			"three creatures entered; the cap must hold it to one draw"
		);
	}

	// ===== HELPERS =====

	private static Card Find(string name) =>
		CoresetCube.Cards.First(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	private static ActivateAbilityAction Equip(
		Card equipment,
		int equipmentId,
		int targetId,
		int playerId
	) =>
		new()
		{
			CardId = equipmentId,
			ActivatingPlayerId = playerId,
			AbilityIndex = AbilityIndex(equipment, "Equip"),
			TargetIds = [targetId],
		};

	private static int AbilityIndex(Card card, string name) =>
		card.GetComponents<ActivatedAbilityComponent>().ToList().FindIndex(a => a.Name == name);

	private static Card Bear() =>
		new()
		{
			Name = "Bear",
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
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
