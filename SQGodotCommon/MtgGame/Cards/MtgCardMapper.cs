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
			ArtworkTexture = CardArtLoader.Load(card.Name),
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

		// Dynamic P/T. Without this a Tarmogoyf-style card reads as a plain 0/1.
		if (card.HasComponent<GraveyardCountComponent>())
			lines.Add(
				"Power and toughness are each equal to the number of cards in your graveyard"
			);

		foreach (var threshold in card.GetComponents<ThresholdComponent>())
			lines.Add(DescribeThreshold(threshold));

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

		// The other half of a double-faced card. Without this a werewolf reads as a plain
		// vanilla creature and the drafter cannot see what it becomes.
		var transform = card.GetComponent<TransformComponent>();
		if (transform != null)
			lines.Add(DescribeTransform(transform));

		// Last, as on a real card. Flashback changes how a card is drafted more than almost
		// anything else in this set, so it must never be missing from the text.
		var flashback = card.GetComponent<FlashbackComponent>();
		if (flashback != null)
			lines.Add(
				card.HasComponent<CreatureComponent>()
					? $"Cast from graveyard for {flashback.FlashbackManaCost} (stays on the battlefield)"
					: $"Flashback {flashback.FlashbackManaCost}"
			);

		return string.Join("\n", lines);
	}

	private static string DescribeThreshold(ThresholdComponent t)
	{
		var parts = new List<string>();
		if (t.PowerBonus != 0 || t.ToughnessBonus != 0)
			parts.Add($"+{t.PowerBonus}/+{t.ToughnessBonus}");

		var keywords = new List<string>();
		if (t.GrantsFlying)
			keywords.Add("Flying");
		if (t.GrantsHaste)
			keywords.Add("Haste");
		if (t.GrantsTaunt)
			keywords.Add("Taunt");
		if (t.GrantsReach)
			keywords.Add("Reach");
		if (t.GrantsLifelink)
			keywords.Add("Lifelink");
		if (t.GrantsTrample)
			keywords.Add("Trample");
		if (t.GrantsDeathtouch)
			keywords.Add("Deathtouch");
		if (keywords.Count > 0)
			parts.Add($"gains {string.Join(", ", keywords)}");

		var effect = parts.Count > 0 ? string.Join(" and ", parts) : "has no bonus";
		return $"Threshold — while {t.Minimum}+ cards are in your graveyard, this {effect}";
	}

	private static string DescribeTransform(TransformComponent transform)
	{
		var nightCreature = transform
			.OtherFaceComponents.OfType<CreatureComponent>()
			.FirstOrDefault();
		var stats =
			nightCreature == null ? "" : $" ({nightCreature.Power}/{nightCreature.Toughness})";
		return $"Transforms into {transform.OtherFaceName}{stats}";
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
		if (creature.HasDeathtouch)
			keywords.Add("Deathtouch");
		if (creature.HasShroud)
			keywords.Add("Shroud");
		if (creature.HasHexproof)
			keywords.Add("Hexproof");
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
		// A trigger may carry several effects; join the ones we can describe and drop the
		// rest, so an undescribable effect degrades the text rather than blanking the ability.
		var effectStr = string.Join(
			", ",
			trigger.Effects.Select(DescribeEffect).Where(s => s != null)
		);
		if (string.IsNullOrEmpty(effectStr))
			return null;
		return $"{DescribeTriggerCondition(trigger.Condition)}: {effectStr}";
	}

	private static string DescribeTriggerCondition(TriggerCondition condition)
	{
		// Werewolf transform conditions are not EventTriggerConditions, so they must be
		// matched before the cast below or every werewolf reads "When triggered".
		if (condition is SpellsCastLastTurnCondition s)
			return s.Maximum == 0
				? "If no spells were cast last turn"
				: $"If {s.Minimum}+ spells were cast last turn";

		if (condition is LandsPlayedCondition l)
			return $"Whenever you play a land, if you have played {l.Threshold}+";

		if (condition is not EventTriggerCondition e)
			return "When triggered";

		// A source-card filter means the event is about THIS card, which changes the wording
		// from "whenever a card is discarded" to "when you discard this" — the difference
		// between a generic trigger and a madness card.
		var isSelf = e.Filter is IsSourceCardSpecification;

		return e.EventTypeName switch
		{
			EventTypeNames.CreatureEnteredBattlefield => isSelf
				? "When this enters"
				: "Whenever a creature enters",
			EventTypeNames.PermanentEnteredBattlefield => "When this enters",
			EventTypeNames.TurnStarted => "At the beginning of your upkeep",
			EventTypeNames.CombatDamageDealtToPlayer =>
				"Whenever this deals combat damage to a player",
			EventTypeNames.CreatureDestroyed => isSelf
				? "When this dies"
				: "Whenever a creature dies",
			EventTypeNames.CreatureAttacked => "Whenever this attacks",
			EventTypeNames.SpellCast => "Whenever you cast a spell",
			EventTypeNames.CardDiscarded => isSelf
				? "Madness — when you discard this"
				: "Whenever you discard a card",
			EventTypeNames.CardMilled => "Whenever a card of yours is milled",
			EventTypeNames.LandPlayed => "Landfall — whenever you play a land",
			EventTypeNames.TurnEnded => "At end of turn",
			_ => "When triggered",
		};
	}

	private static string? DescribeStaticAbility(StaticAbilityComponent ability)
	{
		var text = ability switch
		{
			StaticPTBoostAbility p => $"+{p.PowerBonus}/+{p.ToughnessBonus} to matching creatures",
			StaticGrantKeywordAbility g => DescribeGrantedKeywords(g),
			_ => null,
		};
		if (text == null)
			return null;

		// A graveyard-active static is a completely different card from a battlefield one —
		// Wonder does nothing while it is in play. The zone has to be in the text.
		return ability.ActiveInZone == ZoneType.Graveyard
			? $"While this is in your graveyard: {text}"
			: text;
	}

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
		if (g.GrantsDeathtouch)
			keywords.Add("Deathtouch");
		if (g.GrantsShroud)
			keywords.Add("Shroud");
		if (g.GrantsHexproof)
			keywords.Add("Hexproof");
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
			DiscardCardsAction => "Discard a card",
			MoveCardToHandAction => $"Return{targetSuffix} to hand",
			AddTemporaryManaAction m => $"Add {m.Amount} mana",
			MillAction m => DescribeMill(m, effect),
			ReturnToHandAction => $"Return{targetSuffix} to hand",
			DrainLifeAction d => $"Target opponent loses {d.Amount} life and you gain {d.Amount}",
			DiscardRandomCardAction => "Target opponent discards a card at random",
			GrantKeywordAction g => DescribeGrantKeyword(g, targetSuffix),
			GiveFlashbackAction => "An instant or sorcery in your graveyard gains Flashback",
			PutIntoBattlefieldAction => $"Put{targetSuffix} onto the battlefield",
			TransformAction => "Transform this",
			PipelineAction p => DescribePipeline(p),
			_ => null,
		};
	}

	/// Mill reads very differently depending on who it hits, and the targeting strategy is
	/// the only thing that says which — so it is resolved here rather than in the table.
	private static string DescribeMill(MillAction mill, CardEffect effect)
	{
		var who = effect.TargetingStrategy.SelectionMode switch
		{
			TargetSelectionMode.CastingPlayer => "You mill",
			TargetSelectionMode.AllValid => "Each player mills",
			_ => "Target player mills",
		};
		return $"{who} {mill.Amount}";
	}

	private static string DescribeGrantKeyword(GrantKeywordAction g, string targetSuffix)
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
		if (g.GrantsDeathtouch)
			keywords.Add("Deathtouch");

		if (keywords.Count == 0)
			return "Grants nothing";

		var duration = g.Duration == ModifierDuration.UntilEndOfTurn ? " until end of turn" : "";
		return $"Gain {string.Join(", ", keywords)}{targetSuffix}{duration}";
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
			MillAction m => $"mill {m.Amount}",
			MoveCardToHandAction => "return it to hand",
			MoveCardToExileAction => "exile it",
			ReturnToHandAction => "return it to hand",
			DrainLifeAction d => $"drain {d.Amount}",
			DiscardRandomCardAction => "opponent discards at random",
			CreateCardAction c => string.IsNullOrEmpty(c.CountInputKey)
				? $"create {c.Count} {c.CardTemplate.Name}"
				: $"create that many {c.CardTemplate.Name}s",
			CountCardsWithSubtypeAction s => string.IsNullOrEmpty(s.Subtype)
				? $"count the cards in your {s.Zone.ToString().ToLowerInvariant()}"
				: $"count {s.Subtype}s in your {s.Zone.ToString().ToLowerInvariant()}",
			SelectCardFromZoneAction s => string.IsNullOrEmpty(s.Subtype)
				? $"choose a card from a {s.Zone.ToString().ToLowerInvariant()}"
				: $"choose a {s.Subtype} from a {s.Zone.ToString().ToLowerInvariant()}",
			_ => null,
		};
}
