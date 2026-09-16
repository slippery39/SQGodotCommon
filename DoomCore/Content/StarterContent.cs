using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Placeholder content so the game is playable. Balance here is a guess, not a measurement —
/// every number is meant to be changed after the first real playtest.
/// </summary>
public static class StarterContent
{
	/// <summary>Countdown length per scenario — content, see <see cref="ScenarioLibrary"/>.</summary>
	public static int CountdownFor(DoomScenario scenario) => ScenarioLibrary.Of(scenario).Countdown;

	/// <summary>
	/// What the apocalypse does, in one line, for the banner that is always on screen.
	///
	/// **Flavour only — never mechanics.** Nothing a player needs in order to decide comes from
	/// here, so this going stale can mislead about tone but never about rules.
	/// </summary>
	public static string DescriptionFor(DoomScenario scenario) =>
		ScenarioLibrary.Of(scenario).Description;

	/// <summary>
	/// What a scenario is allowed to change. **A fixed property of its design**, and the thing that
	/// decides which hook implements it — see <see cref="DoomScope"/>.
	/// </summary>
	public static DoomScope ScopeOf(DoomScenario scenario) => ScenarioLibrary.Of(scenario).Scope;

	/// <summary>
	/// Which apocalypses a floor may roll. **Scope is the difficulty curve**: early floors get
	/// battle-only ones you merely navigate, later floors get ones that leave marks on the run.
	/// Rapture is excluded everywhere by being flagged unimplemented.
	/// </summary>
	public static ImmutableArray<DoomScenario> PlayableOn(int floor) =>
		[.. ScenarioLibrary.PlayableOn(floor).Select(d => d.Scenario)];

	/// <summary>
	/// A card that is not a body. It resolves, does its thing, and goes to Discard.
	///
	/// **The first cards in this game that are not units.** Effects are declared as data — a
	/// trigger, a target rule and an action template — so a new one needs no engine change. See
	/// DoomEffect.
	/// </summary>
	private static RunCard Rite(string name, int cost, string text, params DoomEffect[] effects) =>
		new()
		{
			Name = name,
			Cost = cost,
			Description = text,
			IsUnit = false,
			Effects = [.. effects],
		};

	private static DoomEffect OnPlay(DoomTarget target, GameAction template, string text) =>
		new()
		{
			Trigger = EffectTrigger.OnPlay,
			Target = target,
			Template = template,
			Text = text,
		};

	private static RunCard Unit(string name, int cost, int power, int toughness, string text) =>
		new()
		{
			Name = name,
			Cost = cost,
			Description = text,
			IsUnit = true,
			Power = power,
			Toughness = toughness,
		};

	/// <summary>The starting companion. One for now; picking between several is a later job.</summary>
	public static Companion StarterCompanion =>
		new()
		{
			Name = "Ash",
			Description = "Followed you out of the first one. Has not left since.",
			BasePower = 2,
			BaseToughness = 6,
		};

	public static Run NewRun(int seed = 1) =>
		new Run
		{
			Life = 200,
			MaxLife = 200,
			RngSeed = seed,
			Companion = StarterCompanion,
		}.WithCards(
			[
				Unit("Scavenger", 1, 6, 6, "Takes what is left."),
				Unit("Scavenger", 1, 6, 6, "Takes what is left."),
				Unit("Scavenger", 1, 6, 6, "Takes what is left."),
				Unit("Scavenger", 1, 6, 6, "Takes what is left."),
				Unit("Bulwark", 1, 2, 10, "Stands in the way."),
				Unit("Bulwark", 1, 2, 10, "Stands in the way."),
				Unit("Bulwark", 1, 2, 10, "Stands in the way."),
				Unit("Ash Walker", 2, 8, 10, "Walked out of the last one."),
				Unit("Ash Walker", 2, 8, 10, "Walked out of the last one."),
				Unit("Lantern Bearer", 0, 4, 4, "Small light, long night."),
			]
		);

