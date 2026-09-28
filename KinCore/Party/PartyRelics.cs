using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **RELICS — the trainer's run items** (`KinFamiliesPlan.md`, round 2: an elite's prize). Global,
/// permanent, family-neutral; each one rule. The first eight were drafted and kept by Shayne
/// (2026-09-28). Held items (a monster's) are a separate, later system.
/// </summary>
public enum Relic
{
	Whetstone,
	IronShell,
	Lantern,
	QuickBoots,
	FieldKit,
	LuckyCoin,
	SnarePouch,
	TrainersEye,
}

public static class PartyRelics
{
	public const int WhetstonePower = 1;
	public const int IronShellBlock = 8;
	public const int LanternEnergy = 1;
	public const int QuickBootsDraw = 2;
	public const int FieldKitHeal = 4;
	public const double LuckyCoinGold = 1.5;
	public const int SnarePouchNow = 2;
	public const int TrainersEyeOffer = 4;

	public static readonly ImmutableList<Relic> All = [.. Enum.GetValues<Relic>()];

	public static string Name(Relic relic) =>
		relic switch
		{
			Relic.IronShell => "Iron Shell",
			Relic.QuickBoots => "Quick Boots",
			Relic.FieldKit => "Field Kit",
			Relic.LuckyCoin => "Lucky Coin",
			Relic.SnarePouch => "Snare Pouch",
			Relic.TrainersEye => "Trainer's Eye",
			_ => relic.ToString(),
		};

	public static string Text(Relic relic) =>
		relic switch
		{
			Relic.Whetstone => $"Your monsters have +{WhetstonePower} Power.",
			Relic.IronShell =>
				$"Each fight, your front monster starts with {IronShellBlock} Block.",
			Relic.Lantern => $"+{LanternEnergy} energy on your first turn of each fight.",
			Relic.QuickBoots =>
				$"Draw {QuickBootsDraw} more cards on your first turn of each fight.",
			Relic.FieldKit => $"After each won fight, every monster heals {FieldKitHeal} HP.",
			Relic.LuckyCoin => "Fights pay 50% more gold.",
			Relic.SnarePouch => $"Gain a Snare at every town, and {SnarePouchNow} now.",
			Relic.TrainersEye => $"Card rewards offer {TrainersEyeOffer} cards, not 3.",
			_ => "",
		};

	/// <summary>
	/// **The hand is dealt**: Whetstone, Lantern and Quick Boots. Called once, after the first turn
	/// starts — before DEPLOY, so the hand you order your line with already has the extra cards.
	/// </summary>
	public static GameState Dealt(GameState s)
	{
		var party = s.GetParty();
		if (party.Relics.Contains(Relic.Whetstone))
			foreach (var ally in s.Allies().ToList())
				s = s.UpdateObject(ally.Id, ally with { Power = ally.Power + WhetstonePower });
		if (party.Relics.Contains(Relic.Lantern))
			s = s.UpdateObject(
				party.Id,
				s.GetParty() with
				{
					Energy = party.Energy + LanternEnergy,
				}
			);
		if (party.Relics.Contains(Relic.QuickBoots))
			(s, _) = StartTurnAction.DrawCards(s, QuickBootsDraw);
		return s;
	}

	/// <summary>
	/// **The fight begins — the line is set** (FIGHT, or the deal when there is no deploy): Iron
	/// Shell shields whoever ended up in front.
	/// </summary>
	public static GameState FightBegins(GameState s)
	{
		if (s.GetParty().Relics.Contains(Relic.IronShell) && s.AllyAt(0) is { } front)
			s = s.UpdateObject(front.Id, front with { Block = front.Block + IronShellBlock });
		return s;
	}
}
