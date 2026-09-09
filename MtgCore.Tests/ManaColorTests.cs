using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The production half of the colour system: lands grant colour, tap lands defer it, and the
/// turn refill restores it. Nothing consumes colour yet — that is the payment step.
///
/// Lands are built inline rather than fetched from a set, so a card retune cannot break these.
/// </summary>
[TestFixture]
public class ManaColorTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();

		// CreateForTesting hands out 99 of every colour so ordinary tests need no manabase. This
		// fixture is the exception it names: colour starts at ZERO here, or nothing below could
		// tell "the colour rule works" from "the fixture was generous".
		foreach (var pid in new[] { _ids.Player1Id, _ids.Player2Id })
		{
			var player = _state.GetPlayer(pid);
			_state = _state.UpdateObject(
				pid,
				player with
				{
					MaxColorMana = ManaPool.Empty,
					CurrentColorMana = ManaPool.Empty,
				}
			);
		}
	}

	private static Card Land(string name, ManaPool produces, bool deferred = false)
	{
		var components = ImmutableArray.CreateBuilder<GameComponent>();
		components.Add(new LandColorComponent { Produces = produces });
		if (deferred)
			components.Add(new BonusManaLandComponent { Deferred = true });

		return new Card
		{
			Name = name,
			ManaCost = 0,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
			Components = components.ToImmutable(),
		};
	}

	private (GameState, Card) PutInHand(GameState state, Card land)
	{
		var stamped = land with { OwnerId = _ids.Player1Id, ControllerId = _ids.Player1Id };
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		return state.AddObject(stamped, parentId: handId);
	}

	private GameState Play(GameState state, Card land)
	{
		var (withLand, inHand) = PutInHand(state, land);
		var (result, _) = withLand
			.AddAction(new PlayLandAction { CardId = inHand.Id, CastingPlayerId = _ids.Player1Id })
			.ProcessAllActions();
		return result;
	}

	private MtgPlayer Player(GameState state) => state.GetPlayer(_ids.Player1Id);

	// CreateForTesting starts a player on 99 mana so tests need not build a manabase, so every
	// generic assertion here is a DELTA against the fixture's starting value rather than an
	// absolute. Colour starts at zero and is asserted absolutely.
	private int BaseMana => Player(_state).MaxMana;

	[Test]
	public void BasicLand_AddsOneGeneric_AndOneOfItsColour()
	{
		var player = Player(Play(_state, CardLibrary.BasicLand(ManaColor.Red)));

		Assert.Multiple(() =>
		{
			Assert.That(player.MaxMana - BaseMana, Is.EqualTo(1), "generic");
			Assert.That(player.CurrentMana - BaseMana, Is.EqualTo(1), "generic available now");
			Assert.That(player.MaxColorMana.Red, Is.EqualTo(1), "red source");
			Assert.That(player.CurrentColorMana.Red, Is.EqualTo(1), "red available now");
			Assert.That(player.MaxColorMana.Total, Is.EqualTo(1), "no other colour granted");
		});
	}

	[Test]
	public void DualLand_AddsBothColours_ButStillOnlyOneGeneric()
	{
		// The reason a dual is not a strict upgrade: generic still caps total spend, so the
		// second colour buys breadth within a turn, never more mana.
		var dual = Land("Tundra", new ManaPool { White = 1, Blue = 1 });
		var player = Player(Play(_state, dual));

		Assert.Multiple(() =>
		{
			Assert.That(player.MaxMana - BaseMana, Is.EqualTo(1), "one generic, not two");
			Assert.That(player.CurrentColorMana.White, Is.EqualTo(1));
			Assert.That(player.CurrentColorMana.Blue, Is.EqualTo(1));
		});
	}

	[Test]
	public void TapLand_GrantsNeitherGenericNorColour_UntilTheRefill()
	{
		var tapLand = Land("Sunken Ruins", new ManaPool { Black = 1 }, deferred: true);
		var played = Play(_state, tapLand);
		var onPlay = Player(played);

		Assert.Multiple(() =>
		{
			Assert.That(onPlay.CurrentMana - BaseMana, Is.EqualTo(0), "no generic this turn");
			Assert.That(onPlay.CurrentColorMana.Black, Is.EqualTo(0), "no colour this turn");
			Assert.That(onPlay.MaxMana - BaseMana, Is.EqualTo(1), "but it counts from next turn");
			Assert.That(onPlay.MaxColorMana.Black, Is.EqualTo(1));
		});

		var (refilled, _) = played
			.AddAction(new StartTurnAction { ActivePlayerId = _ids.Player1Id })
			.ProcessAllActions();
		var next = Player(refilled);

		Assert.Multiple(() =>
		{
			Assert.That(next.CurrentMana - BaseMana, Is.EqualTo(1));
			Assert.That(next.CurrentColorMana.Black, Is.EqualTo(1), "colour refills with generic");
		});
	}

	[Test]
	public void FetchedLand_GrantsItsColour_JustLikeAPlayedOne()
	{
		// PlayLandAction and PutLandIntoPlayAction used to carry separate copies of this logic.
		// If they diverge again, a Rampant Growth'd Forest stops making green and nothing says so.
		var (withLand, land) = PutInHand(_state, CardLibrary.BasicLand(ManaColor.Green));
		var (result, _) = withLand
			.AddAction(new PutLandIntoPlayAction { CardId = land.Id, PlayerId = _ids.Player1Id })
			.ProcessAllActions();

		var player = Player(result);

		Assert.Multiple(() =>
		{
			Assert.That(player.CurrentColorMana.Green, Is.EqualTo(1));
			Assert.That(player.LandsPlayedThisTurn, Is.EqualTo(0), "a fetch is not a land drop");
			Assert.That(player.LandsPlayedTotal, Is.EqualTo(1));
		});
	}

	// ===== PAYING WITH COLOUR =====

	/// <summary>A vanilla creature costing 1W — inline so no card retune can move these goalposts.</summary>
	private Card WhiteOneDrop(string name = "White One-Drop", int whitePips = 1) =>
		CardFactory.Creature(name, manaCost: 1, power: 1, toughness: 1).Build() with
		{
			ColorPips = new ManaPool { White = whitePips },
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

	private GameState WithColor(GameState state, ManaPool pool)
	{
		var player = Player(state);
		return state.UpdateObject(
			_ids.Player1Id,
			player with
			{
				MaxColorMana = pool,
				CurrentColorMana = pool,
			}
		);
	}

	private (GameState, int) InHand(GameState state, Card card)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (result, added) = state.AddObject(card, parentId: handId);
		return (result, added.Id);
	}

	private bool CanCast(GameState state, int cardId)
	{
		var (_, ok) = state.TryAddAction(
			new CastCreatureAction { CardId = cardId, CastingPlayerId = _ids.Player1Id }
		);
		return ok;
	}

	[Test]
	public void ColouredSpell_IsUncastable_WithNoSourceOfThatColour()
	{
		// The fixture hands out 99 GENERIC mana and zero colour, so generic can never be the
		// reason this fails.
		var (state, id) = InHand(_state, WhiteOneDrop());

		Assert.That(CanCast(state, id), Is.False);
	}

	[Test]
	public void ColouredSpell_IsCastable_OnceItsColourIsAvailable()
	{
		var (state, id) = InHand(WithColor(_state, new ManaPool { White = 1 }), WhiteOneDrop());

		Assert.That(CanCast(state, id), Is.True);
	}

	[Test]
	public void DoublePip_NeedsTwoSources_NotOne()
	{
		var oneSource = WithColor(_state, new ManaPool { White = 1 });
		var (withOne, idOne) = InHand(oneSource, WhiteOneDrop("Double", whitePips: 2));
		Assert.That(CanCast(withOne, idOne), Is.False, "WW off a single White source");

		var twoSources = WithColor(_state, new ManaPool { White = 2 });
		var (withTwo, idTwo) = InHand(twoSources, WhiteOneDrop("Double", whitePips: 2));
		Assert.That(CanCast(withTwo, idTwo), Is.True, "WW off two White sources");
	}

	[Test]
	public void ColourDepletes_WithinATurn_EvenWhenGenericIsPlentiful()
	{
		// THE point of a depleting model. One White source, 99 generic: the first spell resolves
		// and the second is stranded on colour alone. Under a non-depleting (Eternal influence)
		// model both would cast, and a splash would cost nothing.
		var state = WithColor(_state, new ManaPool { White = 1 });
		var (s1, first) = InHand(state, WhiteOneDrop("First"));
		var (s2, second) = InHand(s1, WhiteOneDrop("Second"));

		var (afterFirst, _) = s2.AddAction(
				new CastCreatureAction { CardId = first, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				Player(afterFirst).CurrentColorMana.White,
				Is.EqualTo(0),
				"casting spent the White"
			);
			Assert.That(Player(afterFirst).CurrentMana, Is.LessThan(99), "and the generic");
			Assert.That(CanCast(afterFirst, second), Is.False, "second white spell is stranded");
		});
	}

	[Test]
	public void CostReduction_ReducesGenericOnly_AndNeverThePips()
	{
		// Affinity with plenty of artifacts drives the generic cost to 0. The colour requirement
		// must survive that — in MTG a spell reduced to zero generic still needs its colours.
		var state = _state;
		for (var i = 0; i < 6; i++)
		{
			var token = CardLibrary.ClueToken() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(state, _) = state.AddObject(token, parentId: _ids.Player1BattlefieldId);
		}

		var bear = CardFactory
			.Creature("Affinity Bear", manaCost: 4, power: 2, toughness: 2)
			.Build();
		// ADD affinity to the built components — replacing them would drop CreatureComponent and
		// the cast would fail for a reason that has nothing to do with colour.
		var affinityCreature = bear with
		{
			ColorPips = new ManaPool { Blue = 1 },
			Components = bear.Components.Add(new AffinityComponent()),
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var (noBlue, id) = InHand(state, affinityCreature);
		Assert.That(CanCast(noBlue, id), Is.False, "generic reduced to 0, but still needs U");

		var (withBlue, id2) = InHand(WithColor(state, new ManaPool { Blue = 1 }), affinityCreature);
		Assert.That(CanCast(withBlue, id2), Is.True);
	}
}
