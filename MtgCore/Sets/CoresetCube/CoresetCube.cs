namespace MtgCore;

/// <summary>
/// https://cubecobra.com/cube/list/magiccoreset20xx
/// </summary>
public static class CoresetCube
{
	public const string Code = "CSC";
	public const string Name = "Core Set Cube";

	/// <summary>
	/// The full set, assembled from its colour files. The split is by the cube's own colour
	/// sections, which keeps each file reviewable and mirrors how the source list is organised.
	///
	/// Tokens live in CoresetCubeTokens and are deliberately excluded — they must never be
	/// drafted.
	///
	/// THE CUBE'S 42 LANDS ARE DELIBERATELY ABSENT — 35 duals and 7 utility. Draft.Create excludes
	/// lands from packs and Draft.BuildDeck supplies the mana base, so none of them could ever be
	/// drafted or played; with no colours in this engine a dual land is a basic; and Rogue's
	/// Passage and Lotus Field each need a mechanic that is structurally absent. So the cube's 450
	/// is 408 here, and that is the complete set rather than a partial one.
	/// </summary>
	public static IReadOnlyList<Card> Cards { get; } =
		[
			.. CoresetCubeWhite.Cards,
			.. CoresetCubeWhiteSpells.Cards,
			.. CoresetCubeWhitePermanents.Cards,
			.. CoresetCubeBlue.Cards,
			.. CoresetCubeBlueSpells.Cards,
			.. CoresetCubeBluePermanents.Cards,
			.. CoresetCubeBlack.Cards,
			.. CoresetCubeBlackSpells.Cards,
			.. CoresetCubeBlackPermanents.Cards,
			.. CoresetCubeRed.Cards,
			.. CoresetCubeRedSpells.Cards,
			.. CoresetCubeRedPermanents.Cards,
			.. CoresetCubeGreen.Cards,
			.. CoresetCubeGreenSpells.Cards,
			.. CoresetCubeGreenPermanents.Cards,
			.. CoresetCubeColourlessCreatures.Cards,
			.. CoresetCubeColourlessArtifacts.Cards,
			.. CoresetCubeColourlessEquipment.Cards,
			.. CoresetCubeMulticolour.Cards,
			.. CoresetCubeMulticolourSpells.Cards,
		];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
