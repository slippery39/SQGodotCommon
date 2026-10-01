using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// A monster as authored: stats, a PASSIVE and the CYCLE of moves it plays on its own. Monster decks
/// are gone (2026-09-28): every card comes from the trainer's deck and its rewards.
/// </summary>
public record PartyCompanion(
	string Name,
	int Hp,
	int Power,
	ImmutableList<Intent> Moves,
	string Passive = "",
	string PassiveRule = "",
	int Thorns = 0,
	int FinisherPerAlly = 0,
	int Unbalances = 0
)
{
	/// <summary>
	/// **What it does to how you PLAY** — a `Trigger` ("when you draw, gain energy") or a static
	/// component ("your spells deal +2", MtgCore's StaticAbilityComponent idea). Put on its `Ally`
	/// and active only while it stands in the line.
	/// </summary>
	public ImmutableList<GameComponent> Abilities { get; init; } = [];

	/// <summary>**Spell Power**: added to every spell you cast while it stands (round 4).</summary>
	public int SpellPower { get; init; }

	/// <summary>Its FAMILY (`PartyFamilies`).</summary>
	public Family Family { get; init; }
}

/// <summary>
/// A monster in a battle. `Position` is its place in your line (0 = the front). `Hp` is where a RUN
/// left it; null = full.
/// </summary>
public record PlacedCompanion(PartyCompanion Companion, int Position, int? Hp = null);

/// <summary>One fight of a run: who you face, in their line's order (the first is their front).</summary>
public record Encounter(string Name, ImmutableList<Foe> Foes);

/// <summary>
/// One battle to play. `OpeningHand` names cards to put on top of the shuffled deck, so a first
/// play can be a known puzzle rather than a random one.
/// </summary>
public record PartyScenario(
	string Name,
	string Description,
	ImmutableList<PlacedCompanion> Companions,
	ImmutableList<Foe> Foes,
	ImmutableList<KinCard> Deck,
	ImmutableList<string> OpeningHand,
	bool Deploy = false,
	Family Family = Family.None,
	ImmutableList<Relic>? Relics = null
);

/// <summary>
/// **THE RELAY content** (`KinRelayPlan.md`). Every number is a guess: this is exploring, not
/// tuning — the question is whether the decisions are real.
/// </summary>
public static class PartyContent
{
	private static KinCard Card(string name, int cost, string text, params GameAction[] steps) =>
		PartyCards.Card(name, cost, text, steps);

	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static Intent Guard(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
		};

	/// <summary>
	/// **Bramble, the Wall — wants the FRONT.** Every foe that strikes her pays for it, and in a line
	/// the front is struck every round.
	/// </summary>
	public static readonly PartyCompanion Bramble =
		new(
			"Bramble",
			// 30 → 27 (2026-09-30): the family balance pass.
			Hp: 27,
			Power: 2,
			[Attack("Bash", 4), Guard("Brace", 6)],
			Passive: "THORNS",
			PassiveRule: "A foe that attacks her takes 2.",
			Thorns: 2
		)
		{
			Family = Family.Grove,
			// THORNWALL (2 + her Block) went 2026-09-30: every Block card was also a damage card. Her
			// first attack each turn still roots her wall.
			Abilities = [new FirstAttack { Rooted = 4 }],
		};

	/// <summary>
	/// **Pike — Ember's starter, the flexible one** (Ember draft 2): SPELLBLADE — its attacks add your
	/// Spell Power, so a stacked turn can be cashed with a Strike as well as a spell.
	/// </summary>
	public static readonly PartyCompanion Pike =
		new(
			"Pike",
			// 18 → 24 (2026-09-28): one Gore in region 1 was a knockout. 24 → 28 (2026-09-30): the
			// family balance pass — Pike won 7% to Bramble's 72%. 28 → 30: the foe pass after it.
			Hp: 30,
			Power: 3,
			[Attack("Jab", 2), Attack("Jab", 2), Attack("Flurry", 0, Aim.Sweep)],
			Passive: "SPELLBLADE",
			PassiveRule: "Its attacks add your Spell Power."
		)
		{
			Family = Family.Ember,
			SpellPower = 1,
			Abilities = [new Spellblade(), new FirstAttack { SpellPower = 2 }],
		};

