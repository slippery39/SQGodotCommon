namespace MtgCore;

/// <summary>
/// https://cubecobra.com/cube/list/magiccoreset20xx
/// </summary>
public static class CoresetCube
{
	public const string Code = "CSC";
	public const string Name = "Core Set Cube";

	/// <summary>
	/// The full set, assembled from its theme files. Themes are split across files to keep
	/// each reviewable; the split is by primary theme, but most cards touch several.
	/// </summary>
	public static IReadOnlyList<Card> Cards { get; } = [];

	public static CardSet Set { get; } = new(Code, Name, Cards);
}
