using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// Placeholder content so the game is playable. Balance here is a guess, not a measurement —
/// every number is meant to be changed after the first real playtest.
/// </summary>
public static partial class StarterContent
{
	/// <summary>
	/// How many tickets a card of each rarity puts in the reward bag.
	///
	/// **A weight, never a gate.** Every card is offerable on every floor; a rare is simply a
	/// sixth as likely to come up as a common. That is what keeps an early rare possible, and an
	/// early rare you build the rest of the run around is the memorable run — see
	/// <see cref="KinRarity"/>. Floor-gating the rares was measured and thrown away: it made the
	/// early bag small enough to CONCENTRATE the best card instead of hiding it, and it cost the
	/// runs worth remembering. `docs/findings/doom-balance.md` run 9 has the numbers.
	///
	/// With roughly seven commons, eight uncommons and three rares in an act's pool that puts a
	/// rare in a three-card offer about one screen in eight.
	/// </summary>
	public static int WeightOf(KinRarity rarity) =>
		rarity switch
		{
			KinRarity.Common => 6,
			KinRarity.Uncommon => 3,
			KinRarity.Rare => 1,
			_ => throw new ArgumentOutOfRangeException(
				nameof(rarity),
				$"No weight for {rarity}, so a card of that rarity would never be offered at all"
			),
		};

	/// <summary>
	/// A card that is not a body. It resolves, does its thing, and goes to Discard.
	///
	/// **The first cards in this game that are not units.** Effects are declared as data — a
	/// trigger, a target rule and an action template — so a new one needs no engine change. See
	/// KinEffect.
	/// </summary>
	private static RunCard Rite(string name, int cost, string text, params KinEffect[] effects) =>
		new()
		{
			Name = name,
			Cost = cost,
			Description = text,
			IsUnit = false,
			Effects = [.. effects],
		};