	/// <summary>
	/// **Gale, the Controller — wants the BACK.** It acts first, so its Gust reorders their front
	/// before anyone else swings — and every foe it moves is Off-Balance for the round.
	/// </summary>
	public static readonly PartyCompanion Gale =
		new(
			"Gale",
			Hp: 22,
			Power: 2,
			[Attack("Buffet", 1, Aim.Sweep), new Intent { Name = "Gust", Kind = IntentType.Shove }],
			Passive: "OFF-BALANCE +2",
			PassiveRule: "While Gale stands, a foe you move takes 2 more from every hit that round.",
			Unbalances: 2
		)
		{
			Family = Family.Storm,
		};

	private static readonly KinCard BasicGuard = PartyCards.Plus(
		Card("Guard", 1, "Gain 8 Block.", new GuardAction { Amount = 8 }),
		Card("Guard", 1, "Gain 11 Block.", new GuardAction { Amount = 11 })
	);

	private static readonly KinCard BasicStrike = PartyCards.Plus(
		Card(
			"Strike",
			1,
			"It attacks their front now: 5 + Power.",
			new StrikeAction { Amount = 5 }
		),
		Card("Strike", 1, "It attacks their front now: 8 + Power.", new StrikeAction { Amount = 8 })
	);

	private static readonly KinCard Rally = Card(
		"Rally",
		1,
		"+4 Power this turn.",
		new PowerAction { Amount = 4 }
	);

	private static readonly KinCard Stagger = Card(
		"Stagger",
		1,
		"Drop on a foe: it loses its next move.",
		new StaggerAction()
	);

	/// <summary>
	/// **The BASICS — 4 Strike, 4 Guard** (Shayne, 2026-09-28: "8 basics + 2 family"). Every card is
	/// played ON something. Practice scenarios deal these plus their own cards.
	/// </summary>
	public static readonly ImmutableList<KinCard> StarterDeck =
	[
		BasicStrike,
		BasicStrike,
		BasicStrike,
		BasicStrike,
		BasicGuard,
		BasicGuard,
		BasicGuard,
		BasicGuard,
	];

	/// <summary>
	/// **A run's first deck: the basics and two small cards of its FAMILY** that show the engine on
	/// turn one — STS's Bash. A family with none yet (Storm, Mire) gets the basics alone.
	/// </summary>
	public static ImmutableList<KinCard> StartingDeck(Family family) =>
		[
			.. StarterDeck,
			.. family switch
			{
				Family.Grove => GroveCards.Starting,
				Family.Ember => EmberCards.Starting,
				_ => ImmutableList<KinCard>.Empty,
			},
		];

	private static Foe Creature(string name, int hp, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Pattern = [.. cycle],
		};

	public static Foe Boar(int position) =>
		Creature(
			"Boar",
			22,
			Attack("Charge", 9) with
			{
				Crushes = true,
			},
			Attack("Thrash", 5, Aim.Sweep)
		) with
		{
			Trait = "CRUSH: its Charge ignores Block.",
			Family = Family.Grove,
			Position = position,
		};

	/// <summary>**Wisp — HUNTS the weakest**, wherever it stands, then drifts toward the back.</summary>
	public static Foe Wisp(int position) =>
		Creature(
			"Wisp",
			12,
			Attack("Zap", 4, Aim.Hunt),
			Attack("Zap", 4, Aim.Hunt),
			new Intent
			{
				Name = "Drift",
				Kind = IntentType.Move,
				Amount = 1,
			}
		) with
		{
			Family = Family.Mire,
			Position = position,
		};

