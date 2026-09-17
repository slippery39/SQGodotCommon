using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// Placeholder content so the game is playable. Balance here is a guess, not a measurement —
/// every number is meant to be changed after the first real playtest.
/// </summary>
public static class StarterContent
{
	/// <summary>
	/// How many tickets a card of each rarity puts in the reward bag.
	///
	/// **A weight, never a gate.** Every card is offerable on every floor; a rare is simply a
	/// sixth as likely to come up as a common. That is what keeps an early rare possible, and an
	/// early rare you build the rest of the run around is the memorable run — see
	/// <see cref="DoomRarity"/>. Floor-gating the rares was measured and thrown away: it made the
	/// early bag small enough to CONCENTRATE the best card instead of hiding it, and it cost the
	/// runs worth remembering. `docs/findings/doom-balance.md` run 9 has the numbers.
	///
	/// With roughly seven commons, eight uncommons and three rares in an act's pool that puts a
	/// rare in a three-card offer about one screen in eight.
	/// </summary>
	public static int WeightOf(DoomRarity rarity) =>
		rarity switch
		{
			DoomRarity.Common => 6,
			DoomRarity.Uncommon => 3,
			DoomRarity.Rare => 1,
			_ => throw new ArgumentOutOfRangeException(
				nameof(rarity),
				$"No weight for {rarity}, so a card of that rarity would never be offered at all"
			),
		};

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
	/// Which apocalypses a floor is ALLOWED to hold. Nothing selects with this any more — the
	/// theme's schedule does — but it still answers "is this doom legal here", which is what the
	/// schedule is validated against.
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

	/// <summary>A unit that DOES something. Every unit in the game was a vanilla body before this.</summary>
	private static RunCard Unit(
		string name,
		int cost,
		int power,
		int toughness,
		string text,
		params DoomEffect[] effects
	) => Unit(name, cost, power, toughness, text) with { Effects = [.. effects] };

	/// <summary>
	/// An effect that fires when the apocalypse lands.
	///
	/// **Its text starts "Doom:", not "when the doom fires:".** The long form did not fit a card —
	/// "when the doom fires: 6 to every enemy" needed three lines in a two-line box and rendered
	/// TRUNCATED, which is the one thing DoomUI.md forbids outright: when text does not fit, the
	/// text is wrong, not the box. "Doom" is a keyword with reminder text in `KeywordLibrary`, so
	/// the short form still explains itself on hover — which is exactly what the glossary is for.
	/// </summary>
	private static DoomEffect On(DoomTarget target, GameAction template, string text) =>
		new()
		{
			Trigger = EffectTrigger.OnDoomFires,
			Target = target,
			Template = template,
			Text = text,
		};

	/// <summary>
	/// **The most frequent trigger in the game.** A turn effect fires 40-50 times in a run against
	/// 5-10 for a doom effect and 5-10 for a death — see `docs/findings/doom-balance.md` run 12.
	/// Price accordingly: small numbers, over and over.
	/// </summary>
	private static DoomEffect EachTurn(DoomTarget target, GameAction template, string text) =>
		new()
		{
			Trigger = EffectTrigger.OnTurnEnd,
			Target = target,
			Template = template,
			Text = text,
		};

