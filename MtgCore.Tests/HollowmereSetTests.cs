using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural checks on the Hollowmere set. These guard the invariants that make a card
/// playable at all — a card that violates one is not a balance problem, it is a dead card
/// that would silently do nothing in a draft.
///
/// Card balance is deliberately NOT asserted here; that is what the per-batch draft review is
/// for. The only numeric assertions are the per-theme counts, which exist to catch a theme
/// file being dropped from the set assembly.
/// </summary>
[TestFixture]
public class HollowmereSetTests
{
	[Test]
	public void Set_IsRegistered()
	{
		var set = SetRegistry.Get(Hollowmere.Code);
		Assert.That(set.Name, Is.EqualTo(Hollowmere.Name));
	}

	[Test]
	public void Set_HasExpectedSizeSoFar()
	{
		Assert.That(Hollowmere.Cards.Count, Is.EqualTo(150), "Batches 1-3 are 50 cards each");
	}

	[TestCase(nameof(HollowmereGraveyard), 43)]
	[TestCase(nameof(HollowmereDiscard), 30)]
	[TestCase(nameof(HollowmereHumans), 30)]
	[TestCase(nameof(HollowmereMill), 5)]
	[TestCase(nameof(HollowmereAngelsDemons), 4)]
	[TestCase(nameof(HollowmereSpirits), 4)]
	[TestCase(nameof(HollowmereSpells), 5)]
	[TestCase(nameof(HollowmereZombies), 4)]
	[TestCase(nameof(HollowmereWerewolves), 20)]
	[TestCase(nameof(HollowmereVampires), 3)]
	[TestCase(nameof(HollowmereGlue), 2)]
	public void Theme_HasExpectedCardCount(string theme, int expected)
	{
		var actual = theme switch
		{
			nameof(HollowmereGraveyard) => HollowmereGraveyard.Cards.Count,
			nameof(HollowmereDiscard) => HollowmereDiscard.Cards.Count,
			nameof(HollowmereHumans) => HollowmereHumans.Cards.Count,
			nameof(HollowmereMill) => HollowmereMill.Cards.Count,
			nameof(HollowmereAngelsDemons) => HollowmereAngelsDemons.Cards.Count,
			nameof(HollowmereSpirits) => HollowmereSpirits.Cards.Count,
			nameof(HollowmereSpells) => HollowmereSpells.Cards.Count,
			nameof(HollowmereZombies) => HollowmereZombies.Cards.Count,
			nameof(HollowmereWerewolves) => HollowmereWerewolves.Cards.Count,
			nameof(HollowmereVampires) => HollowmereVampires.Cards.Count,
			nameof(HollowmereGlue) => HollowmereGlue.Cards.Count,
			_ => throw new ArgumentException($"Unknown theme {theme}"),
		};

		Assert.That(actual, Is.EqualTo(expected));
	}

	[Test]
	public void EveryCard_HasAName()
	{
		Assert.That(Hollowmere.Cards.All(c => !string.IsNullOrWhiteSpace(c.Name)), Is.True);
	}

	[Test]
	public void CardNames_AreUnique()
	{
		var duplicates = Hollowmere
			.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		Assert.That(duplicates, Is.Empty, $"Duplicate names: {string.Join(", ", duplicates)}");
	}

