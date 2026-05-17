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
		var player = state.GetPlayer(playerId);
		bool canPlay;
		string manaCostDisplay;
		if (card.HasSubtype("Land"))
		{
			canPlay =
				player.LandsPlayedThisTurn < PlayLandAction.ComputeLandsAllowed(state, playerId);
			manaCostDisplay = "";
		}
		else
		{
			canPlay = player.CurrentMana >= card.ManaCost;
			manaCostDisplay = $"{card.ManaCost}";
		}

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name,
			ManaCost = manaCostDisplay,
			RulesText = GetRulesText(card),
			OutlineColor = canPlay ? new Color(0, 1.5f, 0, 1) : new Color(0.3f, 0.3f, 0.3f, 1),
			OutlineThickness = canPlay ? 3f : 0f,
		};
	}

	public static string GetRulesText(Card card)
	{
		if (card.HasSubtype("Land"))
			return card.HasSubtype("Basic") ? "Basic Land\n(Tap: Add 1 mana)" : "Land";

		var creature = card.GetComponent<CreatureComponent>();
		var spell = card.GetComponent<SpellComponent>();
		var lines = new List<string>();

		if (creature != null)
		{
			lines.Add($"{creature.Power}/{creature.Toughness}");
			var keywords = DescribeKeywords(creature);
			if (keywords != null)
				lines.Add(keywords);
		}
		else if (spell != null)
		{
			var spellText = DescribeSpell(spell);
			if (!string.IsNullOrEmpty(spellText))
				lines.Add(spellText);
		}

		foreach (var ability in card.GetComponents<ActivatedAbilityComponent>())
		{
			var text = DescribeActivatedAbility(ability);
			if (text != null)
				lines.Add(text);
		}

		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		{
			var text = DescribeTriggeredAbility(trigger);
			if (text != null)
				lines.Add(text);
		}

		foreach (var staticAbility in card.GetComponents<StaticAbilityComponent>())
		{
			var text = DescribeStaticAbility(staticAbility);
			if (text != null)
				lines.Add(text);
		}

		return string.Join("\n", lines);
	}

	private static string? DescribeKeywords(CreatureComponent creature)
	{
		var keywords = new List<string>();
		if (creature.HasFlying)
			keywords.Add("Flying");
		if (creature.HasHaste)
			keywords.Add("Haste");
		if (creature.HasDoubleStrike)
			keywords.Add("Double Strike");
		if (creature.HasTaunt)
			keywords.Add("Taunt");
		if (creature.HasReach)
			keywords.Add("Reach");
		if (creature.HasLifelink)
			keywords.Add("Lifelink");
		if (creature.HasTrample)
			keywords.Add("Trample");
		return keywords.Count > 0 ? string.Join(", ", keywords) : null;
	}

	private static string DescribeSpell(SpellComponent spell)
	{
		var lines = spell.Effects.Select(DescribeEffect).Where(d => d != null).ToList();
		var text = string.Join("\n", lines);
		if (spell.HasStorm)
			return string.IsNullOrEmpty(text) ? "Storm" : $"Storm — {text}";
		return text;
	}

	private static string? DescribeActivatedAbility(ActivatedAbilityComponent ability)
	{
		var effectStr = DescribeEffect(ability.Effect);
		if (effectStr == null)
			return null;

		var costParts = new List<string>();
		if (ability.ManaCost > 0)
			costParts.Add($"{ability.ManaCost} mana");
		foreach (var cost in ability.AdditionalCosts)
		{
			var costText = cost switch
			{
				SacrificeAdditionalCost s when s.Filter is IsSubtypeSpecification sub =>
					$"sacrifice a {sub.Subtype}",
				SacrificeAdditionalCost => "sacrifice a permanent",
				DiscardAdditionalCost d => d.Count == 1
					? "discard a card"
					: $"discard {d.Count} cards",
				LifeAdditionalCost l => $"pay {l.Amount} life",
				_ => null,
			};
			if (costText != null)
				costParts.Add(costText);
		}

		var costStr = costParts.Count > 0 ? string.Join(", ", costParts) : "free";
		return $"{ability.Name} ({costStr}): {effectStr}";
	}

	private static string? DescribeTriggeredAbility(TriggeredAbilityComponent trigger)
	{
		var effectStr = DescribeEffect(trigger.Effect);
		if (effectStr == null)
			return null;
		return $"{DescribeTriggerCondition(trigger.Condition)}: {effectStr}";
	}

	private static string DescribeTriggerCondition(TriggerCondition condition)
	{
		if (condition is not EventTriggerCondition e)
			return "When triggered";

		return e.EventTypeName switch
		{
			EventTypeNames.CreatureEnteredBattlefield => "When this enters",
			EventTypeNames.TurnStarted => "At the beginning of your upkeep",
			EventTypeNames.CombatDamageDealtToPlayer =>
				"Whenever this deals combat damage to a player",
			EventTypeNames.CreatureDestroyed => "When this dies",
			EventTypeNames.CreatureAttacked => "Whenever this attacks",
			EventTypeNames.SpellCast => "Whenever a spell is cast",
			EventTypeNames.TurnEnded => "At end of turn",
			_ => "When triggered",
		};
	}

	private static string? DescribeStaticAbility(StaticAbilityComponent ability) =>
		ability switch
		{
			StaticPTBoostAbility p => $"+{p.PowerBonus}/+{p.ToughnessBonus} to matching creatures",
			StaticGrantKeywordAbility g => DescribeGrantedKeywords(g),
			_ => null,
		};

	private static string? DescribeGrantedKeywords(StaticGrantKeywordAbility g)
	{
		var keywords = new List<string>();
		if (g.GrantsFlying)
			keywords.Add("Flying");
		if (g.GrantsHaste)
			keywords.Add("Haste");
		if (g.GrantsTaunt)
			keywords.Add("Taunt");
		if (g.GrantsReach)
			keywords.Add("Reach");
		if (g.GrantsLifelink)
			keywords.Add("Lifelink");
		if (g.GrantsTrample)
			keywords.Add("Trample");
		return keywords.Count > 0 ? $"Matching creatures gain {string.Join(", ", keywords)}" : null;
	}

	private static string? DescribeEffect(CardEffect effect)
	{
		var targetSuffix = effect.TargetingStrategy.RequiresUserSelection ? " to target" : "";
		return effect.ActionTemplate switch
		{
			DealDamageAction d => $"Deal {d.Amount} damage{targetSuffix}",
			DestroyCreatureAction => $"Destroy{targetSuffix}",
			AddModifierAction m => $"+{m.PowerBonus}/+{m.ToughnessBonus}{targetSuffix}",
			DrawCardsAction d => d.Amount == 1 ? "Draw a card" : $"Draw {d.Amount} cards",
			GainLifeAction g => $"Gain {g.Amount} life",
			LoseLifeAction l => $"Lose {l.Amount} life",
			CreateCardAction c => c.Count == 1
				? $"Create a {c.CardTemplate.Name}"
				: $"Create {c.Count} {c.CardTemplate.Name}s",
			ExileAction => $"Exile{targetSuffix}",
			DiscardCardsAction => $"Discard a card{targetSuffix}",
			MoveCardToHandAction => $"Return{targetSuffix} to hand",
			AddTemporaryManaAction m => $"Add {m.Amount} mana",
			PipelineAction p => DescribePipeline(p),
			_ => null,
		};
	}

	private static string? DescribePipeline(PipelineAction pipeline)
	{
		var parts = pipeline.Steps.Select(DescribeStep).Where(d => d != null).ToList();
		return parts.Count > 0 ? string.Join(", ", parts) : null;
	}

	private static string? DescribeStep(GameAction action) =>
		action switch
		{
			DrawCardsAction d => d.Amount == 1 ? "draw a card" : $"draw {d.Amount} cards",
			LoseLifeAction l => l.Amount > 0 ? $"lose {l.Amount} life" : "lose life",
			GainLifeAction g => $"gain {g.Amount} life",
			AddTemporaryManaAction m => string.IsNullOrEmpty(m.BonusAmountContextKey)
				? $"add {m.Amount} mana"
				: $"add {m.Amount}+X mana",
			RevealTopCardAction => "reveal top card",
			LookAtTopCardsAction l => $"look at top {l.Amount} cards, put one in hand",
			SelectCardFromLibraryAction s => string.IsNullOrEmpty(s.Subtype)
				? "search your library"
				: $"search your library for a {s.Subtype}",
			PutIntoBattlefieldAction => "put it into play",
			DiscardCardsAction => "discard",
			_ => null,
		};
}
