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
	/// End of turn, once the lanes have resolved.
	///
	/// **Combat v3 made this fire exactly ONCE for an ordinary unit, and the text must not say
	/// "each turn".** A unit withdraws at the end of the turn it was played, so an `OnTurnEnd`
	/// effect on it is an `OnPlay` that happens later — Almoner and The Choirmaster both shipped
	/// saying "each turn: gain N" and both were lying on the card face. Rules text is not cosmetic.
	///
	/// The trigger itself is kept, and becomes honest again for a `Persistent` unit that really
	/// does survive into the next turn. **Until persistence exists, do not author "each turn" text
	/// against it.**
	///
	/// Was: "the most frequent trigger in the game, 40-50 firings a run" — true of v2, where a unit
	/// held its lane until something killed it.
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

	/// <summary>
	/// The starting companion, and **the first card in this game with an ability that a deck can be
	/// built around**.
	///
	/// **The body was 2/6 and is 6/12.** It was a third of a one-drop — the pool scaled up over
	/// eleven balance passes and the companion never came with it, so the one permanent thing on
	/// the board was also the least relevant. 6/12 puts it beside a 1-cost.
	///
	/// **The ability: +2/+0 for every unit of yours the enemy killed last turn, and it STACKS
	/// within a battle.** Two readings were possible and this is the cheap one on purpose:
	///
	/// - *cumulative* (this) — a triggered `BuffAction`, which the effect system already does. Ash
	///   grows through a grinding battle and resets when the next one starts. No new machinery.
	/// - *recalculated* — a bonus that rises and falls with what died each turn. That is a
	///   CONTINUOUS effect, which means a layer system this game has deliberately not built.
	///
	/// **It only means anything because withdrawn ≠ dead** (combat v3): it reads units the enemy
	/// took, not the four that walked off the board at end of turn. That rule was tidiness when it
	/// was written and is load-bearing now.
	///
	/// **OnTurnStart is the right trigger and the companion is the ONLY thing that can use it** —
	/// the field is empty when a turn begins, so a card carrying this trigger would never fire.
	/// `NoCardDeclaresOnTurnStart` holds that.
	/// </summary>
	public static Companion StarterCompanion =>
		new()
		{
			Name = "Ash",
			Description = "Followed you out of the first one. Has not left since.",
			BasePower = 6,
			BaseToughness = 12,
			Effects =
			[
				new DoomEffect
				{
					Trigger = EffectTrigger.OnTurnStart,
					Target = DoomTarget.Self,
					Template = new BuffAction { Power = 2, PerEach = CountOf.DiedLastTurn },
					Text = "+2/+0 per Loss",
				},
			],
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
	/// **RE-CUT FOR COMBAT v3 (2026-09-17). Two measured rules decided nearly all of it:**
	///
	/// **1. NOTHING COSTS 3.** Energy is 3 a turn and a lane is the resource, so a 3-cost spends the
	/// whole turn holding ONE lane where two 1-costs hold two. Run 15 measured the consequence and it
	/// is brutal: Siege Ram +0.30, The Choirmaster +0.67, Long Watcher +2.51, against +5.12 for a
	/// 0-cost 4/4 and +4.81 for a 1-cost. Every 3-cost is now a 2-cost with smaller numbers.
	///
	/// **2. ON-DEATH TRIGGERS ARE DEAD, but READING deaths is not.** A unit withdraws at end of turn
	/// (see DoomJam.md "Combat v3"), so it rarely dies, so Gravedigger — "on death: 16 to the
	/// Opponent" — measured **-0.26**, the only negative card in the game. Replaced throughout by
	/// <see cref="CountOf.DiedLastTurn"/>, which reads what the ENEMY killed across your whole board
	/// instead of requiring this particular card to be the thing that died.
	///
	/// **Card NAMES are deliberately unchanged.** `DoomArt` resolves art by name from 52 authored
	/// SVGs, so renaming a card silently downgrades it to a generated figure. Re-stat and re-ability
	/// the names that exist; a genuinely new name is an art debt and should be taken knowingly.
	///
	/// **Every entry must beat a starter card**, which is why Ash Walker and Bulwark are not in here:
	/// they ARE starter cards. **Power, not toughness** — measured repeatedly, and still true in v3.
	///
	/// Weighted by rarity, never gated by floor — see <see cref="WeightOf"/>.
	/// </summary>
	public static ImmutableArray<RunCard> SharedPool =>
		[
			Unit("Stray", 0, 6, 6, "Followed the noise."),
			Unit("Scrapper", 1, 10, 4, "Fast, and does not last."),
			Unit("Tunneller", 1, 8, 6, "Comes up where it is needed."),
			// Was 4/12 and measured +0.59 — the worst body in the pool. Toughness blocks for one
			// turn and power removes the thing permanently; v3 did not change which of those pays.
			Unit("Shieldbearer", 1, 6, 12, "Holds the line, and holds a spike."),
			Rite(
				"Field Dressing",
				1,
				"It will hold. It will not heal.",
				OnPlay(DoomTarget.Player, new GainLifeAction { Amount = 12 }, "gain 12 life")
			),
			// **The volume axis, and the first card that makes ORDER WITHIN A TURN a decision.**
			// It counts itself, so it is never worse than 3 to every enemy — but held back until
			// after two cheap bodies it is 9. Nothing in this game has ever rewarded sequencing.
			Rite(
				"Scavenged Rounds",
				1,
				"Spend it on something that is already close.",
				OnPlay(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 3, PerEach = CountOf.CardsPlayedThisTurn },
					"3 to every enemy per card this turn"
				)
			) with
			{
				Rarity = DoomRarity.Rare,
			},
			Unit("Bonepicker", 2, 14, 4, "Arrives after the fighting."),
			Unit("Feral Pack", 2, 12, 8, "Hungry, and there are several.") with
			{
				Rarity = DoomRarity.Uncommon,
			},
			Unit("Rust Golem", 2, 10, 14, "Slow. Very hard to move.") with
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
			Rite(
				"Last Orders",
				2,
				"Everyone takes what they can carry.",
				OnPlay(DoomTarget.Player, new DrawCardsAction { Amount = 3 }, "draw 3")
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// **Adjacency, offensive.** Was a 3-cost 18/6. Where you put it now matters more than
			// what it is: dropped between two enemies it is 12 extra damage, on the edge it is 6.
			Unit(
				"Siege Ram",
				2,
				14,
				6,
				"One job, done once.",
				OnPlay(
					DoomTarget.EnemiesInAdjacentLanes,
					new DealDamageAction { Amount = 6 },
					"6 to Adjacent enemies"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// **Adjacency, defensive.** Was a 3-cost 12/16. Toughness is worth one turn of blocking,
			// so handing it to the neighbours is worth more than holding it.
			Unit(
				"Warden",
				2,
				8,
				16,
				"The last thing still standing.",
				OnPlay(
					DoomTarget.YourUnitsInAdjacentLanes,
					new BuffAction { Toughness = 4 },
					"Adjacent units get +0/+4"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// **The clock axis: it pays for apocalypses you ATE.** Was a 3-cost 12/20 vanilla. A
			// deck that races sees this as a 10/18; a deck that has taken four firings sees a
			// 18/26. That is the dodge-vs-eat bargain expressed on a card for the first time.
			Unit(
				"Long Watcher",
				2,
				10,
				18,
				"Has seen four of these.",
				OnPlay(
					DoomTarget.Self,
					new BuffAction
					{
						Power = 2,
						Toughness = 2,
						PerEach = CountOf.DoomsFired,
					},
					"+2/+2 per Doom fired"
				)
			) with
			{
				Rarity = DoomRarity.Rare,
			},
		];

	/// <summary>
	/// **The Long Emergency's own cards.** Its dooms read what is STANDING, so the act rewards
	/// committing on the firing turn. These lean into the clock: they want the apocalypse to land.
	/// </summary>
	private static ImmutableArray<RunCard> LongEmergencyCards =>
		[
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
			// Was a 3-cost with "Doom: gain 10", which paid only if you were still holding it when
			// the clock ran out. It now pays for every firing you have ALREADY survived, so it is
			// a reward for having eaten apocalypses rather than a bet on the next one.
			Unit(
				"Reactor Crew",
				2,
				6,
				10,
				"They stayed at the desk.",
				OnPlay(
					DoomTarget.Player,
					new GainLifeAction { Amount = 5, PerEach = CountOf.DoomsFired },
					"gain 5 per Doom fired"
				)
			) with
			{
				Rarity = DoomRarity.Rare,
			},
		];

	/// <summary>
	/// **The Rising — your losses are ammunition.**
	///
	/// This act was built on death: Zombie reads what died, and its cards triggered on their own
	/// deaths. **Combat v3 broke all of it**, because a unit withdraws at end of turn instead of
	/// dying — Gravedigger measured -0.26, the only negative card in the game, and Pyre Tender
	/// +0.57.
	///
	/// **The fix is not a trigger but a READ.** `CountOf.DiedLastTurn` counts what the enemy
	/// actually killed across your whole board, so these pay off attrition without needing to be
	/// the thing that died. The act keeps its identity and loses the mechanic that no longer works.
	///
	/// They are deliberately blank on turn one — nothing has died yet — and enormous in a grinding
	/// fight. That is the act, and it is the same axis Ash's ability reads.
	/// </summary>
	private static ImmutableArray<RunCard> RisingCards =>
		[
			Unit(
				"Gravedigger",
				1,
				8,
				6,
				"He has been busy. He is not finished.",
				OnPlay(
					DoomTarget.Opponent,
					new DealDamageAction { Amount = 8, PerEach = CountOf.DiedLastTurn },
					"8 to the Opponent per Loss"
				)
			),
			Unit(
				"Pyre Tender",
				2,
				10,
				8,
				"Burning them is the only thing that has worked.",
				OnPlay(
					DoomTarget.AllEnemies,
					new DealDamageAction { Amount = 6, PerEach = CountOf.DiedLastTurn },
					"6 to every enemy per Loss"
				)
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// Was a 3-cost 12/18 with "Doom: gain 12; each turn: gain 3" — and the second half was
			// a lie, because an ephemeral unit never sees a second turn. It is now the act's payoff
			// body: small after a clean turn, a monster after a bad one.
			Unit(
				"The Choirmaster",
				2,
				8,
				12,
				"Still conducting. Nobody told him.",
				OnPlay(
					DoomTarget.Self,
					new BuffAction
					{
						Power = 4,
						Toughness = 4,
						PerEach = CountOf.DiedLastTurn,
					},
					"+4/+4 per Loss"
				)
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
	/// **The Reckoning — cheap, plentiful, and nothing is precious.** Famine takes what you never
	/// played and Judgement flattens everything standing, so hoarding and monsters are both
	/// punished. The act's axis is VOLUME: how many cards you got out this turn.
	/// </summary>
	private static ImmutableArray<RunCard> ReckoningCards =>
		[
			Unit("Penitent", 0, 4, 4, "Walked here. Will walk further."),
			// Was a 1-cost 2/8 whose text said "each turn: gain 2" and fired ONCE — v3 withdraws
			// the unit that would have fired it again. Now it pays for a wide turn instead, which
			// is honest and is the act's own axis.
			Unit(
				"Almoner",
				1,
				4,
				8,
				"Gives away what little is left.",
				OnPlay(
					DoomTarget.Player,
					new GainLifeAction { Amount = 2, PerEach = CountOf.CardsPlayedThisTurn },
					"gain 2 per card this turn"
				)
			),
			Rite(
				"Tithe",
				1,
				"Give it up before it is taken.",
				OnPlay(DoomTarget.Player, new DrawCardsAction { Amount = 2 }, "draw 2")
			) with
			{
				Rarity = DoomRarity.Uncommon,
			},
			// Was a 2-cost 4/10 with "Doom: gain 14". The act wants a WIDE board, so its rare now
			// pays the board rather than the player — and Judgement flattening everything standing
			// is the tension that keeps it from being free.
			Unit(
				"Reliquary Guard",
				2,
				6,
				10,
				"Guarding a box nobody has opened.",
				OnPlay(
					DoomTarget.YourUnits,
					new BuffAction { Power = 2, Toughness = 2 },
					"every unit you hold gets +2/+2"
				)
			) with
			{
				Rarity = DoomRarity.Rare,
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
