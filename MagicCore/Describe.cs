using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore;

/// <summary>Plain-text names for actions, choices and cards — shared by every front end and log.</summary>
public static class Describe
{
	public static string Action(GameState s, GameAction action) =>
		action switch
		{
			PassPriorityAction => "Pass",
			KeepHandAction => "Keep hand",
			MulliganAction => "Mulligan",
			PlayLandAction p =>
				$"Play {(p.BackFace ? s.GetCard(p.CardId).ModalBackFace()?.Name : null) ?? Name(s, p.CardId)}"
					+ (s.IsIn(p.CardId, p.PlayerId, ZoneType.Exile) ? " from exile" : ""),
			CastSpellAction c => (
				c.Adventure
				&& s.HasObject(c.CardId)
				&& s.GetObject(c.CardId) is Card { } adventurer
				&& adventurer.GetComponent<AdventureComponent>() is { } adventure
					? $"Cast {adventure.Face.Name} (Adventure)"
					: $"Cast {Name(s, c.CardId)}"
			)
				+ (
					s.IsIn(c.CardId, c.PlayerId, ZoneType.Graveyard)
						? " from graveyard (flashback)"
						: ""
				)
				+ (
					s.GetObject(c.CardId) is Card { IsCopy: true } ? " (prepared copy)"
					: s.IsIn(c.CardId, c.PlayerId, ZoneType.Exile) ? " from exile"
					: ""
				)
				+ (c.X > 0 ? $" (X={c.X})" : "")
				// Baleful Mastery's {1}{B}, Snuff Out's life: told from the paid cast beside it (a resolution's free
				// cast is a choice's option, whose text a record's fingerprint hashes — left as it was)
				+ (
					!c.WithoutPayingManaCost || c.DuringResolution ? ""
					// Damn: "cast this spell for its overload cost"
					// … Springheart Nantuko: cast for its bestow cost
					: Rules.Playable.OwnAlternativeCost(s, s.GetCard(c.CardId), c.PlayerId)
						? s.GetCard(c.CardId).GetComponent<MayCastFromHandFree>() switch
						{
							{ Overload: true } => " (overload)",
							{ Enchant: not null } => " (bestow)",
							_ => " (not paying its mana cost)",
						}
					: " (not paying its mana cost)"
				)
				+ (c.Replicate > 0 ? $" (replicate ×{c.Replicate})" : "")
				+ (c.Delve > 0 ? $" (delve {c.Delve})" : "")
				+ (
					c.Kicked
						? $" ({s.GetCard(c.CardId).GetComponent<KickerComponent>()?.Word ?? "kicked"})"
						: ""
				)
				+ (
					c.Splice.IsDefaultOrEmpty
						? ""
						: $" (splicing {string.Join(", ", c.Splice.Select(id => Name(s, id)))})"
				)
				+ (c.Modes.Length > 0 ? $" ({ModeText(s, c)})" : "")
				+ Targets(s, c.Targets)
				+ (c.SacrificeId != 0 ? $", sacrificing {Name(s, c.SacrificeId)}" : "")
				+ (c.DiscardId != 0 ? $", discarding {Name(s, c.DiscardId)}" : "")
				+ (c.BeholdId != 0 ? $", beholding {Name(s, c.BeholdId)}" : "")
				+ (c.ReturnId != 0 ? $", returning {Name(s, c.ReturnId)}" : "")
				+ (c.ExileId != 0 ? $", exiling {Name(s, c.ExileId)}" : "")
				+ (c.LifeForPhyrexian > 0 ? $", paying {2 * c.LifeForPhyrexian} life" : ""),
			ActivateAbilityAction a => $"Activate {Name(s, a.CardId)}{WhichAbility(s, a)}"
				+ (s.IsIn(a.CardId, a.PlayerId, ZoneType.Hand) ? " from hand" : "")
				+ (s.IsIn(a.CardId, a.PlayerId, ZoneType.Graveyard) ? " from graveyard" : "")
				+ (a.Modes.Length > 0 ? $" ({ModeText(s, a)})" : "")
				+ (a.X > 0 ? $" (X = {a.X})" : "")
				+ Targets(s, a.Targets)
				+ (a.SacrificeId != 0 ? $", sacrificing {Name(s, a.SacrificeId)}" : "")
				+ (a.DiscardId != 0 ? $", discarding {Name(s, a.DiscardId)}" : "")
				+ (a.ExileId != 0 ? $", exiling {Name(s, a.ExileId)}" : "")
				+ (a.ReturnId != 0 ? $", returning {Name(s, a.ReturnId)}" : "")
				+ (a.LifeForPhyrexian > 0 ? $", paying {2 * a.LifeForPhyrexian} life" : "")
				+ (a.TapId != 0 ? $", tapping {Name(s, a.TapId)}" : "")
				+ (a.SecondTapId != 0 ? $" and {Name(s, a.SecondTapId)}" : "")
				+ (
					a.TapIds.IsDefaultOrEmpty
						? ""
						: $", tapping {string.Join(", ", a.TapIds.Select(id => Name(s, id)))}"
				),
			ActivateLoyaltyAction l =>
				$"{Name(s, l.CardId)} [{LoyaltyCost(s, l)}]{Targets(s, l.Targets)}",
			TapForManaAction { SacrificeId: not 0, NoTap: true } t =>
				$"{Name(s, t.CardId)}: sacrifice {Name(s, t.SacrificeId)} for {t.Color}",
			TapForManaAction { SacrificeId: not 0 } t =>
				$"Tap {Name(s, t.CardId)}, sacrificing {Name(s, t.SacrificeId)}, for {t.Color}{(t.OfTheOther > 0 ? $" ({t.OfTheOther} of its other colour)" : "")}",
			TapForManaAction { NoTap: true } t => $"{NoTapCosts(s, t)} for {t.Color}",
			TapForManaAction { PaysItsCost: true } t =>
				$"Pay for and {CashedInBy(s, t.CardId)} {Name(s, t.CardId)} for {t.Color}",
			TapForManaAction t => $"Tap {Name(s, t.CardId)} for {t.Color}",
			TurnFaceUpAction f => TurnFaceUpAction.CardOf(s, f.PlayerId, f.CardId) is { } card
				? $"Turn face up: {card.Name} ({card.ManaCost})"
				: "Turn face up",
			_ => action.GetType().Name,
		};

