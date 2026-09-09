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
	/// COLOUR is stamped here, at the section boundary, rather than on 335 individual cards:
	/// InColor gives one pip of the section's colour to every card that has not declared its own.
	/// A card needing a double pip or two colours calls WithPips in its own definition and is
	/// passed through untouched. The colourless and multicolour sections are deliberately NOT
	/// stamped — colourless has no pips, and every gold card states both of its colours.
	///
	/// THE CUBE'S 42 LANDS ARE DELIBERATELY ABSENT — 35 duals and 7 utility. Draft.Create excludes
	/// lands from packs and Draft.BuildDeck supplies the mana base, so none of them could ever be
	/// drafted or played; with no colours in this engine a dual land is a basic; and Rogue's
	/// Passage and Lotus Field each need a mechanic that is structurally absent. So the cube's 450
	/// is 408 here, and that is the complete set rather than a partial one.
	/// </summary>
	public static IReadOnlyList<Card> Cards { get; } =
		[
			.. CoresetCubeWhite.Cards.InColor(ManaColor.White),
			.. CoresetCubeWhiteSpells.Cards.InColor(ManaColor.White),
			.. CoresetCubeWhitePermanents.Cards.InColor(ManaColor.White),
			.. CoresetCubeBlue.Cards.InColor(ManaColor.Blue),
			.. CoresetCubeBlueSpells.Cards.InColor(ManaColor.Blue),
			.. CoresetCubeBluePermanents.Cards.InColor(ManaColor.Blue),
			.. CoresetCubeBlack.Cards.InColor(ManaColor.Black),
			.. CoresetCubeBlackSpells.Cards.InColor(ManaColor.Black),
			.. CoresetCubeBlackPermanents.Cards.InColor(ManaColor.Black),
			.. CoresetCubeRed.Cards.InColor(ManaColor.Red),
			.. CoresetCubeRedSpells.Cards.InColor(ManaColor.Red),
			.. CoresetCubeRedPermanents.Cards.InColor(ManaColor.Red),
			.. CoresetCubeGreen.Cards.InColor(ManaColor.Green),
			.. CoresetCubeGreenSpells.Cards.InColor(ManaColor.Green),
			.. CoresetCubeGreenPermanents.Cards.InColor(ManaColor.Green),
			.. CoresetCubeColourlessCreatures.Cards,
			.. CoresetCubeColourlessArtifacts.Cards,
			.. CoresetCubeColourlessEquipment.Cards,
			.. CoresetCubeMulticolour.Cards,
			.. CoresetCubeMulticolourSpells.Cards,
		];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