	/// <summary>
	/// What a cleared floor can offer. **This is the only progression that works from floor 1.**
	///
	/// The dooms are supposed to BE the power curve, but `PlayableOn` gives battle-scope Flood only
	/// below floor 3, and a battle-scope doom changes nothing permanent — so a deck could not
	/// improve at all until floor 3 while enemy health more than doubled. Rewards close that gap.
	///
	/// **Every entry must beat a starter card**, which is why Ash Walker and Bulwark are no longer
	/// in here: they ARE starter cards, so drawing them as a reward was an upgrade of nothing. The
	/// deck you build has to be visibly better than the deck you were handed.
	///
	/// **Power, not toughness.** Toughness absorbs damage once and never heals; power removes the
	/// source of it permanently. Measured: every purely defensive body in the old pool was worth
	/// nothing at all — see `docs/findings/doom-balance.md`. The walls kept their toughness and were
	/// given enough power to matter.
	///
	/// **This pool moves whenever the starter deck moves.** Buffing the starters to 3/3 and 4/5 left
	/// half of these below the cards they were supposed to replace, and the measured value of the
	/// whole pool collapsed toward zero — a reward you would not play is not a reward. The benchmark
	/// is the starter card of the same cost, beaten clearly, with the surplus in POWER.
	///
	/// The pool is FLAT — floor 10 offers the same cards as floor 1. `MinFloor` on a RunCard is the
	/// obvious next step.
	/// </summary>
	public static ImmutableArray<RunCard> RewardPool =>
		[
			Unit("Scrapper", 1, 10, 4, "Fast, and does not last."),
			Unit("Shieldbearer", 1, 4, 12, "Holds the line, and holds a spike."),
			Unit("Tunneller", 1, 8, 6, "Comes up where it is needed."),
			Unit("Rust Golem", 2, 10, 14, "Slow. Very hard to move."),
			Unit("Feral Pack", 2, 12, 8, "Hungry, and there are several."),
			Unit("Stray", 0, 6, 6, "Followed the noise."),
			Unit("Siege Ram", 3, 18, 6, "One job, done once."),
			Unit("Warden", 3, 12, 16, "The last thing still standing."),
			Unit("Bonepicker", 2, 14, 4, "Arrives after the fighting."),
			Unit("Long Watcher", 3, 12, 20, "Has seen four of these."),
			Rite(
				"Scavenged Rounds",
				1,
				"Spend it on something that is already close.",
				OnPlay(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 6 },
					"6 to every enemy"
				)
			),
			Rite(
				"Field Dressing",
				1,
				"It will hold. It will not heal.",
				OnPlay(DoomTarget.Player, new GainLifeAction { Amount = 12 }, "gain 12 life")
			),
			Rite(
				"Last Orders",
				2,
				"Everyone takes what they can carry.",
				OnPlay(DoomTarget.Player, new DrawCardsAction { Amount = 3 }, "draw 3")
			),
			Rite(
				"Breaching Charge",
				2,
				"Straight past whatever is in the way.",
				OnPlay(
					DoomTarget.Opponent,
					new DealDamageAction { Amount = 14 },
					"14 to the Opponent"
				)
			),
		];

	/// <summary>
	/// Three distinct cards to choose between, deterministic from the seed and floor so a run
	/// replays exactly — the same property that makes a bug report actionable.
	/// </summary>
	public static ImmutableArray<RunCard> RewardsFor(int seed, int floor, int count = 3)
	{
		var pool = RewardPool.ToList();
		var rng = new Random(seed * 104729 + floor * 31);
		var picked = new List<RunCard>();

		for (var i = 0; i < count && pool.Count > 0; i++)
		{
			var index = rng.Next(pool.Count);
			picked.Add(pool[index]);
			pool.RemoveAt(index);
		}

		return [.. picked];
	}

	/// <summary>
	/// What is on a floor. **Every fourth floor is a rest, plus the one before the boss, and the
	/// last floor never is** — the act has to end on the thing you came for.
	///
	/// The rest at `ActLength - 1` is the campfire before the boss, and it is not decoration:
	/// floors 17-20 were four unbroken battles at the hardest tier and clear rates fell to 22% by
	/// the end. You should arrive at the last thing having had a moment to bind what is bleeding.
	///
	/// Five rests across twenty floors means fifteen battles. That ratio is the single biggest lever
	/// on whether an act can be finished at all, because it sets both how many fights the life
	/// budget must cover and how much of it comes back.
	/// </summary>
	public static FloorKind FloorKindFor(int floor) =>
		(floor % 4 == 0 || floor == Run.ActLength - 1) && floor != Run.ActLength
			? FloorKind.Rest
			: FloorKind.Battle;

	/// <summary>
	/// What a rest gives back: 30% of max, the Slay the Spire number.
	///
	/// A FRACTION of max rather than a flat amount, so it keeps its meaning if the life budget ever
	/// moves again — and it has moved twice already.
	/// </summary>
	public static int RestHealFor(int maxLife) => maxLife * 3 / 10;

	/// <summary>
	/// Which Opponent waits on a floor — see <see cref="EnemyLibrary.ForFloor"/>.
	///
	/// This used to be `20 + floor * 6`, which gave every floor the same faceless body with a bigger
	/// number. An Opponent is content now: it has a name, a reinforcement of its own, and effects.
	/// </summary>
	public static OpponentDefinition OpponentFor(int floor, int seed)
	{
		var body = EnemyLibrary.ForFloor(floor);
		var traits = EnemyLibrary.TraitsFor(seed);

		// How many battles this Opponent has already fielded, including this one. Counting the
		// floors below rather than tracking history keeps this a PURE function of (floor, seed) —
		// the front end and the simulator can each ask it cold and get the same answer.
		// Max(0) because a REST floor fields nobody: the count comes back zero there and the
		// subtraction would run off the end of the trait list. Asking a rest floor who its
		// Opponent is should answer, not throw.
		var nth = Math.Max(
			0,
			Enumerable
				.Range(1, floor)
				.Count(f =>
					FloorKindFor(f) == FloorKind.Battle
					&& EnemyLibrary.ForFloor(f).Name == body.Name
				) - 1
		);

		// **No Opponent is fought twice with the same trait.** The longest any Opponent holds the
		// curve is six battles and there are six traits, so within a run nth never wraps. If a
		// future roster gives one Opponent more floors than there are traits, it wraps rather than
		// throwing — a repeated fight is worse content, not a broken run.
		return traits[nth % traits.Length].ApplyTo(body);
	}

	/// <summary>
	/// Kept because the console and the tests still speak in plain health. Reads the definition
	/// rather than recomputing a formula, so there is one answer to "how tough is this floor".
	/// </summary>
	public static int OpponentHealthFor(int floor) => EnemyLibrary.ForFloor(floor).Health;

	/// <summary>
	/// What the Opponent puts back into an open lane, and it comes with its effects.
	///
	/// The BODY is content — whichever reinforcement this Opponent fields. The scaling on top is a
	/// tuning dial, and it is on the TURN rather than the floor on purpose: that is what stops a
	/// stalled battle being a safe one. The longer you fail to break through, the worse the thing
	/// you have to break through.
	/// </summary>
	public static PendingSummon SummonFor(int turnNumber, int lane, int floor = 1)
	{
		var body = EnemyLibrary.ForFloor(floor).Reinforcement;

		return body.ToSummon(lane) with
		{
			Health = body.Health + turnNumber,
			Attack = body.Attack + turnNumber / 2,
		};
	}

	/// <summary>
	/// The enemies for a floor, already placed in lanes and carrying their own behaviour.
	///
	/// **Lanes need more than one enemy to be a decision.** One enemy across five lanes is covered
	/// by a single unit and stops being a threat; the count is what makes "which lanes do I contest"
	/// cost something.
	///
	/// **The ramp was `floor / 3`, which filled all five lanes by floor 9.** Energy is a flat 3 and
	/// never grows, so a five-lane board cannot be contested at all — two lanes leaked every turn
	/// and late battles cost 20 life apiece regardless of play. `floor / 6` reaches five lanes at
	/// floor 18 instead. See `docs/findings/doom-balance.md`.
	///
	/// Bodies come from <see cref="EnemyLibrary"/> rather than from a health formula, so an enemy
	/// has an identity and can do something. Which ones a floor may field is the difficulty curve,
	/// written as content — see `EnemyLibrary.PlayableOn`.
	///
	/// Enemies are spread from the outside in, so the companion's centre lane is the LAST one
	/// contested. A free blocker pre-matched with the only enemy would make the opening turn decide
	/// itself.
	/// </summary>
	public static IReadOnlyList<Enemy> EnemiesFor(int floor, int seed = 0)
	{
		var count = Math.Min(2 + floor / 6, DoomBattle.LaneCount);
		var roster = EnemyLibrary.PlayableOn(floor);
		var rng = new Random(seed * 7717 + floor);

		int[] order = [0, 4, 1, 3, 2];

		return Enumerable
			.Range(0, count)
			.Select(i =>
			{
				// The hardest thing the floor allows leads, so a new tier is felt the moment it
				// unlocks rather than waiting on a lucky roll.
				var definition =
					i == 0 ? roster.MaxBy(e => e.MinFloor)! : roster[rng.Next(roster.Length)];

				return definition.ToEnemy(order[i]);
			})
			.ToList();
	}

	/// <summary>
	/// Which apocalypse waits on a floor. Deterministic from the seed so a run is reproducible —
	/// the console prints the seed, which is what makes a bug report actionable.
	/// </summary>
	public static DoomScenario ScenarioFor(int seed, int floor)
	{
		var pool = PlayableOn(floor);
		return pool[new Random(seed * 7919 + floor).Next(pool.Length)];
	}
}
