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
			RulesText = GetRulesText(card, state),
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

		// Card.EffectiveTypes now answers this properly. Checked before the component-sniffing
		// fallbacks below, which stay for hand-built cards that never declared a type.
		if (card.HasType(CardType.Planeswalker))
			return "Planeswalker";
		if (card.HasType(CardType.Enchantment))
			return card.GetComponent<EquipmentComponent>()?.IsAura == true ? "Aura" : "Enchantment";

		if (card.HasComponent<CreatureComponent>())
		{
			// No "Creature — " prefix when there are subtypes: the P/T badge already says the card
			// is a creature, and the prefix cost 11 of the ~22 characters the band can fit, which
			// clipped every three-subtype card ("Creature — Werewolf Human Soldier").
			var tribes = MtgCardTheme.OrderedTribes(card).ToList();
			return tribes.Count > 0 ? string.Join(" ", tribes) : "Creature";
		}

		if (card.HasSubtype("Artifact"))
			return card.HasComponent<EquipmentComponent>() ? "Equipment" : "Artifact";

		// Instant and Sorcery are distinguishable for any card built through
		// CardFactory.Instant/.Sorcery. A card built through the older Spell() entry point
		// declares no type, and Card.EffectiveTypes reports Instant|Sorcery for it — "this is a
		// spell, but which kind is unknown". Requiring exactly one is what tells the two cases
		// apart, so an undeclared spell still reads a flat "Spell" rather than guessing.
		var isInstant = card.HasType(CardType.Instant);
		var isSorcery = card.HasType(CardType.Sorcery);

		if (isInstant && !isSorcery)
			return "Instant";
		if (isSorcery && !isInstant)
			return "Sorcery";
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
		// A planeswalker's badge shows loyalty. Without this the card renders with no number at
		// all, so a player cannot see how close it is to dying — the only thing that matters
		// about it on the board.
		var walker = card.GetComponent<PlaneswalkerComponent>();
		if (walker != null)
			return state == null ? $"{walker.StartingLoyalty}" : $"{walker.Loyalty}";

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

	/// <param name="state">
	/// The live game, when there is one. Keywords are then read from the card's effective stats,
	/// so a creature granted Haste by something else says Haste. Without it only the printed
	/// keywords show — which is all a draft pack card has.
	/// </param>
	public static string GetRulesText(Card card, GameState state = null)
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
			var keywords = DescribeKeywords(
				creature,
				state != null && state.HasObject(card.Id) ? state.GetEffectiveStats(card.Id) : null
			);
			if (keywords != null)
				lines.Add(keywords);
		}
		else if (spell != null)
		{
			var spellText = DescribeSpell(spell);
			if (!string.IsNullOrEmpty(spellText))
				lines.Add(spellText);
		}

		// A counter trap has a SpellComponent with NO effects, so without this the card renders
		// completely blank — the single most important line in this method for the blue section.
		var trap = card.GetComponent<CounterTrapComponent>();
		if (trap != null)
			lines.Add(DescribeCounterTrap(trap));

		// Dynamic P/T. Without this a Tarmogoyf-style card reads as a plain 0/1.
		if (card.HasComponent<GraveyardCountComponent>())
			lines.Add(
				"Power and toughness are each equal to the number of cards in your graveyard"
			);

		if (card.HasComponent<CreatureCountComponent>())
			lines.Add("Power and toughness are each equal to the number of creatures you control");

		foreach (var lifeBonus in card.GetComponents<LifeTotalComponent>())
			lines.Add(
				$"Gets +{lifeBonus.PowerBonus}/+{lifeBonus.ToughnessBonus} "
					+ $"while you have {lifeBonus.Minimum}+ life"
			);

		foreach (var gain in card.GetComponents<LifeGainBonusComponent>())
			lines.Add($"If you would gain life, gain that much plus {gain.Amount} instead");

		foreach (var exalted in card.GetComponents<ExaltedComponent>())
			lines.Add(
				exalted.Count == 1
					? "Exalted (attacks alone: +1/+1 until end of turn)"
					: $"Exalted {exalted.Count}"
			);

		foreach (var protection in card.GetComponents<ProtectionFromSubtypeComponent>())
			lines.Add($"Protection from {string.Join(" and ", protection.Subtypes)}");

		foreach (var tax in card.GetComponents<SpellTaxComponent>())
			lines.Add(
				tax.NonCreatureOnly
					? $"Noncreature spells cost {tax.Amount} more to cast"
					: $"Spells cost {tax.Amount} more to cast"
			);

		if (card.HasComponent<CopyOnEnterComponent>())
			lines.Add("Enters as a copy of the strongest creature on the battlefield");

		if (card.HasComponent<TauntUntilAttackedComponent>())
			lines.Add("Loses Taunt once it has been attacked this turn");

		if (card.HasComponent<PreventsCombatDamageComponent>())
			lines.Add("Prevents all combat damage dealt to and by this creature");

		if (card.HasComponent<ConvokeComponent>())
			lines.Add("Convoke (costs 1 less per ready creature; those creatures tap)");

		if (card.HasComponent<XCostComponent>())
			lines.Add("X is all the mana you have left when you cast it");

		foreach (var reduction in card.GetComponents<ConditionalCostReductionComponent>())
			lines.Add(
				$"Costs {reduction.Amount} less to cast if "
					+ LowerFirst(reduction.Condition?.Describe() ?? "a condition is met")
			);

		// Serra Avenger reads as a free 3/3 flyer for two without this.
		foreach (var restriction in card.GetComponents<CastRestrictionComponent>())
			lines.Add(restriction.Describe());

		// Equipment and Auras. An Aura with only a can't-attack flag (Pacifism) has no other
		// component to describe it at all.
		var attachment = card.GetComponent<EquipmentComponent>();
		if (attachment != null)
			lines.Add(DescribeAttachment(attachment));

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

	/// <param name="stats">
	/// Effective stats when the card is in play, so granted keywords (a lord's Flying, Fury of
	/// the Mere's Haste from the graveyard) print alongside the printed ones. Null falls back to
	/// the printed keywords. Double Strike is not carried on CreatureStats, so it always reads
	/// from the component.
	/// </param>
	private static string? DescribeKeywords(CreatureComponent creature, CreatureStats stats)
	{
		var keywords = new List<string>();

		void Add(bool present, string name)
		{
			if (present)
				keywords.Add(name);
		}

		Add(stats?.HasFlying ?? creature.HasFlying, "Flying");
		Add(stats?.HasHaste ?? creature.HasHaste, "Haste");
		Add(stats?.HasDoubleStrike ?? creature.HasDoubleStrike, "Double Strike");
		// Only when double strike is absent — double strike already includes first strike, and
		// printing both reads as two abilities.
		Add(
			(stats?.HasFirstStrike ?? creature.HasFirstStrike)
				&& !(stats?.HasDoubleStrike ?? creature.HasDoubleStrike),
			"First Strike"
		);
		Add(stats?.HasIndestructible ?? creature.HasIndestructible, "Indestructible");
		Add(stats?.HasTaunt ?? creature.HasTaunt, "Taunt");
		Add(stats?.HasReach ?? creature.HasReach, "Reach");
		Add(stats?.HasLifelink ?? creature.HasLifelink, "Lifelink");
		Add(stats?.HasTrample ?? creature.HasTrample, "Trample");
		Add(stats?.HasDeathtouch ?? creature.HasDeathtouch, "Deathtouch");
		Add(stats?.HasShroud ?? creature.HasShroud, "Shroud");
		Add(stats?.HasHexproof ?? creature.HasHexproof, "Hexproof");
		Add(stats?.CantAttack ?? false, "Can't attack");

		return keywords.Count > 0 ? string.Join(", ", keywords) : null;
	}

	/// <summary>
	/// A counterspell trap. These fire from hand and are never cast, so a player holding one has
	/// no other way to learn what it does — and the card has no effects to describe.
	/// </summary>
	private static string DescribeCounterTrap(CounterTrapComponent trap)
	{
		var what =
			trap.ExcludeTypes.HasFlag(CardType.Creature) ? "a noncreature spell"
			: trap.TargetTypes == CardType.Creature ? "a creature spell"
			: "a spell";

		var unless =
			trap.TaxAllRemaining ? " unless they pay your remaining mana"
			: trap.ManaTax > 0 ? $" unless they pay {trap.ManaTax}"
			: "";

		var fate =
			trap.ExileInstead ? " Exiled instead of buried."
			: trap.ReturnToHandInstead ? " Returned to hand instead of buried."
			: "";

		var draw = trap.DrawOnCounter > 0 ? $" Draw {trap.DrawOnCounter}." : "";

		return $"Trap — if you leave this card's cost unspent, it fires from your hand and "
			+ $"counters {what} an opponent casts{unless}.{fate}{draw}";
	}

	/// <summary>Equipment and Auras — they share one component, differing only by IsAura.</summary>
	private static string DescribeAttachment(EquipmentComponent attachment)
	{
		var boost = attachment.CustomBoostTemplate as EquippedBoostComponent;
		var parts = new List<string>();

		var power = boost?.PowerBonus ?? attachment.PowerBonus;
		var toughness = boost?.ToughnessBonus ?? attachment.ToughnessBonus;
		if (power != 0 || toughness != 0)
			parts.Add($"{Signed(power)}/{Signed(toughness)}");

		if (boost != null)
		{
			if (boost.GrantsFlying)
				parts.Add("Flying");
			if (boost.GrantsFirstStrike)
				parts.Add("First Strike");
			if (boost.GrantsLifelink)
				parts.Add("Lifelink");
			if (boost.GrantsIndestructible)
				parts.Add("Indestructible");
			if (boost.GrantsHexproof)
				parts.Add("Hexproof");
			if (boost.PreventsAttacking)
				parts.Add("can't attack");
		}

		var effect = parts.Count > 0 ? string.Join(", ", parts) : "nothing";
		var subject = attachment.IsAura ? "Enchanted creature" : "Equipped creature";
		var lead = attachment.IsAura ? "Aura — attaches when it enters. " : "";

		return $"{lead}{subject} gets {effect}";
	}

	private static string LowerFirst(string text) =>
		string.IsNullOrEmpty(text) ? text : char.ToLowerInvariant(text[0]) + text[1..];

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

		// A loyalty ability's cost IS its identity — "+1" and "-8" are how a player reads a
		// planeswalker. Printed as the real notation rather than as a mana cost.
		if (ability.IsLoyaltyAbility)
			return $"{Signed(ability.LoyaltyCost)}: {effectStr}";

		var costParts = new List<string>();
		if (ability.ManaCost > 0)
			costParts.Add($"{ability.ManaCost} mana");
		if (ability.RequiresTap)
			costParts.Add("tap");
		foreach (var cost in ability.AdditionalCosts)
		{
			var costText = DescribeCost(cost);
			if (costText != null)
				costParts.Add(costText);
		}

		// A gate is part of the cost from the player's point of view — an ability they cannot
		// use yet needs to say why.
		var gate = ability.Condition != null ? $" — {ability.Condition.Describe()}" : "";

		var costStr = costParts.Count > 0 ? string.Join(", ", costParts) : "free";
		return $"{ability.Name} ({costStr}){gate}: {effectStr}";
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
				: $"Whenever {FilterPhrase(e.Filter)} enters",
			EventTypeNames.PermanentEnteredBattlefield => "When this enters",
			EventTypeNames.TurnStarted => "At the beginning of your upkeep",
			EventTypeNames.CombatDamageDealtToPlayer =>
				"Whenever this deals combat damage to a player",
			EventTypeNames.CreatureDestroyed => isSelf
				? "When this dies"
				: $"Whenever {FilterPhrase(e.Filter)} dies",
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

	/// <summary>
	/// The noun phrase a creature trigger fires on, with its article — "another Human you
	/// control", "a creature an opponent controls". Champion of the Parish read "Whenever a
	/// creature enters" while only ever counting Humans, which is the worst kind of text bug:
	/// the card promises more than it does.
	///
	/// Falls back to the generic wording when the filter carries nothing that narrows it, so an
	/// unrecognised specification under-promises rather than mis-promises.
	/// </summary>
	private static string FilterPhrase(TargetSpecification? filter)
	{
		const string fallback = "a creature";
		if (filter == null)
			return fallback;

		var phrase = DescribeSpecification(filter);
		if (phrase is "it" or "permanent" or "creature")
			return fallback;

		if (phrase.StartsWith("other ", StringComparison.Ordinal))
			return "another " + phrase["other ".Length..];

		return ("aeiou".Contains(char.ToLowerInvariant(phrase[0])) ? "an " : "a ") + phrase;
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

			// ===== Core Set Cube =====
			// Every one of these left its card rendering completely blank before it was added.
			DestroyPermanentAction => $"Destroy {t}",
			ExhaustCreatureAction e => DescribeExhaust(e, t),
			MoveCardToTopOfLibraryAction => $"Put {t} on top of its owner's library",
			MoveCardToBottomOfLibraryAction => $"Put {t} on the bottom of its owner's library",
			PutOnLibraryAction p => p.Bottom
				? $"Put {t} on the bottom of its owner's library"
				: $"Put {t} on top of its owner's library",
			PreventDamageAction p => p.PreventAll
				? "Prevent all damage to you and your creatures this turn"
				: $"Prevent the next {p.Amount} damage to you and your creatures this turn",
			TakeExtraTurnAction x => x.Turns == 1
				? "Take an extra turn after this one"
				: $"Take {x.Turns} extra turns after this one",
			GainControlAction => $"Gain control of {t}",
			ExileLinkedAction => $"Exile {t} until this leaves the battlefield",
			ReturnLinkedExileAction => "Return the exiled card to the battlefield",
			GrantEmblemAction g => $"You get an emblem: {g.Emblem?.Name ?? "an emblem"}",
			ReanimateManyAction r => r.MaxManaCost > 0
				? $"Return X creatures costing {r.MaxManaCost} or less from your graveyard to play"
				: "Return X creatures from your graveyard to the battlefield",
			CreateTokensPerPowerAction c =>
				$"Create a {c.CardTemplate?.Name ?? "token"} for each +1/+1 counter on this",
			AddCustomModifierAction m => DescribeCustomModifier(m, t),
			ConditionalAction c => DescribeConditional(c),
			ApplyChosenModeAction m => DescribeModes(m),
			GainPermanentManaAction g => $"Add {g.Amount} permanent mana",
			AttachEquipmentAction => $"Attach this to {t}",

			PipelineAction p => DescribePipeline(p),
			_ => null,
		};
	}

	private static string DescribeExhaust(ExhaustCreatureAction e, string target)
	{
		if (e.FreezeWhileSourceRemains)
			return $"Tap {target}; it doesn't untap while this remains";
		if (e.FreezeTurns > 0)
			return $"Tap {target}; it doesn't untap during its controller's next untap step";
		return $"Tap {target}";
	}

	private static string? DescribeCustomModifier(AddCustomModifierAction m, string target) =>
		m.Modifier switch
		{
			BecomesBaseCreatureComponent b =>
				$"{Capitalise(target)} loses all abilities and becomes a {b.Power}/{b.Toughness}"
					+ " until end of turn",
			_ => null,
		};

	/// <summary>
	/// An intervening-if clause. The condition is the whole point of these cards — Timely
	/// Reinforcements without it reads as an unconditional "gain 6 life".
	/// </summary>
	private static string? DescribeConditional(ConditionalAction c)
	{
		var inner =
			c.Action == null
				? null
				: DescribeEffect(
					new CardEffect
					{
						ActionTemplate = c.Action,
						TargetingStrategy = TargetingStrategy.NoTarget(),
					}
				);

		if (inner == null)
			return null;

		var condition = c.Condition?.Describe();
		return condition == null ? inner : $"If {LowerFirst(condition)}, {LowerFirst(inner)}";
	}

	/// <summary>"Choose one —" on a modal spell. The modes carry their own display names.</summary>
	private static string? DescribeModes(ApplyChosenModeAction m)
	{
		if (m.Modes.IsEmpty)
			return null;

		var described = m
			.Modes.Select(mode =>
				DescribeEffect(
					new CardEffect
					{
						ActionTemplate = mode,
						TargetingStrategy = TargetingStrategy.AllValid(
							TargetSpecification.CreatureControlledByYou()
						),
					}
				)
			)
			.Where(d => d != null)
			.ToList();

		return described.Count == 0 ? null : $"Choose one — {string.Join("; or ", described)}";
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

		/// Types a card-type spec narrows to, and types it excludes. Without these, Disenchant
		/// read "Destroy target permanent" while only ever hitting artifacts and enchantments —
		/// text that promises more than the card does.
		public CardType Types;
		public CardType ExcludedTypes;

		public int MaxManaCost;
		public bool Exhausted;
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
			case IsCardTypeSpecification t:
				f.Types |= t.Types;
				break;
			case IsNotCardTypeSpecification t:
				f.ExcludedTypes |= t.Types;
				break;
			case HasManaCostAtMostSpecification m:
				f.MaxManaCost = m.Maximum;
				break;
			case IsExhaustedSpecification:
				f.Exhausted = true;
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

		var noun =
			f.Subtype
			?? (f.Creature ? "creature" : null)
			?? TypeNoun(f.Types, f.ExcludedTypes)
			?? "permanent";

		var prefix = f.Other ? "other " : "";
		if (f.Exhausted)
			prefix += "tapped ";

		var suffix =
			f.Yours ? " you control"
			: f.Opponents ? " an opponent controls"
			: "";

		if (f.MaxManaCost > 0)
			suffix += $" costing {f.MaxManaCost} or less";

		return $"{prefix}{noun}{suffix}";
	}

	/// <summary>
	/// The noun a card-type filter should print — "artifact or enchantment", "nonland permanent".
	/// Null when the filter narrows nothing, so the caller falls back to "permanent".
	/// </summary>
	private static string? TypeNoun(CardType types, CardType excluded)
	{
		if (excluded != CardType.None)
			return excluded == CardType.Land ? "nonland permanent" : "permanent";

		if (types == CardType.None || types == CardType.AnyPermanent)
			return null;

		var names = new List<string>();
		foreach (
			var (flag, name) in new[]
			{
				(CardType.Creature, "creature"),
				(CardType.Artifact, "artifact"),
				(CardType.Enchantment, "enchantment"),
				(CardType.Planeswalker, "planeswalker"),
				(CardType.Land, "land"),
			}
		)
			if (types.HasFlag(flag))
				names.Add(name);

		return names.Count switch
		{
			0 => null,
			1 => names[0],
			_ => string.Join(" or ", names),
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

			// A modal spell is a pipeline of [choose a mode][apply it]. The choice step is
			// silent — the modes themselves carry the text — so only the apply step speaks.
			SelectModeAction => null,
			ApplyChosenModeAction m => LowerFirst(DescribeModes(m) ?? ""),
			SelectTopCardsToBottomAction s => $"scry {s.Amount}",
			MoveCardToBottomOfLibraryAction => "put the rest on the bottom",
			ExhaustCreatureAction e => e.FreezeTurns > 0 ? "tap it; it stays tapped" : "tap it",
			ReanimateManyAction => "return them to the battlefield",
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
