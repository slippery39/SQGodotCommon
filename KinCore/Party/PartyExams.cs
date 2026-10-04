using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **THE EXAMS — regions 1–2's bosses and elites** (`KinFamiliesPlan.md`, round 3; each approved by
/// Shayne, 2026-09-28). Every one TESTS A DECK QUALITY and says its answer. **Numbers are AUTHORED at
/// the region's strength** (2026-10-03, no levels): what the level and boss multipliers made of them,
/// then +50% HP for a team of three from the start. None can be caught.
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
		};

	/// <summary>A MINION: stays until beaten (no fade).</summary>
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
					195,
					0,
					WindUp("Paw the Ground"),
					Attack("Gore", 21),
					Attack("Trample", 5, Aim.Sweep)
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
					126,
					0,
					Summon("Call the Band", Minion("Goblin", 9, Attack("Stab", 3))),
					Attack("Spear", 8),
					Summon("Call the Band", Minion("Goblin", 9, Attack("Stab", 3)))
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
				Exam("Iron Sentinel", 135, 0, Guard("Brace", 12), Attack("Slam", 10)) with
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
				Exam("Goblin", 46, 0, Attack("Stab", 5)),
				Exam("Goblin", 46, 1, Attack("Stab", 5)),
				Exam(
					"Goblin Thief",
					46,
					2,
					Attack("Snatch", 4) with
					{
						Steals = true,
					},
					Attack("Stab", 5)
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
					294,
					0,
					new Intent { Name = "Tongue", Kind = IntentType.Pull },
					Attack("Swallow", 29) with
					{
						Inflicts = Debuff.Weak,
						InflictTurns = 2,
					},
					Attack("Deluge", 9, Aim.Sweep)
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
								Guard("Wallow", 21),
								Attack("Swallow", 29) with
								{
									Inflicts = Debuff.Weak,
									InflictTurns = 2,
								},
								Summon("Spawn", Minion("Toadling", 14, Attack("Spit", 6))),
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
					270,
					0,
					Attack("Cleave", 14, Aim.Pierce) with
					{
						Inflicts = Debuff.Vulnerable,
						InflictTurns = 2,
					},
					Guard("Guard", 14)
				) with
				{
					Trait =
						$"ENRAGE: +{new Enrage().PerRound} to its attacks every round. At half HP, a SECOND WIND.",
					Components = [new Enrage(), new Phase { Name = "SECOND WIND", Block = 28 }],
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
				Exam("Golem", 142, 0, Attack("Slam", 16), Attack("Stomp", 6, Aim.Sweep)),
				Exam(
					"Hexer",
					92,
					1,
					Attack("Bolt", 10, Aim.Hunt) with
					{
						Inflicts = Debuff.Silence,
						InflictTurns = 1,
					},
					Guard("Ward", 13, Aim.Ahead)
				) with
				{
					Components = [new FirstCardCost { Amount = 1 }],
					Trait =
						"HEX: your first card each turn costs 1 more while she stands. Her Bolt SILENCES.",
				},
			]
		);

	/// <summary>**The Harpy Flock — PROTECTING the fragile.** They rake your weakest; one screeches your line apart.</summary>
	public static readonly Encounter HarpyFlock =
		new(
			"Harpy Flock",
			[
				Exam("Harpy", 44, 0, Attack("Rake", 9, Aim.Hunt)),
				Exam("Harpy", 44, 1, Attack("Rake", 9, Aim.Hunt)),
				Exam(
					"Screeching Harpy",
					44,
					2,
					new Intent
					{
						Name = "Screech",
						Kind = IntentType.Shove,
						Inflicts = Debuff.Shaken,
						InflictTurns = 1,
					},
					Attack("Rake", 9, Aim.Hunt)
				) with
				{
					Trait = "Its SCREECH swaps your front two and SHAKES the new front.",
				},
			]
		);
}
