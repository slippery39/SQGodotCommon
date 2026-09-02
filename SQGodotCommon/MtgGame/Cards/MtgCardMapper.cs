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
			// The cost the player will actually pay, not the printed one. CostEngine is the single
			// place that answers this, and the cast actions already use it — showing card.ManaCost
			// here meant a Stormwing Entity discounted to 2 still read 5 in hand.
			var cost = state.ComputeEffectiveCost(card, playerId);
			canPlay = player.CurrentMana >= cost;
			manaCostDisplay = $"{cost}";
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
			return card.HasSubtype("Basic") ? "Basic Land\n(Exhaust: Add 1 mana)" : "Land";

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

			// Cover gets its own line with reminder text rather than joining the keyword list.
			// Taunt can go unexplained because a player arrives already knowing the word; Cover is
			// invented here, and a bare "Cover 2" among "Flying, Lifelink" tells a drafter nothing.
			// It carries a number, so it could not read as a bare keyword anyway.
			//
			// Reads live on the battlefield and printed in a pack, from the same field — a card
			// whose Cover has burned down correctly stops advertising it.
			if (creature.CoverTurns > 0)
				lines.Add(
					$"Cover {creature.CoverTurns} (can't be attacked for "
						+ $"{creature.CoverTurns} of your turns, or until it attacks)"
				);
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
		// Both fields matter, and dropping either mis-describes the card. Enigma Drake is a */4
		// counting instants and sorceries; the unconditional sentence made it a */* counting
		// everything — bigger AND tougher than the card it is.
		foreach (var gy in card.GetComponents<GraveyardCountComponent>())
		{
			var what = gy.Types == null ? "cards" : $"{DescribeCardTypes(gy.Types.Value)} cards";
			var stat = gy.AffectsToughness ? "Power and toughness are each" : "Power is";
			lines.Add($"{stat} equal to the number of {what} in your graveyard");
		}

		// Not one fixed sentence: the component gained a Subtype and per-creature amounts for the
		// Goblin lords, so the Crusader of Odric wording would have printed "power and toughness
		// are each equal to the number of creatures you control" on a card that is actually
		// "+2/+0 for each other Goblin you control" — confidently wrong text, which is worse than
		// blank text because nothing looks broken.
		foreach (var count in card.GetComponents<CreatureCountComponent>())
			lines.Add(DescribeCreatureCount(count));

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
			lines.Add("Convoke (costs 1 less per ready creature; those creatures exhaust)");

		if (card.HasComponent<XCostComponent>())
			lines.Add("X is all the mana you have left when you cast it");

		// The Hydras are printed 0/0 — without this line they read as a creature that dies the
		// moment it arrives, which is the exact opposite of what they do.
		foreach (var entersWith in card.GetComponents<EntersWithCountersComponent>())
			lines.Add(
				entersWith.FromXValue ? "Enters with X +1/+1 counters on it"
				: entersWith.Count == 1 ? "Enters with a +1/+1 counter on it"
				: $"Enters with {entersWith.Count} +1/+1 counters on it"
			);

		// Not reminder text: blue's counterspell traps really do fire from hand here, so this
		// clause protects against something the opponent can actually be holding. Rendering
		// nothing would hide the only reason Exquisite Firecraft beats a counterspell deck.
		foreach (var uncounterable in card.GetComponents<CannotBeCounteredComponent>())
			lines.Add(
				uncounterable.Condition == null
					? "This spell can't be countered"
					: $"This spell can't be countered if {LowerFirst(uncounterable.Condition.Describe())}"
			);

		// Conclave Mentor's main ability, and it rendered nothing — the card showed only its death
		// trigger, so the replacement that is the entire reason to draft it was invisible.
		foreach (var bonus in card.GetComponents<CounterBonusComponent>())
			lines.Add(
				$"If one or more +1/+1 counters would be put on a creature you control, "
					+ $"that many plus {bonus.Amount} are put on it instead"
			);

		// Platinum Angel's entire card. Without this it rendered as a seven-mana 4/4 flyer with a
		// blank text box — the most expensive vanilla creature in the cube, as far as a player
		// reading it could tell.
		// Scope matters and the unqualified sentence is now a lie: CannotLoseComponent covers the
		// LIFE clause only, and its controller can still deck. Printing "you can't lose the game"
		// tells a drafter they have an unbeatable permanent when milling still answers it.
		if (card.HasComponent<CannotLoseComponent>())
			lines.Add("You can't lose the game from damage (you can still deck)");

		// "You may play lands from the top of your library" (Radha). A permanent granting this and
		// saying nothing is worse than most blank text: the enabling card is on the battlefield
		// while the card it enables shows up somewhere the player is not looking.
		foreach (var top in card.GetComponents<PlayFromLibraryTopComponent>())
			lines.Add(
				top.Types == CardType.Land
					? "You may play lands from the top of your library"
					: "You may play cards from the top of your library"
			);

		// Two shapes, and they describe different cards. A Condition discounts THIS card; an
		// AppliesTo sits on a battlefield permanent and discounts OTHER cards you cast. Goreclaw
		// has only the latter, and without this branch it printed a dangling "Costs 2 less to
		// cast" with no subject and no clause — text that reads like the card discounts itself.
		foreach (var reduction in card.GetComponents<ConditionalCostReductionComponent>())
			lines.Add(
				// The noun comes back as "creature with power 4 or greater", so "spells you cast"
				// has to be spliced after the head noun rather than appended — otherwise it reads
				// "Creature with power 4 or greater spells you cast".
				reduction.AppliesTo != null
					? SpliceSpellsYouCast(
						DescribeSpecification(reduction.AppliesTo),
						reduction.Amount
					)
					: $"Costs {reduction.Amount} less to cast if "
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

		// What has been DONE to this permanent, as opposed to what it does. Everything above is
		// printed text; these lines are live state. Without them a creature under Sensory
		// Deprivation shows a smaller number in its badge and says nothing about why, and a
		// tapped or frozen creature is indistinguishable from a ready one.
		lines.AddRange(DescribeAppliedEffects(card, state));

		// Last, as on a real card. Flashback changes how a card is drafted more than almost
		// anything else in this set, so it must never be missing from the text.
		var flashback = card.GetComponent<FlashbackComponent>();
		if (flashback != null)
		{
			// The extra costs are not decoration — Despoiler of Souls exiles two creature cards
			// from your graveyard to come back, which is the only thing bounding the loop and
			// the whole reason to weigh it against a cheaper recursion. Omitting them printed a
			// strictly better card than the one being played.
			var extra = flashback
				.AdditionalCosts.Select(DescribeCost)
				.Where(c => c != null)
				.ToList();
			var extraStr = extra.Count > 0 ? $", {string.Join(", ", extra)}" : "";

			lines.Add(
				card.HasComponent<CreatureComponent>()
					? $"Cast from graveyard for {flashback.FlashbackManaCost}{extraStr}"
						+ " (stays on the battlefield)"
					: $"Flashback {flashback.FlashbackManaCost}{extraStr}"
			);
		}

		return string.Join("\n", MergeSharedClauses(lines));
	}

	/// <summary>
	/// Live effects stamped on a permanent in play: P/T modifiers with the card that applied them,
	/// what an attachment is currently attached to, and tapped/frozen state.
	///
	/// Only these lines change during a game, so they are the only ones that can explain a board
	/// the player did not expect — "my creature has no legal attacks" is answered by "Tapped",
	/// "Frozen" or the "Can't attack" keyword, none of which were shown anywhere before.
	///
	/// The self-describing modifiers are skipped: they compute their bonus from live state and
	/// already print their own rule ("Threshold — while 7+ cards…") higher up the card.
	/// </summary>
	private static IEnumerable<string> DescribeAppliedEffects(Card card, GameState state)
	{
		if (state == null || !state.HasObject(card.Id))
			yield break;

		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
		{
			if (
				modifier
				is ThresholdComponent
					or GraveyardCountComponent
					or LifeTotalComponent
					or CreatureCountComponent
					or LandsPlayedCountComponent
			)
				continue;

			var power = modifier.GetPowerBonus(state, card.Id);
			var toughness = modifier.GetToughnessBonus(state, card.Id);
			if (power == 0 && toughness == 0)
				continue;

			var source = CardName(state, modifier.SourceCardId);
			yield return $"{Signed(power)}/{Signed(toughness)}"
				+ (source == null ? "" : $" from {source}")
				+ DurationSuffix(modifier.Duration);
		}

		var attachedTo = card.GetComponent<EquipmentComponent>()?.EquippedToCardId ?? 0;
		if (CardName(state, attachedTo) is string host)
			yield return $"Attached to {host}";

		var creature = card.GetComponent<CreatureComponent>();
		if (creature == null)
			yield break;

		if (creature.FrozenBySourceId != 0 || creature.FrozenTurns > 0)
			yield return "Frozen — does not ready";
		else if (creature.IsExhausted)
			yield return "Exhausted";
	}

	private static string? CardName(GameState state, int cardId) =>
		cardId != 0 && state.HasObject(cardId) && state.GetObject(cardId) is Card card
			? card.Name
			: null;

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

	/// <summary>
	/// The three counter verbs. A Multiplier is not a number of counters, and printing "Put 0
	/// +1/+1 counters" for Primordial Hydra would describe a card that does nothing.
	/// </summary>
	private static string DescribeCounters(AddCountersAction c, string target)
	{
		if (c.Multiplier > 1)
			return $"Double the number of +1/+1 counters on {target}";

		if (!string.IsNullOrEmpty(c.AmountContextKey))
			return $"Put that many +1/+1 counters on {target}";

		if (c.Amount < 0)
		{
			var removed = Math.Abs(c.Amount);
			return removed == 1
				? $"Remove a +1/+1 counter from {target}"
				: $"Remove {removed} +1/+1 counters from {target}";
		}

		return c.Amount == 1
			? $"Put a +1/+1 counter on {target}"
			: $"Put {c.Amount} +1/+1 counters on {target}";
	}

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

		// Primordial Hydra counts COUNTERS, not graveyard cards. Without this branch it reads
		// "while 10+ cards are in your graveyard" — a clause about a completely different zone,
		// on a card that has nothing to do with the graveyard.
		// Each source is a clause about a different zone, and getting it wrong does not look wrong:
		// Blood-Cursed Knight read "Threshold — while 1+ cards are in your graveyard", which is a
		// condition that is true from turn two onwards and has nothing to do with the card.
		return t.CountSource switch
		{
			ThresholdSource.PlusOneCounters =>
				$"While this has {t.Minimum}+ +1/+1 counters on it, it {effect}",
			ThresholdSource.ControlledEnchantments => t.Minimum == 1
				? $"As long as you control an enchantment, this {effect}"
				: $"As long as you control {t.Minimum} enchantments, this {effect}",
			_ => $"Threshold — while {t.Minimum}+ cards are in your graveyard, this {effect}",
		};
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

		// "can't attack" is a clause, not something a creature "gets", so it is kept out of the
		// gets-list and joined on separately. Pacifism read "Enchanted creature gets can't attack".
		var cantAttack = false;

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
			// Trample is Rancor's whole reason to exist and was absent from this list, so the
			// Aura described itself as a bare +2/+0.
			if (boost.GrantsTrample)
				parts.Add("Trample");
			if (boost.GrantsReach)
				parts.Add("Reach");
			if (boost.GrantsDeathtouch)
				parts.Add("Deathtouch");
			// Haste, double strike and shroud were absent, and EquippedBoostComponent has always
			// carried all three. The symptom was not a missing word: with no stat bonus either,
			// the clause list came out empty and the card rendered "Equipped creature gets
			// nothing" — Fireshrieker, Swiftfoot Boots, Whispersilk Cloak and Ring of Valkas all
			// described themselves as doing literally nothing.
			if (boost.GrantsHaste)
				parts.Add("Haste");
			if (boost.GrantsDoubleStrike)
				parts.Add("Double Strike");
			if (boost.GrantsShroud)
				parts.Add("Shroud");
			if (boost.GrantsTaunt)
				parts.Add("Taunt");
			cantAttack = boost.PreventsAttacking;
		}

		var subject = attachment.IsAura ? "Enchanted creature" : "Equipped creature";
		var lead = attachment.IsAura ? "Aura — attaches when it enters. " : "";

		var clauses = new List<string>();
		if (parts.Count > 0)
			clauses.Add($"gets {string.Join(", ", parts)}");
		if (cantAttack)
			clauses.Add("can't attack");
		if (clauses.Count == 0)
			clauses.Add("gets nothing");

		return $"{lead}{subject} {string.Join(" and ", clauses)}";
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
			SacrificeAdditionalCost s when s.Filter is IsSourceCardSpecification =>
				"sacrifice this",
			// "Sacrifice a permanent" understates every sacrifice outlet in the black section:
			// they all require a CREATURE, which is a much narrower cost when your board is a
			// planeswalker and an Aura.
			SacrificeAdditionalCost s => s.Count == 1
				? $"sacrifice a {SacrificeNoun(s.Filter)}"
				: $"sacrifice {s.Count} {SacrificeNoun(s.Filter)}s",
			// The filter is the cost. "Discard a card" for Magmatic Insight and Molten Vortex
			// understates it badly in one direction and overstates it in the other: it reads as
			// though any card will do, when only a land will, and a hand with no land cannot pay
			// at all. FilterDescription is set by the builder alongside the filter itself.
			DiscardAdditionalCost d when !string.IsNullOrEmpty(d.FilterDescription) => d.Count == 1
				? $"discard a {d.FilterDescription} card"
				: $"discard {d.Count} {d.FilterDescription} cards",
			DiscardAdditionalCost d => d.Count == 1 ? "discard a card" : $"discard {d.Count} cards",
			LifeAdditionalCost l => $"pay {l.Amount} life",
			// Without this Dragon's Hoard read "Spend the Hoard (free): Draw a card" — a free,
			// unlimited draw engine as far as the card face was concerned. The cost is the card.
			RemoveCounterAdditionalCost r => r.Count == 1
				? $"remove a {r.Kind} counter"
				: $"remove {r.Count} {r.Kind} counters",
			// The SAME failure as the line above, one type later: without this, Walking Ballista
			// read "Fling Spore (free): Deal 1 damage to any target" — an unlimited free damage
			// engine on the card face, with the counter that is its only limiter invisible.
			RemovePlusOneCounterAdditionalCost p => p.Count == 1
				? "remove a +1/+1 counter"
				: $"remove {p.Count} +1/+1 counters",
			ExileFromGraveyardAdditionalCost x => x.Count == 1
				? "exile a card from your graveyard"
				: $"exile {x.Count} cards from your graveyard",
			_ => null,
		};

	private static string SacrificeNoun(TargetSpecification? filter) =>
		filter != null
		&& DescribeSpecification(filter).Contains("creature", StringComparison.Ordinal)
			? "creature"
			: "permanent";

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
			costParts.Add("exhaust");
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

		// MaxTriggers is the LIFETIME cap — it is what "if it isn't renowned" means. Without this
		// suffix Citadel Castellan reads as a creature that grows every time it connects, which is
		// a far better card than renown 2.
		var once = trigger.MaxTriggers == 1 ? " (once only)" : "";

		return $"{DescribeTriggerCondition(trigger.Condition)}: {effectStr}{once}";
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

		// Knight of the Ebon Legion's whole reason to exist. Without this it read "When
		// triggered", which tells a drafter nothing about the card's only real ability.
		if (condition is LifeLostThisTurnCondition lost)
			return lost.ControllerOnly
				? $"At end of turn, if you lost {lost.Minimum}+ life this turn"
				: $"At end of turn, if a player lost {lost.Minimum}+ life this turn";

		if (condition is AndTriggerCondition and)
			return
				string.Join(
					", and ",
					and.Conditions.Select(DescribeTriggerCondition)
						.Where(c => c != "When triggered")
				)
					is { Length: > 0 } joined
				? joined
				: "When triggered";

		if (condition is not EventTriggerCondition e)
			return "When triggered";

		// The subject of a player-scoped event is a PLAYER, so a controller filter reads
		// "your"/"an opponent's" rather than naming a creature.
		var isYours = e.Filter is IsControlledByYouSpecification;
		var isOpponents = e.Filter is IsControlledByOpponentSpecification;

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
			// Stab Wound drains on the ENCHANTED creature's controller's upkeep, not yours.
			// Printing "your upkeep" for it named the wrong player on the wrong turn.
			EventTypeNames.TurnStarted => isOpponents
				? "At the beginning of each opponent's upkeep"
				: "At the beginning of your upkeep",
			EventTypeNames.CombatDamageDealtToPlayer =>
				"Whenever this deals combat damage to a player",
			EventTypeNames.CreatureDestroyed => isSelf
				? "When this dies"
				: $"Whenever {FilterPhrase(e.Filter)} dies",
			// Blood Reckoning read "Whenever this attacks" — it is an enchantment that never
			// attacks, and the clause is about the OPPONENT's attackers.
			EventTypeNames.CreatureAttacked => isSelf ? "Whenever this attacks"
			: isOpponents ? "Whenever a creature attacks you"
			: $"Whenever {FilterPhrase(e.Filter)} attacks",
			EventTypeNames.PlayerLostLife => isYours
				? "Whenever you lose life"
				: "Whenever a player loses life",
			// The controller filter is load-bearing, exactly as it is for CreatureAttacked above.
			// Scab-Clan Berserker punishes the OPPONENT's spells and read "Whenever you cast a
			// spell", which describes the opposite card.
			// An UNFILTERED SpellCast means either player — Managorger Hydra grows on the
			// opponent's turn too, and "Whenever you cast a spell" describes half the card.
			// "an instant or sorcery", not "a spell". SpellCastEvent is emitted only by
			// CastSpellAction and CastFromGraveyardAction — CastCreatureAction and
			// CastPermanentAction do not fire it — so every prowess card in the cube was
			// over-promising, printing a trigger that fires on creatures and artifacts too.
			EventTypeNames.SpellCast => isOpponents
				? "Whenever an opponent casts an instant or sorcery"
			: e.Filter == null ? "Whenever a player casts an instant or sorcery"
			: "Whenever you cast an instant or sorcery",
			// Brash Taunter's entire card. Without this it read "When triggered", which says
			// nothing about the only reason to play it.
			EventTypeNames.CreatureDamaged => isSelf
				? "Whenever this is dealt damage"
				: $"Whenever {FilterPhrase(e.Filter)} is dealt damage",
			EventTypeNames.CardDiscarded => isSelf
				? "Madness — when you discard this"
				: "Whenever you discard a card",
			EventTypeNames.CardMilled => "Whenever a card of yours is milled",
			// Lorescale Coatl printed "When triggered" — the fallback, which says nothing at all
			// about the only reason to play the card.
			EventTypeNames.CardDrawn => "Whenever you draw a card",
			EventTypeNames.LandPlayed => "Landfall — whenever you play a land",
			EventTypeNames.TurnEnded => "At end of turn",

			// ===== Green section =====
			// Thragtusk and Rancor both hang their whole payoff off leaving play, and this read
			// "When triggered" — a card that says nothing about the only reason to play it.
			EventTypeNames.PermanentLeftBattlefield => isSelf
				? "When this leaves the battlefield"
				: $"Whenever {FilterPhrase(e.Filter)} leaves the battlefield",
			EventTypeNames.CountersAdded =>
				"Whenever one or more +1/+1 counters are put on another creature you control",
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

		// Every event this phrase serves is about a CREATURE — entering, dying, attacking, being
		// damaged — so a filter that describes itself in terms of "permanent" is describing the
		// wrong noun. Poison-Tip Archer read "whenever another permanent dies" and Corpse Knight
		// "whenever another permanent you control enters", both of which promise a trigger that
		// also fires on artifacts and enchantments.
		phrase = phrase.Replace("permanent", "creature", StringComparison.Ordinal);

		// "equipped creature" takes no article — there is exactly one, and "an equipped creature"
		// reads as though any equipped creature on the board would do.
		if (phrase == "equipped creature")
			return phrase;

		// These are all creature-scoped events, so a bare controller filter describes itself as
		// "permanent" only because nothing in the spec says "creature" — the EVENT does. Blood
		// Seeker and Massacre Wurm both read "whenever a permanent an opponent controls dies",
		// promising far more than they do.
		if (phrase.StartsWith("permanent ", StringComparison.Ordinal))
			phrase = "creature " + phrase["permanent ".Length..];

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
				return PluralNoun(phrase[..^suffix.Length]) + suffix;
		return PluralNoun(phrase);
	}

	/// <summary>
	/// Plural of a bare noun, with the irregulars the cube actually contains. Green's Elf lords
	/// read "Elfs get +1/+1" without this, and a card that misspells its own tribe looks like a
	/// bug in the card rather than in the text.
	/// </summary>
	private static string PluralNoun(string noun) =>
		noun switch
		{
			"Elf" => "Elves",
			"Dwarf" => "Dwarves",
			"Wolf" => "Wolves",
			_ => noun.EndsWith('s') ? noun : noun + "s",
		};

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
		// First strike, double strike, indestructible and exalted were all missing while
		// StaticGrantKeywordAbility has carried them since the white pass. Akroma's Memorial grants
		// four keywords and printed three.
		if (g.GrantsFirstStrike)
			keywords.Add("First Strike");
		if (g.GrantsDoubleStrike)
			keywords.Add("Double Strike");
		if (g.GrantsIndestructible)
			keywords.Add("Indestructible");
		if (g.GrantsExalted)
			keywords.Add("Exalted");
		return keywords.Count > 0 ? $"{Capitalise(who)} gain {string.Join(", ", keywords)}" : null;
	}

	private static string? DescribeEffect(CardEffect effect)
	{
		// The target phrase must be placed grammatically per action, not appended as a generic
		// suffix — a suffix is what produced "Put to target onto the battlefield".
		var t = DescribeTarget(effect);

		return effect.ActionTemplate switch
		{
			// A context-driven amount is not a number the card can print. Without this Brash
			// Taunter read "Deal 0 damage" and Volley Veteran "Deal 0 damage to it" — both of
			// which look like a finished card that simply does nothing.
			DealDamageAction d => !string.IsNullOrEmpty(d.AmountContextKey)
				? $"Deal that much damage to {t}"
				: $"Deal {d.Amount} damage to {t}",
			DestroyCreatureAction => $"Destroy {t}",
			ExileAction => $"Exile {t}",
			// Haunted Plate Mail's animate mode is half its card and rendered nothing at all.
			AnimateAction a => $"Until end of turn, this becomes a {a.Power}/{a.Toughness}"
				+ (string.IsNullOrEmpty(a.Subtype) ? "" : $" {a.Subtype}")
				+ " creature",
			// Dragon's Hoard accumulates gold counters and printed no clause that put any there,
			// so the ability that spends them looked like it could never be turned on.
			AddChargeCountersAction c => c.Amount >= 0
				? $"Put {(c.Amount == 1 ? "a" : c.Amount.ToString())} {c.Kind} counter"
					+ (c.Amount == 1 ? "" : "s")
					+ " on this"
				: $"Remove {Math.Abs(c.Amount)} {c.Kind} counters from this",
			// A context-driven bonus is not a number the card can print. Without this Primal Might
			// read "Target creature gets +0/+0" and Overwhelming Stampede the same — the Brash
			// Taunter "Deal 0 damage" bug in a second switch, and just as invisible.
			AddModifierAction m => !string.IsNullOrEmpty(m.PowerBonusContextKey)
			|| !string.IsNullOrEmpty(m.ToughnessBonusContextKey)
				? $"{Capitalise(t)} gets +X/+X{DurationSuffix(m.Duration)}"
				: $"{Capitalise(t)} gets {Signed(m.PowerBonus)}/"
					+ $"{Signed(m.ToughnessBonus)}{DurationSuffix(m.Duration)}",
			// A context-driven amount is not a number the card can print — Vilis draws "that
			// many", scaling with the life just lost, and printing "Draw a card" understated it
			// by most of the card.
			DrawCardsAction d => !string.IsNullOrEmpty(d.AmountContextKey) ? "Draw that many cards"
			: d.Amount == 1 ? "Draw a card"
			: $"Draw {d.Amount} cards",
			// Context-driven amount, fourth instance of the "Deal 0 damage" class. Dwynen scales
			// with your Elf count and printed "Gain 0 life" — a card that appears to do nothing.
			GainLifeAction g => !string.IsNullOrEmpty(g.AmountContextKey)
				? "Gain that much life"
				: $"Gain {g.Amount} life",
			// The target was ignored entirely, so every card that drains someone ELSE printed
			// "Lose N life" — Blood Reckoning and Indulgent Tormentor both read as though they
			// hurt their own controller, which is the opposite of what they do.
			LoseLifeAction l => l.AmountContextKey == ContextKeys.RevealedCardManaCost
				? "Lose life equal to its mana value"
			// **The context-driven-amount trap, and `GainLifeAction` one line above already guards
			// it.** Only the reveal key was special-cased, so any OTHER context key fell through to
			// the literal `Amount`, which is 0 — Sanguine Bond printed "Your opponent loses 0 life",
			// a card that reads as doing nothing while being half of a two-card kill. The gain half
			// printed "that much life" correctly, which is what made the pair's faces disagree.
			: !string.IsNullOrEmpty(l.AmountContextKey)
				? TargetsSelf(effect) ? "Lose that much life"
					: $"{Capitalise(t)} loses that much life"
			: TargetsSelf(effect) ? $"Lose {l.Amount} life"
			: $"{Capitalise(t)} loses {l.Amount} life",
			CreateCardAction c => c.Count == 1
				? $"Create {Article(c.CardTemplate.Name)} {c.CardTemplate.Name} token"
				: $"Create {c.Count} {c.CardTemplate.Name} tokens",
			DiscardCardsAction => "Discard a card",
			MoveCardToHandAction => $"Return {t} to your hand",
			ReturnToHandAction => $"Return {t} to your hand",
			// Elvish Archdruid scales with your Elf count and printed "Add 0 mana".
			AddTemporaryManaAction m => !string.IsNullOrEmpty(m.AmountContextKey)
				? "Add that much mana"
				: $"Add {m.Amount} mana",
			MillAction m => DescribeMill(m, effect),
			DrainLifeAction d => $"Target opponent loses {d.Amount} life and you gain {d.Amount}",
			DiscardRandomCardAction => "Target opponent discards a card at random",
			GrantKeywordAction g => DescribeGrantKeyword(g, t),
			GiveFlashbackAction => "An instant or sorcery in your graveyard gains Flashback",
			PutIntoBattlefieldAction => $"Put {t} onto the battlefield",
			TransformAction => "Transform this",
			// "This" is the source creature on an ETB fight, but on a SPELL the source is the
			// spell itself and FightAction falls back to your strongest creature — so the text
			// has to say which creature is actually swinging.
			FightAction f => f.OneSided
				? $"Your strongest creature deals damage equal to its power to {t}"
				: $"Your strongest creature fights {t}",
			LookAtTopCardsAction l => $"Look at the top {l.Amount} cards, put one in your hand",

			// ===== Green section =====
			// +1/+1 counters. Every one of these renders blank without a case, and the doubling
			// clause is the entire reason Primordial Hydra is worth a card.
			AddCountersAction c => DescribeCounters(c, t),
			CountGreatestPowerAction => "Find the greatest power among creatures you control",

			// ===== Core Set Cube =====
			// Every one of these left its card rendering completely blank before it was added.
			ExileTopCardPlayableAction =>
				"Exile the top card of your library. You may play it this turn",
			DestroyPermanentAction => $"Destroy {t}",
			ExhaustCreatureAction e => DescribeExhaust(e, t),
			// "Ready" is the engine's word for untapping — see DescribeExhaust's "does not ready
			// on its controller's next turn". Never print "untap": there is no tapping here.
			UnexhaustCreatureAction => $"Ready {t}",
			CreateTokenCopyAction c => c.Count == 1
				? $"Create a token that's a copy of {t}"
				: $"Create {c.Count} tokens that are copies of {t}",
			MoveCardToTopOfLibraryAction => $"Put {t} on top of its owner's library",
			MoveCardToBottomOfLibraryAction => $"Put {t} on the bottom of its owner's library",
			PutOnLibraryAction p => p.Bottom
				? $"Put {t} on the bottom of its owner's library"
				: $"Put {t} on top of its owner's library",
			PreventDamageAction p => DescribePrevention(p, effect, t),
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
			GainPermanentManaAction g => g.Amount < 0
				? $"Lose {-g.Amount} permanent mana"
				: $"Add {g.Amount} permanent mana",
			AttachEquipmentAction => $"Attach this to {t}",

			// ===== Core Set Cube: black =====
			// "You lose the game" is Demonic Pact's whole identity and rendered as nothing at
			// all — the card offered three good modes and silently hid the clock. Sorin's -3
			// vanished the same way.
			// A NoTarget strategy with TargetContextKey = CastingPlayerId means "you", but
			// DescribeTarget has no target list to read and falls back to the generic phrase —
			// which turned Demonic Pact's fourth mode into "each creature you control loses the
			// game". The subject has to come from the context key, not the strategy.
			SetLifeTotalAction s => SelfTargeted(s.TargetContextKey)
				? (s.Amount == 0 ? "You lose the game" : $"Your life total becomes {s.Amount}")
				: (
					s.Amount == 0
						? $"{Capitalise(t)} loses the game"
						: $"{Capitalise(t)}'s life total becomes {s.Amount}"
				),

			PipelineAction p => DescribePipeline(p),
			_ => null,
		};
	}

	/// <summary>
	/// "Creature spells you cast with power 4 or greater cost 2 less" — the qualifier goes after
	/// "spells you cast", not before it, which is how the real card is worded.
	/// </summary>
	private static string SpliceSpellsYouCast(string noun, int amount)
	{
		var head = noun;
		var qualifier = "";

		var split = noun.IndexOf(" with ", StringComparison.Ordinal);
		if (split >= 0)
		{
			head = noun[..split];
			qualifier = noun[split..];
		}

		return $"{Capitalise(head)} spells you cast{qualifier} cost {amount} less";
	}

	private static bool SelfTargeted(string targetContextKey) =>
		targetContextKey == ContextKeys.CastingPlayerId;

	/// <summary>
	/// Whether an effect lands on its own controller — either it targets the casting player, or
	/// it takes no target at all and therefore falls back to them.
	/// </summary>
	private static bool TargetsSelf(CardEffect effect) =>
		effect.TargetingStrategy.SelectionMode
			is TargetSelectionMode.CastingPlayer
				or TargetSelectionMode.None;

	/// <summary>
	/// Renders CreatureCountComponent in either of its two shapes: the */* templating it was built
	/// for (Crusader of Odric, equal per creature, counting itself) and the per-creature bonus the
	/// Goblin lords use (+2/+0 for each OTHER Goblin).
	/// </summary>
	private static string DescribeCreatureCount(CreatureCountComponent count)
	{
		var what = string.IsNullOrEmpty(count.Subtype) ? "creature" : count.Subtype;
		var others = count.CountsSelf ? "" : "other ";
		var scope = $"{others}{what}s you control";

		// The */* case: the card IS its count, rather than getting a bonus on top of a base.
		if (count.CountsSelf && count.PowerPerCreature == 1 && count.ToughnessPerCreature == 1)
			return $"Power and toughness are each equal to the number of {scope}";

		return $"Gets +{count.PowerPerCreature}/+{count.ToughnessPerCreature} for each {others}"
			+ $"{what} you control";
	}

	private static string DescribeExhaust(ExhaustCreatureAction e, string target)
	{
		if (e.FreezeWhileSourceRemains)
			return $"Exhaust {target}; it stays exhausted while this remains";
		if (e.FreezeTurns > 0)
			return $"Exhaust {target}; it does not ready on its controller's next turn";
		return $"Exhaust {target}";
	}

	private static string? DescribeCustomModifier(AddCustomModifierAction m, string target) =>
		m.Modifier switch
		{
			// The new body's own keywords have to print. Skinshifter's three modes are chosen
			// between, and with flying and trample invisible two of them read as strictly worse
			// copies of the third.
			BecomesBaseCreatureComponent b =>
				$"{Capitalise(target)} loses all abilities and becomes a {b.Power}/{b.Toughness}"
					+ BecomesKeywords(b)
					+ " until end of turn",
			// Radha's {4}{R}{G}. The null fallback blanks the WHOLE ability, not just the clause —
			// her activated ability simply did not appear on the card.
			LandsPlayedCountComponent => $"{Capitalise(target)} gets +X/+X until end of turn, "
				+ "where X is the number of lands you control",
			_ => null,
		};

	private static string BecomesKeywords(BecomesBaseCreatureComponent b)
	{
		var keywords = new List<string>();
		if (b.GrantsFlying)
			keywords.Add("Flying");
		if (b.GrantsTrample)
			keywords.Add("Trample");
		if (b.GrantsReach)
			keywords.Add("Reach");

		return keywords.Count == 0 ? "" : $" with {string.Join(" and ", keywords)}";
	}

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

		// EACH MODE'S OWN TARGETING, not a placeholder. This used to hardcode
		// AllValid(CreatureControlledByYou) for every mode, so a targeted mode described a
		// completely different card: Return to Nature's "destroy target artifact" rendered as
		// "Destroy each creature you control" — text that reads like a one-sided board wipe on
		// what is actually a Naturalize.
		var described = m
			.Modes.Select(
				(mode, i) =>
					DescribeEffect(
						new CardEffect
						{
							ActionTemplate = mode,
							TargetingStrategy =
								(i < m.ModeTargeting.Count ? m.ModeTargeting[i] : null)
								?? TargetingStrategy.AllValid(
									TargetSpecification.CreatureControlledByYou()
								),
						}
					)
			)
			.Where(d => d != null)
			.ToList();

		return described.Count == 0 ? null : $"Choose one — {string.Join("; or ", described)}";
	}

	private static string StripYouControl(string noun) =>
		noun.EndsWith(" you control", StringComparison.Ordinal)
			? noun[..^" you control".Length]
			: noun;

	/// "a Beast" but "an Elf Warrior" — token names are card names, so the article has to be
	/// chosen rather than hardcoded.
	private static string Article(string noun) =>
		!string.IsNullOrEmpty(noun) && "AEIOU".Contains(char.ToUpperInvariant(noun[0]))
			? "an"
			: "a";

	private static string Signed(int n) => n >= 0 ? $"+{n}" : n.ToString();

	/// Permanent grants print no suffix — that is the creature's own text, not a temporary buff.
	/// UntilYourNextTurn must say so: it is a strictly longer shield than end-of-turn and it is
	/// the whole reason a one-mana trick survives the opponent's attack step.
	private static string DurationSuffix(ModifierDuration d) =>
		d switch
		{
			ModifierDuration.UntilEndOfTurn => " until end of turn",
			ModifierDuration.UntilYourNextTurn => " until your next turn",
			_ => "",
		};

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

		// "equipped creature" is already singular and already unambiguous — there is exactly one.
		// AllValid is how an attachment addresses its wearer (nothing can inject a mass target
		// into a trigger), so the mass phrasing leaks out as "each equipped creature", which reads
		// as a board-wide pump. The five Rings all printed that.
		if (noun == "equipped creature")
			return noun;

		return strategy.SelectionMode switch
		{
			TargetSelectionMode.AllValid => $"each {noun}",
			// There is exactly one opponent, so "a random opponent" describes a choice that is not
			// being made. Every other random target really is a pick among several.
			TargetSelectionMode.Random => noun == "opponent" ? "your opponent" : $"a random {noun}",
			// The engine picks this one, not the player, so calling it "target" would promise a
			// choice the card never offers — and falling through to the default did exactly that.
			// "your strongest" already says whose, so the spec's own " you control" is stripped
			// rather than yielding "your strongest creature you control".
			TargetSelectionMode.Best => $"your strongest {StripYouControl(noun)}",
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

		/// "with power N or greater" (Goreclaw). 0 means unrestricted.
		public int MinPower;

		/// "equipped creature" — the wearer of the attachment running this effect. Without it the
		/// five Rings printed "put a +1/+1 counter on each permanent" (a board-wide pump) and
		/// Sword of the Animist printed "whenever a creature attacks" (either player's, any
		/// creature). Both read as far stronger cards than they are.
		public bool EquippedBySource;

		public int MaxManaCost;

		/// "with mana value N or greater" (Dragon's Hoard). Dropping it is the understate-the-
		/// restriction bug: the Hoard read "whenever a creature you control enters", triggering on
		/// everything, when it only wants the expensive ones.
		public int MinManaCost;
		public bool Exhausted;

		/// Black-section narrowings. Each one is the entire restriction on its card: Royal
		/// Assassin without "that attacked this turn" reads as unconditional removal, and
		/// Gilt-Leaf Winnower without the P/T clause reads as a five-mana Murder on a body.
		public bool Attacked;
		public bool UnequalPowerToughness;
		public bool CreatureInAnyGraveyard;
		public bool Flying;
		public bool WithoutFlying;
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
			case HasManaCostAtLeastSpecification ml:
				f.MinManaCost = ml.Minimum;
				break;
			case IsExhaustedSpecification:
				f.Exhausted = true;
				break;
			case HasAttackedThisTurnSpecification:
				f.Attacked = true;
				break;
			case DifferentPowerAndToughnessSpecification:
				f.UnequalPowerToughness = true;
				break;
			case IsCreatureInAnyGraveyardSpecification:
				f.CreatureInAnyGraveyard = true;
				break;
			case HasFlyingSpecification:
				f.Flying = true;
				break;
			// Earthquake's whole card is the flying EXEMPTION, and without this case the Not
			// wrapper was walked straight past: it read "deal X damage to each creature", which
			// is a different and much worse card. Matched narrowly rather than by inverting the
			// walker, because negation does not distribute over the other facts sensibly —
			// "not (creature you control)" is not "creature you don't control".
			case NotSpecification { Inner: HasFlyingSpecification }:
				f.WithoutFlying = true;
				break;
			// Goreclaw's discount applies only to power-4-and-up creatures, and without this the
			// card read "Creature spells you cast cost 2 less" — a strictly better card.
			case PowerAtLeastSpecification p:
				f.MinPower = p.Minimum;
				break;
			case IsEquippedBySourceSpecification:
				f.EquippedBySource = true;
				break;
			// Garruk's +1 destroys a PLANESWALKER and read "destroy target permanent" — text that
			// promises unconditional removal of anything on the board.
			case IsPlaneswalkerSpecification:
				f.Types |= CardType.Planeswalker;
				break;
		}
	}

	public static string DescribeSpecification(TargetSpecification? spec)
	{
		if (spec == null)
			return "it";

		var f = new SpecFacts();
		Collect(spec, f);

		// Checked first: it fully determines the noun, and every other narrowing is irrelevant
		// once the target is "whatever this attachment is on".
		if (f.EquippedBySource)
			return "equipped creature";

		// The graveyard and hand specs imply the zone, but they can still be narrowed by a
		// subtype — and dropping that narrowing is the worst kind of text bug, because the
		// card then promises MORE than it does. Zombie Apocalypse read as "each creature card
		// in your graveyard" while only ever returning Zombies.
		if (f.CreatureInAnyGraveyard)
			return "creature card from a graveyard";
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

		// An OR of "artifact or enchantment" with "creature with flying" (Vivien Reid's -3) walks
		// into facts that say BOTH, and taking creature first dropped the artifact and
		// enchantment halves entirely — text that promises less than the card does. The walker
		// deliberately flattens And and Or the same way, so this reassembles the union here
		// rather than teaching it to distinguish them.
		if (f.Creature && f.Flying && TypeNoun(f.Types, f.ExcludedTypes) is { } alsoTypes)
			return $"{alsoTypes} or creature with flying";

		var noun =
			f.Subtype
			?? (f.Creature ? "creature" : null)
			?? TypeNoun(f.Types, f.ExcludedTypes)
			?? "permanent";

		var prefix = f.Other ? "other " : "";
		if (f.Exhausted)
			prefix += "exhausted ";
		if (f.Flying)
			prefix += "flying ";

		var suffix =
			f.Yours ? " you control"
			: f.Opponents ? " an opponent controls"
			: "";

		if (f.MaxManaCost > 0)
			suffix += $" costing {f.MaxManaCost} or less";
		if (f.MinManaCost > 0)
			suffix += $" costing {f.MinManaCost} or more";

		if (f.MinPower > 0)
			suffix += $" with power {f.MinPower} or greater";

		if (f.Attacked)
			suffix += " that attacked this turn";

		if (f.UnequalPowerToughness)
			suffix += " with different power and toughness";

		if (f.WithoutFlying)
			suffix += " without flying";

		return $"{prefix}{noun}{suffix}";
	}

	/// <summary>
	/// The noun a card-type filter should print — "artifact or enchantment", "nonland permanent".
	/// Null when the filter narrows nothing, so the caller falls back to "permanent".
	/// </summary>
	/// <summary>
	/// Card types as they appear mid-sentence — "instant and sorcery", "creature". Distinct from
	/// TypeNoun, which answers "what noun is being targeted"; this answers "which kinds are being
	/// counted", so it joins with "and" and knows about instants and sorceries, which TypeNoun's
	/// permanent-shaped list does not.
	/// </summary>
	private static string DescribeCardTypes(CardType types)
	{
		if (types == CardType.AnySpell)
			return "instant and sorcery";

		var names = new List<string>();
		foreach (
			var (flag, name) in new[]
			{
				(CardType.Creature, "creature"),
				(CardType.Artifact, "artifact"),
				(CardType.Enchantment, "enchantment"),
				(CardType.Planeswalker, "planeswalker"),
				(CardType.Instant, "instant"),
				(CardType.Sorcery, "sorcery"),
				(CardType.Land, "land"),
			}
		)
			if (types.HasFlag(flag))
				names.Add(name);

		return names.Count == 0 ? "" : string.Join(" and ", names);
	}

	private static string? TypeNoun(CardType types, CardType excluded)
	{
		// "noncreature permanent" is Bramblecrush's whole restriction, and collapsing every
		// exclusion but Land to a bare "permanent" promised removal for anything — text that says
		// the card does strictly more than it does.
		if (excluded != CardType.None)
			return excluded switch
			{
				CardType.Land => "nonland permanent",
				CardType.Creature => "noncreature permanent",
				_ => "permanent",
			};

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

	/// <summary>
	/// Prevention has two shapes and one sentence cannot cover both. Stamped on the caster
	/// (Safe Passage, Harm's Way) it shields them and their whole board; stamped on a chosen
	/// creature (Gods Willing) it shields that creature alone. Printing the player wording on
	/// the creature version would be confidently wrong text, which is worse than blank.
	///
	/// The duration was also wrong for every prevention card in the set: it read "this turn"
	/// while PreventDamageAction has defaulted to UntilYourNextTurn since that default is what
	/// made prevention work here at all.
	/// </summary>
	private static string DescribePrevention(
		PreventDamageAction p,
		CardEffect effect,
		string target
	)
	{
		var amount = p.PreventAll ? "all damage" : $"the next {p.Amount} damage";
		var window =
			p.Duration == ModifierDuration.UntilYourNextTurn ? "until your next turn" : "this turn";

		var who =
			effect.TargetingStrategy.SelectionMode == TargetSelectionMode.CastingPlayer
				? "you and your creatures"
				: target;

		return $"Prevent {amount} to {who} {window}";
	}

	private static string DescribeGrantKeyword(GrantKeywordAction g, string target)
	{
		var keywords = GrantedKeywordList(g);

		if (keywords.Length == 0)
			return "Grants nothing";

		return $"{Capitalise(target)} gains {keywords}" + DurationSuffix(g.Duration);
	}

	/// <summary>
	/// The keyword names a GrantKeywordAction hands out, comma-joined. Extracted so the
	/// standalone and pipeline-step renderings cannot drift — thirteen fields duplicated across
	/// two switches is how Xathrid Slyblade came to print half its ability.
	/// </summary>
	private static string GrantedKeywordList(GrantKeywordAction g)
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
		// GrantKeywordAction has grown six more keywords than this list did. Xathrid Slyblade
		// grants first strike AND deathtouch and printed only the deathtouch — half its ability.
		// Keep this in step with GrantKeywordAction's fields.
		if (g.GrantsFirstStrike)
			keywords.Add("First strike");
		if (g.GrantsDoubleStrike)
			keywords.Add("Double strike");
		if (g.GrantsIndestructible)
			keywords.Add("Indestructible");
		if (g.GrantsShroud)
			keywords.Add("Shroud");
		if (g.GrantsHexproof)
			keywords.Add("Hexproof");
		if (g.GrantsExalted)
			keywords.Add("Exalted");

		return string.Join(", ", keywords);
	}

	/// <summary>
	/// A zone selection is only meaningful with WHOSE zone and WHICH card — "choose a card
	/// from a graveyard" leaves a drafter guessing on both counts.
	/// </summary>
	private static string DescribeZoneSelection(SelectCardFromZoneAction s)
	{
		var whose = s.TargetOpponent ? "an opponent's" : "your";
		var zone = s.Zone.ToString().ToLowerInvariant();

		// Any other Filter has to be described too, not silently ignored. Woodland Bellower reads
		// "a nonlegendary creature card with mana value 3 or less"; without this it rendered as
		// "choose a card from your library", which describes an unrestricted tutor.
		var what =
			!string.IsNullOrEmpty(s.Subtype) ? $"a {s.Subtype}"
			: s.Filter is IsCreatureInOwnGraveyardSpecification ? "a creature card"
			: s.Filter is IsInstantOrSorceryInOwnGraveyardSpecification ? "an instant or sorcery"
			: s.Filter != null ? $"a {DescribeSpecification(s.Filter)}"
			: "a card";

		// The mana bound is the restriction, not a detail. Evolutionary Leap replaces a dead
		// creature with a strictly CHEAPER one; without this clause it rendered as an
		// unrestricted tutor — confidently wrong text, which is worse than blank.
		var bound = string.IsNullOrEmpty(s.MaxManaCostExclusiveFromCardContextKey)
			? ""
			: " costing less than it";

		return $"choose {what}{bound} from {whose} {zone}";
	}

	private static string? DescribePipeline(PipelineAction pipeline)
	{
		// A symmetric edict is four steps that describe one sentence. Rendered step by step it
		// came out as "take the opponent's cheapest creature, destroy it, take your cheapest
		// creature, destroy it" — accurate, four times as long as the card, and on three cards.
		// Verbosity is a bug here: the rules box has a hard line budget.
		if (DescribeSymmetricEdict(pipeline) is { } edict)
			return edict;

		var parts = pipeline.Steps.Select(DescribeStep).Where(d => d != null).ToList();
		return parts.Count > 0 ? CombineParts(parts, ", ") : null;
	}

	/// <summary>
	/// Matches the exact shape WithSymmetricEdict builds — select/destroy for the opponent, then
	/// select/destroy for you — and nothing else. A looser match would silently reword unrelated
	/// pipelines.
	/// </summary>
	private static string? DescribeSymmetricEdict(PipelineAction pipeline)
	{
		if (pipeline.Steps.Count != 4)
			return null;

		var selects = pipeline
			.Steps.OfType<SelectCreatureFromBattlefieldByManaCostAction>()
			.ToList();
		if (selects.Count != 2 || pipeline.Steps.OfType<DestroyCreatureAction>().Count() != 2)
			return null;
		if (!selects.Any(s => s.TargetOpponent) || !selects.Any(s => !s.TargetOpponent))
			return null;

		var exclude = selects[0].ExcludeSubtype;
		return string.IsNullOrEmpty(exclude)
			? "Each player sacrifices a creature"
			: $"Each player sacrifices a non-{exclude} creature";
	}

	private static string? DescribeStep(GameAction action) =>
		action switch
		{
			DrawCardsAction d => !string.IsNullOrEmpty(d.AmountContextKey) ? "draw that many cards"
			: d.Amount == 1 ? "draw a card"
			: $"draw {d.Amount} cards",
			LoseLifeAction l => l.AmountContextKey == ContextKeys.RevealedCardManaCost
				? "lose life equal to its mana value"
			: l.Amount > 0 ? $"lose {l.Amount} life"
			: "lose life",
			// The context-driven-amount trap AGAIN, in the pipeline switch this time. Fixing only
			// the standalone arm left Dwynen printing "gain 0 life" and Elvish Archdruid "add 0
			// mana" — both scale with your Elf count, and both read as cards that do nothing.
			GainLifeAction g => !string.IsNullOrEmpty(g.AmountContextKey)
				? "gain that much life"
				: $"gain {g.Amount} life",
			AddTemporaryManaAction m => !string.IsNullOrEmpty(m.AmountContextKey)
				? "add that much mana"
			: string.IsNullOrEmpty(m.BonusAmountContextKey) ? $"add {m.Amount} mana"
			: $"add {m.Amount}+X mana",
			RevealTopCardAction => "reveal top card",
			LookAtTopCardsAction l => $"look at top {l.Amount} cards, put one in hand",
			SelectCardFromLibraryAction s => string.IsNullOrEmpty(s.Subtype)
				? "search your library for a card"
				: $"search your library for {Article(s.Subtype)} {s.Subtype}",
			// Says "choose" because the player actually does, unlike the auto-picking sibling
			// above. The distinction is the whole reason the card is worth its life cost.
			SearchLibraryAction s => string.IsNullOrEmpty(s.Subtype)
				? "search your library and choose a card"
				: $"search your library and choose {Article(s.Subtype)} {s.Subtype}",
			PutIntoBattlefieldAction => "put it into play",
			SelectCardsFromHandAction => "choose a card",
			DiscardCardsAction => "discard it",
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
				: $"count {PluralNoun(s.Subtype)} in your {s.Zone.ToString().ToLowerInvariant()}",
			SelectCardFromZoneAction s => DescribeZoneSelection(s),

			// A modal spell is a pipeline of [choose a mode][apply it]. The choice step is
			// silent — the modes themselves carry the text — so only the apply step speaks.
			SelectModeAction => null,
			ApplyChosenModeAction m => LowerFirst(DescribeModes(m) ?? ""),
			SelectTopCardsToBottomAction s => $"scry {s.Amount}",
			MoveCardToBottomOfLibraryAction => "put the rest on the bottom",
			ExhaustCreatureAction e => e.FreezeTurns > 0
				? "exhaust it; it stays exhausted"
				: "exhaust it",
			UnexhaustCreatureAction => "ready it",
			CreateTokenCopyAction => "create a token copy of it",
			ReanimateManyAction => "return them to the battlefield",
			// The trigger-safe verbs are pipelines that pick a target themselves, so their
			// steps have to read as one sentence: "the opponent's best creature, destroy it".
			SelectCreatureFromBattlefieldByManaCostAction s => s.TargetOpponent
				? $"take the opponent's {(s.SelectLowest ? "cheapest" : "best")} creature"
				: $"take your {(s.SelectLowest ? "cheapest" : "best")} creature",
			DestroyCreatureAction => "destroy it",
			ExileAction => "exile it",
			// Same context-driven-amount trap as the standalone case above: Volley Veteran's
			// damage scales with its Goblin count and printed "deal 0 damage to it".
			DealDamageAction d => !string.IsNullOrEmpty(d.AmountContextKey)
				? "deal that much damage to it"
				: $"deal {d.Amount} damage to it",
			FightAction f => f.OneSided ? "deal damage equal to its power to it" : "fight it",
			// Same context-driven trap again — Overwhelming Stampede's buff scales with the
			// greatest power you control and printed "give it +0/+0".
			AddModifierAction m => !string.IsNullOrEmpty(m.PowerBonusContextKey)
			|| !string.IsNullOrEmpty(m.ToughnessBonusContextKey)
				? "give them +X/+X"
				: $"give it {Signed(m.PowerBonus)}/{Signed(m.ToughnessBonus)}",

			// ===== Core Set Cube: green =====
			AddCountersAction c => LowerFirst(DescribeCounters(c, "it")),
			CountGreatestPowerAction => "find the greatest power among your creatures",
			// DescribeGrantKeyword builds "{target} gains X", which is ungrammatical for a plural
			// subject — Overwhelming Stampede read "them gains Trample".
			GrantKeywordAction g => GrantedKeywordList(g) is { Length: > 0 } kw
				? $"they gain {kw}"
				: null,

			// ===== Core Set Cube: black =====
			// Every one of these left part or all of its card unrendered before it was added.
			// Kitesail Freebooter's whole ETB was invisible; Demonic Pact's fourth mode — the one
			// that ends the game — simply did not appear among its choices.
			SetLifeTotalAction s => s.Amount == 0
				? "you lose the game"
				: $"their life total becomes {s.Amount}",
			SelectCardFromHandByManaCostAction s => s.TargetOpponent
				? $"look at their hand and take their {(s.SelectLowest ? "cheapest" : "best")} card"
				: $"take your {(s.SelectLowest ? "cheapest" : "best")} card",
			ExileLinkedAction => "exile it until this leaves the battlefield",
			ReturnLinkedExileAction => "return the exiled card",
			PutOnLibraryAction p => p.Bottom
				? "put it on the bottom of your library"
				: "put it on top of your library",
			GainPermanentManaAction g => g.Amount < 0
				? $"lose {-g.Amount} permanent mana"
				: $"add {g.Amount} permanent mana",
			_ => null,
		};
}
