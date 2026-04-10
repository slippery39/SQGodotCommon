using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Computes effective power and toughness for creatures by aggregating
/// all PowerToughnessModifier components on the card.
///
/// Values are always derived at read time — never stored as computed state.
/// Both game actions and the UI call these methods to get effective values.
///
/// Static ability bonuses (anthem effects, lord effects) will be added
/// as a separate layer in a future step.
/// </summary>
public static class CreatureEvaluator
{
	/// <summary>
	/// Returns the effective power of a creature after applying all
	/// PowerToughnessModifiers on the card.
	/// </summary>
	public static int GetEffectivePower(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return 0;

		var power = creature.Power;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
			power += modifier.PowerBonus;

		return power;
	}

	/// <summary>
	/// Returns the effective toughness of a creature after applying all
	/// PowerToughnessModifiers on the card.
	/// Does not subtract accumulated damage — use HasLethalDamage for that.
	/// </summary>
	public static int GetEffectiveToughness(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return 0;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return 0;

		var toughness = creature.Toughness;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
			toughness += modifier.ToughnessBonus;

		return toughness;
	}

	/// <summary>
	/// Returns true if the creature has accumulated damage >= its effective toughness.
	/// </summary>
	public static bool HasLethalDamage(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return false;

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return false;

		return creature.Damage >= state.GetEffectiveToughness(cardId);
	}

	/// <summary>
	/// Removes all UntilEndOfTurn PowerToughnessModifiers from a card.
	/// Called by StartTurnAction at the start of each turn.
	/// </summary>
	public static GameState ClearEndOfTurnModifiers(this GameState state, int cardId)
	{
		var card = state.GetObject(cardId) as Card;
		if (card == null)
			return state;

		var hasEndOfTurnModifiers = card.GetComponents<PowerToughnessModifier>()
			.Any(m => m.Duration == ModifierDuration.UntilEndOfTurn);

		if (!hasEndOfTurnModifiers)
			return state;

		var updatedComponents = card
			.Components.Where(c =>
				c is not PowerToughnessModifier m || m.Duration != ModifierDuration.UntilEndOfTurn
			)
			.ToImmutableList();

		return state.UpdateObject(cardId, card with { Components = updatedComponents });
	}
}