	/// <summary>
	/// A card with neither SpellComponent nor PermanentComponent has no casting route in
	/// MtgActionGenerator — it would sit in hand forever.
	/// </summary>
	[Test]
	public void EveryCard_IsCastable()
	{
		var uncastable = Hollowmere
			.Cards.Where(c =>
				!c.HasComponent<SpellComponent>()
				&& !c.HasComponent<PermanentComponent>()
				&& !c.HasSubtype("Land")
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(uncastable, Is.Empty, $"Uncastable: {string.Join(", ", uncastable)}");
	}

	/// <summary>
	/// CastCreatureAction and CastPermanentAction reject each other's cards, so a creature
	/// missing PermanentComponent — or a permanent that is wrongly also a creature — cannot
	/// be cast at all.
	/// </summary>
	[Test]
	public void EveryCreature_HasPermanentComponent()
	{
		var broken = Hollowmere
			.Cards.Where(c =>
				c.HasComponent<CreatureComponent>() && !c.HasComponent<PermanentComponent>()
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(
			broken,
			Is.Empty,
			$"Creature without PermanentComponent: {string.Join(", ", broken)}"
		);
	}

	/// <summary>
	/// A triggered ability with no effects is silently inert. The builders throw on this, but
	/// hand-written card definitions bypass them.
	/// </summary>
	[Test]
	public void EveryTriggeredAbility_HasAnEffectAndCondition()
	{
		var broken = Hollowmere
			.Cards.Where(c =>
				c.GetComponents<TriggeredAbilityComponent>()
					.Any(t => t.Effects.IsEmpty || t.Condition == null)
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(broken, Is.Empty, $"Broken trigger: {string.Join(", ", broken)}");
	}

	/// <summary>
	/// Death triggers must be ActiveInZone = Graveyard or they never fire — by the time
	/// CheckStateBasedEffectsAction scans, the card has already left the battlefield.
	/// </summary>
	[Test]
	public void DeathTriggers_AreActiveInGraveyard()
	{
		var broken = Hollowmere
			.Cards.Where(c =>
				c.GetComponents<TriggeredAbilityComponent>()
					.Any(t =>
						t.Condition is EventTriggerCondition e
						&& e.EventTypeName == EventTypeNames.CreatureDestroyed
						&& e.Filter is IsSourceCardSpecification
						&& t.ActiveInZone != ZoneType.Graveyard
					)
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(
			broken,
			Is.Empty,
			$"Death trigger not active in graveyard: {string.Join(", ", broken)}"
		);
	}

	/// <summary>
	/// Threshold and other dynamic modifiers must be Permanent, or StartTurnAction's
	/// end-of-turn cleanup strips them on the controller's next turn.
	/// </summary>
	[Test]
	public void ThresholdModifiers_ArePermanent()
	{
		var broken = Hollowmere
			.Cards.Where(c =>
				c.GetComponents<ThresholdComponent>()
					.Any(t => t.Duration != ModifierDuration.Permanent)
			)
			.Select(c => c.Name)
			.ToList();

		Assert.That(broken, Is.Empty, $"Non-permanent threshold: {string.Join(", ", broken)}");
	}

	/// <summary>
	/// Every pack is a uniform random sample of the non-land pool, so the whole set must be
	/// draftable and big enough to fill a 15-card pack.
	/// </summary>
	[Test]
	public void Set_CanFillABoosterPack()
	{
		Assert.That(Hollowmere.Set.Draftable.Count, Is.GreaterThanOrEqualTo(15));
	}

	/// <summary>
	/// A real werewolf from the set must actually flip. Deliberately asserts no names or
	/// stats — it takes whichever double-faced card the set happens to define first — so
	/// balance changes cannot break it, but a broken transform wiring will.
	/// </summary>
	[Test]
	public void Werewolf_TransformsWhenNoSpellsWereCastLastTurn()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		var template = HollowmereWerewolves.Cards.First(c => c.HasComponent<TransformComponent>());
		var nightName = template.GetComponent<TransformComponent>()!.OtherFaceName;

		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			battlefieldId
		);

		// No spells cast last turn is the default (SpellsCastThisTurn starts at 0), so simply
		// starting the turn should satisfy the transform condition.
		var (final, _) = withCard
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = ids.Player1Id,
					BattlefieldId = battlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(
			((Card)final.GetObject(card.Id)).Name,
			Is.EqualTo(nightName),
			"Werewolf should have transformed to its night face"
		);
	}

	/// <summary>
	/// And back again when the previous turn was busy — the night face's own trigger.
	/// </summary>
	[Test]
	public void Werewolf_TransformsBackWhenTwoSpellsWereCastLastTurn()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		var template = HollowmereWerewolves.Cards.First(c => c.HasComponent<TransformComponent>());
		var dayName = template.Name;

		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			battlefieldId
		);

		StartTurnAction StartTurn() =>
			new()
			{
				ActivePlayerId = ids.Player1Id,
				BattlefieldId = battlefieldId,
				SkipDraw = true,
			};

		// Quiet turn: flips to night.
		var (night, _) = withCard.AddAction(StartTurn()).ProcessAllActions();
		Assert.That(
			((Card)night.GetObject(card.Id)).Name,
			Is.Not.EqualTo(dayName),
			"Precondition: flipped to night"
		);

		// Busy turn: two spells cast, so the next turn start flips it back.
		var game = (MtgGame)night.GetObject(ids.GameId);
		var busy = night.UpdateObject(ids.GameId, game with { SpellsCastThisTurn = 2 });

		var (day, _) = busy.AddAction(StartTurn()).ProcessAllActions();

		Assert.That(
			((Card)day.GetObject(card.Id)).Name,
			Is.EqualTo(dayName),
			"Werewolf should have transformed back to its day face"
		);
	}

	/// <summary>
	/// Curve sanity. Draft.BuildDeck takes the first 27 picks in pick order rather than the
	/// best 27 by curve, so a top-heavy set produces uncastable decks.
	/// </summary>
	[Test]
	public void Set_CurveIsNotTopHeavy()
	{
		var costs = Hollowmere.Set.Draftable.Select(c => c.ManaCost).OrderBy(x => x).ToList();
		var median = costs[costs.Count / 2];
		var expensive = costs.Count(c => c >= 6);

		Assert.That(median, Is.LessThanOrEqualTo(3), $"Median cost {median} is too high");
		Assert.That(
			expensive,
			Is.LessThanOrEqualTo(costs.Count / 5),
			$"{expensive}/{costs.Count} cards cost 6+"
		);
	}
}
