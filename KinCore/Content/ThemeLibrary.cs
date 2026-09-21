using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// Which apocalypse you chose to live through. **Picked once, at the start of a run.**
///
/// A theme is not a reskin: it decides the whole SEQUENCE of dooms you face, and eventually the
/// enemies and cards you see with them. It is the closest thing this game has to picking a
/// character, and it does the job without a character existing.
/// </summary>
public enum KinTheme
{
	/// <summary>Things we built, turning on us. Riot, fallout, and the machines that outlast both.</summary>
	LongEmergency = 0,

	/// <summary>The dead, the thirsty, and whatever is wearing them by the end.</summary>
	Rising,

	/// <summary>Water, hunger, judgement, fire. An ending with an opinion about you.</summary>
	Reckoning,
}

/// <summary>
/// One theme: the dooms it walks you through, in order, and the one waiting at the end.
/// </summary>
public record ThemeDefinition
{
	public KinTheme Theme { get; init; } = KinTheme.LongEmergency;
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	/// <summary>
	/// **The thing waiting on this act's last floor.**
	///
	/// One Opponent used to fight all three finales, and the only thing separating them was a
	/// multiplier — which could not work, because a boss is a race and a race has a cliff rather
	/// than a slope. `BossScaleFor` was deleted when this arrived: three fights authored at the
	/// numbers they should be is both more tunable and more interesting than one fight times 1.45.
	/// </summary>
	public OpponentDefinition Boss { get; init; } = new();
}

/// <summary>
/// Every act: what it is called, and the thing waiting on its last floor.
/// </summary>
public static class ThemeLibrary
{
	/// <summary>
	/// **Man-made.** It stops being about the sky almost immediately.
	///
	/// Civil Unrest is the gentlest opening in the game — battle scope, and it comes for you rather
	/// than the board. AI Uprising then standardises everything standing to a free 8/8, and Grey
	/// Goo starts copying what you commit. Detonation is the thing all three were the long tail of.
	///
	/// **Fallout (`Nuclear`) is deliberately not here.** It is the aftermath, and the bomb reads
	/// better as the finale than as a middle band — which is also why the finale had to be battle
	/// scope, since a permanent doom on the boss floor rewrites a deck nobody draws again.
	/// </summary>
	public static readonly ThemeDefinition LongEmergency =
		new()
		{
			Theme = KinTheme.LongEmergency,
			Boss = EnemyLibrary.TheChoir,
			Name = "The Long Emergency",
			Description = "It did not arrive. It accumulated.",
		};

	/// <summary>
	/// **Horror.** Your deck fills with the dead, then the dead get hungry.
	///
	/// The Rising opens: permanent, but it only ADDS, which makes it the softest possible
	/// introduction to a doom that rewrites your deck. The Thirst is the mid-act tax — battle
	/// scope, the enemy line drinks what it takes off you. Hell Uprising is the rewrite you carry
	/// into the boss: +6 power and -2 toughness on everything standing, a line that hits like a
	/// truck and folds to a stiff breeze.
	/// </summary>
	public static readonly ThemeDefinition Rising =
		new()
		{
			Theme = KinTheme.Rising,
			Boss = EnemyLibrary.TheLastMorning,
			Name = "The Rising",
			Description = "They did not stay where you left them.",
		};

	/// <summary>
	/// **Religious.** Four endings with an opinion about what you deserve.
	///
	/// The Flood takes the board but leaves the cards — you lose the fight in front of you and
	/// nothing else. Famine is the only doom that THINS: what you never played starves, and what
	/// stood learns to cost less. Judgement flattens everything standing to 10/10, which lifts a
	/// deck of chaff and humbles one built around a single monster. Brimstone closes it.
	/// </summary>
	public static readonly ThemeDefinition Reckoning =
		new()
		{
			Theme = KinTheme.Reckoning,
			Boss = EnemyLibrary.TheLastWarden,
			Name = "The Reckoning",
			Description = "It has read the whole ledger.",
		};

	public static readonly ImmutableArray<ThemeDefinition> All = [LongEmergency, Rising, Reckoning];

	public static ThemeDefinition Of(KinTheme theme) =>
		All.FirstOrDefault(t => t.Theme == theme)
		?? throw new ArgumentOutOfRangeException(
			nameof(theme),
			$"No definition for {theme}. An act with no entry has no boss and no name."
		);
}