	private static DoomEffect OnDeath(DoomTarget target, GameAction template, string text) =>
		new()
		{
			Trigger = EffectTrigger.OnDeath,
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

	/// <summary>
	/// The life budget, and **the single biggest number in the game**.
	///
	/// `mean floor = life budget / life lost per battle` has held across every content change for
	/// eleven measured runs, which makes this the one lever that moves difficulty without touching
	/// how a battle plays. It was 200 while a battle cost 29.6 life; cutting enemy and Opponent
	/// health by 30% took the bill to 22.2 a battle and completion to 74% against a 25% target —
	/// the same budget simply buys a third more battles now.
	///
	/// Rests heal a FRACTION of max (see <see cref="RestHealFor"/>), so they follow this down on
	/// their own and the ratio of healing to damage is preserved.
	/// </summary>
	public const int StartingLife = 120;

	public static Run NewRun(int seed = 1) =>
		new Run
		{
			Life = StartingLife,
			MaxLife = StartingLife,
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
	/// The pool is WEIGHTED BY RARITY and not gated by floor — floor 1 can offer anything floor 20
	/// can, just less often. See <see cref="WeightOf"/> for why it is not a gate.
	/// </summary>
	public static ImmutableArray<RunCard> SharedPool =>
		[
			Unit("Scrapper", 1, 10, 4, "Fast, and does not last."),
			Unit("Shieldbearer", 1, 4, 12, "Holds the line, and holds a spike."),
			Unit("Tunneller", 1, 8, 6, "Comes up where it is needed."),
			Unit("Rust Golem", 2, 10, 14, "Slow. Very hard to move.") with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit("Feral Pack", 2, 12, 8, "Hungry, and there are several.") with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit("Stray", 0, 6, 6, "Followed the noise."),
			Unit("Siege Ram", 3, 18, 6, "One job, done once.") with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit("Warden", 3, 12, 16, "The last thing still standing.") with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit("Bonepicker", 2, 14, 4, "Arrives after the fighting."),
			Unit("Long Watcher", 3, 12, 20, "Has seen four of these.") with
			{
				Rarity = DoomRarity.Rare,
			},
			Rite(
				"Scavenged Rounds",
				1,
				"Spend it on something that is already close.",
				OnPlay(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 6 },
					"6 to every enemy"
				)
			) with
			{
				Rarity = DoomRarity.Rare,
			},
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
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Rite(
				"Breaching Charge",
				2,
				"Straight past whatever is in the way.",
				OnPlay(
					DoomTarget.Opponent,
					new DealDamageAction { Amount = 14 },
					"14 to the Opponent"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
		];

	/// <summary>
	/// **The Long Emergency's own cards.** Its dooms read what is STANDING (AI Uprising) and what
	/// you COMMITTED (Grey Goo), so the act rewards putting bodies down and keeping them there.
	/// These lean into that: they want to still be on the field when the clock runs out.
	/// </summary>
	private static ImmutableArray<RunCard> LongEmergencyCards =>
		[
			// **Was a 1-cost 2/14 and measured −0.45** — the only card in the game with a negative
			// delta, over 215 takes. Not a pricing problem: 16 total stats beats the 6/6 starter
			// on paper. It is that 2 power never threatens anything, and the pool has said twice
			// that POWER is what pays and toughness barely does — Shieldbearer at 4/12 is +0.16
			// and equally dead, while Tunneller at 8/6 is +1.82.
			//
			// 8/10 clears the starter in the direction that matters and keeps the body that this
			// act wants standing when the clock runs out.
			Unit("Riot Shield", 1, 8, 10, "Issued for a crowd, not for this."),
			Unit(
				"Salvage Rig",
				2,
				4,
				8,
				"It keeps working through it. That is all it does.",
				On(DoomTarget.None, new DrawCardsAction { Amount = 2 }, "Doom: draw 2")
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit(
				"Drone Swarm",
				2,
				6,
				6,
				"Somebody's fleet, still flying the last order it got.",
				On(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 6 },
					"Doom: 6 to every enemy"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit(
				"Reactor Crew",
				3,
				8,
				12,
				"They stayed at the desk.",
				On(DoomTarget.Player, new GainLifeAction { Amount = 10 }, "Doom: gain 10")
			) with
			{
				Rarity = DoomRarity.Rare,
			},
		];

	/// <summary>
	/// **The Rising's own cards**, and the act that asks for two opposite things. Zombie reads what
	/// DIED, in the first band; Hell Uprising reads what was LEFT STANDING, in the last. So the
	/// early game wants bodies worth losing and the late game wants bodies that hold — which is why
	/// these are split between cards that pay out on death and one that pays out on surviving.
	/// </summary>
	private static ImmutableArray<RunCard> RisingCards =>
		[
			// **The death trigger is here on EVIDENCE, not by default.** It was swapped for
			// "each turn: 3 to the Opponent" on the frequency argument — a turn trigger fires far
			// more often than a death — and measured −2.77 against this card's +0.43. Restoring
			// the body alone did not recover it, so the trigger was the damage.
			//
			// Why the frequency argument failed here: **OnTurnEnd only pays if the unit is still
			// standing at the end of the turn.** A 1-cost body in a contested lane usually is not,
			// so the "40-50 firings a run" this was costed at never happened. Almoner is the card
			// that makes the trigger look good and it is a 2/8 — the toughness IS the engine.
			Unit(
				"Gravedigger",
				1,
				8,
				6,
				"He has been busy. He is not finished.",
				OnDeath(
					DoomTarget.Opponent,
					new DealDamageAction { Amount = 16 },
					"on death: 16 to the Opponent"
				)
			),
			// Restored alongside Gravedigger, same measurement and same reason: "each turn: 3 to
			// every enemy" read −1.30 against this card's +0.86, because a 2-cost body in a
			// contested lane is rarely alive at end of turn to fire it.
			Unit(
				"Pyre Tender",
				2,
				10,
				8,
				"Burning them is the only thing that has worked.",
				OnDeath(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 12 },
					"on death: 12 to every enemy"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// The act keeps ONE doom payoff — it is the act's own clock and something should pay
			// off it. What it lacked was anything between firings, so the per-turn line is added
			// rather than swapped in: 3 a turn is the Almoner lesson (a 1-cost gaining 2 a turn
			// measured +1.83, against this card's +0.40 for gaining 12 on the doom).
			Unit(
				"The Choirmaster",
				3,
				12,
				18,
				"Still conducting. Nobody told him.",
				On(DoomTarget.Player, new GainLifeAction { Amount = 12 }, "Doom: gain 12"),
				EachTurn(DoomTarget.Player, new GainLifeAction { Amount = 3 }, "each turn: gain 3")
			) with
			{
				Rarity = DoomRarity.Rare,
			},
			Rite(
				"Blood Price",
				1,
				"It costs what it costs.",
				OnPlay(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 10 },
					"10 to every enemy"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
		];

	/// <summary>
	/// **The Reckoning's own cards.** Famine takes what you never played and Judgement flattens
	/// everything standing to 6/6, so the act punishes hoarding and punishes monsters. Cheap and
	/// plentiful is correct here, and nothing should be precious.
	/// </summary>
	private static ImmutableArray<RunCard> ReckoningCards =>
		[
			Unit("Penitent", 0, 4, 4, "Walked here. Will walk further."),
			Unit(
				"Almoner",
				1,
				2,
				8,
				"Gives away what little is left.",
				EachTurn(DoomTarget.Player, new GainLifeAction { Amount = 2 }, "each turn: gain 2")
			),
			Unit(
				"Reliquary Guard",
				2,
				4,
				10,
				"Guarding a box nobody has opened.",
				On(DoomTarget.Player, new GainLifeAction { Amount = 14 }, "Doom: gain 14")
			) with
			{
				Rarity = DoomRarity.Rare,
			},
			Rite(
				"Tithe",
				1,
				"Give it up before it is taken.",
				OnPlay(DoomTarget.None, new DrawCardsAction { Amount = 2 }, "draw 2")
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
		];

	/// <summary>
	/// What a floor may offer: the shared core every act draws from, plus the act's own cards.
	///
	/// **Shared core plus a themed slice, not three separate pools.** Three pools would thin the
	/// variety in each act, and if acts are ever chained into one run the pools would have to be
	/// merged anyway — this composes for free.
	/// </summary>
	public static ImmutableArray<RunCard> RewardPool(DoomTheme theme) =>
		[
			.. SharedPool,
			.. (
				theme switch
				{
					DoomTheme.LongEmergency => LongEmergencyCards,
					DoomTheme.Rising => RisingCards,
					DoomTheme.Reckoning => ReckoningCards,
					_ => throw new ArgumentOutOfRangeException(
						nameof(theme),
						$"No card slice for {theme}. A theme with no cards of its own would be a "
							+ "reskin of the shared pool and nothing else."
					),
				}
			).Select(c => c with { Theme = theme }),
		];

	/// <summary>
	/// Three distinct cards to choose between, deterministic from the seed and floor so a run
	/// replays exactly — the same property that makes a bug report actionable.
	/// </summary>
	public static ImmutableArray<RunCard> RewardsFor(
		DoomTheme theme,
		int seed,
		int floor,
		int count = 3
	)
	{
		var pool = RewardPool(theme).ToList();
		var rng = new Random(seed * 104729 + floor * 31);
		var picked = new List<RunCard>();

		// WEIGHTED, and without replacement so the three offers stay distinct. Every card is in
		// the bag on every floor — rarity only decides how many tickets it holds, so an early rare
		// is uncommon rather than impossible.
		for (var i = 0; i < count && pool.Count > 0; i++)
		{
			var roll = rng.Next(pool.Sum(card => WeightOf(card.Rarity)));
			var index = 0;

			while (roll >= WeightOf(pool[index].Rarity))
				roll -= WeightOf(pool[index++].Rarity);

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
		// **LaneCount - 1, so one lane is always open.** At five enemies across five lanes there
		// is no open lane at all and the Opponent cannot be damaged until you kill something,
		// while it heals every turn — that is where the 40-55 turn battles came from. Cutting
		// health lowered the average battle and left that tail untouched, because the tail is this
		// structure rather than any health total.
		var count = Math.Min(2 + floor / 6, DoomBattle.LaneCount - 1);
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
	/// Which apocalypse waits on a floor. **Decided by the theme's schedule, not by a roll** — a
	/// run of a theme always faces the same escalation, which is what makes it a story. The seed
	/// still varies the enemies, the rewards and the Opponent traits.
	/// </summary>
	public static DoomScenario ScenarioFor(DoomTheme theme, int floor) =>
		ThemeLibrary.ScenarioFor(theme, floor);
}