	/// <summary>**Stonebeak — PIERCES your front two**, so a stacked front pair both bleed.</summary>
	public static Foe Stonebeak(int position) =>
		Creature(
			"Stonebeak",
			16,
			Attack("Dive", 7, Aim.Pierce),
			Attack("Dive", 7, Aim.Pierce),
			Guard("Preen", 6)
		) with
		{
			Family = Family.Storm,
			Position = position,
		};

	// ===== THE RUN

	/// <summary>
	/// **The starters — choosing one IS choosing the run's family.** Grove and Ember only: Storm and
	/// Mire are shelved until built (`KinFamiliesPlan.md`, round 2). Gale stays for the practice fights.
	/// </summary>
	public static readonly ImmutableList<PartyCompanion> Roster = [Bramble, Pike];

	/// <summary>
	/// **Every card a win or a shop can offer.** A run sees only its family's and the colourless ones
	/// (`PartyRun.RewardOffer`); Storm's and Mire's wait here, shelved, for their families.
	/// </summary>
	public static readonly ImmutableList<KinCard> Rewards =
	[
		// ----- Colourless
		Rally,
		Stagger,
		PartyCards.Charge,
		PartyCards.HoldTheLine,
		Card(
			"Strike",
			1,
			"It attacks their front now: 3 + Power.",
			new StrikeAction { Amount = 3 }
		),
		Card(
			"Whirl",
			2,
			"It attacks their whole line now: 2 + Power.",
			new StrikeAction { Amount = 2, Aim = Aim.Sweep }
		) with
		{
			Rarity = Rarity.Uncommon,
		},
		Card("Bulwark", 2, "Gain 12 Block.", new GuardAction { Amount = 12 }),
		Card("Frenzy", 1, "+5 Power this turn.", new PowerAction { Amount = 5 }),
		PartyCards.Tailwind,
		PartyCards.Surge,
		PartyCards.Quicken,
		PartyCards.BattleCry,
		PartyCards.Unleash,
		PartyCards.Sift,
		PartyCards.Rummage,
		PartyCards.Ration,
		PartyCards.ScrapHammer,
		PartyCards.PageStorm,
		.. GroveCards.Pool,
		.. EmberCards.Pool,
	];

	public static readonly PartyScenario Alone =
		new(
			"One against two",
			"Pike alone against a Boar and a Wisp.",
			[new(Pike, 0)],
			[Boar(0), Wisp(1)],
			StarterDeck,
			[],
			Deploy: true
		);

	public static readonly PartyScenario Pair =
		new(
			"Two against three",
			"Bramble holds the front for Pike against a Boar, a Stonebeak and a Wisp.",
			[new(Bramble, 0), new(Pike, 1)],
			[Boar(0), Stonebeak(1), Wisp(2)],
			StarterDeck,
			[],
			Deploy: true
		);

	/// <summary>
	/// **The Relay's intro: the Wall in front, the Finisher behind her, the Controller at the back.**
	/// Charge swaps Pike to the front to cash the relay — where the Boar's Charge lands.
	/// </summary>
	public static readonly PartyScenario Trio =
		new(
			"Three against three",
			"Bramble, Pike and Gale against a Boar, a Stonebeak and a Wisp.",
			[new(Bramble, 0), new(Pike, 1), new(Gale, 2)],
			[Boar(0), Stonebeak(1), Wisp(2)],
			[.. StarterDeck, PartyCards.Charge, PartyCards.Gust, Rally, Stagger],
			["Charge", "Gust", "Rally", "Guard", "Stagger"],
			Deploy: true
		);