	/// <summary>Arena of Glory's cashed-in mana ability exerts it, Loot's exhausts it; Chromatic Star's sacrifices itself.</summary>
	static string CashedInBy(GameState s, int cardId) =>
		s.HasObject(cardId)
		&& s.GetObject(cardId) is Cards.Card card
		&& ManaAbilities.AbilitiesOf(s, card).FirstOrDefault(m => m.CashedIn) is { } cashed
			? cashed.Exerts
				? "exert"
				: cashed.Exhaust
					? "exhaust"
					: "sacrifice"
			: "sacrifice";

	/// <summary>
	/// A mana ability without {T}, by what it costs — Rain of Filth's "Sacrifice Swamp"; Pentad Prism's "Pentad Prism:
	/// Remove 1 charge counter from this"; Lion's Eye Diamond's "…: Discard your hand, Sacrifice this".
	/// </summary>
	static string NoTapCosts(GameState s, TapForManaAction t) =>
		s.HasObject(t.CardId)
		&& s.GetObject(t.CardId) is Card card
		&& ManaAbilities.AbilitiesOf(s, card).FirstOrDefault(m => m.NoTap)
			is { Costs: not [SacrificeSelfCost] } ability
			? $"{card.Name}: {string.Join(", ", ability.Costs.Select(c => c.Describe()))}"
			: $"Sacrifice {Name(s, t.CardId)}";

	/// <summary>Tells a card's activated abilities apart — " [#2: {2}{B}, T]"; empty when it has one.</summary>
	static string WhichAbility(GameState s, ActivateAbilityAction a)
	{
		if (!s.HasObject(a.CardId) || s.GetObject(a.CardId) is not Card card)
			return "";
		var abilities = s.ActivatedAbilitiesOf(card).ToList();
		if (abilities.Count < 2 || abilities.ElementAtOrDefault(a.AbilityIndex) is not { } ability)
			return "";
		var cost = new[]
		{
			ability.Cost.ToString(),
			ability.TapsItself ? "T" : "",
			ability.Costs.Any(c => c is SacrificeSelfCost) ? "sacrifice it" : "",
		}.Where(part => part != "");
		// The number too: two abilities may cost the same.
		return $" [#{a.AbilityIndex + 1}: {string.Join(", ", cost)}]";
	}

