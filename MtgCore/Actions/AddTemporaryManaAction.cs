using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Adds mana to each target player's current mana pool without changing their max mana.
/// Used by fast-mana spells (Rite of Flame, Seething Song, Lotus Bloom).
///
/// BonusAmountContextKey reads an additional int from pipeline context and adds it on
/// top of the base Amount. Used by Rite of Flame to add 1 per Rite in the graveyard.
/// </summary>
public record AddTemporaryManaAction : EffectAction
{
	public int Amount { get; init; } = 0;
	public string BonusAmountContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = ResolveAmount(Amount);
		var bonus = string.IsNullOrEmpty(BonusAmountContextKey)
			? 0
			: GetInput<int>(BonusAmountContextKey, 0);
		var total = amount + bonus;

		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var updated = player with { CurrentMana = player.CurrentMana + total };
			state = state.UpdateObject(targetId, updated);
		}

		return new ActionResult(state);
	}
}
