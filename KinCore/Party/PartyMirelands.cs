namespace KinCore.Party;

/// <summary>
/// **REGION 2 — THE MIRELANDS: bog beasts and HAGS** (`KinEnemiesPlan.md`, step 3 — the first of its
/// real roster). Authored at region 2's strength (~×1.6 of the Greenwood). These four bring JUNK
/// (`PartyJunk`): they fight your DECK, not only your HP. The rest of the region is still placeholder.
/// </summary>
public static class PartyMirelands
{
	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	/// <summary>A move that only adds junk.</summary>
	private static Intent Curse(string name, Junk junk, int count) =>
		new()
		{
			Name = name,
			Kind = IntentType.Curse,
			AddsJunk = junk,
			JunkCount = count,
		};

	private static Foe Creature(string name, int hp, string trait, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Pattern = [.. cycle],
			Trait = trait,
		};

	/// <summary>Clogs the hand: MIRE ×2 into your draw pile, then a claw.</summary>
	public static readonly Foe BogHag = Creature(
		"Bog Hag",
		26,
		"Her CURSE shuffles 2 Mire into your draw pile — unplayable.",
		Curse("Curse", Junk.Mire, 2),
		Attack("Claw", 8)
	);

	/// <summary>Every turn, a DOUBT in your hand: play it, or lose an energy.</summary>
	public static readonly Foe Witch = Creature(
		"Witch",
		20,
		"Each WHISPER puts a Doubt in your hand.",
		Attack("Whisper", 5, Aim.Hunt) with
		{
			AddsJunk = Junk.Doubt,
			JunkCount = 1,
		}
	);

	/// <summary>Fragile, but its bite leaves ROT in your discard pile.</summary>
	public static readonly Foe PlagueRat = Creature(
		"Plague Rat",
		12,
		"Its BITE puts a Rot in your discard pile.",
		Attack("Bite", 6) with
		{
			AddsJunk = Junk.Rot,
			JunkCount = 1,
		}
	);

	/// <summary>Holds your line still: a WEB in your hand, then a heavy bite.</summary>
	public static readonly Foe GiantSpider = Creature(
		"Giant Spider",
		32,
		"Its WEB in your hand stops your line cards until you pay to clear it.",
		Curse("Spin Web", Junk.Web, 1),
		Attack("Bite", 11)
	);
}
