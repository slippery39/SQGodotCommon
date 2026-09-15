using System.Collections.Generic;
using Godot;

namespace DoomGame;

/// <summary>
/// Flat card faces, generated rather than drawn.
///
/// `Card2D` is a stack of Sprite2Ds — frame, name plate, art window, cost badge, rules box — and
/// every one of those textures is settable through `Details`. So DOOMJAM keeps the whole shared
/// card: its drag, its hover, its fan, its shader. **Only the pixels change.** Forking `CardUI2D`
/// to get a flat look would have cost 300 lines of duplicated interaction to change the colour of
/// a rectangle.
///
/// Each texture is generated at the EXACT size of the part it replaces, because the scene positions
/// those sprites for those dimensions. Change a size here and the card silently comes apart.
///
/// Colours are the five in <see cref="DoomPalette"/>. See DoomUI.md.
/// </summary>
public static class DoomCardArt
{
	private const int FrameW = 312;
	private const int FrameH = 445;
	private const int PlateW = 279;

	private static Texture2D _frame;
	private static Texture2D _namePlate;
	private static readonly Dictionary<Color, Texture2D> Rules = new();
	private static Texture2D _costBadge;
	private static Texture2D _statBadge;
	private static readonly Dictionary<Color, Texture2D> Art = new();

	/// <summary>The card body: flat slate with a bone edge, corners rounded like the reference.</summary>
	public static Texture2D Frame =>
		_frame ??= RoundedRect(FrameW, FrameH, DoomPalette.Slate, DoomPalette.Bone, 3, 20);

	public static Texture2D NamePlate =>
		_namePlate ??= RoundedRect(PlateW, 53, DoomPalette.Navy, DoomPalette.Navy, 0, 8);

	/// <summary>
	/// The lower half of the card. Painted in the SAME colour as the art block above it, so the two
	/// sprites read as one solid card face — see the note where this is assigned.
	/// </summary>
	public static Texture2D RulesBlock(Color colour)
	{
		if (!Rules.TryGetValue(colour, out var texture))
			Rules[colour] = texture = RoundedRect(PlateW, 158, colour, colour, 0, 0);

		return texture;
	}

	/// <summary>The cost badge, and the one circle on the card — it is what the eye goes to first.</summary>
	public static Texture2D CostBadge =>
		_costBadge ??= Circle(81, 83, DoomPalette.Navy, DoomPalette.Bone, 4);

	/// <summary>
	/// The power/toughness badge. RED, because red means "the enemy, and life" everywhere else on
	/// this screen and a unit's toughness IS life — the same currency in two forms. The shared card
	/// ships a blue one.
	/// </summary>
	public static Texture2D StatBadge =>
		_statBadge ??= Circle(64, 64, DoomPalette.Red, DoomPalette.Bone, 3);

	/// <summary>
	/// The art window as a single flat colour block. There is no card art yet, and a flat block is
	/// not a placeholder for it — it is the reference's own treatment.
	/// </summary>
	public static Texture2D ArtBlock(Color colour)
	{
		if (!Art.TryGetValue(colour, out var texture))
			Art[colour] = texture = RoundedRect(278, 198, colour, colour, 0, 0);

		return texture;
	}

	/// <summary>
	/// Which flat colour a card's art block gets. Stable per name, so a Scavenger is always the same
	/// colour and the hand stays readable at a glance — the block is doing the job an icon would.
	/// </summary>
	public static Color ColourFor(string cardName)
	{
		Color[] options =
		[
			Color.FromHtml("#2B4257"),
			Color.FromHtml("#6E3630"),
			Color.FromHtml("#2F5450"),
			Color.FromHtml("#3B3A63"),
			Color.FromHtml("#5A4A2C"),
		];

		// Seeded with the length so that names of the same shape do not collide — Scavenger and
		// Bulwark landed on the same colour otherwise, which defeats the point of colouring them.
		var hash = (cardName ?? "").Length * 7;
		foreach (var c in cardName ?? "")
			hash = (hash * 31 + c) & 0x7FFFFFFF;

		return options[hash % options.Length];
	}

	// ===== Generation =====

	private static Texture2D RoundedRect(
		int width,
		int height,
		Color fill,
		Color border,
		int borderWidth,
		int radius
	)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

		for (var y = 0; y < height; y++)
		for (var x = 0; x < width; x++)
		{
			if (OutsideCorner(x, y, width, height, radius))
			{
				image.SetPixel(x, y, Colors.Transparent);
				continue;
			}

			var onBorder =
				borderWidth > 0
				&& (
					x < borderWidth
					|| y < borderWidth
					|| x >= width - borderWidth
					|| y >= height - borderWidth
					|| NearCornerEdge(x, y, width, height, radius, borderWidth)
				);

			image.SetPixel(x, y, onBorder ? border : fill);
		}

		return ImageTexture.CreateFromImage(image);
	}

	private static Texture2D Circle(int width, int height, Color fill, Color ring, int ringWidth)
	{
		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		var cx = (width - 1) / 2f;
		var cy = (height - 1) / 2f;
		var outer = Mathf.Min(cx, cy);

		for (var y = 0; y < height; y++)
		for (var x = 0; x < width; x++)
		{
			var distance = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

			image.SetPixel(
				x,
				y,
				distance > outer ? Colors.Transparent
					: distance > outer - ringWidth ? ring
					: fill
			);
		}

		return ImageTexture.CreateFromImage(image);
	}

	/// <summary>True for pixels cut away by a rounded corner.</summary>
	private static bool OutsideCorner(int x, int y, int width, int height, int radius) =>
		CornerDistance(x, y, width, height, radius) > radius;

	/// <summary>True for pixels inside a corner but within the border band.</summary>
	private static bool NearCornerEdge(
		int x,
		int y,
		int width,
		int height,
		int radius,
		int borderWidth
	) => CornerDistance(x, y, width, height, radius) > radius - borderWidth;

	/// <summary>
	/// Distance from the nearest corner's arc centre, or 0 anywhere that is not in a corner box.
	/// </summary>
	private static float CornerDistance(int x, int y, int width, int height, int radius)
	{
		if (radius <= 0)
			return 0;

		var cx =
			x < radius ? radius
			: x >= width - radius ? width - 1 - radius
			: -1;
		var cy =
			y < radius ? radius
			: y >= height - radius ? height - 1 - radius
			: -1;

		if (cx < 0 || cy < 0)
			return 0;

		return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
	}
}
