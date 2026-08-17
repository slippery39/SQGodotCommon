using System;
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
			TypeLine = GetTypeLine(card),
			PowerToughness = GetPowerToughness(card, state),
			RulesText = GetRulesText(card),
			ArtworkTexture = CardArtLoader.Load(card.Name),
			FrameColor = MtgCardTheme.FrameColor(card),
			NamePlateColor = MtgCardTheme.NamePlateColor(card),
			OutlineColor = canPlay ? new Color(0, 1.5f, 0, 1) : new Color(0.3f, 0.3f, 0.3f, 1),
			OutlineThickness = canPlay ? 3f : 0f,
		};
	}

	/// <summary>
	/// The card's type line, e.g. "Creature — Human Soldier". Derived from component presence and
	/// <see cref="Card.Subtypes"/>, which is all the engine carries — there is no card-type system.
	/// Never blank, so it is the reliable "this card face rendered something" check.
	///
	/// Every instant and sorcery reads a flat "Spell": <c>SpellCardBuilder</c> has no
	/// <c>WithSubtype</c> and the engine has no Instant/Sorcery marker. See DesignNotes.md.
	/// </summary>
	public static string GetTypeLine(Card card)
	{
		if (card.HasSubtype("Land"))
			return card.HasSubtype("Basic") ? "Basic Land" : "Land";

		if (card.HasComponent<CreatureComponent>())
		{
			// No "Creature — " prefix when there are subtypes: the P/T badge already says the card
			// is a creature, and the prefix cost 11 of the ~22 characters the band can fit, which
			// clipped every three-subtype card ("Creature — Werewolf Human Soldier").
			var tribes = MtgCardTheme.OrderedTribes(card).ToList();
			return tribes.Count > 0 ? string.Join(" ", tribes) : "Creature";
		}

		if (card.HasSubtype("Artifact"))
			return "Artifact";
		if (card.HasComponent<SpellComponent>())
			return "Spell";

		return card.HasComponent<PermanentComponent>() ? "Permanent" : "Card";
	}

	/// <summary>
	/// The single source of displayed power/toughness. Returns null for non-creatures so the
	/// caller can hide the badge entirely.
	/// </summary>
	/// <param name="state">
	/// Null for a card that is not in a game — a draft pack, for instance. Stats then come from
	/// the printed P/T, since a card outside a GameState can carry no modifiers or damage.
	/// </param>
	public static string GetPowerToughness(Card card, GameState state)
	{
		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			return null;

		if (state == null)
			return $"{creature.Power}/{creature.Toughness}";

		var stats = state.GetEffectiveStats(card.Id);
		var text = $"{stats.Power}/{stats.Toughness}";
		// Damage goes on its own line — the badge is round, so two short lines fit where one
		// long one would overflow.
		return creature.Damage > 0 ? $"{text}\n-{creature.Damage}" : text;
	}

	public static string GetRulesText(Card card)
	{
		if (card.HasSubtype("Land"))
			return card.HasSubtype("Basic") ? "Basic Land\n(Tap: Add 1 mana)" : "Land";

		var creature = card.GetComponent<CreatureComponent>();
		var spell = card.GetComponent<SpellComponent>();
		var lines = new List<string>();

		// First, as on a real card. A cost paid before the spell resolves is invisible in the
		// effect text, so a reanimate-for-a-discard read as pure upside until the prompt appeared.
		foreach (
			var costText in card.AdditionalCastCosts.Select(DescribeCost).Where(t => t != null)
		)
			lines.Add($"As an additional cost, {costText}");

		if (creature != null)
		{
			// P/T deliberately absent — it has its own badge on the frame. Printing it here too
			// meant a lord-buffed creature read "2/2" in its text box and "4/4" in its corner.
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
		//
		// Folded into the trigger that causes it when there is one, rather than added as its own
		// line. A werewolf otherwise spent two of its five lines saying transform twice:
		//   "If no spells were cast last turn: Transform this"
		//   "Transforms into Blood-Moon Devourer (6/5)"
		var transform = card.GetComponent<TransformComponent>();
		if (transform != null && !FoldTransformIntoTrigger(lines, transform))
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

		return string.Join("\n", MergeSharedClauses(lines));
	}

	private const string TransformVerb = "Transform this";

	/// <summary>
	/// Rewrites "…: Transform this" into "…: Transforms into X (P/T)" so the card states its
	/// trigger and its destination on one line. Returns false when no trigger mentions
	/// transforming, in which case the caller still needs a standalone line.
	/// </summary>
	private static bool FoldTransformIntoTrigger(List<string> lines, TransformComponent transform)
	{
		var index = lines.FindIndex(l => l.EndsWith(TransformVerb, StringComparison.Ordinal));
		if (index < 0)
			return false;

		lines[index] =
			lines[index][..^TransformVerb.Length] + Decapitalise(DescribeTransform(transform));
		return true;
	}

	/// <summary>
	/// Joins rendered effect fragments, squeezing out the two ways this set's cards repeat
	/// themselves: a whole sequence authored twice over, and consecutive clauses that differ only
	/// in their verb. Every place that joins fragments goes through here, because the repetition
	/// can sit at any level — a pipeline's steps, an ability's effects, or a card's lines.
	/// </summary>
	private static string CombineParts(List<string> parts, string separator)
	{
		// A repeated effect is authored as the same fragments twice over, which spelled itself out
		// in full each time: "take the opponent's best creature, destroy it, take the opponent's
		// best creature, destroy it". State the cycle once and say how often it happens.
		var period = SmallestRepeatingPeriod(parts);
		if (period < parts.Count)
			return $"{string.Join(separator, parts.Take(period))} — {Times(parts.Count / period)}";

		return string.Join(separator, MergeSharedClauses(parts));
	}

	/// Length of the shortest block the list is a whole repetition of; the full length when the
	/// list does not repeat.
	private static int SmallestRepeatingPeriod(List<string> parts)
	{
		for (var period = 1; period < parts.Count; period++)
		{
			if (parts.Count % period != 0)
				continue;
			var repeats = true;
			for (var i = period; i < parts.Count && repeats; i++)
				repeats = parts[i] == parts[i % period];
			if (repeats)
				return period;
		}
		return parts.Count;
	}

	private static string Times(int n) =>
		n switch
		{
			2 => "twice",
			3 => "three times",
			_ => $"{n} times",
		};

	/// <summary>
	/// Merges consecutive lines that share a leading subject and a trailing qualifier, so a mass
	/// pump reads as one sentence instead of two near-identical ones:
	///   "Each creature you control gets +2/+2 until end of turn"
	///   "Each creature you control gains Flying until end of turn"
	/// becomes "Each creature you control gets +2/+2 and gains Flying until end of turn".
	///
	/// Operates on the rendered strings rather than the effect data because the two halves come
	/// from different action types (AddModifierAction and GrantKeywordAction) that share no
	/// common shape to merge at the source.
	/// </summary>
	private static List<string> MergeSharedClauses(List<string> lines)
	{
		var merged = new List<string>();
		foreach (var line in lines)
		{
			if (merged.Count == 0 || !TryMerge(merged[^1], line, out var combined))
			{
				merged.Add(line);
				continue;
			}
			merged[^1] = combined;
		}
		return merged;
	}

	private static bool TryMerge(string first, string second, out string combined)
	{
		combined = null;

		var a = first.Split(' ');
		var b = second.Split(' ');

		var prefix = 0;
		while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
			prefix++;

		var suffix = 0;
		while (
			suffix < a.Length - prefix
			&& suffix < b.Length - prefix
			&& a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix]
		)
			suffix++;

		// Both halves must contribute something distinct, and the shared subject has to be a real
		// phrase — a single shared word like "Deal" is a coincidence, not a common subject.
		var middleA = a.Length - prefix - suffix;
		var middleB = b.Length - prefix - suffix;
		if (prefix < 2 || suffix < 1 || middleA < 1 || middleB < 1)
			return false;

		// Only merge predicates. Merging noun middles distributes the shared trailing noun across
		// both, which changes the meaning: "Create 2 Human tokens" + "Create 2 Spirit tokens"
		// became "Create 2 Human and Spirit tokens", i.e. two tokens instead of four.
		// Failing to merge only costs a line; merging wrongly misprints the card.
		if (!IsMergeableVerb(a[prefix]) || !IsMergeableVerb(b[prefix]))
			return false;

		combined = string.Join(
			' ',
			a[..prefix]
				.Concat(a[prefix..^suffix])
				.Append("and")
				.Concat(b[prefix..^suffix])
				.Concat(a[^suffix..])
		);
		return true;
	}

	/// Verbs the effect describers emit at the head of a predicate. Deliberately a closed list:
	/// a new verb simply misses a merge until it is added, which is the safe direction.
	private static bool IsMergeableVerb(string word) =>
		word is "gets" or "gains" or "loses" or "has" or "deals";

	private static string Decapitalise(string s) =>
		string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

	private static string DescribeThreshold(ThresholdComponent t)
	{
		var parts = new List<string>();
		if (t.PowerBonus != 0 || t.ToughnessBonus != 0)
			parts.Add($"gets {Signed(t.PowerBonus)}/{Signed(t.ToughnessBonus)}");

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
		var text = CombineParts(lines, "\n");
		if (spell.HasStorm)
			return string.IsNullOrEmpty(text) ? "Storm" : $"Storm — {text}";
		return text;
	}

	/// Lowercase phrase for one additional cost, so it reads inside a longer sentence.
	/// Null means "no wording for this cost", and the caller drops it.
	private static string? DescribeCost(AdditionalCost cost) =>
		cost switch
		{
			SacrificeAdditionalCost s when s.Filter is IsSubtypeSpecification sub =>
				$"sacrifice a {sub.Subtype}",
			SacrificeAdditionalCost => "sacrifice a permanent",
			DiscardAdditionalCost d => d.Count == 1 ? "discard a card" : $"discard {d.Count} cards",
			LifeAdditionalCost l => $"pay {l.Amount} life",
			_ => null,
		};

	private static string? DescribeActivatedAbility(ActivatedAbilityComponent ability)
	{
		// An ability may carry several effects — "discard a card, then draw a card" is two.
		var effectStr = CombineParts(
			ability.Effects.Select(DescribeEffect).Where(s => s != null).ToList(),
			", "
		);
		if (string.IsNullOrEmpty(effectStr))
			return null;

		var costParts = new List<string>();
		if (ability.ManaCost > 0)
			costParts.Add($"{ability.ManaCost} mana");
		foreach (var cost in ability.AdditionalCosts)
		{
			var costText = DescribeCost(cost);
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
		var effectStr = CombineParts(
			trigger.Effects.Select(DescribeEffect).Where(s => s != null).ToList(),
			", "
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
		// "Matching creatures" tells a drafter nothing — the filter is the whole card. A lord's
		// filter is composed three specs deep, so it is walked rather than pattern-matched.
		var who = Plural(DescribeSpecification(ability.Filter));

		var text = ability switch
		{
			StaticPTBoostAbility p =>
				$"{Capitalise(who)} get {Signed(p.PowerBonus)}/{Signed(p.ToughnessBonus)}",
			StaticGrantKeywordAbility g => DescribeGrantedKeywords(g, who),
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

	/// Pluralises the head noun of a filter phrase so a static reads "other Spirits you
	/// control" rather than "other Spirit you control".
	private static string Plural(string phrase)
	{
		var suffixes = new[] { " you control", " an opponent controls" };
		foreach (var suffix in suffixes)
			if (phrase.EndsWith(suffix))
				return phrase[..^suffix.Length] + "s" + suffix;
		return phrase.EndsWith("s") ? phrase : phrase + "s";
	}

	private static string? DescribeGrantedKeywords(StaticGrantKeywordAbility g, string who)
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
		return keywords.Count > 0 ? $"{Capitalise(who)} gain {string.Join(", ", keywords)}" : null;
	}

	private static string? DescribeEffect(CardEffect effect)
	{
		// The target phrase must be placed grammatically per action, not appended as a generic
		// suffix — a suffix is what produced "Put to target onto the battlefield".
		var t = DescribeTarget(effect);

		return effect.ActionTemplate switch
		{
			DealDamageAction d => $"Deal {d.Amount} damage to {t}",
			DestroyCreatureAction => $"Destroy {t}",
			ExileAction => $"Exile {t}",
			AddModifierAction m => $"{Capitalise(t)} gets {Signed(m.PowerBonus)}/"
				+ $"{Signed(m.ToughnessBonus)}{DurationSuffix(m.Duration)}",
			DrawCardsAction d => d.Amount == 1 ? "Draw a card" : $"Draw {d.Amount} cards",
			GainLifeAction g => $"Gain {g.Amount} life",
			LoseLifeAction l => $"Lose {l.Amount} life",
			CreateCardAction c => c.Count == 1
				? $"Create a {c.CardTemplate.Name} token"
				: $"Create {c.Count} {c.CardTemplate.Name} tokens",
			DiscardCardsAction => "Discard a card",
			MoveCardToHandAction => $"Return {t} to your hand",
			ReturnToHandAction => $"Return {t} to your hand",
			AddTemporaryManaAction m => $"Add {m.Amount} mana",
			MillAction m => DescribeMill(m, effect),
			DrainLifeAction d => $"Target opponent loses {d.Amount} life and you gain {d.Amount}",
			DiscardRandomCardAction => "Target opponent discards a card at random",
			GrantKeywordAction g => DescribeGrantKeyword(g, t),
			GiveFlashbackAction => "An instant or sorcery in your graveyard gains Flashback",
			PutIntoBattlefieldAction => $"Put {t} onto the battlefield",
			TransformAction => "Transform this",
			FightAction => $"This fights {t}",
			LookAtTopCardsAction l => $"Look at the top {l.Amount} cards, put one in your hand",
			PipelineAction p => DescribePipeline(p),
			_ => null,
		};
	}

	private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();

	private static string DurationSuffix(ModifierDuration d) =>
		d == ModifierDuration.UntilEndOfTurn ? " until end of turn" : "";

	private static string Capitalise(string s) =>
		string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

	/// <summary>
	/// Turns a targeting strategy into the noun phrase a real card would print — "target
	/// creature card in your graveyard", "each creature you control", "this". Without it every
	/// targeted effect reads as a bare verb and the drafter cannot tell what it hits.
	/// </summary>
	private static string DescribeTarget(CardEffect effect)
	{
		// An effect aimed at its own source through a context key carries no targeting
		// strategy to read, so the strategy would misleadingly say "no target". Two different
		// keys mean the same thing here — EffectAction targets, and the card-id key that
		// PutIntoBattlefieldAction uses for madness and self-recursion.
		if (
			effect.ActionTemplate is EffectAction ea
			&& ea.TargetContextKey == ContextKeys.SourceCardId
		)
			return "this";

		if (
			effect.ActionTemplate is PutIntoBattlefieldAction pib
			&& pib.CardIdContextKey == ContextKeys.SourceCardId
		)
			return "this";

		var strategy = effect.TargetingStrategy;
		if (strategy.SelectionMode == TargetSelectionMode.CastingPlayer)
			return "you";
		if (strategy.SelectionMode == TargetSelectionMode.None)
			return "it";

		var noun = DescribeSpecification(strategy.Specification);

		// "any target" is already a complete target phrase — prefixing it yields
		// "target any target".
		if (noun == "any target")
			return strategy.SelectionMode == TargetSelectionMode.AllValid
				? "each opponent and creature"
				: noun;

		return strategy.SelectionMode switch
		{
			TargetSelectionMode.AllValid => $"each {noun}",
			TargetSelectionMode.Random => $"a random {noun}",
			_ => $"target {noun}",
		};
	}

	/// Facts gathered by walking a composed specification tree.
	private sealed class SpecFacts
	{
		public string? Subtype;
		public bool Other;
		public bool Yours;
		public bool Opponents;
		public bool Creature;
		public bool Player;
		public bool CreatureInGraveyard;
		public bool SpellInGraveyard;
		public bool InHand;
	}

	/// Specifications are composed with And/Or, so the shape has to be walked rather than
	/// matched — a lord filter is three specs deep.
	private static void Collect(TargetSpecification spec, SpecFacts f)
	{
		switch (spec)
		{
			case AndSpecification a:
				Collect(a.Left, f);
				Collect(a.Right, f);
				break;
			case OrSpecification o:
				Collect(o.Left, f);
				Collect(o.Right, f);
				break;
			case IsSubtypeSpecification s:
				f.Subtype = s.Subtype;
				break;
			case IsNotSelfSpecification:
				f.Other = true;
				break;
			case IsControlledByYouSpecification:
				f.Yours = true;
				break;
			case IsControlledByOpponentSpecification:
				f.Opponents = true;
				break;
			case IsCreatureSpecification:
				f.Creature = true;
				break;
			case IsPlayerSpecification:
				f.Player = true;
				break;
			case IsCreatureInOwnGraveyardSpecification:
				f.CreatureInGraveyard = true;
				break;
			case IsInstantOrSorceryInOwnGraveyardSpecification:
				f.SpellInGraveyard = true;
				break;
			case IsInHandSpecification:
				f.InHand = true;
				break;
		}
	}

	public static string DescribeSpecification(TargetSpecification? spec)
	{
		if (spec == null)
			return "it";

		var f = new SpecFacts();
		Collect(spec, f);

		// The graveyard and hand specs imply the zone, but they can still be narrowed by a
		// subtype — and dropping that narrowing is the worst kind of text bug, because the
		// card then promises MORE than it does. Zombie Apocalypse read as "each creature card
		// in your graveyard" while only ever returning Zombies.
		if (f.CreatureInGraveyard)
			return f.Subtype == null
				? "creature card in your graveyard"
				: $"{f.Subtype} card in your graveyard";
		if (f.SpellInGraveyard)
			return "instant or sorcery in your graveyard";
		if (f.InHand)
			return f.Subtype == null ? "card from your hand" : $"{f.Subtype} card from your hand";

		// Players OR creatures is the classic burn-spell target — collapsing it to "creature"
		// would hide that these spells can go to the face.
		if (f.Player && f.Creature)
			return "any target";

		// Player-only specs, which is what mill and hand attack aim at.
		if (f.Player)
			return f.Opponents ? "opponent" : "player";

		var noun = f.Subtype ?? (f.Creature ? "creature" : "permanent");
		var prefix = f.Other ? "other " : "";
		var suffix =
			f.Yours ? " you control"
			: f.Opponents ? " an opponent controls"
			: "";

		return $"{prefix}{noun}{suffix}";
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

	private static string DescribeGrantKeyword(GrantKeywordAction g, string target)
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

		return $"{Capitalise(target)} gains {string.Join(", ", keywords)}"
			+ DurationSuffix(g.Duration);
	}

	/// <summary>
	/// A zone selection is only meaningful with WHOSE zone and WHICH card — "choose a card
	/// from a graveyard" leaves a drafter guessing on both counts.
	/// </summary>
	private static string DescribeZoneSelection(SelectCardFromZoneAction s)
	{
		var whose = s.TargetOpponent ? "an opponent's" : "your";
		var zone = s.Zone.ToString().ToLowerInvariant();

		var what =
			!string.IsNullOrEmpty(s.Subtype) ? $"a {s.Subtype}"
			: s.Filter is IsCreatureInOwnGraveyardSpecification ? "a creature card"
			: s.Filter is IsInstantOrSorceryInOwnGraveyardSpecification ? "an instant or sorcery"
			: "a card";

		return $"choose {what} from {whose} {zone}";
	}

	private static string? DescribePipeline(PipelineAction pipeline)
	{
		var parts = pipeline.Steps.Select(DescribeStep).Where(d => d != null).ToList();
		return parts.Count > 0 ? CombineParts(parts, ", ") : null;
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
			SelectCardFromZoneAction s => DescribeZoneSelection(s),
			// The trigger-safe verbs are pipelines that pick a target themselves, so their
			// steps have to read as one sentence: "the opponent's best creature, destroy it".
			SelectCreatureFromBattlefieldByManaCostAction s => s.TargetOpponent
				? $"take the opponent's {(s.SelectLowest ? "cheapest" : "best")} creature"
				: $"take your {(s.SelectLowest ? "cheapest" : "best")} creature",
			DestroyCreatureAction => "destroy it",
			ExileAction => "exile it",
			DealDamageAction d => $"deal {d.Amount} damage to it",
			FightAction => "fight it",
			AddModifierAction m => $"give it {Signed(m.PowerBonus)}/{Signed(m.ToughnessBonus)}",
			_ => null,
		};
}
