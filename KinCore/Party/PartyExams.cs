using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE EXAMS — regions 1–2's bosses and elites** (`KinFamiliesPlan.md`, round 3; each approved by
/// Shayne, 2026-09-28). Every one TESTS A DECK QUALITY and says its answer; numbers are at Lv 5 like
/// all content, and `PartyWorld` scales them to the region. None is a species: none can be caught.
/// </summary>
public static class PartyExams
{
	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static Intent Guard(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
			Target = aim,
		};

	private static Intent WindUp(string name) => new() { Name = name, Kind = IntentType.WindUp };

	private static Intent Summon(string name, TokenTemplate who) =>
		new()
		{
			Name = name,
			Kind = IntentType.Summon,
			Summons = who,
		};

	private static Foe Exam(string name, int hp, int position, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Position = position,
			Pattern = [.. cycle],
			Catchable = false,
		};

	/// <summary>A MINION: stays until beaten (no fade), and comes in at its summoner's level.</summary>
	private static TokenTemplate Minion(string name, int hp, Intent move) =>
		new(new PartyCompanion(name, hp, 0, [move]), FadesIn: 0);

	// ===== REGION 1 BOSSES — one idea each, for a team of two

	/// <summary>
	/// **The Old Tusker — BLOCK: read the telegraph.** Its wind-up shows the Gore a turn early; Guard,
	/// Stagger or a wall in front answers it. Nothing else it does is big.
	/// </summary>
	public static readonly Encounter OldTusker =
		new(
			"The Old Tusker",
			[
				Exam(
					"Old Tusker",
					56,
					0,
					WindUp("Paw the Ground"),
					Attack("Gore", 18),
					Attack("Trample", 4, Aim.Sweep)
				) with
				{
					Trait = "Paws the ground a turn before it GORES.",
				},
			]
		);

	/// <summary>
	/// **The Goblin Chief — FOCUS vs SPREAD.** It calls a goblin to its FRONT every other move, so the
	/// band grows between you and it: sweep the band, or reach past it for the Chief.
	/// </summary>
	public static readonly Encounter GoblinChief =
		new(
			"The Goblin Chief",
			[
				Exam(
					"Goblin Chief",
					36,
					0,
					Summon("Call the Band", Minion("Goblin", 8, Attack("Stab", 3))),
					Attack("Spear", 7),
					Summon("Call the Band", Minion("Goblin", 8, Attack("Stab", 3)))
				) with
				{
					Trait = "Calls a GOBLIN to its front, again and again.",
				},
			]
		);

	// ===== REGION 1 ELITES — beatable by a starter and one half-HP catch

	/// <summary>**The Iron Sentinel — BIG HITS.** Chip damage and tokens bounce off its Shell.</summary>
	public static readonly Encounter IronSentinel =
		new(
			"The Iron Sentinel",
			[
				Exam("Iron Sentinel", 40, 0, Guard("Brace", 10), Attack("Slam", 8)) with
				{
					Components = [new Shell()],
					Trait = $"SHELL: a hit of {new Shell().AtMost} or less does nothing.",
				},
			]
		);

	/// <summary>**Goblin Raiders — SPREAD.** Three goblins; the back one snatches a card until it falls.</summary>
	public static readonly Encounter GoblinRaiders =
		new(
			"Goblin Raiders",
			[
				Exam("Goblin", 14, 0, Attack("Stab", 4)),
				Exam("Goblin", 14, 1, Attack("Stab", 4)),
				Exam(
					"Goblin Thief",
					14,
					2,
					Attack("Snatch", 3) with
					{
						Steals = true,
					},
					Attack("Stab", 4)
				) with
				{
					Trait = "A THIEF: its Snatch takes a card until it is beaten.",
				},
			]
		);

	// ===== REGION 2 BOSSES — a phase and a rule each, for a team of three

	/// <summary>
	/// **The Old Mire — LINE ORDER.** Its Tongue drags your BACK monster to the front, where the
	/// Swallow lands: the answer is who you put at the back. At half HP it SUBMERGES — Block, and
	/// toadlings.
	/// </summary>
	public static readonly Encounter OldMire =
		new(
			"The Old Mire",
			[
				Exam(
					"Old Mire",
					70,
					0,
					new Intent { Name = "Tongue", Kind = IntentType.Pull },
					Attack("Swallow", 16),
					Attack("Deluge", 5, Aim.Sweep)
				) with
				{
					Trait =
						"Its TONGUE drags your back monster to the front. At half HP it SUBMERGES.",
					Components =
					[
						new Phase
						{
							Name = "SUBMERGED",
							Pattern =
							[
								Guard("Wallow", 15),
								Attack("Swallow", 16),
								Summon("Spawn", Minion("Toadling", 10, Attack("Spit", 4))),
							],
							Trait = "SUBMERGED: it wallows for Block and spawns toadlings.",
						},
					],
				},
			]
		);

	/// <summary>
	/// **The Black Knight — a DAMAGE RACE.** ENRAGE sharpens every blow each round; a slow wall loses.
	/// At half HP, a Second Wind of Block, once.
	/// </summary>
	public static readonly Encounter BlackKnight =
		new(
			"The Black Knight",
			[
				Exam(
					"Black Knight",
					64,
					0,
					Attack("Cleave", 8, Aim.Pierce),
					Guard("Guard", 10)
				) with
				{
					Trait =
						$"ENRAGE: +{new Enrage().PerRound} to its attacks every round. At half HP, a SECOND WIND.",
					Components = [new Enrage(), new Phase { Name = "SECOND WIND", Block = 20 }],
				},
			]
		);

	// ===== REGION 2 ELITES

	/// <summary>
	/// **The Hexer and her Golem — REACH.** Her HEX taxes your first card each turn while she stands,
	/// and she WARDS the Golem in front of her: kill her first, past it.
	/// </summary>
	public static readonly Encounter HexerAndGolem =
		new(
			"The Hexer and her Golem",
			[
				Exam("Golem", 40, 0, Attack("Slam", 9), Attack("Stomp", 4, Aim.Sweep)),
				Exam("Hexer", 26, 1, Attack("Bolt", 6, Aim.Hunt), Guard("Ward", 10, Aim.Ahead)) with
				{
					Components = [new FirstCardCost { Amount = 1 }],
					Trait = "HEX: your first card each turn costs 1 more while she stands.",
				},
			]
		);

	/// <summary>**The Harpy Flock — PROTECTING the fragile.** They rake your weakest; one screeches your line apart.</summary>
	public static readonly Encounter HarpyFlock =
		new(
			"Harpy Flock",
			[
				Exam("Harpy", 12, 0, Attack("Rake", 5, Aim.Hunt)),
				Exam("Harpy", 12, 1, Attack("Rake", 5, Aim.Hunt)),
				Exam(
					"Screeching Harpy",
					12,
					2,
					new Intent { Name = "Screech", Kind = IntentType.Shove },
					Attack("Rake", 5, Aim.Hunt)
				) with
				{
					Trait = "Its SCREECH swaps your front two.",
				},
			]
		);
}