	/// <summary>
	/// **Discard + Draw, to try by hand.** A caught Magpie and Inkling, with Sift, Rummage and Jot
	/// dealt in; the payoffs in the deck; a Hoard Drake and a wild Magpie to test them.
	/// A property, not a field: it reads `PartyWorld`, which reads this class.
	/// </summary>
	public static PartyScenario Looting =>
		new(
			"Draw and discard",
			"Pike, a Magpie and an Inkling against a Hoard Drake and a wild Magpie.",
			[
				new(Pike, 0),
				new(PartyRun.FromFoe(PartyWorld.Magpie), 1),
				new(PartyRun.FromFoe(PartyWorld.Inkling), 2),
			],
			[PartyWorld.HoardDrake with { Position = 0 }, PartyWorld.Magpie with { Position = 1 }],
			[
				.. StarterDeck,
				PartyCards.Sift,
				PartyCards.Rummage,
				PartyCards.Jot,
				PartyCards.ScrapHammer,
				PartyCards.PageStorm,
				PartyCards.Ration,
			],
			["Sift"],
			Deploy: true
		);

	/// <summary>
	/// **Ember, to try by hand.** Pike, an Emberling (Stoker) and an Echo Owl (the 3rd spell twice)
	/// against a Warden in front (spells deal half) guarding a Wisp and a Briar Viper.
	/// </summary>
	public static PartyScenario Spellcraft =>
		new(
			"Ember",
			"Pike, an Emberling and an Echo Owl against a Warden, a Wisp and a Briar Viper.",
			[new(Pike, 0), new(EmberCards.Emberling, 1), new(EmberCards.EchoOwl, 2)],
			[
				PartyWorld.Warden with
				{
					Position = 0,
				},
				Wisp(1),
				PartyWorld.BriarViper with
				{
					Position = 2,
				},
			],
			[
				.. StarterDeck,
				EmberCards.Kindle,
				EmberCards.Zap,
				EmberCards.Overload,
				EmberCards.Singe,
				EmberCards.Spark,
			],
			["Kindle", "Singe", "Spark"],
			Deploy: true
		);

	/// <summary>
	/// **Surge, to try by hand.** A caught Glowmoth (unhit: +1 energy) and Stormbuck (a kill during
	/// your turn: +1 energy) against a Hushcap (your first card costs 1 more), whose Spores HUNT the
	/// fragile moth wherever it stands.
	/// </summary>
	public static PartyScenario SurgeScenario =>
		new(
			"Surge",
			"Pike, a Stormbuck and a Glowmoth against a Hushcap, a Boar and a Wisp.",
			[
				new(Pike, 0),
				new(PartyRun.FromFoe(PartyWorld.Stormbuck), 1),
				new(PartyRun.FromFoe(PartyWorld.Glowmoth), 2),
			],
			[PartyWorld.Hushcap with { Position = 0 }, Boar(1), Wisp(2)],
			[
				.. StarterDeck,
				PartyCards.Surge,
				PartyCards.Quicken,
				PartyCards.Unleash,
				EmberCards.Meteor,
				PartyCards.BattleCry,
			],
			["Surge", "Quicken", "Unleash"],
			Deploy: true
		);

	/// <summary>
	/// **Grove, to try by hand.** Bramble, a Broodvine (tokens arrive bigger) and a Howler (a token lost
	/// grows the team) against an Ironhorn, whose Trample goes through a token into the one behind.
	/// </summary>
	public static PartyScenario SummonScenario =>
		new(
			"Grove",
			"Bramble, a Broodvine and a Howler against an Ironhorn, a Wisp and a Stonebeak.",
			[new(Bramble, 0), new(GroveCards.Broodvine, 1), new(GroveCards.Howler, 2)],
			[PartyWorld.Ironhorn with { Position = 0 }, Wisp(1), Stonebeak(2)],
			[
				.. StarterDeck,
				GroveCards.Sow,
				GroveCards.Seedlings,
				GroveCards.Graft,
				GroveCards.Harvest,
				GroveCards.PackCharge,
				GroveCards.Compost,
			],
			["Sow", "Seedlings", "Graft"],
			Deploy: true
		);

