using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>
/// **LEVELS** (Shayne, 2026-09-27: "a level system … so we could design areas so that we only see
/// monsters of a certain level, which gives us a way to discuss balancing"). Every creature has a
/// level; its HP, Power and move amounts are its BASE × <see cref="Factor"/>. **The content's own
/// numbers are the base, at <see cref="Base"/>**, so nothing already authored moves at Lv 5.
///
/// Wild levels come from the region (`Region.MinLevel/MaxLevel`); yours grow from XP. Every number is
/// a guess (exploring, not tuning) — the point is that there is now ONE handle to turn.
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

	/// <summary>**A monster of yours at a level**: HP, Power and its moves scaled.</summary>
	public static PartyCompanion Scale(PartyCompanion companion, int level) =>
		companion with
		{
			Hp = At(companion.Hp, level),
			Power = At(companion.Power, level),
			Moves = Scaled(companion.Moves, level),
			Level = level,
		};

	// ===== XP

	/// <summary>XP to grow from this level to the next.</summary>
	/// <remarks>22 × level, tuned with `party-sim` (2026-09-27): at 30 the team trailed every leader
	/// by 1.5–2.5 levels and the routes after them killed.</remarks>
	public static int XpToNext(int level) => 22 * level;

	/// <summary>
	/// **What a won fight is worth to each monster on the team**: 12 × the foes' average level — a
	/// fight is a fight, however many foes — doubled for a leader.
	/// </summary>
	public static int XpFor(IEnumerable<Foe> foes, bool leader)
	{
		var list = foes.ToList();
		if (list.Count == 0)
			return 0;
		var xp = (int)Math.Round(12 * list.Average(f => f.Level));
		return leader ? xp * 2 : xp;
	}

	/// <summary>
	/// **XP in; levels out.** A level gained raises its max HP, and its current HP by the same amount.
	/// </summary>
	public static RunCompanion Gain(RunCompanion m, int xp)
	{
		var (level, pool) = (m.Level, m.Xp + xp);
		while (pool >= XpToNext(level))
			pool -= XpToNext(level++);
		var grown = m with { Level = level, Xp = pool };
		return grown with { Hp = Math.Max(0, m.Hp + grown.MaxHp - m.MaxHp) };
	}
}
