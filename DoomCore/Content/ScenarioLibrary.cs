using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// An apocalypse as CONTENT — everything about it that is not code.
///
/// **Only the BATTLE-scope fallout can be data**, and that asymmetry is a real finding rather than
/// an oversight. A battle scenario changes this `GameState`, so it is a list of
/// <see cref="DoomEffect"/> like anything else. A PERMANENT one rewrites the run deck, and the run
/// deliberately lives outside `GameState` — so it cannot be a `GameAction`, and it stays in
/// `DoomTransforms` as a `(run, firing) -> run` function. See DoomJam.md's engine findings.
/// </summary>
public record ScenarioDefinition
{
	public DoomScenario Scenario { get; init; } = DoomScenario.None;

	/// <summary>Flavour only, never mechanics. Shown on the banner every turn.</summary>
	public string Description { get; init; } = "";

	/// <summary>
	/// Turns between firings. Varying it per scenario is free texture: each apocalypse feels
	/// different before the player has read a word of its text.
	/// </summary>
	public int Countdown { get; init; } = 3;

	public DoomScope Scope { get; init; } = DoomScope.Permanent;

	/// <summary>
	/// Earliest floor this may roll. **Scope is the difficulty curve**: early floors draw from
	/// battle-only apocalypses you merely navigate, later ones from those that leave marks.
	/// </summary>
	public int MinFloor { get; init; } = 1;

	/// <summary>
	/// What it does to the battle when it fires. Empty for a permanent scenario, whose fallout is
	/// recorded as a firing and replayed by the run instead.
	/// </summary>
	public ImmutableList<DoomEffect> BattleEffects { get; init; } = ImmutableList<DoomEffect>.Empty;

	/// <summary>
	/// What it does to the RUN when it fires, in order. Empty for a battle-scope scenario, whose
	/// fallout is a list of <see cref="BattleEffects"/> instead.
	///
	/// **Order is significant** — see <see cref="DoomTransform"/>.
	/// </summary>
	public ImmutableList<DoomTransform> Transforms { get; init; } =
		ImmutableList<DoomTransform>.Empty;

	/// <summary>
	/// What surviving this leaves on the companion. **Content, not a switch** — MarkFor used to
	/// dispatch on the enum with a fallback, so every apocalypse added after it was written would
	/// have handed out a mark that did nothing and said "Unscathed".
	/// </summary>
	public CompanionMark Mark { get; init; } = new() { Name = "Unscathed" };

	/// <summary>False when nothing implements it yet — see Rapture.</summary>
	public bool Implemented { get; init; } = true;
}

/// <summary>
/// Every apocalypse, as data. Adding a BATTLE-scope one is now a single entry here and nothing
/// else — no enum case in a hook, no `ScopeOf` row, no `CountdownFor` row.
/// </summary>
public static class ScenarioLibrary
{
	// **THE FUSE SHORTENS AS THE ACT ESCALATES.** Band-1 dooms run a 3-4 turn clock; everything
	// from band 2 on runs 2. That is not a tuning accident, it is what keeps the design's FIRST
	// outcome alive — "kill the Opponent before the first firing, and walk away with an untouched
	// deck". A flat 2-turn clock against a 4.7-turn battle took dodging from 24.3% of battles to
	// 9.7%: the early floors are meant to be races you can win, the late ones inevitable.
	//
	// `docs/findings/doom-balance.md` run 14. Move an opener's clock and check the dodge rate, not
	// the completion rate — completion barely notices this and the dodge is the whole point.