	/// <summary>
	/// **The monsters a boss can offer a run of this family** (round 4: monsters come from bosses).
	/// Each family's four, one per archetype (`GroveCards`, `EmberCards`).
	/// </summary>
	public static ImmutableList<PartyCompanion> MonstersOf(Family family) =>
		family switch
		{
			Family.Grove => GroveCards.Monsters,
			Family.Ember => EmberCards.Monsters,
			_ => [],
		};

	public static ImmutableList<PartyScenario> Scenarios =>
		[Trio, Alone, Pair, Looting, Spellcraft, SurgeScenario, SummonScenario];
}

/// <summary>Builds one battle's GameState from a scenario and deals the first hand.</summary>
public static class PartyBattleFactory
{
	public static GameState Create(PartyScenario scenario, int seed = 0)
	{
		var s = new GameState
		{
			RngSeed = seed,
			PostActionProcessor = new FirePartyTriggersAction(),
		};

		(s, var battle) = s.AddObject(
			new PartyBattle
			{
				Name = scenario.Name,
				Description = scenario.Description,
				Energy = 3,
				Relics = scenario.Relics ?? [],
			}
		);
		(s, var draw) = s.AddObject(
			new Zone { Name = "Draw", ZoneType = ZoneType.Draw },
			battle.Id
		);
		(s, var hand) = s.AddObject(
			new Zone { Name = "Hand", ZoneType = ZoneType.Hand },
			battle.Id
		);
		(s, var discard) = s.AddObject(
			new Zone { Name = "Discard", ZoneType = ZoneType.Discard },
			battle.Id
		);

		// The deck zones reuse the OLD game's keys, so `StartTurnAction.DrawCards` works unchanged.
		s = s.RegisterWellKnownId(PartyState.BattleKey, battle.Id)
			.RegisterWellKnownId(KinObjectKeys.Draw, draw.Id)
			.RegisterWellKnownId(KinObjectKeys.Hand, hand.Id)
			.RegisterWellKnownId(KinObjectKeys.Discard, discard.Id);

		foreach (
			var ((companion, position, hp), slot) in scenario.Companions.Select((c, i) => (c, i))
		)
		{
			(s, _) = s.AddObject(
				new Ally
				{
					Slot = slot,
					Name = companion.Name,
					Family = companion.Family,
					Hp = hp ?? companion.Hp,
					MaxHp = companion.Hp,
					Power = companion.Power,
					SpellPower = companion.SpellPower,
					Position = position,
					Pattern = companion.Moves,
					Passive = companion.Passive,
					PassiveRule = companion.PassiveRule,
					Thorns = companion.Thorns,
					FinisherPerAlly = companion.FinisherPerAlly,
					Unbalances = companion.Unbalances,
					Components = [.. companion.Abilities],
				},
				battle.Id
			);
		}

		foreach (var foe in scenario.Foes)
			(s, _) = s.AddObject(foe, battle.Id);

		// Both lines closed up from whatever positions the scenario named — contiguous from the front.
		(s, _) = s.Settle();

		foreach (var card in scenario.Deck)
			(s, _) = s.AddObject(card, draw.Id);

		s = KinRng.ShuffleZone(s, draw.Id);

		// Reversed, so the FIRST name ends up on top.
		var used = new HashSet<int>();
		foreach (var name in scenario.OpeningHand.Reverse())
		{
			var id = s.GetChildren(draw.Id)
				.OfType<KinCard>()
				.First(c => c.Name == name && !used.Contains(c.Id))
				.Id;
			used.Add(id);
			s = s.MoveObjectToFront(id, draw.Id);
		}

		s = s.AddAction(new StartPartyTurnAction()).ProcessAllActions().State;
		s = PartyRelics.Dealt(s);

		// DEPLOY (R2): the hand is dealt, and the fight waits for FIGHT.
		return scenario.Deploy
			? s.UpdateObject(s.GetParty().Id, s.GetParty() with { Deploying = true })
			: PartyRelics.FightBegins(s);
	}
}
