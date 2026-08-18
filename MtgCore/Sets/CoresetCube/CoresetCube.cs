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
		];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
