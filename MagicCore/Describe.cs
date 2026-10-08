using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

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
			PlayLandAction p => $"Play {Name(s, p.CardId)}",
			CastSpellAction c => $"Cast {Name(s, c.CardId)}"
				+ (
					s.IsIn(c.CardId, c.PlayerId, ZoneType.Graveyard)
						? " from graveyard (flashback)"
						: ""
				)
				+ (c.X > 0 ? $" (X={c.X})" : "")
				+ (c.Modes.Length > 0 ? $" ({ModeText(s, c)})" : "")
				+ Targets(s, c.Targets)
				+ (c.SacrificeId != 0 ? $", sacrificing {Name(s, c.SacrificeId)}" : "")
				+ (c.DiscardId != 0 ? $", discarding {Name(s, c.DiscardId)}" : ""),
			ActivateAbilityAction a =>
				$"Activate {Name(s, a.CardId)}{Targets(s, a.Targets)}{(a.SacrificeId != 0 ? $", sacrificing {Name(s, a.SacrificeId)}" : "")}",
			ActivateLoyaltyAction l =>
				$"{Name(s, l.CardId)} [{LoyaltyCost(s, l)}]{Targets(s, l.Targets)}",
			TapForManaAction t => $"Tap {Name(s, t.CardId)} for {t.Color}",
			_ => action.GetType().Name,
		};

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
		return string.Join(" ", parts);
	}

	static string LoyaltyCost(GameState s, ActivateLoyaltyAction l) =>
		s.HasObject(l.CardId)
		&& Loyalty.AbilitiesOf(s, s.GetCard(l.CardId)).ElementAtOrDefault(l.AbilityIndex)
			is { } ability
			? Loyalty.CostText(ability.Cost)
			: "?";

	static string ModeText(GameState s, CastSpellAction c) =>
		s.GetCard(c.CardId).GetComponent<Effects.SpellEffectsComponent>() is { } text
			? string.Join(" + ", c.Modes.Select(i => text.Modes[i].Text.TrimStart('•', ' ')))
			: "";

	static string Name(GameState s, int id) => s.HasObject(id) ? s.GetObject(id).Name : $"#{id}";

	static string Targets(GameState s, ImmutableArray<int> targets) =>
		targets.IsDefaultOrEmpty || targets.All(t => t == 0)
			? ""
			: " → " + string.Join(", ", targets.Where(t => t != 0).Select(t => Name(s, t)));
}