	private static KinEffect OnPlay(KinTarget target, GameAction template, string text) =>
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
		params KinEffect[] effects
	) => Unit(name, cost, power, toughness, text) with { Effects = [.. effects] };

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
	private static KinEffect EachTurn(KinTarget target, GameAction template, string text) =>
		new()
		{
			Trigger = EffectTrigger.OnTurnEnd,
			Target = target,
			Template = template,
			Text = text,
		};

	private static KinEffect OnDeath(KinTarget target, GameAction template, string text) =>
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
	/// **The ability: +2/+0 for every unit of yours the enemy killed last turn, for THAT TURN ONLY.**
	///
	/// It shipped cumulative — stacking for the whole battle — and a playtest called it immediately.
	/// Run 16 had already measured the ability alone as worth +32/+38/+8 points of act completion,
	/// more than doubling Ash's stats was worth, and stacking it across a grinding fight made him a
	/// monster. Single-turn is what was asked for originally; cumulative was chosen because it
	/// looked cheaper to build.
	///
	/// **It was not cheaper.** See the second effect below: an expiring bonus is the same buff
	/// negated on the opposite trigger, which needs no duration system and no continuous layer.
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
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnStart,
					Target = KinTarget.Self,
					Template = new BuffAction { Power = 2, PerEach = CountOf.DiedLastTurn },
					Text = "+2/+0 per Loss",
				},
				// **THE SAME BUFF, NEGATED, ON THE OPPOSITE TRIGGER — and that is the whole
				// duration system.** The bonus lasts exactly one turn and then unwinds itself.
				//
				// It shipped CUMULATIVE and a playtest called it immediately: stacking +2 a Loss
				// across a grinding battle made Ash a monster, and run 16 measured the ability
				// alone as worth +32/+38/+8 points of act completion. A single-turn bonus is what
				// was asked for originally; the cumulative reading was chosen because it looked
				// cheaper to build.
				//
				// It was not cheaper. An expiring effect needs no duration machinery and no
				// continuous layer — it needs a symmetric pair of triggers.
				//
				// **THE CONSTRAINT, and it is sharp: this only works for a count that cannot
				// change within a turn.** `DiedLastTurn` is written once, by `StartTurnAction`, and
				// is fixed until the next one, so both firings read the same number and the unwind
				// is exact. Doing this with `CardsPlayedThisTurn` would apply a small buff and
				// remove a large one, quietly draining the unit. Check the count before copying
				// this pattern.
				//
				// Ordering is safe: OnTurnEnd fires after the lanes have resolved, so Ash swings
				// with the bonus and gives it back afterwards.
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnEnd,
					Target = KinTarget.Self,
					Template = new BuffAction { Power = -2, PerEach = CountOf.DiedLastTurn },
					Text = "",
				},
			],
		};

	/// <summary>
	/// **The roster — the run's one build declaration, chosen before the first card is drawn.**
	///
	/// A companion is one record with a `KinEffect` list, and the effect system does not know what
	/// holds it, so this is content and not a system. Each entry is a DIFFERENT AXIS, because the
	/// point is that a different companion makes a different reward screen correct.
	///
	/// **Two facts about triggers constrain every design here, and both bite silently.**
	///
	/// 1. **The field is EMPTY at `OnTurnStart`** — units withdraw at the end of the turn that
	///    played them, so the companion is the only thing standing. An `OnTurnStart` effect aimed
	///    at your board therefore reads nothing and looks exactly like one that worked. Only
	///    `Self`, `Player` and counts already written (`DiedLastTurn`) are safe there.
	/// 2. **`OnTurnEnd` fires AFTER the lanes have resolved.** A buff there is wasted — the trade
	///    already happened — but damage, life and draw are not. That is why the board-reading
	///    companions pay in life and damage rather than in stats.
	///
	/// The `KinBot` plays all of them, so `sim` measures each one honestly.
	/// </summary>
	public static ImmutableArray<Companion> Roster =>
		[StarterCompanion, Bramble, Tally, Pike, Moss];

	/// <summary>
	/// **BULWARK. Pays the Opponent for what your units ABSORBED** — hold the line, and every blow
	/// the line takes comes back at the thing that sent it.
	///
	/// **Reworked 2026-09-22 from "end of turn: 2 life per unit still standing"**, which measured
	/// life GAINED per battle on most act-1 floors (`docs/findings/kin-balance.md` run 26). A
	/// repeatable heal in a game about keeping life across a run is a stall engine: a longer fight
	/// paid it more. This pays in damage to the Opponent instead, so a longer fight pays it by
	/// ENDING SOONER — the test every ability now has to pass (root `CLAUDE.md`).
	///
	/// It also gives Bulwark the finisher a Thorns deck otherwise has to draft, and it makes each
	/// counter mean something: a Flier gives it nothing (the wall absorbs nothing), a Flail Knight
	/// feeds it twice, and a Razorback's spikes on your striker feed it too.
	/// </summary>
	public static Companion Bramble =>
		new()
		{
			Name = "Bramble",
			// BULWARK. See StarterContent.Archetypes.cs.
			Starter = [BriarSentinel, BriarSentinel, BarbedBanner],
			Description = "Slow to start, harder to move every year.",
			BasePower = 4,
			BaseToughness = 16,
			Effects =
			[
				// **End of turn, after the lanes** — the only moment the turn's absorption is known.
				// Bramble's own soak counts: she stands in the centre and is the biggest wall on
				// the board.
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnEnd,
					Target = KinTarget.Opponent,
					Template = new DealDamageAction
					{
						Amount = 1,
						PerEach = CountOf.AbsorbedThisTurn,
					},
					Text = "end of turn: the Opponent takes what your units absorbed",
				},
			],
		};

	/// <summary>**VOLUME.** Pays per card played, so it wants a wide, cheap hand.</summary>
	public static Companion Tally =>
		new()
		{
			Name = "Tally",
			Description = "Counts everything. Twice, if you are slow about it.",
			BasePower = 5,
			BaseToughness = 12,
			Effects =
			[
				// `CardsPlayedThisTurn` is still full at end of turn — `StartTurnAction` clears it
				// on the way in, not on the way out. Reading it at turn START would be zero every
				// time.
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnEnd,
					Target = KinTarget.Opponent,
					Template = new DealDamageAction
					{
						Amount = 3,
						PerEach = CountOf.CardsPlayedThisTurn,
					},
					Text = "end of turn: 3 to the Opponent per card you played",
				},
			],
		};

	/// <summary>**THE FACE.** A flat clock on the Opponent. Races, and ignores the board.</summary>
	public static Companion Pike =>
		new()
		{
			Name = "Pike",
			// FACE. See StarterContent.Archetypes.cs.
			Starter = [Whetstone, Whetstone, Berserker],
			Description = "Only ever looking at the one thing.",
			BasePower = 8,
			BaseToughness = 9,
			Effects =
			[
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnEnd,
					Target = KinTarget.Opponent,
					Template = new DealDamageAction { Amount = 6 },
					Text = "end of turn: 6 to the Opponent",
				},
			],
		};

	/// <summary>
	/// **SPATIAL.** Splashes the lanes either side of itself.
	///
	/// It sits in the centre lane, so it reaches lanes 1 and 3 and never the flanks. That is a real
	/// shape to play around today — and the moment the companion's lane becomes a choice, this is
	/// the companion that makes the choice matter most.
	/// </summary>
	public static Companion Moss =>
		new()
		{
			Name = "Moss",
			Description = "Spreads. Not quickly, but it does not stop.",
			BasePower = 5,
			BaseToughness = 14,
			Effects =
			[
				new KinEffect
				{
					Trigger = EffectTrigger.OnTurnEnd,
					Target = KinTarget.EnemiesInAdjacentLanes,
					Template = new DealDamageAction { Amount = 5 },
					Text = "end of turn: 5 to the enemies either side of it",
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

	/// <summary>
	/// **Seven generic cards plus three of the companion's archetype** — see
	/// <see cref="Companion.Starter"/>. A companion with no archetype set yet brings the three
	/// generic cards it always had, so its run is exactly what it was before the slice.
	/// </summary>
	public static Run NewRun(int seed = 1, Companion? companion = null)
	{
		var chosen = companion ?? StarterCompanion;

		ImmutableList<RunCard> generic =
		[
			Unit("Scavenger", 1, 6, 6, "Takes what is left."),
			Unit("Scavenger", 1, 6, 6, "Takes what is left."),
			Unit("Scavenger", 1, 6, 6, "Takes what is left."),
			Unit("Bulwark", 1, 2, 10, "Stands in the way."),
			Unit("Bulwark", 1, 2, 10, "Stands in the way."),
			Unit("Bulwark", 1, 2, 10, "Stands in the way."),
			Unit("Ash Walker", 2, 12, 14, "Walked out of the last one."),
		];

		ImmutableList<RunCard> archetype = chosen.Starter.IsEmpty
			?
			[
				Unit("Scavenger", 1, 6, 6, "Takes what is left."),
				Unit("Ash Walker", 2, 8, 10, "Walked out of the last one."),
				Unit("Lantern Bearer", 0, 4, 4, "Small light, long night."),
			]
			: chosen.Starter;

		return new Run
		{
			Life = StartingLife,
			MaxLife = StartingLife,
			RngSeed = seed,
			Companion = chosen,
		}.WithCards([.. generic, .. archetype]);
	}

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
	/// (see KinJam.md "Combat v3"), so it rarely dies, so Gravedigger — "on death: 16 to the
	/// Opponent" — measured **-0.26**, the only negative card in the game. Replaced throughout by
	/// <see cref="CountOf.DiedLastTurn"/>, which reads what the ENEMY killed across your whole board
	/// instead of requiring this particular card to be the thing that died.
	///
	/// **Card NAMES are deliberately unchanged.** `KinArt` resolves art by name from 52 authored
	/// SVGs, so renaming a card silently downgrades it to a generated figure. Re-stat and re-ability
	/// the names that exist; a genuinely new name is an art debt and should be taken knowingly.
	///
	/// **Every entry must beat a starter card**, which is why Ash Walker and Bulwark are not in here:
	/// they ARE starter cards. **Power, not toughness** — measured repeatedly, and still true in v3.
	///
	/// **THE COST RULE, and it decides every stat line below (2026-09-18).**
	///
	/// A card costs energy AND a lane, and the LANE is the scarce one — that is what the pricing had
	/// wrong. Three 1-drops fill three lanes for about 42 total stats; a 2-drop plus a 1-drop fills
	/// two for (2-drop) + 14. **So a 2-drop needs ~28 total just to break even on stats, and MORE
	/// than that to pay for the lane it gives up.**
	///
	/// Every 2-drop in the game was at 16-24 and the best two 1-drops came to 18/10 across two lanes,
	/// so a playthrough found "almost 0 situations where playing a 2 drop was better than 2 1 drops".
	/// It was not close and it was structural, not a rounding error.
	///
	/// Vanilla 2-drops now land 26-36 total. Ones carrying an effect land lower, because the effect
	/// is the rest of the card.
	///
	/// **Do not fix this by nerfing 1-drops.** They are correctly priced against 0-drops, and the
	/// measured table has cheap bodies as the best cards in the game for the same structural reason.
	/// 3-drops were deleted outright for this rule taken one step further — see run 17.
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
			// **Exhausts.** A 1-cost heal in a ~20 card deck that discards its hand every turn came
			// back roughly every other turn, so healing stopped being a decision and became an
			// income stream. Found in a playtest. Battle scope — it is back next fight.
			Rite(
				"Field Dressing",
				1,
				"It will hold. It will not heal.",
				OnPlay(
					KinTarget.Player,
					new GainLifeAction { Amount = 12 },
					"gain 12 life, Exhaust"
				)
			) with
			{
				Exhausts = true,
			},
			// **The volume axis, and the first card that makes ORDER WITHIN A TURN a decision.**
			// It counts itself, so it is never worse than 3 to every enemy — but held back until
			// after two cheap bodies it is 9. Nothing in this game has ever rewarded sequencing.
			Rite(
				"Scavenged Rounds",
				1,
				"Spend it on something that is already close.",
				OnPlay(
					KinTarget.AllEnemies,
					new DealDamageAction { Amount = 3, PerEach = CountOf.CardsPlayedThisTurn },
					"3 to every enemy per card this turn"
				)
			) with
			{
				Rarity = KinRarity.Rare,
			},
			// **The cross-synergy card of the sacrifice cluster.** A fine 1-cost body on its own,
			// and renewable fodder in a deck that wants deaths — it comes back for another card
			// and another energy, which is the throttle on every loop it enables.
			Unit(
				"Twice Buried",
				1,
				4,
				4,
				"It has not noticed yet.",
				OnDeath(KinTarget.Self, new ReturnToHandAction(), "Dies: return it to your hand")
			),
			Unit("Bonepicker", 2, 18, 8, "Arrives after the fighting."),
			Unit("Feral Pack", 2, 16, 12, "Hungry, and there are several.") with
			{
				Rarity = KinRarity.Uncommon,
			},
			Unit("Rust Golem", 2, 12, 20, "Slow. Very hard to move.") with
			{
				Rarity = KinRarity.Uncommon,
			},
			Rite(
				"Breaching Charge",
				2,
				"Straight past whatever is in the way.",
				OnPlay(
					KinTarget.Opponent,
					new DealDamageAction { Amount = 14 },
					"14 to the Opponent"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			Rite(
				"Last Orders",
				2,
				"Everyone takes what they can carry.",
				OnPlay(KinTarget.Player, new DrawCardsAction { Amount = 3 }, "draw 3")
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// **Adjacency, offensive.** Was a 3-cost 18/6. Where you put it now matters more than
			// what it is: dropped between two enemies it is 12 extra damage, on the edge it is 6.
			Unit(
				"Siege Ram",
				2,
				16,
				10,
				"One job, done once.",
				OnPlay(
					KinTarget.EnemiesInAdjacentLanes,
					new DealDamageAction { Amount = 6 },
					"6 to Adjacent enemies"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// **Adjacency, defensive.** Was a 3-cost 12/16. Toughness is worth one turn of blocking,
			// so handing it to the neighbours is worth more than holding it.
			Unit(
				"Warden",
				2,
				10,
				20,
				"The last thing still standing.",
				OnPlay(
					KinTarget.YourUnitsInAdjacentLanes,
					new BuffAction { Toughness = 4 },
					"Adjacent units get +0/+4"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// **The clock axis: it pays for apocalypses you ATE.** Was a 3-cost 12/20 vanilla. A
			// ponytail: rehomed off the deleted doom clock onto board width — a stand-in read, not
			// a design. Wants a real one in the card pass.
			Unit(
				"Long Watcher",
				2,
				14,
				22,
				"Has seen four of these.",
				OnPlay(
					KinTarget.Self,
					new BuffAction
					{
						Power = 2,
						Toughness = 2,
						PerEach = CountOf.YourUnits,
					},
					"+2/+2 per unit you hold"
				)
			) with
			{
				Rarity = KinRarity.Rare,
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
				8,
				12,
				"It keeps working through it. That is all it does.",
				OnPlay(KinTarget.None, new DrawCardsAction { Amount = 2 }, "draw 2")
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			Unit(
				"Drone Swarm",
				2,
				10,
				10,
				"Somebody's fleet, still flying the last order it got.",
				OnPlay(
					KinTarget.AllEnemies,
					new DealDamageAction { Amount = 6 },
					"6 to every enemy"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// ponytail: rehomed off the deleted doom clock onto board width — a stand-in read, not
			// a design. Wants a real one in the card pass.
			Unit(
				"Reactor Crew",
				2,
				10,
				14,
				"They stayed at the desk.",
				OnPlay(
					KinTarget.Player,
					new GainLifeAction { Amount = 5, PerEach = CountOf.YourUnits },
					"gain 5 per unit you hold"
				)
			) with
			{
				Rarity = KinRarity.Rare,
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
			// **The sacrifice cluster — the act's SUPPLY of deaths.** `CountOf.DiedLastTurn` fixed
			// the reads; until these existed, every one of them was fed only by what the enemy
			// chose to kill, which made the whole act a passenger in its own theme.
			//
			// Pyre Keeper's lane is its choice of what to eat: Devour makes the unit it replaces
			// die rather than leave. Play it over a Stray you no longer need and Ash swings four
			// points harder next turn.
			Unit("Pyre Keeper", 1, 6, 8, "It carries the fire to them.") with
			{
				Devours = true,
			},
			// **Scaled on the sacrifice rather than validated against it.** With an empty lane the
			// Destroy resolves to nothing, `DiedThisTurn` stays at zero and the card gives you
			// nothing — so it cannot be cashed in as a 1-cost draw-and-heal, and no new validation
			// rule was needed to stop that.
			Rite(
				"Gallows Feast",
				1,
				"Nothing is wasted. Nothing ever was.",
				OnPlay(
					KinTarget.UnitInSourceLane,
					new DestroyAction(),
					"Sacrifice the unit in this lane"
				),
				OnPlay(
					KinTarget.Player,
					new DrawCardsAction { Amount = 2, PerEach = CountOf.DiedThisTurn },
					"draw 2"
				),
				OnPlay(
					KinTarget.Player,
					new GainLifeAction { Amount = 10, PerEach = CountOf.DiedThisTurn },
					"gain 10 life"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// The board you built, spent all at once. **Ash is spared** — `DestroyAction` skips the
			// companion, which is what keeps "no doom can touch it" true of your own cards too.
			Rite(
				"Butcher's Bill",
				2,
				"Everyone pays it eventually.",
				OnPlay(KinTarget.YourUnits, new DestroyAction(), "Sacrifice every unit you hold"),
				OnPlay(
					KinTarget.AllEnemies,
					new DealDamageAction { Amount = 8, PerEach = CountOf.DiedThisTurn },
					"8 to every enemy per Loss this turn"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			Unit(
				"Gravedigger",
				1,
				8,
				6,
				"He has been busy. He is not finished.",
				OnPlay(
					KinTarget.Opponent,
					new DealDamageAction { Amount = 8, PerEach = CountOf.DiedLastTurn },
					"8 to the Opponent per Loss"
				)
			),
			Unit(
				"Pyre Tender",
				2,
				14,
				12,
				"Burning them is the only thing that has worked.",
				OnPlay(
					KinTarget.AllEnemies,
					new DealDamageAction { Amount = 6, PerEach = CountOf.DiedLastTurn },
					"6 to every enemy per Loss"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// Was a 3-cost 12/18 with a doom trigger plus "each turn: gain 3" — and that half was
			// a lie, because an ephemeral unit never sees a second turn. It is now the act's payoff
			// body: small after a clean turn, a monster after a bad one.
			Unit(
				"The Choirmaster",
				2,
				12,
				16,
				"Still conducting. Nobody told him.",
				OnPlay(
					KinTarget.Self,
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
				Rarity = KinRarity.Rare,
			},
			Rite(
				"Blood Price",
				1,
				"It costs what it costs.",
				OnPlay(
					KinTarget.AllEnemies,
					new DealDamageAction { Amount = 10 },
					"10 to every enemy"
				)
			) with
			{
				Rarity = KinRarity.Uncommon,
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
					KinTarget.Player,
					new GainLifeAction { Amount = 2, PerEach = CountOf.CardsPlayedThisTurn },
					"gain 2 per card this turn"
				)
			),
			Rite(
				"Tithe",
				1,
				"Give it up before it is taken.",
				OnPlay(KinTarget.Player, new DrawCardsAction { Amount = 2 }, "draw 2")
			) with
			{
				Rarity = KinRarity.Uncommon,
			},
			// Was a 2-cost 4/10 with a doom trigger. The act wants a WIDE board, so its rare now
			// pays the board rather than the player — and Judgement flattening everything standing
			// is the tension that keeps it from being free.
			Unit(
				"Reliquary Guard",
				2,
				10,
				14,
				"Guarding a box nobody has opened.",
				OnPlay(
					KinTarget.YourUnits,
					new BuffAction { Power = 2, Toughness = 2 },
					"every unit you hold gets +2/+2"
				)
			) with
			{
				Rarity = KinRarity.Rare,
			},
		];

	/// <summary>
	/// What a floor may offer: the shared core every act draws from, plus the act's own cards.
	///
	/// **Shared core plus a themed slice, not three separate pools.** Three pools would thin the
	/// variety in each act, and if acts are ever chained into one run the pools would have to be
	/// merged anyway — this composes for free.
	/// </summary>
	/// <remarks>
	/// **ONE pool for every act, since 2026-09-22 — act-exclusive pools are off for now.** The
	/// Loss archetype lived entirely in The Rising, so it could be built for 15 floors of 45, and
	/// an archetype you can only draft in one act is not something a companion can declare. The
	/// act slices are still TAGGED with their act, so the content dump can group them and the
	/// decision is cheap to reverse; `theme` is kept on the signature for the same reason.
	/// </remarks>
	public static ImmutableArray<RunCard> RewardPool(KinTheme theme) =>
		[
			.. SharedPool,
			.. LongEmergencyCards.Select(c => c with { Theme = KinTheme.LongEmergency }),
			.. RisingCards.Select(c => c with { Theme = KinTheme.Rising }),
			.. ReckoningCards.Select(c => c with { Theme = KinTheme.Reckoning }),
			.. BulwarkCards,
			.. FaceCards,
		];

	/// <summary>
	/// Three distinct cards to choose between, deterministic from the seed and floor so a run
	/// replays exactly — the same property that makes a bug report actionable.
	/// </summary>
	/// <summary>
	/// Everything a companion can become. **Three are offered per cleared floor; you take one.**
	///
	/// Deliberately mixed: pure stat upgrades that any companion wants, and EFFECT upgrades that
	/// only some companions want. That mix is what stops the pick being arithmetic — Warding on a
	/// Bramble already gaining life is redundant, while on Pike it is the only healing in the run.
	///
	/// **Echo is the rare and it is the build-around.** It copies everything the companion has, so
	/// its value is whatever you have already chosen — worthless first, enormous last.
	/// </summary>
	public static ImmutableArray<CompanionUpgrade> UpgradePool =>
		[
			new()
			{
				Name = "Thickset",
				Text = "+0/+8",
				Toughness = 8,
			},
			new()
			{
				Name = "Sharpened",
				Text = "+5/+0",
				Power = 5,
			},
			new()
			{
				Name = "Steady",
				Text = "+3/+4",
				Power = 3,
				Toughness = 4,
			},
			new()
			{
				Name = "Barbed",
				Text = "end of turn: 3 to the enemies either side of it",
				Rarity = KinRarity.Uncommon,
				Effects =
				[
					new KinEffect
					{
						Trigger = EffectTrigger.OnTurnEnd,
						Target = KinTarget.EnemiesInAdjacentLanes,
						Template = new DealDamageAction { Amount = 3 },
						Text = "end of turn: 3 to the enemies either side of it",
					},
				],
			},
			new()
			{
				Name = "Warding",
				Text = "end of turn: gain 3 life",
				Rarity = KinRarity.Uncommon,
				Effects =
				[
					new KinEffect
					{
						Trigger = EffectTrigger.OnTurnEnd,
						Target = KinTarget.Player,
						Template = new GainLifeAction { Amount = 3 },
						Text = "end of turn: gain 3 life",
					},
				],
			},
			new()
			{
				Name = "Goring",
				Text = "end of turn: 4 to the Opponent",
				Rarity = KinRarity.Uncommon,
				Effects =
				[
					new KinEffect
					{
						Trigger = EffectTrigger.OnTurnEnd,
						Target = KinTarget.Opponent,
						Template = new DealDamageAction { Amount = 4 },
						Text = "end of turn: 4 to the Opponent",
					},
				],
			},
			new()
			{
				Name = "Echo",
				Text = "everything it does, it does twice",
				Rarity = KinRarity.Rare,
				EchoesAbility = true,
			},
		];

	/// <summary>
	/// How often an upgrade is offered, in floors. **The power curve's single biggest dial.**
	///
	/// Offering one after EVERY cleared battle measured 82.5% act completion against a 25% target —
	/// twenty-four upgrades in a run compound far past anything the enemy curve answers. Every
	/// third floor is eight a run, which also makes each pick a bigger moment than a stat tick.
	/// </summary>
	public const int FloorsPerUpgrade = 2;

	/// <summary>
	/// Whether this floor offers a companion upgrade at all.
	///
	/// **Asked in ONE place so the front end and the simulator cannot disagree.** Gating this in
	/// `RunSimulator` alone would have measured a curve no player ever receives — the exact class
	/// of silent divergence this codebase keeps rediscovering.
	/// </summary>
	public static bool OffersUpgradeOn(int floor) => floor % FloorsPerUpgrade == 0;

	/// <summary>
	/// The three upgrades offered on a floor, or NOTHING on a floor that offers none — see
	/// <see cref="OffersUpgradeOn"/>. Weighted by rarity and without replacement, exactly as
	/// <see cref="RewardsFor"/> does it.
	///
	/// A DIFFERENT seed mix from the card rewards, or the same floor would correlate the two
	/// offers and a run would feel narrower than it is.
	/// </summary>
	public static ImmutableArray<CompanionUpgrade> UpgradesFor(int seed, int floor, int count = 3)
	{
		if (!OffersUpgradeOn(floor))
			return [];

		var pool = UpgradePool.ToList();
		var rng = new Random(seed * 65537 + floor * 97 + 13);
		var picked = new List<CompanionUpgrade>();

		for (var i = 0; i < count && pool.Count > 0; i++)
		{
			var roll = rng.Next(pool.Sum(u => WeightOf(u.Rarity)));
			var index = 0;

			while (roll >= WeightOf(pool[index].Rarity))
				roll -= WeightOf(pool[index++].Rarity);

			picked.Add(pool[index]);
			pool.RemoveAt(index);
		}

		return [.. picked];
	}

	/// <summary>Three cards for a floor, weighted by rarity.</summary>
	public static ImmutableArray<RunCard> RewardsFor(
		KinTheme theme,
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
	/// What a shop floor is selling. **Deterministic from (seed, floor)**, like a reward screen.
	///
	/// Prices scale with the act, the same way <see cref="GoldFor"/> does, so a shop in act 3 is
	/// reachable on act 3's earnings rather than on everything banked since act 1.
	///
	/// Every number here is a guess. Roughly: an act pays 200-280 gold across its battles and holds
	/// two shops, so you can afford two or three things an act.
	/// </summary>
	public static ShopOffer ShopFor(KinTheme theme, int seed, int floor, int cardsRemoved)
	{
		var act = ActMap.ActIndexFor(floor);

		// Offset from the reward stream so a shop does not stock the three cards you were just
		// offered — the floor is nudged by a constant rather than reseeded, which keeps it a pure
		// function of (seed, floor).
		var cards = RewardsFor(theme, seed, floor + 977, count: 3);

		return new ShopOffer
		{
			Cards = cards,
			CardPrice = 55 + 15 * act,

			// **Each removal costs more than the last.** Thinning is the strongest thing gold can
			// buy under combat v3, and a flat price would make it the only thing worth buying.
			RemovalPrice = 70 + 20 * act + 30 * cardsRemoved,
			HealPrice = 40 + 10 * act,
			HealAmount = StartingLife / 4,
		};
	}

	/// <summary>
	/// What is on a floor. **Read straight off <see cref="ActMap.Layout"/>** — it was
	/// `floor % 4 == 0 || floor == ActLength - 1`, which made "put a shop on 5" a puzzle instead of
	/// an edit, and kept the act's length and its shape in two places that had to agree by hand.
	/// </summary>
	public static FloorKind FloorKindFor(int floor) => ActMap.KindFor(floor);

	/// <summary>
	/// How much harder an act is than the one before it, **for an ORDINARY floor**.
	///
	/// Acts 2 and 3 reuse act 1's roster with this on top rather than the game authoring forty more
	/// enemies it does not have. That is necessary because content is chosen by the floor's position
	/// WITHIN its act — without a multiplier, act 2 floor 1 would field act 1 floor 1 and the run
	/// would get easier every time you cleared an act.
	///
	/// **Raised from 0.7 after run 20.** One death in roughly fourteen hundred ordinary battles, at
	/// 10.9 life apiece against 21.9 in a single-act run. An act cost about 90 life and the rests
	/// plus the act break handed all of it back, so the life budget never bound anywhere.
	/// </summary>
	/// <summary>
	/// **The base dropped from 1.0 to 0.82 when the companion's marks were cut (2026-09-18).**
	///
	/// Marks were worth roughly +40 power and +60 toughness on Ash by the end of a run, and removing
	/// them took act completion from 10% to 0% and mean floor from 33.5 to 23.0 on the same seeds —
	/// see run 23. The player lost a large, permanent source of power, so the thing they are hitting
	/// comes down to meet them.
	/// </summary>
	/// **The per-act step dropped from 0.9 to 0.72 as well.** Your damage output is flat — 3 energy,
	/// every turn, for the whole run — so an act that multiplies enemy health by 2.6 is not "harder",
	/// it is a different game where nothing you do arrives in time. Act 3 was clearing 100% of its
	/// first band and 33% of its third for that reason.
	/// **Base 0.82, step 0.55, and both numbers were argued out of measurements (2026-09-18).**
	///
	/// The STEP came down from 0.9 because your damage output is flat — 3 energy, every turn, for the
	/// whole run — so an act that multiplied enemy health by 2.6 was not "harder", it was a different
	/// game where nothing you did arrived in time. Act 3 cleared 100% of its first band and 33% of
	/// its third.
	///
	/// The BASE was tried at 0.95, to put teeth in an act 1 that killed 2 runs in 24 across fifteen
	/// floors. **It cost more than the back half gained** — mean floor fell 34.3 to 31.1 — so it went
	/// back to 0.82. Act 1 being gentle is a PACING problem and this is the wrong dial for it: the
	/// fix is a curve inside the act, not a bigger opening number.
	public static double HealthScaleFor(int floor) => 0.82 + 0.55 * ActMap.ActIndexFor(floor);

	/// <summary>
	/// **The base is above 1.0 on purpose.** Ordinary floors were costing 9.4 life against a 120
	/// budget with two rests and an act break on top, so nothing in an act could kill you and the
	/// whole run's difficulty sat on its last floor. The act multiplier could not fix that — act 1
	/// has a multiplier of one by definition, and act 1 was the problem.
	/// </summary>
	public static double AttackScaleFor(int floor) => 1.2 + 0.45 * ActMap.ActIndexFor(floor);

	/// <summary>
	/// **A boss floor scales far more gently, and run 20 is why.** Death rates on the three act
	/// finales were 39.3%, 74.6% and 61.1% while ordinary floors killed almost nobody — the entire
	/// run's difficulty lived on three floors out of forty-five.
	///
	/// The cause was applying ONE multiplier to both. A boss floor already carries its own authored
	/// Opponent, sized as the end of a whole act; multiplying that by the ordinary curve as well
	/// took act 3's boss to 336 health and 113 life a battle.
	///
	/// **Ordinary floors and boss floors needed opposite corrections, so they get separate dials.**
	/// </summary>
	// **`BossScaleFor` lived here and is gone.** It existed only because one Opponent fought all
	// three finales; each act authors its own now, so there is nothing left for a multiplier to do.
	// Deleting a dial is a better outcome than finding the right value for it.

	private static int Scaled(int value, double scale) =>
		Math.Max(1, (int)Math.Round(value * scale));

	/// <summary>
	/// Gold for clearing a floor. **Scales with the ACT, not the run-wide floor** — a shop in act 3
	/// should be reachable on act 3's earnings, and the prices there scale the same way.
	///
	/// A guess. Roughly 160 an act at eight battles, which buys about two things at a shop.
	/// </summary>
	public static int GoldFor(int floor) => 20 + ActMap.ActIndexFor(floor) * 8;

	/// <summary>
	/// What a rest gives back: 30% of max, the Slay the Spire number.
	///
	/// A FRACTION of max rather than a flat amount, so it keeps its meaning if the life budget ever
	/// moves again — and it has moved twice already.
	/// </summary>
	public static int RestHealFor(int maxLife) => maxLife * 3 / 10;

	/// <summary>
	/// What clearing an act gives back: **half of max, not all of it.**
	///
	/// A full restore made the run three independent acts rather than one run — nothing you spent
	/// in act 1 could ever cost you in act 2, so the only floor that could kill you was the one
	/// whose numbers happened to spike. Half keeps the act break a real relief while letting damage
	/// carry a debt forward, which is the only thing that makes a long run feel like one run.
	///
	/// A fraction of max, like the rest, so it keeps its meaning if the life budget moves again.
	/// </summary>
	public static int ActBreakHealFor(int maxLife) => maxLife / 2;

	/// <summary>
	/// Which Opponent waits on a floor — see <see cref="EnemyLibrary.ForFloor"/>.
	///
	/// This used to be `20 + floor * 6`, which gave every floor the same faceless body with a bigger
	/// number. An Opponent is content now: it has a name, a reinforcement of its own, and effects.
	/// </summary>
	public static OpponentDefinition OpponentFor(int floor, int seed)
	{
		// **Selected on the floor's position WITHIN its act, scaled by which act that is.** The
		// roster's tiers were authored against one act's length, so choosing on the run-wide floor
		// would pin every floor from act 2 onward to the last tier — one Opponent for thirty floors.
		var inAct = ActMap.FloorInAct(floor);
		var traits = EnemyLibrary.TraitsFor(seed);

		// **A boss floor fields the ACT'S OWN boss, at the numbers it was authored with.** No
		// multiplier: three fights written at the right size beat one fight times a coefficient,
		// and the coefficient could not work anyway — a boss is a race, and a race has a cliff.
		if (ActMap.IsBossFloor(floor))
			return ThemeLibrary.Of(ActMap.ThemeFor(floor)).Boss;

		var body = EnemyLibrary.ForFloor(inAct);

		// **The ordinary Opponent also ramps WITHIN the act**, now that the tier list is one entry.
		// Without it floor 14 would field exactly what floor 1 did; the traits keep each fight
		// distinct, but they do not make it bigger.
		var withinAct = 1.0 + 0.08 * (inAct - 1);

		body = body with
		{
			Health = Scaled(body.Health, HealthScaleFor(floor) * withinAct),
			Reinforcement = body.Reinforcement with
			{
				Health = Scaled(body.Reinforcement.Health, HealthScaleFor(floor)),
				Attack = Scaled(body.Reinforcement.Attack, AttackScaleFor(floor)),
			},
		};

		// How many battles this Opponent has already fielded, including this one. Counting the
		// floors below rather than tracking history keeps this a PURE function of (floor, seed) —
		// the front end and the simulator can each ask it cold and get the same answer.
		// Max(0) because a REST floor fields nobody: the count comes back zero there and the
		// subtraction would run off the end of the trait list. Asking a rest floor who its
		// Opponent is should answer, not throw.
		// Counted WITHIN the act, matching how the Opponent was selected. Counting across the whole
		// run would make act 2 continue act 1's trait sequence for a different curve entirely.
		var nth = Math.Max(
			0,
			Enumerable
				.Range(1, inAct)
				.Count(f =>
					ActMap.Layout[f - 1] == FloorKind.Battle
					&& EnemyLibrary.ForFloor(f).Name == EnemyLibrary.ForFloor(inAct).Name
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
	public static int OpponentHealthFor(int floor) => OpponentFor(floor, seed: 0).Health;

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
		var body = EnemyLibrary.ForFloor(ActMap.FloorInAct(floor)).Reinforcement;

		return body.ToSummon(lane) with
		{
			Health = Scaled(body.Health, HealthScaleFor(floor)) + turnNumber,
			Attack = Scaled(body.Attack, AttackScaleFor(floor)) + turnNumber / 2,
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
		var inAct = ActMap.FloorInAct(floor);
		// Reaches the four-lane cap by the middle of an act rather than only at its end. With 15
		// floors and 8 battles there is not room for a slow ramp, and run 20 measured the result:
		// ordinary floors killed nobody.
		var count = Math.Min(2 + inAct / 4, KinBattle.LaneCount - 1);
		var roster = EnemyLibrary.PlayableOn(inAct);
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

				// Act-relative selection, act-scaled body. See HealthScaleFor.
				definition = definition with
				{
					Health = Scaled(definition.Health, HealthScaleFor(floor)),
					Attack = Scaled(definition.Attack, AttackScaleFor(floor)),
				};

				return definition.ToEnemy(order[i]);
			})
			.ToList();
	}
}
