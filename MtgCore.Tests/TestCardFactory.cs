using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgCore.Tests;

/// <summary>
/// Centralized helpers for constructing cards in tests.
/// All methods return plain Card instances with the appropriate components attached.
/// OwnerId and ControllerId must always be provided — no defaults to avoid
/// accidentally creating cards owned by player 0.
/// </summary>
public static class TestCardFactory
{
	/// <summary>
	/// A plain card with no components. Suitable for use as a library filler
	/// or any test that just needs a card object without type-specific behaviour.
	/// </summary>
	public static Card MakePlainCard(string name, int ownerId, int manaCost = 1) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	/// <summary>
	/// A spell card with the given effects.
	/// </summary>
	public static Card MakeSpellCard(
		string name,
		int ownerId,
		ImmutableList<CardEffect> effects,
		int manaCost = 1
	) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent { Effects = effects }
			),
		};

	/// <summary>
	/// A spell card with a single effect.
	/// </summary>
	public static Card MakeSpellCard(
		string name,
		int ownerId,
		CardEffect effect,
		int manaCost = 1
	) => MakeSpellCard(name, ownerId, ImmutableList.Create(effect), manaCost);

	/// <summary>
	/// A creature card with the given power and toughness.
	/// </summary>
	public static Card MakeCreatureCard(
		string name,
		int ownerId,
		int power,
		int toughness,
		int manaCost = 2
	) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	/// <summary>
	/// Adds plain filler cards to a player's library. Returns the updated state.
	/// </summary>
	public static GameState AddCardsToLibrary(GameState state, int playerId, params string[] names)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		foreach (var name in names)
			state = state.AddObject(MakePlainCard(name, playerId), parentId: libraryId).GameState;
		return state;
	}

	/// <summary>
	/// Builds a standard CastSpellAction with no targets.
	/// </summary>
	public static CastSpellAction MakeCastAction(int cardId, int castingPlayerId) =>
		new()
		{
			CardId = cardId,
			CastingPlayerId = castingPlayerId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};

	/// <summary>
	/// Builds a CastSpellAction targeting a single object.
	/// </summary>
	public static CastSpellAction MakeCastActionWithTarget(
		int cardId,
		int castingPlayerId,
		int targetId
	) =>
		new()
		{
			CardId = cardId,
			CastingPlayerId = castingPlayerId,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(targetId)
			),
		};
}
