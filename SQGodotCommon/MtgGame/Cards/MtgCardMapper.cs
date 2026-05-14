using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public static class MtgCardMapper
{
	public static InternalCardUI2D.Details ToDetails(Card card, GameState state, int playerId)
	{
		var creature = card.GetComponent<CreatureComponent>();
		var spell = card.GetComponent<SpellComponent>();
		var player = state.GetPlayer(playerId);
		var canAfford = player.CurrentMana >= card.ManaCost;

		string rulesText;
		if (creature != null)
			rulesText = $"{creature.Power}/{creature.Toughness}";
		else if (spell != null)
			rulesText = DescribeSpell(spell);
		else
			rulesText = string.Empty;

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name,
			ManaCost = $"{card.ManaCost}",
			RulesText = rulesText,
			OutlineColor = canAfford ? new Color(0, 1.5f, 0, 1) : new Color(0.3f, 0.3f, 0.3f, 1),
			OutlineThickness = canAfford ? 3f : 0f,
		};
	}

	private static string DescribeSpell(SpellComponent spell)
	{
		var lines = spell.Effects.Select(DescribeEffect).Where(d => d != null).ToList();
		return string.Join("\n", lines);
	}

	private static string? DescribeEffect(CardEffect effect)
	{
		var targetSuffix = effect.TargetingStrategy.RequiresUserSelection ? " to target" : "";
		return effect.ActionTemplate switch
		{
			DealDamageAction d => $"Deal {d.Amount} damage{targetSuffix}",
			DestroyCreatureAction => $"Destroy{targetSuffix}",
			AddModifierAction m => $"+{m.PowerBonus}/+{m.ToughnessBonus}{targetSuffix}",
			_ => null,
		};
	}
}
