using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

public static class ActionDescriber
{
	public static string Describe(GameAction action, GameState state) =>
		action switch
		{
			PlayLandAction a => $"Play Land: {CardName(state, a.CardId)}",
			CastCreatureAction a => $"Cast Creature: {CardName(state, a.CardId)}",
			CastSpellAction a => $"Cast Spell: {CardName(state, a.CardId)}",
			CastPermanentAction a => $"Cast Permanent: {CardName(state, a.CardId)}",
			CastFromGraveyardAction a => $"Flashback: {CardName(state, a.CardId)}",
			AttackAction a =>
				$"Attack: {CardName(state, a.AttackerId)} → {ObjectName(state, a.TargetId)}",
			ActivateAbilityAction a => $"Ability: {CardName(state, a.CardId)}[{a.AbilityIndex}]",
			EndTurnAction => "End Turn",
			_ => action.GetType().Name,
		};

	private static string CardName(GameState state, int id) =>
		state.HasObject(id) ? (state.GetObject(id) as Card)?.Name ?? id.ToString() : id.ToString();

	private static string ObjectName(GameState state, int id)
	{
		if (!state.HasObject(id))
			return id.ToString();
		return state.GetObject(id) switch
		{
			Card c => c.Name,
			MtgPlayer p => p.Name,
			_ => id.ToString(),
		};
	}
}
