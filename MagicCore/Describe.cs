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
			PlayLandAction p => $"Play {Name(s, p.CardId)}"
				+ (s.IsIn(p.CardId, p.PlayerId, ZoneType.Exile) ? " from exile" : ""),
			CastSpellAction c => $"Cast {Name(s, c.CardId)}"
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
				+ (c.Replicate > 0 ? $" (replicate ×{c.Replicate})" : "")
				+ (c.Modes.Length > 0 ? $" ({ModeText(s, c)})" : "")
				+ Targets(s, c.Targets)
				+ (c.SacrificeId != 0 ? $", sacrificing {Name(s, c.SacrificeId)}" : "")
				+ (c.DiscardId != 0 ? $", discarding {Name(s, c.DiscardId)}" : "")
				+ (c.BeholdId != 0 ? $", beholding {Name(s, c.BeholdId)}" : "")
				+ (c.ReturnId != 0 ? $", returning {Name(s, c.ReturnId)}" : ""),
			ActivateAbilityAction a => $"Activate {Name(s, a.CardId)}{WhichAbility(s, a)}"
				+ (s.IsIn(a.CardId, a.PlayerId, ZoneType.Hand) ? " from hand" : "")
				+ (s.IsIn(a.CardId, a.PlayerId, ZoneType.Graveyard) ? " from graveyard" : "")
				+ (a.Modes.Length > 0 ? $" ({ModeText(s, a)})" : "")
				+ Targets(s, a.Targets)
				+ (a.SacrificeId != 0 ? $", sacrificing {Name(s, a.SacrificeId)}" : "")
				+ (a.DiscardId != 0 ? $", discarding {Name(s, a.DiscardId)}" : "")
				+ (a.ExileId != 0 ? $", exiling {Name(s, a.ExileId)}" : "")
				+ (a.TapId != 0 ? $", tapping {Name(s, a.TapId)}" : "")
				+ (a.SecondTapId != 0 ? $" and {Name(s, a.SecondTapId)}" : ""),
			ActivateLoyaltyAction l =>
				$"{Name(s, l.CardId)} [{LoyaltyCost(s, l)}]{Targets(s, l.Targets)}",
			TapForManaAction t => $"Tap {Name(s, t.CardId)} for {t.Color}",
			_ => action.GetType().Name,
		};

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
		var parts = new List<string> { c.Name };
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
		string.Join(" + ", chosen.Select(i => modes[i].Text.TrimStart('•', ' ')));

	static string Name(GameState s, int id) => s.HasObject(id) ? s.GetObject(id).Name : $"#{id}";

	static string Targets(GameState s, ImmutableArray<int> targets) =>
		targets.IsDefaultOrEmpty || targets.All(t => t == 0)
			? ""
			: " → " + string.Join(", ", targets.Where(t => t != 0).Select(t => Name(s, t)));
}
