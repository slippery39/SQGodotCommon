using System.Collections.Generic;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Every symbol the MTG front end draws, with its meaning written ONCE — tooltips, legends and
/// hovers all read it here, so two copies cannot drift (the add-icon skill). Icons are
/// game-icons.net silhouettes in <c>Assets/Icons/</c> (credited in CREDITS.md), light so code can
/// tint them.
///
/// Meanings describe THIS engine, not paper Magic: there is no blocking, so Flying and Reach are
/// about who may ATTACK whom (AttackAction.CanReach).
/// </summary>
public static class MtgSymbols
{
	public sealed record Symbol(string Name, Texture2D Icon, Color Tint, string Meaning);

	private const string Dir = "res://MtgGame/Assets/Icons/";

	private static readonly Color Gold = new("f4e29b");

	private static readonly Dictionary<string, Symbol> Keywords =
		new()
		{
			["Flying"] = Make(
				"Flying",
				"kw_flying",
				"Only creatures with Flying or Reach can attack it."
			),
			["Reach"] = Make("Reach", "kw_reach", "Can attack creatures with Flying."),
			["Taunt"] = Make(
				"Taunt",
				"kw_taunt",
				"Enemy creatures must attack this first if they can."
			),
			["Haste"] = Make("Haste", "kw_haste", "Can attack the turn it arrives."),
			["First Strike"] = Make(
				"First Strike",
				"kw_first_strike",
				"Deals its combat damage first."
			),
			["Double Strike"] = Make(
				"Double Strike",
				"kw_double_strike",
				"Deals combat damage twice, the first time before its target."
			),
			["Deathtouch"] = Make(
				"Deathtouch",
				"kw_deathtouch",
				"Any combat damage it deals is lethal."
			),
			["Lifelink"] = Make(
				"Lifelink",
				"kw_lifelink",
				"Damage it deals also gains you that much life."
			),
			["Trample"] = Make(
				"Trample",
				"kw_trample",
				"Attacking a creature, its excess damage hits that creature's controller."
			),
			["Indestructible"] = Make(
				"Indestructible",
				"kw_indestructible",
				"Damage and \"destroy\" effects don't kill it."
			),
			["Shroud"] = Make(
				"Shroud",
				"kw_shroud",
				"Can't be the target of any spell or ability."
			),
			["Hexproof"] = Make(
				"Hexproof",
				"kw_hexproof",
				"Can't be the target of your opponent's spells or abilities."
			),
			["Can't attack"] = Make(
				"Can't attack",
				"kw_cant_attack",
				"This creature can't attack."
			),
			["Cover"] = Make(
				"Cover",
				"kw_cover",
				"Can't be attacked for this many of its controller's turns, or until it attacks."
			),
		};

	private static readonly Dictionary<ManaColor, Texture2D> ManaIcons =
		new()
		{
			[ManaColor.White] = Load("mana_white"),
			[ManaColor.Blue] = Load("mana_blue"),
			[ManaColor.Black] = Load("mana_black"),
			[ManaColor.Red] = Load("mana_red"),
			[ManaColor.Green] = Load("mana_green"),
		};

	/// <summary>
	/// The symbol for a keyword as <see cref="MtgCardMapper.KeywordNames"/> spells it. A counted
	/// keyword ("Cover 2") finds its base name. Null for a keyword with no symbol yet, which the
	/// caller shows as text rather than dropping.
	/// </summary>
	public static Symbol? Keyword(string name)
	{
		if (Keywords.TryGetValue(name, out var symbol))
			return symbol;
		var space = name.LastIndexOf(' ');
		return space > 0 && Keywords.TryGetValue(name[..space], out symbol) ? symbol : null;
	}

	public static Texture2D Mana(ManaColor colour) => ManaIcons[colour];

	private static Symbol Make(string name, string file, string meaning) =>
		new(name, Load(file), Gold, meaning);

	private static Texture2D Load(string file) => GD.Load<Texture2D>(Dir + file + ".svg");
}
