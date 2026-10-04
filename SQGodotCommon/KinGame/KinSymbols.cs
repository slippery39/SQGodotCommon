using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **What every symbol means — said once, here** (Shayne, 2026-10-01: "I should be able to at least
/// hover over the icons and see what it means"). The ? panel's legend, a creature's icon tips, a
/// tile's tooltips and a card's hover panel all read these lines, so they can never disagree.
/// </summary>
public static class KinSymbols
{
	public sealed record Symbol(string Name, Texture2D Icon, Color Tint, string Meaning)
	{
		public string Line => $"{Name}: {Meaning}";
	}

	private static readonly Color GroveTint = KinPalette.Family(Family.Grove).Lightened(0.35f);
	private static readonly Color EmberTint = Color.FromHtml("#FF9A3C");

	public static readonly Symbol Block =
		new("Block", KinArt.GuardIcon, KinPalette.Bone, "stops damage until your next turn.");
	public static readonly Symbol Rooted =
		new("Rooted Block", KinArt.GuardIcon, GroveTint, "Block that stays one more turn.");
	public static readonly Symbol Power =
		new("Power", KinArt.PowerIcon, KinPalette.Bone, "added to its attacks.");
	public static readonly Symbol SpellPower =
		new(
			"Spell Power",
			KinArt.SpellPowerIcon,
			EmberTint,
			"your team's total, added to every spell's damage."
		);
	public static readonly Symbol Spell =
		new("Spell damage", KinArt.SpellIcon, EmberTint, "damage to a foe, plus your Spell Power.");
	public static readonly Symbol Attack =
		new(
			"Attack",
			KinArt.AttackIcon,
			KinPalette.Bone,
			"the monster it is played on strikes their front, adding its Power."
		);
	public static readonly Symbol Thorns =
		new("Thorns", KinArt.ThornsIcon, GroveTint, "a foe that hits it takes this much.");
	public static readonly Symbol Burn =
		new(
			"Burn",
			KinArt.BurnIcon,
			EmberTint,
			"it takes this much as the foes' turn starts, through Block; then Burn drops by 1."
		);

	// ===== DEBUFFS on your monsters (`PartyDebuffs`): the number is the turns left
	private static readonly Color DebuffTint = Color.FromHtml("#C59BE8");

	public static readonly Symbol Weak =
		new("Weak", KinArt.DebuffIcon(Debuff.Weak), DebuffTint, "its attacks deal 25% less.");
	public static readonly Symbol Vulnerable =
		new("Vulnerable", KinArt.DebuffIcon(Debuff.Vulnerable), DebuffTint, "it takes 50% more.");
	public static readonly Symbol Silence =
		new(
			"Silence",
			KinArt.DebuffIcon(Debuff.Silence),
			DebuffTint,
			"its first-attack bonus is off."
		);
	public static readonly Symbol Shaken =
		new(
			"Shaken",
			KinArt.DebuffIcon(Debuff.Shaken),
			DebuffTint,
			"no attack card can be played on it."
		);

	/// <summary>A debuff's symbol.</summary>
	public static Symbol Of(Debuff debuff) =>
		debuff switch
		{
			Debuff.Weak => Weak,
			Debuff.Vulnerable => Vulnerable,
			Debuff.Silence => Silence,
			_ => Shaken,
		};

	public static readonly Symbol Grow =
		new("Grow", KinArt.PowerIcon, GroveTint, "+Power for the rest of the fight.");
	public static readonly Symbol Token =
		new(
			"Token",
			KinArt.GrowIcon,
			GroveTint,
			"a summoned creature: a wall, fuel and an attacker. Gone after the fight."
		);
	public static readonly Symbol Fades =
		new("Fades", KinArt.ClockIcon, KinPalette.Bone, "gone in this many turns.");
	public static readonly Symbol Passive =
		new("Passive", KinArt.PassiveIcon, KinPalette.Gold, "a rule this creature always has.");
	public static readonly Symbol FirstAttack =
		new(
			"First attack",
			KinArt.AttackIcon,
			KinPalette.Gold,
			"the first attack card played on it each turn also gives this bonus."
		);
	public static readonly Symbol Crush =
		new("CRUSH", KinArt.CrushIcon, KinPalette.Red.Lightened(0.3f), "this hit ignores Block.");
	public static readonly Symbol Aim =
		new(
			"Aim",
			KinArt.PipHit,
			KinPalette.Bone,
			"one dot a place in your line, back to front: a filled dot is hit."
		);
	public static readonly Symbol Draw =
		new("Draw", KinArt.DrawIcon, KinPalette.Bone, "draw this many cards.");
	public static readonly Symbol Energy =
		new(
			"Energy",
			KinArt.EnergyIcon,
			KinPalette.Gold,
			"what cards cost to play; refilled each turn."
		);
	public static readonly Symbol Aura =
		new(
			"Aura",
			KinArt.AuraIcon,
			KinPalette.Gold,
			"a rule on your side for the rest of the fight."
		);
	public static readonly Symbol Health =
		new(
			"Health",
			KinArt.LifeIcon,
			KinPalette.Red.Lightened(0.3f),
			"at 0 it is knocked out; it is back at 1 after the fight."
		);
	public static readonly Symbol Sacrifice =
		new("Sacrifice", KinArt.GrowIcon, GroveTint, "one of your tokens falls, on purpose.");

	/// <summary>The ? panel's legend, in reading order.</summary>
	public static readonly IReadOnlyList<Symbol> Legend =
	[
		Block,
		Rooted,
		Power,
		SpellPower,
		Spell,
		Thorns,
		Burn,
		Token,
		Fades,
		Passive,
		FirstAttack,
		Crush,
		Aim,
		Draw,
		Energy,
		Aura,
		Weak,
		Vulnerable,
		Silence,
		Shaken,
	];

	/// <summary>
	/// **The symbols a card uses** — from its STEPS, never parsed from its words — for its hover
	/// panel. Each once, in the order its steps first need it.
	/// </summary>
	public static IReadOnlyList<Symbol> Of(KinCard card)
	{
		var found = new List<Symbol>();
		void Add(Symbol s)
		{
			if (!found.Contains(s))
				found.Add(s);
		}
		foreach (var step in card.Effects.Select(e => e.Template))
			switch (step)
			{
				case StrikeAction:
					Add(Attack);
					Add(Power);
					break;
				case SpellDamageAction:
					Add(Spell);
					Add(SpellPower);
					break;
				case GuardAction
				or EmberBlockAction:
					Add(Block);
					break;
				case GroveBlockAction g:
					Add(g.RootAll ? Rooted : Block);
					break;
				case RootAction
				or DeepRootsAction:
					Add(Rooted);
					break;
				case ThornsAction:
					Add(Thorns);
					break;
				case BurnAction:
					Add(Burn);
					break;
				case DrawAction:
					Add(Draw);
					break;
				case GainEnergyAction
				or EnergyNextTurnAction:
					Add(Energy);
					break;
				case SpellPowerAction:
					Add(SpellPower);
					break;
				case GrowAction:
					Add(Grow);
					break;
				case SummonTokenAction:
					Add(Token);
					break;
				case GraftAction:
					Add(Sacrifice);
					Add(Grow);
					Add(Block);
					break;
				case SacrificeTokenAction
				or HarvestAction:
					Add(Sacrifice);
					break;
				case AuraAction:
					Add(Aura);
					break;
			}
		return found;
	}

	/// <summary>
	/// **A card explained, in plain text** — its name, its rules in words, then each symbol it uses and
	/// what it means. For a native tooltip (no BBCode); the board's hover panel adds the icons.
	/// </summary>
	public static string PlainTip(KinCard card) =>
		string.Join(
			"\n",
			new[] { card.Name.ToUpperInvariant(), KinCardFace.RulesTextFor(card) }.Concat(
				Of(card).Select(s => s.Line)
			)
		);

	/// <summary>
	/// **Tooltips in the game's own look** — navy, gold-edged, readable — for any screen that uses
	/// native tooltips. Set on a screen's root; its children inherit it.
	/// </summary>
	public static Theme TooltipTheme()
	{
		var theme = new Theme();
		theme.SetStylebox(
			"panel",
			"TooltipPanel",
			KinPalette.Box(KinPalette.Navy, KinPalette.Gold, 2)
		);
		theme.SetFontSize("font_size", "TooltipLabel", 22);
		theme.SetColor("font_color", "TooltipLabel", KinPalette.Bone);
		return theme;
	}

	/// <summary>An icon as BBCode, for a rich-text tip.</summary>
	public static string Img(Symbol s, int size = 26) =>
		$"[img={size}x{size} color=#{s.Tint.ToHtml(false)}]{s.Icon.ResourcePath}[/img]";
}
