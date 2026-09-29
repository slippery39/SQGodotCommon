using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>
/// **LEVELS** (Shayne, 2026-09-27: "a level system … so we could design areas so that we only see
/// monsters of a certain level, which gives us a way to discuss balancing"). Every creature has a
/// level; its HP, Power and move amounts are its BASE × <see cref="Factor"/>. **The content's own
/// numbers are the base, at <see cref="Base"/>**, so nothing already authored moves at Lv 5.
///
/// **Only FOES have levels now** (round 4, 2026-09-28: your monsters grow through cards, relics and
/// upgrades, not XP): a region sets its foes' levels, and `Toughen` makes its exams harder still.
/// </summary>
public static class PartyLevels
{
	/// <summary>The level the content's numbers are written at — and the starters' level.</summary>
	public const int Base = 5;

	/// <summary>A creature's stats at a level, as a share of its base: Lv 2 = 0.76, Lv 5 = 1, Lv 10 = 1.4.</summary>
	public static double Factor(int level) => 0.6 + 0.08 * level;

	/// <summary>An amount at a level — never scaled away to 0 if it was more.</summary>
	public static int At(int amount, int level) =>
		amount <= 0 ? amount : Math.Max(1, (int)Math.Round(amount * Factor(level)));

	private static ImmutableList<Intent> Scaled(ImmutableList<Intent> moves, int level) =>
		[
			.. moves.Select(i =>
				i.Kind is IntentType.Attack or IntentType.Block
					? i with
					{
						Amount = At(i.Amount, level),
					}
					: i
			),
		];

	/// <summary>**A wild creature at a level**: HP, Block and attacks scaled, and the level on it.</summary>
	public static Foe Scale(Foe foe, int level)
	{
		var hp = At(foe.MaxHp, level);
		return foe with
		{
			Hp = hp,
			MaxHp = hp,
			Level = level,
			Pattern = Scaled(foe.Pattern, level),
			// A PHASE's second pattern and Block are the same foe's numbers, at the same level.
			Components =
			[
				.. foe.Components.Select(c =>
					c is Phase phase
						? phase with
						{
							Pattern = Scaled(phase.Pattern, level),
							Block = At(phase.Block, level),
						}
						: c
				),
			],
		};
	}

	/// <summary>
	/// **An EXAM made tougher than its level** — HP times `hp`, every attack (its phase's too) times
	/// `hit`. Levels move a foe ~7% a level, too gentle to make a boss a threat (party-sim,
	/// 2026-09-28); this is the tuning knob for bosses and elites.
	/// </summary>
	public static Foe Toughen(Foe foe, double hp, double hit)
	{
		int Times(int amount, double by) => amount <= 0 ? amount : (int)Math.Round(amount * by);
		ImmutableList<Intent> Harder(ImmutableList<Intent> moves) =>
			[
				.. moves.Select(i =>
					i.Kind == IntentType.Attack ? i with { Amount = Times(i.Amount, hit) } : i
				),
			];
		var max = Times(foe.MaxHp, hp);
		return foe with
		{
			Hp = max,
			MaxHp = max,
			Pattern = Harder(foe.Pattern),
			Components =
			[
				.. foe.Components.Select(c =>
					c is Phase phase ? phase with { Pattern = Harder(phase.Pattern) } : c
				),
			],
		};
	}
}
