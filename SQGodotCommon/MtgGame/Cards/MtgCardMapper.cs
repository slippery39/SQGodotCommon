using Common.Cards;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public static class MtgCardMapper
{
	public static InternalCardUI2D.Details ToDetails(Card card, GameState state, int playerId)
	{
		var creature = card.GetComponent<CreatureComponent>();
		var player = state.GetPlayer(playerId);
		var canAfford = player.CurrentMana >= card.ManaCost;

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name,
			ManaCost = $"{card.ManaCost}",
			RulesText = creature != null ? $"{creature.Power}/{creature.Toughness}" : string.Empty,
			OutlineColor = canAfford ? new Color(0, 1.5f, 0, 1) : new Color(0.3f, 0.3f, 0.3f, 1),
			OutlineThickness = canAfford ? 3f : 0f,
		};
	}
}
