using System.Collections.Immutable;

namespace MtgCore;

/// <summary>
/// The five colours. Colourless is the ABSENCE of an entry in a <see cref="ManaPool"/>, not a
/// sixth member — an artifact costing 3 has an empty pip pool, and a Wastes-style land that
/// produces generic mana with no colour simply carries no <c>LandColorComponent</c>.
/// </summary>
public enum ManaColor
{
	White,
	Blue,
	Black,
	Red,
	Green,
}

/// <summary>
/// Five counts, one per colour. Used for four different things, deliberately by the same type:
/// a card's colour requirement (its pips), a land's production, a player's per-turn colour
/// maximum, and their remaining colour mana.
///
/// **Generic and coloured mana are separate tracks and never substitute for each other.** A land
/// grants 1 generic (<c>MtgPlayer.MaxMana</c>) AND 1 coloured, and a cost of "1W" spends 1 from
/// the generic track and 1 White from this one. That is what removes the payment-ordering
/// problem real Magic has: because a generic pip can never be paid with coloured mana, there is
/// no choice about which source to spend, so casting needs no solver and no manual tapping.
///
/// Consequences worth knowing, all of which fall out of the arithmetic rather than being coded:
///   - A mono-colour deck is never colour-limited. Every cost carries at least 1 generic, so the
///     generic track always runs dry first. Focus is free.
///   - A five-colour manabase caps you at one single-pip spell per colour per turn. Greed costs
///     throughput, which is the drawback that makes colour a real deckbuilding constraint.
///   - A double pip (WW) consumes two of a turn's White throughput, so it is a genuine
///     commitment marker rather than a cosmetic one.
///
/// Five int properties rather than a dictionary keyed by <see cref="ManaColor"/>: the set is
/// closed at five and will not grow, and a flat record of ints round-trips through
/// <c>StateJson</c> with no converter and no risk. The indexer is not serialized — indexed
/// properties are invisible to System.Text.Json — so it costs nothing to have.
/// </summary>
public record ManaPool
{
	public static readonly ManaPool Empty = new();

	/// <summary>The five colours in a fixed order, for callers that must iterate them.</summary>
	public static readonly ImmutableArray<ManaColor> Colors =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	public int White { get; init; }
	public int Blue { get; init; }
	public int Black { get; init; }
	public int Red { get; init; }
	public int Green { get; init; }

	public int this[ManaColor color] =>
		color switch
		{
			ManaColor.White => White,
			ManaColor.Blue => Blue,
			ManaColor.Black => Black,
			ManaColor.Red => Red,
			ManaColor.Green => Green,
			_ => 0,
		};

	public int Total => White + Blue + Black + Red + Green;

	public bool IsEmpty => Total == 0;

	/// <summary>This pool with <paramref name="color"/> set to <paramref name="amount"/>.</summary>
	public ManaPool With(ManaColor color, int amount) =>
		color switch
		{
			ManaColor.White => this with { White = amount },
			ManaColor.Blue => this with { Blue = amount },
			ManaColor.Black => this with { Black = amount },
			ManaColor.Red => this with { Red = amount },
			ManaColor.Green => this with { Green = amount },
			_ => this,
		};

	public ManaPool Add(ManaColor color, int amount) => With(color, this[color] + amount);

	public ManaPool Add(ManaPool other) =>
		new()
		{
			White = White + other.White,
			Blue = Blue + other.Blue,
			Black = Black + other.Black,
			Red = Red + other.Red,
			Green = Green + other.Green,
		};

	/// <summary>
	/// This pool minus <paramref name="other"/>, floored at zero per colour. Callers are expected
	/// to have checked <see cref="Covers"/> first; the floor is here so a mistake cannot put a
	/// player on negative mana, which would silently unlock every later cast that turn.
	/// </summary>
	public ManaPool Subtract(ManaPool other) =>
		new()
		{
			White = Math.Max(0, White - other.White),
			Blue = Math.Max(0, Blue - other.Blue),
			Black = Math.Max(0, Black - other.Black),
			Red = Math.Max(0, Red - other.Red),
			Green = Math.Max(0, Green - other.Green),
		};

	/// <summary>True if this pool can pay every pip in <paramref name="required"/>.</summary>
	public bool Covers(ManaPool required) =>
		White >= required.White
		&& Blue >= required.Blue
		&& Black >= required.Black
		&& Red >= required.Red
		&& Green >= required.Green;

	/// <summary>"WWU", or "" when empty — for rules text, the AI inspector and test failures.</summary>
	public string ToPipString() =>
		string.Concat(Colors.Select(c => new string(Symbol(c), this[c])));

	public static char Symbol(ManaColor color) =>
		color switch
		{
			ManaColor.White => 'W',
			ManaColor.Blue => 'U',
			ManaColor.Black => 'B',
			ManaColor.Red => 'R',
			ManaColor.Green => 'G',
			_ => '?',
		};
}