	/// <summary>What the pending choice is about, e.g. "Attack with Bear?".</summary>
	public static string Question(GameState s, ChoiceAction choice) =>
		choice switch
		{
			DeclareAttackerChoice a => $"Attack with {Card(s, s.GetCard(a.CreatureId))}?",
			DeclareBlockerChoice b => $"Block with {Card(s, s.GetCard(b.CreatureId))}?",
			AssignCombatDamageChoice d =>
				$"{Name(s, d.AttackerId)}: how much damage to {Name(s, d.BlockerId)}? ({d.Options.Count - 1} left to divide)",
			ChooseModesChoice m => $"{Name(s, m.SourceId)}: {m.Prompt.ToLowerInvariant()}",
			ChooseTargetChoice t =>
				$"{Name(s, t.SourceId)}: choose target {t.Index + 1}{(t.Spec.Optional ? " (optional)" : "")}",
			_ => choice.Prompt,
		};

	public static string Answer(GameState s, ChoiceAction choice, IEnumerable<int> ids)
	{
		var picked = ids.Select(id => choice.Options.First(o => o.Id == id).DisplayText).ToList();
		return $"{Question(s, choice)} {(picked.Count == 0 ? "none" : string.Join(", ", picked))}";
	}

	public static string Card(GameState s, Card c)
	{
		var parts = new List<string> { c.Name is "" ? "Face-down" : c.Name };
		if (c.Is(CardType.Creature))
			parts.Add($"{s.Power(c)}/{s.Toughness(c)}");
		if (c.Is(CardType.Planeswalker))
			parts.Add($"loyalty {c.Loyalty}");
		if (c.Damage > 0)
			parts.Add($"dmg {c.Damage}");
		if (c.IsTapped)
			parts.Add("tapped");
		if (c.AttackingId != 0)
			parts.Add("attacking");
		if (c.BlockingId != 0)
			parts.Add("blocking");
		if (c.IsToken)
			parts.Add("token");
		if (c.IsPrepared)
			parts.Add("prepared");
		if (Rules.Attachments.HostOf(s, c) is { } host)
			parts.Add($"on {host.Name}");
		return string.Join(" ", parts);
	}

	static string LoyaltyCost(GameState s, ActivateLoyaltyAction l) =>
		s.HasObject(l.CardId)
		&& Loyalty.AbilitiesOf(s, s.GetCard(l.CardId)).ElementAtOrDefault(l.AbilityIndex)
			is { } ability
			? (ability.MinusX ? $"−{l.X}" : Loyalty.CostText(ability.Cost))
			: "?";

	static string ModeText(GameState s, CastSpellAction c) =>
		s.GetCard(c.CardId).GetComponent<Effects.SpellEffectsComponent>() is { } text
			? Modes(text.Modes, c.Modes)
			: "";

	static string ModeText(GameState s, ActivateAbilityAction a) =>
		s.HasObject(a.CardId)
		&& s.GetObject(a.CardId) is Card card
		&& s.ActivatedAbilitiesOf(card).ElementAtOrDefault(a.AbilityIndex) is { } ability
			? Modes(ability.Modes, a.Modes)
			: "";

	/// <summary>The chosen modes' printed lines, "Destroy … + You draw a card …" — for every front end.</summary>
	public static string Modes(IReadOnlyList<Effects.Mode> modes, IEnumerable<int> chosen) =>
		string.Join(" + ", chosen.Select(i => Line(modes[i])));

	/// <summary>A mode's printed line without its bullet — or, spree's "+ {1}{U} — Counter target spell.", its cost.</summary>
	static string Line(Effects.Mode mode) =>
		mode.Cost is null
			? mode.Text.TrimStart('•', ' ')
			: mode.Text[(mode.Text.IndexOf(" — ", StringComparison.Ordinal) + 3)..];

	static string Name(GameState s, int id) =>
		!s.HasObject(id) ? $"#{id}"
		: s.GetObject(id).Name is "" ? "a face-down 2/2" // manifest dread: no name (CR 708.2)
		: s.GetObject(id).Name;

	static string Targets(GameState s, ImmutableArray<int> targets) =>
		targets.IsDefaultOrEmpty || targets.All(t => t == 0)
			? ""
			: " → " + string.Join(", ", targets.Where(t => t != 0).Select(t => Name(s, t)));
}
