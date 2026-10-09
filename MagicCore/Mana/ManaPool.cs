namespace MagicCore;

public enum ManaColor
{
	White,
	Blue,
	Black,
	Red,
	Green,
	Colorless,
}

/// <summary>
/// Six counts — five colours and colourless. Unlike MtgCore there is no separate generic track:
/// a land makes ONE mana of one colour, and generic costs are paid from whatever is in the pool.
/// Flat ints rather than a dictionary so it serializes with no converter.
/// </summary>
public record ManaPool
{
	public static readonly ManaPool Empty = new();

	public int White { get; init; }
	public int Blue { get; init; }
	public int Black { get; init; }
	public int Red { get; init; }
	public int Green { get; init; }
	public int Colorless { get; init; }

	/// <summary>Chandra, Chill of Compliance: "Spend this mana only to cast a noncreature spell" — kept apart.</summary>
	public System.Collections.Immutable.ImmutableList<RestrictedMana> Restricted { get; init; } =
		[];

	/// <summary>Unrestricted mana only: what can pay for anything.</summary>
	public int Total => White + Blue + Black + Red + Green + Colorless;

	public int this[ManaColor color] =>
		color switch
		{
			ManaColor.White => White,
			ManaColor.Blue => Blue,
			ManaColor.Black => Black,
			ManaColor.Red => Red,
			ManaColor.Green => Green,
			_ => Colorless,
		};

	public ManaPool Add(ManaColor color, int amount = 1) =>
		color switch
		{
			ManaColor.White => this with { White = White + amount },
			ManaColor.Blue => this with { Blue = Blue + amount },
			ManaColor.Black => this with { Black = Black + amount },
			ManaColor.Red => this with { Red = Red + amount },
			ManaColor.Green => this with { Green = Green + amount },
			_ => this with { Colorless = Colorless + amount },
		};
}

/// <summary>One floating mana that may pay only for what its restriction allows.</summary>
public record RestrictedMana(ManaColor Color, Effects.ManaRestriction Restriction);