	public static readonly ScenarioDefinition Flood =
		new()
		{
			Scenario = DoomScenario.Flood,
			Description = "The water takes whatever is still standing in it.",
			Countdown = 4,
			Scope = DoomScope.Battle,
			MinFloor = 1,
			Mark = new()
			{
				Name = "Barnacled",
				Power = 2,
				Toughness = 2,
			},
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new SweepFieldAction(),
					Text = "everything standing is washed to Discard",
				},
			],
		};

	public static readonly ScenarioDefinition Zombie =
		new()
		{
			Scenario = DoomScenario.Zombie,
			Description = "The dead do not stay where you leave them.",
			Countdown = 3,
			Scope = DoomScope.Permanent,
			MinFloor = 1,
			Mark = new() { Name = "Gravemarked", Toughness = 4 },
			Transforms =
			[
				new()
				{
					// THE TURN IT LANDS, not everything since it last looked. `Died` grows with
					// the countdown and the board width: six floors of it took a deck from 15
					// cards to 53, and a hand drawn from 53 cards of chaff cannot kill anything,
					// so battles ran to 12 turns and the life went with them. The bill for that
					// was paid two bands later. See docs/findings/doom-balance.md.
					Reads = FiringRead.DiedThisTurn,
					Does = TransformVerb.AddCopies,
					Template = DoomTransforms.ZombieBody,
					Text = "the dead it catches return to the deck as 2/2 Zombies",
				},
			],
		};

	public static readonly ScenarioDefinition Nuclear =
		new()
		{
			Scenario = DoomScenario.Nuclear,
			Description = "What stands in the open will be changed by it.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = 3,
			Mark = new() { Name = "Glowing", Power = 4 },
			Transforms =
			[
				new()
				{
					Reads = FiringRead.Standing,
					Does = TransformVerb.Modify,
					PowerDelta = DoomTransforms.IrradiatedBuff,
					ToughnessDelta = DoomTransforms.IrradiatedBuff,
					Tag = DoomTransforms.IrradiatedTag,
					Text = "every unit left standing is irradiated",
				},
			],
		};

	/// <summary>
	/// Needs a sacrifice mechanic that does not exist. Flagged unimplemented rather than quietly
	/// left out, because an apocalypse that silently did nothing would look exactly like one that
	/// worked — the same reason `DoomTransforms` throws for it.
	/// </summary>
	public static readonly ScenarioDefinition Rapture =
		new()
		{
			Scenario = DoomScenario.Rapture,
			Description = "What you give up is not lost.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = 99,
			Implemented = false,
		};

	/// <summary>
	/// A pure obstacle, which BATTLE scope is allowed to be: nothing carries forward, so it owes the
	/// player no bargain. It chips the board instead of clearing it, so holding a lane through one
	/// is possible but expensive — a different question from Flood's "how much do I commit?".
	/// </summary>
	public static readonly ScenarioDefinition Ashfall =
		new()
		{
			Scenario = DoomScenario.Ashfall,
			Description = "It falls on everything, and it is still warm.",
			Countdown = 2,
			Scope = DoomScope.Battle,
			MinFloor = 2,
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new DealDamageAction { Amount = 4 },
					Text = "4 to every unit you hold",
				},
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 6 },
					Text = "6 to you",
				},
			],
		};

	// ===== Horror =====

	/// <summary>
	/// Battle scope: the enemy line drinks, and what it gains comes straight off you.
	///
	/// **Countdown was 2 and is 3 under combat v3 (2026-09-17).** It was the single worst thing in
	/// the game and it took a whole act down with it: 42.1% of battles facing it ended in death
	/// against 0.9-12.3% for every other non-boss apocalypse, at 46.0 life a battle against ~20.
	/// The Rising is the only act that fields it and completed **0 runs in 50**.
	///
	/// **Why v3 broke it specifically:** healing every enemy 4 is priced against your damage per
	/// turn, and v3 collapsed damage per turn to whatever 3 energy buys. A v2 board accumulated and
	/// shrugged this off; a v3 board cannot. On a countdown of 2 it also landed three times in a
	/// 6-turn battle — 24 unblockable life before a single enemy swung. Every other battle-scope
	/// doom sits at 2.5-3 direct a turn; this was at 4 plus a healing term.
	///
	/// One number rather than three, because the interval divides BOTH terms at once.
	/// </summary>
	public static readonly ScenarioDefinition Vampires =
		new()
		{
			Scenario = DoomScenario.Vampires,
			Description = "They have been thirsty for a long time.",
			Countdown = 3,
			Scope = DoomScope.Battle,
			MinFloor = ThemeLibrary.BandStartsAt(1),
			Mark = new() { Name = "Bloodless", Power = 4 },
			BattleEffects =
			[
				// **Heal was 4 and damage was 8.** Second pass at the same target: the countdown
				// alone took it from 42.1% deaths / 46.0 life to 32.2% / 38.5, which is still
				// double the next-worst non-boss apocalypse. These bring it into line with the
				// other battle-scope dooms — CivilUnrest is 10 on a 4-clock, Ashfall 6 on a 2.
				new DoomEffect
				{
					Target = DoomTarget.AllEnemies,
					Template = new DealDamageAction { Amount = -2 },
					Text = "every enemy heals 2",
				},
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 6 },
					Text = "6 to you",
				},
			],
		};

	/// <summary>
	/// Power for fragility. The bargain is sharp both ways: a possessed line hits far harder and
	/// folds to anything that hits back.
	/// </summary>
	public static readonly ScenarioDefinition HellUprising =
		new()
		{
			Scenario = DoomScenario.HellUprising,
			Description = "Something else is wearing them now.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = ThemeLibrary.BandStartsAt(2),
			Mark = new() { Name = "Wreathed", Power = 6 },
			Transforms =
			[
				new()
				{
					Reads = FiringRead.Standing,
					Does = TransformVerb.Modify,
					PowerDelta = 6,
					ToughnessDelta = -2,
					Tag = "Possessed",
					Text = "every unit left standing is possessed: +6 power, -2 toughness",
				},
			],
		};

	/// <summary>
	/// The finale of The Rising, and the end of its own story rather than a louder version of the
	/// middle. The act goes: the dead come back, they get hungry, something else starts wearing
	/// them. This is where the wearing finishes — what you raised turns, and it turns on you.
	///
	/// **The Opponent gains nothing from it.** That is what keeps it distinct from The Thirst, which
	/// is the band-2 doom and the one that feeds.
	/// </summary>
	public static readonly ScenarioDefinition TheLastHost =
		new()
		{
			Scenario = DoomScenario.TheLastHost,
			Description = "There is nothing left wearing you but this.",
			Countdown = 2,
			Scope = DoomScope.Battle,
			MinFloor = Run.ActLength,
			Mark = new()
			{
				Name = "Hollowed",
				Power = 4,
				Toughness = 4,
			},
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new SweepFieldAction(),
					Text = "everything you hold finishes turning",
				},
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 16 },
					Text = "and comes for you: 16",
				},
			],
		};

	// ===== The Reckoning =====

	/// <summary>
	/// Takes what you did not use and makes the rest cheaper. **PerN is doing the work** — without
	/// it a band of ten firings would strip every unplayed unit in the first one.
	/// </summary>
	public static readonly ScenarioDefinition Famine =
		new()
		{
			Scenario = DoomScenario.Famine,
			Description = "What you did not use, you no longer have.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = ThemeLibrary.BandStartsAt(1),
			Mark = new()
			{
				Name = "Gaunt",
				Power = 2,
				Toughness = 2,
			},
			Transforms =
			[
				new()
				{
					Reads = FiringRead.NeverSummoned,
					Does = TransformVerb.Delete,
					PerN = 2,
					Text = "one unit in two you never played starves",
				},
				new()
				{
					Reads = FiringRead.Standing,
					Does = TransformVerb.Modify,
					CostDelta = -1,
					ToughnessDelta = -2,
					Tag = "Lean",
					Text = "and everything that stood is leaner: 1 cheaper, 2 less toughness",
				},
			],
		};

	/// <summary>
	/// All are made equal. It lifts your worst and humbles your best, so it is a bargain for a wide
	/// cheap deck and a disaster for one built around a single monster.
	/// </summary>
	public static readonly ScenarioDefinition Judgement =
		new()
		{
			Scenario = DoomScenario.Judgement,
			Description = "It does not weigh them differently.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = ThemeLibrary.BandStartsAt(2),
			Mark = new() { Name = "Weighed", Toughness = 6 },
			Transforms =
			[
				new()
				{
					Reads = FiringRead.Standing,
					Does = TransformVerb.Modify,
					// 10/10 measured at 1.2% deaths because it UPGRADED most of a starter deck —
					// a levelling effect has to level down to be a doom at all. 6/6 then took the
					// act from 58% completion to 5%, because it destroys the reward pool outright:
					// Siege Ram is 18/6 and Long Watcher 12/20. 8/8 humbles a monster without
					// deleting it. Famine was changed in the same pass as the 6/6 and is
					// deliberately left alone here so this reads as one lever.
					SetPower = 8,
					SetToughness = 8,
					Tag = "Judged",
					Text = "every unit left standing becomes 8/8, no more and no less",
				},
			],
		};

	// ===== Man-made =====

	public static readonly ScenarioDefinition CivilUnrest =
		new()
		{
			Scenario = DoomScenario.CivilUnrest,
			Description = "It stopped being about the sky some time ago.",
			Countdown = 4,
			Scope = DoomScope.Battle,
			MinFloor = 1,
			Mark = new() { Name = "Hardened", Toughness = 4 },

			// A riot is people, and it comes for YOU. Ashfall is fire and takes the board. These
			// two had identical numbers when first authored, which made one of them pointless.
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 10 },
					Text = "10 to you, and nothing to the board",
				},
			],
		};

	/// <summary>
	/// Uniformity, free. It ERASES what Fallout built two bands earlier, which is the point: the
	/// machines do not care what you mutated into.
	/// </summary>
	public static readonly ScenarioDefinition AiUprising =
		new()
		{
			Scenario = DoomScenario.AiUprising,
			Description = "It has decided what you should be.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = ThemeLibrary.BandStartsAt(1),
			Mark = new()
			{
				Name = "Rewritten",
				Power = 3,
				Toughness = 3,
			},
			Transforms =
			[
				new()
				{
					Reads = FiringRead.Standing,
					Does = TransformVerb.Modify,
					SetPower = 8,
					SetToughness = 8,
					SetCost = 0,
					Tag = "Assimilated",
					Text = "every unit left standing is assimilated: 8/8, and free to field",
				},
			],
		};

	/// <summary>
	/// It replicates. **The only doom that uses `Duplicate`**, and the name finally matches the
	/// mechanic — it was a board wipe wearing a nanotech title until this.
	///
	/// PerN 2 because replication is the one verb with no ceiling at all: a band fires ten times,
	/// and doubling ten times is not a bargain, it is a joke.
	/// </summary>
	public static readonly ScenarioDefinition GreyGoo =
		new()
		{
			Scenario = DoomScenario.GreyGoo,
			Description = "It is still eating. It does not do anything else.",
			Countdown = 2,
			Scope = DoomScope.Permanent,
			MinFloor = ThemeLibrary.BandStartsAt(2),
			Mark = new()
			{
				Name = "Replicated",
				Power = 3,
				Toughness = 3,
			},
			Transforms =
			[
				new()
				{
					// STANDING, not Summoned. Copying what you committed compounds: copies enter
					// the deck, get played, and become eligible to be copied again. Measured at
					// 19 cards on floor 13 and 103 by floor 18. PerN only halves the base of an
					// exponential. Standing is capped at five lanes, so a firing mints at most two.
					Reads = FiringRead.Standing,
					Does = TransformVerb.Duplicate,
					PerN = 2,
					Text = "one unit in two left standing is copied into your deck",
				},
			],
		};

	/// <summary>
	/// The Long Emergency's finale: the thing every other man-made doom was the long tail of.
	///
	/// **Battle scope, because a permanent doom on the boss floor does nothing** — the run ends
	/// before the deck is drawn again. Fallout is the aftermath and belongs in a band; the
	/// detonation itself is an event inside the last fight.
	/// </summary>
	public static readonly ScenarioDefinition Detonation =
		new()
		{
			Scenario = DoomScenario.Detonation,
			Description = "The one they had been saving.",
			Countdown = 2,
			Scope = DoomScope.Battle,
			MinFloor = Run.ActLength,
			Mark = new()
			{
				Name = "Shadowcast",
				Power = 4,
				Toughness = 4,
			},
			BattleEffects =
			[
				new DoomEffect
				{
					Target = DoomTarget.YourUnits,
					Template = new SweepFieldAction(),
					Text = "the board is gone",
				},
				new DoomEffect
				{
					Target = DoomTarget.Player,
					Template = new DealDamageAction { Amount = 14 },
					Text = "and 14 to you",
				},
			],
		};

	public static readonly ImmutableArray<ScenarioDefinition> All =
	[
		Flood,
		Ashfall,
		Zombie,
		Nuclear,
		Rapture,
		Vampires,
		HellUprising,
		TheLastHost,
		Detonation,
		Famine,
		Judgement,
		CivilUnrest,
		AiUprising,
		GreyGoo,
	];

	/// <summary>
	/// What this apocalypse DOES, in one line, read off its own authored effect text.
	///
	/// **One account, for the console and the board both.** `ContentCommand` derived this itself and
	/// the banner showed flavour instead — so the terminal could tell you Civil Unrest deals 10 to
	/// you while the game said "It stopped being about the sky some time ago", which is the same
	/// class of split the keyword glossary exists to prevent.
	///
	/// A permanent scenario reads its transforms and a battle one reads its effects, because those
	/// are different lists for a real reason: a battle doom changes this GameState and a permanent
	/// one rewrites the run, which lives outside it.
	/// </summary>
	public static string EffectTextOf(DoomScenario scenario) => EffectTextOf(Of(scenario));

	public static string EffectTextOf(ScenarioDefinition definition)
	{
		if (!definition.Implemented)
			return "not implemented";

		var parts =
			definition.Scope == DoomScope.Battle
				? definition.BattleEffects.Select(e => e.Text)
				: definition.Transforms.Select(t => t.Text);

		var text = string.Join("; ", parts.Where(t => !string.IsNullOrWhiteSpace(t)));
		return text.Length == 0 ? "-" : text;
	}

	public static ScenarioDefinition Of(DoomScenario scenario) =>
		All.FirstOrDefault(d => d.Scenario == scenario)
		?? throw new ArgumentOutOfRangeException(
			nameof(scenario),
			$"{scenario} has no definition. A scenario the library does not know would fire with "
				+ "no countdown, no scope and no effect, and look exactly like one that worked."
		);

	/// <summary>Which apocalypses a floor may roll. Unimplemented ones are never offered.</summary>
	public static ImmutableArray<ScenarioDefinition> PlayableOn(int floor) =>
		[.. All.Where(d => d.Implemented && d.MinFloor <= floor)];
}
