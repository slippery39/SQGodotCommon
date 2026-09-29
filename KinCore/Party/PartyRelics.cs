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
	TrainersEye,

	// ----- BOSS RELICS: one of three after a boss. Strong, and no drawbacks (Shayne, 2026-09-28).
	WarDrum,
	AncientLens,
	WarbandBanner,
	KinTotem,
}

public static class PartyRelics
{
	public const int WhetstonePower = 1;
	public const int IronShellBlock = 8;
	public const int LanternEnergy = 1;
	public const int QuickBootsDraw = 2;
	public const int FieldKitHeal = 4;
	public const double LuckyCoinGold = 1.5;
	public const int TrainersEyeOffer = 4;
	public const int WarDrumEnergy = 1;
	public const int AncientLensDraw = 1;
	public const int WarbandBannerPower = 2;

	/// <summary>The BOSS relics — a boss offers three of these; an elite never does.</summary>
	public static readonly ImmutableList<Relic> Boss =
	[
		Relic.WarDrum,
		Relic.AncientLens,
		Relic.WarbandBanner,
		Relic.KinTotem,
	];

	/// <summary>The relics an ELITE pays.</summary>
	public static readonly ImmutableList<Relic> All =
	[
		.. Enum.GetValues<Relic>().Where(r => !Boss.Contains(r)),
	];

	public static string Name(Relic relic) =>
		relic switch
		{
			Relic.IronShell => "Iron Shell",
			Relic.QuickBoots => "Quick Boots",
			Relic.FieldKit => "Field Kit",
			Relic.LuckyCoin => "Lucky Coin",
			Relic.TrainersEye => "Trainer's Eye",
			Relic.WarDrum => "War Drum",
			Relic.AncientLens => "Ancient Lens",
			Relic.WarbandBanner => "Warband Banner",
			Relic.KinTotem => "Kin Totem",
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
			Relic.TrainersEye => $"Card rewards offer {TrainersEyeOffer} cards, not 3.",
			Relic.WarDrum => $"+{WarDrumEnergy} energy every turn.",
			Relic.AncientLens => $"Draw {AncientLensDraw} more card every turn.",
			Relic.WarbandBanner => $"Your monsters have +{WarbandBannerPower} Power.",
			Relic.KinTotem => "Your first card each turn costs 0.",
			_ => "",
		};

	/// <summary>
	/// **The hand is dealt**: Whetstone, Lantern and Quick Boots. Called once, after the first turn
	/// starts — before DEPLOY, so the hand you order your line with already has the extra cards.
	/// </summary>
	public static GameState Dealt(GameState s)
	{
		var party = s.GetParty();
		var power =
			(party.Relics.Contains(Relic.Whetstone) ? WhetstonePower : 0)
			+ (party.Relics.Contains(Relic.WarbandBanner) ? WarbandBannerPower : 0);
		if (power > 0)
			foreach (var ally in s.Allies().ToList())
				s = s.UpdateObject(ally.Id, ally with { Power = ally.Power + power });
		// War Drum: every turn's energy (and this first one's); Ancient Lens: every turn's draw.
		if (party.Relics.Contains(Relic.WarDrum))
			s = s.UpdateObject(
				party.Id,
				s.GetParty() with
				{
					MaxEnergy = s.GetParty().MaxEnergy + WarDrumEnergy,
					Energy = s.GetParty().Energy + WarDrumEnergy,
				}
			);
		if (party.Relics.Contains(Relic.AncientLens))
		{
			s = s.UpdateObject(party.Id, s.GetParty() with { DrawBonus = AncientLensDraw });
			(s, _) = StartTurnAction.DrawCards(s, AncientLensDraw);
		}
		party = s.GetParty();
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
