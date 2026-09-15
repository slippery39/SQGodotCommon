using System.Collections.Generic;
using Godot;

namespace DoomGame;

/// <summary>
/// Flat art, generated rather than drawn — card faces and lane figures.
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
public static class DoomArt
{
	private const int FrameW = 312;
	private const int FrameH = 445;
	private const int PlateW = 279;

	private static readonly Dictionary<Color, Texture2D> Rules = new();
	private static Texture2D _costBadge;
	private static Texture2D _statBadge;
	private static readonly Dictionary<Color, Texture2D> Art = new();

	private static readonly Dictionary<Color, Texture2D> Bodies = new();
	private static readonly Dictionary<Color, Texture2D> CardFigures = new();

	/// <summary>
	/// The WHOLE card, in one colour with a bone edge.
	///
	/// The frame sits behind the art window and the rules box, so painting those two transparent
	/// leaves this as the entire face — one solid rounded shape, which is what the reference card
	/// actually is. Building the colour out of stacked panels instead left a seam across the middle
	/// and read as three things glued together.
	/// </summary>
	public static Texture2D Body(Color colour)
	{
		if (!Bodies.TryGetValue(colour, out var texture))
			Bodies[colour] = texture = RoundedRect(FrameW, FrameH, colour, DoomPalette.Bone, 3, 22);

		return texture;
	}

	/// <summary>Nothing at all, at a given size — used to clear a sprite that would otherwise stack.</summary>
	public static Texture2D Blank(int width, int height) =>
		RoundedRect(width, height, Colors.Transparent, Colors.Transparent, 0, 0);

	/// <summary>
	/// The card's centre mark: the same silhouette that stands in a lane, in a darker shade of the
	/// card's own colour. Reusing the lane figure is what makes a card and the body it becomes
	/// legibly the same thing.
	/// </summary>
	public static Texture2D CardFigure(Color colour)
	{
		if (CardFigures.TryGetValue(colour, out var cached))
			return cached;

		var figure = Figure(colour.Darkened(0.45f), hostile: false).GetImage();
		var canvas = Image.CreateEmpty(278, 198, false, Image.Format.Rgba8);
		canvas.Fill(Colors.Transparent);

		var scale = 150;
		figure.Resize(scale, scale, Image.Interpolation.Nearest);
		canvas.BlitRect(
			figure,
			new Rect2I(0, 0, scale, scale),
			new Vector2I((278 - scale) / 2, (198 - scale) / 2)
		);

		return CardFigures[colour] = ImageTexture.CreateFromImage(canvas);
	}

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
		_costBadge ??= Circle(81, 83, DoomPalette.Navy, DoomPalette.Bone, 2);

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

	// ===== Lane figures =====

	private static readonly Dictionary<(Color, bool), Texture2D> Figures = new();

	/// <summary>
	/// The silhouette standing in a lane: a head and a hunched body, and two eyes if it is hostile.
	///
	/// **Silhouettes, not illustrations.** That is the whole reason the flat style was chosen over
	/// the painted one — a shape like this is something one person can vary twenty times during a
	/// jam, and commissioned creature art is not.
	/// </summary>
	public static Texture2D Figure(Color body, bool hostile)
	{
		if (Figures.TryGetValue((body, hostile), out var cached))
			return cached;

		const int size = 72;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

		var headR = size * 0.20f;
		var headX = size / 2f;
		var headY = size * 0.28f;

		for (var y = 0; y < size; y++)
		for (var x = 0; x < size; x++)
		{
			var inHead = Mathf.Sqrt((x - headX) * (x - headX) + (y - headY) * (y - headY)) <= headR;

			// The body is a trapezoid that widens towards the base, which is what makes the shape
			// read as hunched and planted rather than as a floating lollipop.
			var t = Mathf.Clamp((y - size * 0.44f) / (size * 0.52f), 0f, 1f);
			var halfWidth = Mathf.Lerp(size * 0.16f, size * 0.34f, t);
			var inBody =
				y >= size * 0.44f && y <= size * 0.96f && Mathf.Abs(x - headX) <= halfWidth;

			image.SetPixel(x, y, inHead || inBody ? body : Colors.Transparent);
		}

		if (hostile)
			foreach (var dx in new[] { -headR * 0.42f, headR * 0.42f })
				Dot(image, headX + dx, headY, 2.2f, DoomPalette.Red);

		return Figures[(body, hostile)] = ImageTexture.CreateFromImage(image);
	}

	private static void Dot(Image image, float cx, float cy, float radius, Color colour)
	{
		for (var y = (int)(cy - radius) - 1; y <= cy + radius + 1; y++)
		for (var x = (int)(cx - radius) - 1; x <= cx + radius + 1; x++)
		{
			if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
				continue;

			if (Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) <= radius)
				image.SetPixel(x, y, colour);
		}
	}

	private static Texture2D _hooded;

	/// <summary>
	/// The Opponent: a hood, rimmed in red, with no face. It is the thing you are trying to kill and
	/// the only way a battle is won, so it gets the one figure on screen that is not a lane token.
	///
	/// No eyes, deliberately — the Wretches have those. What is under the hood is the one thing this
	/// screen does not tell you.
	/// </summary>
	public static Texture2D Hooded()
	{
		if (_hooded is not null)
			return _hooded;

		const int size = 132;
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

		// The silhouette, tested at two scales: the gap between them is the rim.
		static bool Inside(float x, float y, float scale)
		{
			var nx = ((x - 0.5f) / scale) + 0.5f;
			var ny = y / scale;

			if (ny is < 0.06f or > 1f)
				return false;

			// Half-width grows as a square root, which gives the shoulders a shrug rather than the
			// straight flare of a cone.
			var t = Mathf.Clamp((ny - 0.06f) / 0.94f, 0f, 1f);
			return Mathf.Abs(nx - 0.5f) <= 0.46f * Mathf.Sqrt(t);
		}

		for (var y = 0; y < size; y++)
		for (var x = 0; x < size; x++)
		{
			var u = x / (float)(size - 1);
			var v = y / (float)(size - 1);

			var outer = Inside(u, v, 1f);
			var inner = Inside(u, v, 0.86f);

			// DARKER than the ground it stands on. Filled with Navy it was invisible against a Navy
			// board and read as a hollow red outline — a silhouette has to be darker than its sky.
			image.SetPixel(
				x,
				y,
				inner ? Color.FromHtml("#0C131B")
					: outer ? DoomPalette.Red
					: Colors.Transparent
			);
		}

		return _hooded = ImageTexture.CreateFromImage(image);
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
