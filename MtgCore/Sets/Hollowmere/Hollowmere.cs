namespace MtgCore;

/// <summary>
/// Hollowmere (HLM) — a graveyard-themed gothic-horror draft set.
///
/// Ten overlapping themes stand in for colours, which this engine does not have: with no
/// colour restriction, synergy density is the only thing steering a drafter toward a coherent
/// deck, so cards are designed to read as two or three themes at once.
///
/// Two engine facts drive every rate in this set:
///   - Combat has no blockers. A body alone is only a clock, so expensive cards must affect
///     the board on resolution. Recurring drawbacks are near-unplayable — there is no
///     chump-blocking to convert them into value.
///   - Draft.BuildDeck takes the first 27 picks in PICK ORDER, not the best 27 by curve, so
///     expensive cards are already a structural liability and must earn their slot.
///
/// Target curve: median cost 3, with roughly 25-30 cards at 6+ that the reanimation package
/// makes castable ahead of schedule.
/// </summary>
public static class Hollowmere
{
	public const string Code = "HLM";
	public const string Name = "Hollowmere";

	// Creature types. Shared here rather than repeated as literals so a rename is one edit
	// and a typo cannot silently break a tribal lord's filter.
	public const string Human = "Human";
	public const string Spirit = "Spirit";
	public const string Zombie = "Zombie";
	public const string Vampire = "Vampire";
	public const string Werewolf = "Werewolf";
	public const string Angel = "Angel";
	public const string Demon = "Demon";
	public const string Wizard = "Wizard";
	public const string Cleric = "Cleric";
	public const string Soldier = "Soldier";
	public const string Rogue = "Rogue";
	public const string Horror = "Horror";
	public const string Insect = "Insect";

	/// <summary>
	/// The full set, assembled from its theme files. Themes are split across files to keep
	/// each reviewable; the split is by primary theme, but most cards touch several.
	/// </summary>
	public static IReadOnlyList<Card> Cards { get; } =
		[
			.. HollowmereGraveyard.Cards,
			.. HollowmereDiscard.Cards,
			.. HollowmereHumans.Cards,
			.. HollowmereMill.Cards,
			.. HollowmereAngelsDemons.Cards,
			.. HollowmereSpirits.Cards,
			.. HollowmereSpells.Cards,
			.. HollowmereZombies.Cards,
			.. HollowmereWerewolves.Cards,
			.. HollowmereVampires.Cards,
			.. HollowmereGlue.Cards,
		];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
