using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// Which apocalypse you chose to live through. **Picked once, at the start of a run.**
///
/// A theme is not a reskin: it decides the whole SEQUENCE of dooms you face, and eventually the
/// enemies and cards you see with them. It is the closest thing this game has to picking a
/// character, and it does the job without a character existing.
/// </summary>
public enum DoomTheme
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
	public DoomTheme Theme { get; init; } = DoomTheme.LongEmergency;
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";

	/// <summary>
	/// One apocalypse per band, in descending order. A band is
	/// <see cref="ThemeLibrary.FloorsPerBand"/> floors; the last band covers any remainder.
	/// </summary>
	public ImmutableArray<DoomScenario> Bands { get; init; } = [];

	/// <summary>
	/// The apocalypse on the boss floor, and nowhere else.
	///
	/// **It must be BATTLE scope.** A permanent transform on the final floor rewrites a deck that
	/// is never drawn again — `Run.AfterBattle` applies it and then the act ends — so a permanent
	/// doom here is a no-op with an animation. That it is also the cheapest kind to author is a
	/// happy accident.
	/// </summary>
	public DoomScenario FinalDoom { get; init; } = DoomScenario.None;
}

/// <summary>
/// A stretch of the act that holds one apocalypse, inclusive at both ends.
/// </summary>
public record DoomBand
{
	public int FirstFloor { get; init; }
	public int LastFloor { get; init; }
	public DoomScenario Doom { get; init; }

	/// <summary>True when the band is a single floor — the boss, today.</summary>
	public bool IsOneFloor => FirstFloor == LastFloor;
}

/// <summary>
/// Every theme, and the schedule that turns a floor into an apocalypse.
///
/// **The doom is a SCHEDULE, not a roll.** It used to be drawn at random from everything legal for
/// the floor. Sequencing it is what lets a theme tell an escalating story, and it costs nothing the
/// design did not already want: "certainty is permission to show the player everything" — the whole
/// act can be laid out at run start, which is the theme-select screen and the pitch in one.
/// </summary>
public static class ThemeLibrary
{
	/// <summary>
	/// Floors per band. Twenty floors is three bands of six plus the rest and the boss.
	/// </summary>
	public const int FloorsPerBand = 6;

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
			Theme = DoomTheme.LongEmergency,
			Name = "The Long Emergency",
			Description = "It did not arrive. It accumulated.",
			Bands = [DoomScenario.CivilUnrest, DoomScenario.AiUprising, DoomScenario.GreyGoo],
			FinalDoom = DoomScenario.Detonation,
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
			Theme = DoomTheme.Rising,
			Name = "The Rising",
			Description = "They did not stay where you left them.",
			Bands = [DoomScenario.Zombie, DoomScenario.Vampires, DoomScenario.HellUprising],
			FinalDoom = DoomScenario.TheLastHost,
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
			Theme = DoomTheme.Reckoning,
			Name = "The Reckoning",
			Description = "It has read the whole ledger.",
			Bands = [DoomScenario.Flood, DoomScenario.Famine, DoomScenario.Judgement],
			FinalDoom = DoomScenario.Ashfall,
		};

	public static readonly ImmutableArray<ThemeDefinition> All = [LongEmergency, Rising, Reckoning];

	public static ThemeDefinition Of(DoomTheme theme) =>
		All.FirstOrDefault(t => t.Theme == theme)
		?? throw new ArgumentOutOfRangeException(
			nameof(theme),
			$"No definition for {theme}. A theme with no entry would have no dooms at all."
		);

	/// <summary>
	/// Which apocalypse waits on a floor. **Deterministic and seedless** — the schedule is the
	/// same every run of a theme, which is what makes it a story rather than a shuffle.
	/// </summary>
	/// <summary>
	/// The whole act as FLOOR BANDS: each run of consecutive floors that share an apocalypse, in
	/// order. This is what the theme-select screen shows, and it is the entire pitch — the doom is
	/// a schedule, so the player can be told all of it before drawing a card.
	///
	/// It asks <see cref="ScenarioFor"/> for every floor and collapses the repeats rather than
	/// reading <see cref="ThemeDefinition.Bands"/> and redoing the band arithmetic. A second copy
	/// of that arithmetic is a second schedule, and the first time the two disagreed the screen
	/// would be advertising a run nobody plays.
	/// </summary>
	public static ImmutableArray<DoomBand> BandsOf(DoomTheme theme)
	{
		var bands = ImmutableArray.CreateBuilder<DoomBand>();
		var first = 1;

		for (var floor = 1; floor <= Run.ActLength; floor++)
		{
			var doom = ScenarioFor(theme, floor);

			if (floor < Run.ActLength && ScenarioFor(theme, floor + 1) == doom)
				continue;

			bands.Add(
				new DoomBand
				{
					FirstFloor = first,
					LastFloor = floor,
					Doom = doom,
				}
			);
			first = floor + 1;
		}

		return bands.ToImmutable();
	}

	public static DoomScenario ScenarioFor(DoomTheme theme, int floor)
	{
		var definition = Of(theme);

		if (floor >= Run.ActLength)
			return definition.FinalDoom;

		// The last band absorbs any remainder, so a floor count that is not a clean multiple of
		// the band size still resolves rather than running off the end.
		var band = Math.Clamp((floor - 1) / FloorsPerBand, 0, definition.Bands.Length - 1);
		return definition.Bands[band];
	}
}
