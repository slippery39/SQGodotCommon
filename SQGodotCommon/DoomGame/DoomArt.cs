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
	private static readonly Dictionary<(Color, int), Texture2D> Art = new();

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
			// Border 5, not 3. The mockup's card is edged in a clear bone line that separates it from
			// both the board behind it and the next card in the fan; at 3 the cards bled together
			// where they overlap.
			Bodies[colour] = texture = RoundedRect(FrameW, FrameH, colour, DoomPalette.Bone, 5, 22);

		return texture;
	}

	/// <summary>Nothing at all, at a given size — used to clear a sprite that would otherwise stack.</summary>
	public static Texture2D Blank(int width, int height) =>
		RoundedRect(width, height, Colors.Transparent, Colors.Transparent, 0, 0);

	private static readonly Dictionary<string, Texture2D> Drawn = new();

	/// <summary>
	/// The authored SVG for a subject, or null if nobody has drawn it yet.
	///
	/// **Godot 4 imports SVG natively**, which is the whole reason this style is affordable: a card
	/// subject is a ~1KB file of geometry on the palette, there is no atlas, no build step, and
	/// re-tinting the entire set is a find-and-replace on five hex codes. See DoomUI.md, "Art".
	///
	/// Missing is NORMAL and must stay cheap — the pool grows faster than the art does, and a card
	/// with no drawing falls back to the generated silhouette rather than to a broken texture. The
	/// null is cached too, or every unauthored card retries a failed load on every single render.
	/// </summary>
	public static Texture2D Drawing(string subject)
	{
		var key = FileName(subject);
		if (Drawn.TryGetValue(key, out var cached))
			return cached;

		var path = $"res://DoomGame/Art/{key}.svg";
		return Drawn[key] = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	private static Texture2D _backdrop;
	private static bool _backdropLooked;

	/// <summary>
	/// The board's backdrop image, or null if none has been dropped in.
	///
	/// A PNG rather than an SVG, and the one asset in this folder that is not vector: it is a scene
	/// painted once, not a subject that gets tinted or re-scaled per card. See
	/// `docs/mockups/backdrop-prompt.md` for how it is made and the constraint that matters most —
	/// **the centre 60% stays nearly empty**, because the lanes are drawn over it.
	///
	/// The miss is cached as well as the hit: with no image, this is asked once per board rather
	/// than once per frame.
	/// </summary>
	public static Texture2D Backdrop
	{
		get
		{
			if (_backdropLooked)
				return _backdrop;

			_backdropLooked = true;
			const string path = "res://DoomGame/Art/background.png";

			return _backdrop = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		}
	}

	/// <summary>
	/// `Ash Walker` -> `ash_walker`. The art file is named after the subject, so adding a drawing is
	/// dropping a file in and nothing else.
	///
	/// **Everything after a comma or an em dash is a MODIFIER, and is dropped.** Names in this game
	/// grow: a trait renames an Opponent to `The Opponent, Relentless`, and the companion collects
	/// an apocalypse mark every time it survives one — `Ash — Barnacled, Glowing`. Keyed on the
	/// whole string, the Opponent silently lost its drawing the moment it got a trait, which is
	/// every battle. The base name is the thing that has a picture; the rest is what happened to it.
	/// </summary>
	private static string FileName(string subject)
	{
		var name = (subject ?? "").Split(',')[0].Split('—')[0].Split('-')[0].Trim();

		return name.ToLowerInvariant().Replace(" ", "_").Replace("'", "");
	}

	/// <summary>
	/// The card's centre mark: the authored drawing if there is one, else the same silhouette that
	/// stands in a lane, in a darker shade of the card's own colour. Reusing the lane figure is what
	/// makes a card and the body it becomes legibly the same thing.
	/// </summary>
	public static Texture2D CardArt(string cardName, Color colour, int height)
	{
		var drawing = Drawing(cardName);
		if (drawing is null)
			return CardFigure(colour);

		if (CardDrawings.TryGetValue((cardName, height), out var cached))
			return cached;

		// Composed onto the art window's OWN dimensions rather than handed over at its native size.
		// The card scene positions this sprite for a 278x198 texture, and a square 256x256 dropped
		// in its place overflows the window and rides up over the name plate.
		var canvas = Image.CreateEmpty(ArtWidth, height, false, Image.Format.Rgba8);
		canvas.Fill(Colors.Transparent);

		// Square, fitted to whichever side is smaller, and centred. A card with no rules text gets a
		// TALLER window (see DoomCardFace), so the drawing has to grow into it rather than sit at
		// the top of a box that is now half empty.
		var side = Mathf.Min(ArtWidth, height);
		var art = drawing.GetImage();
		art.Convert(Image.Format.Rgba8);
		art.Resize(side, side, Image.Interpolation.Lanczos);
		canvas.BlitRect(
			art,
			new Rect2I(0, 0, side, side),
			new Vector2I((ArtWidth - side) / 2, (height - side) / 2)
		);

		return CardDrawings[(cardName, height)] = ImageTexture.CreateFromImage(canvas);
	}

	private static readonly Dictionary<(string, int), Texture2D> CardDrawings = new();

	private static Texture2D CardFigure(Color colour)
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
	///
	/// **A pill, not a disc, and the size is load-bearing.** A 64px circle could not hold `2/10`, so
	/// Bulwark showed `2/1` — the wrong stats on screen, not merely a cramped badge. The pool runs
	/// to Long Watcher at 12/20, so this is sized against five glyphs, per DoomUI.md's rule that a
	/// numeric container is built for the longest value its content can produce.
	/// </summary>
	public const int StatBadgeWidth = 116;

	public const int StatBadgeHeight = 60;

	public static Texture2D StatBadge =>
		_statBadge ??= RoundedRect(
			StatBadgeWidth,
			StatBadgeHeight,
			DoomPalette.Red,
			DoomPalette.Bone,
			3,
			StatBadgeHeight / 2
		);

	private static Texture2D _lifeDisc;

	/// <summary>
	/// Toughness, as a red disc — the mockup's treatment, and the same shape the lane already uses
	/// for a body's remaining life. A card and the body it becomes now show their two numbers in the
	/// same two places, in the same two colours.
	///
	/// Sized for two digits: the pool reaches 20 toughness.
	/// </summary>
	public static Texture2D LifeDisc =>
		_lifeDisc ??= Circle(72, 72, DoomPalette.Red, DoomPalette.Bone, 3);

	/// <summary>
	/// The attack mark: a sword, bone, on nothing at all.
	///
	/// **Power gets an icon and no disc; toughness gets a disc and no icon.** Two identical pips
	/// side by side made the eye stop and read both to tell them apart, which is the one thing a
	/// lane game cannot afford five times a row. Different shape, different colour, no ambiguity.
	///
	/// game-icons.net, CC BY 3.0 — see Art/CREDITS.md.
	/// </summary>
	public static Texture2D AttackIcon => Drawing("icons/attack");

	private static readonly Dictionary<(int, int), Texture2D> Dashed = new();

	/// <summary>
	/// An empty lane: a DASHED outline and nothing inside it.
	///
	/// The mockup draws a held lane and an empty one as different KINDS of thing, not as two shades
	/// of the same panel — solid border for a body, dashes for a socket waiting to be filled. It is
	/// the clearest "you can drop here" a board can give without a hover state, and it reads across
	/// five lanes at a glance.
	///
	/// Godot's StyleBoxFlat has no dashed border, so this is a generated texture behind a
	/// StyleBoxTexture. Cheap, because lane cells are a fixed size — see DoomLaneCell.
	/// </summary>
	public static Texture2D DashedSlot(int width, int height)
	{
		if (Dashed.TryGetValue((width, height), out var cached))
			return cached;

		const int dash = 14;
		const int gap = 10;
		const int thickness = 3;
		const int radius = 8;

		var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		// Walked as a perimeter rather than drawn as four sides, so the dashes stay evenly spaced
		// round the corners instead of bunching where the sides meet.
		void Stroke(int x, int y, int along)
		{
			if (along % (dash + gap) >= dash)
				return;

			for (var ty = 0; ty < thickness; ty++)
			for (var tx = 0; tx < thickness; tx++)
			{
				var px = Mathf.Clamp(x + tx, 0, width - 1);
				var py = Mathf.Clamp(y + ty, 0, height - 1);
				image.SetPixel(px, py, DoomPalette.Slate);
			}
		}

		var step = 0;
		for (var x = radius; x < width - radius; x++, step++)
			Stroke(x, 0, step);
		for (var y = radius; y < height - radius; y++, step++)
			Stroke(width - thickness, y, step);
		for (var x = width - radius - 1; x >= radius; x--, step++)
			Stroke(x, height - thickness, step);
		for (var y = height - radius - 1; y >= radius; y--, step++)
			Stroke(0, y, step);

		return Dashed[(width, height)] = ImageTexture.CreateFromImage(image);
	}

	/// <summary>
	/// The art window as a single flat colour block. There is no card art yet, and a flat block is
	/// not a placeholder for it — it is the reference's own treatment.
	/// </summary>
	public static Texture2D ArtBlock(Color colour, int height)
	{
		if (!Art.TryGetValue((colour, height), out var texture))
			Art[(colour, height)] = texture = RoundedRect(ArtWidth, height, colour, colour, 0, 0);

		return texture;
	}

	/// <summary>The art window's width. Fixed by the card scene; only the HEIGHT varies.</summary>
	public const int ArtWidth = 278;

	/// <summary>
	/// The ground every enemy drawing stands on — one colour for all of them.
	///
	/// Light enough that a near-black silhouette reads, and uniform so the enemy row stays a wall
	/// rather than turning into a second hand of cards.
	/// </summary>
	public static readonly Color EnemyGround = Color.FromHtml("#36495E");

	/// <summary>The card body for a unit — a body you put in a lane.</summary>
	public static readonly Color UnitCard = Color.FromHtml("#243748");

	/// <summary>
	/// The card body for a Rite. **Card colour carries ROLE, and nothing else.**
	///
	/// It used to hash the card's NAME into one of five hues, which spent the strongest signal a
	/// card has on noise: two Scavengers sharing a colour is a coincidence that looks like
	/// information, and a Rite — which has no body, resolves at once and goes to Discard — was
	/// distinguishable from a unit only by the absence of a stat badge. One glance at the hand now
	/// answers "which of these are bodies", which is the first question a turn asks. See DoomUI.md.
	/// </summary>
	public static readonly Color RiteCard = Color.FromHtml("#43355C");

	/// <summary>
	/// The ground a card's drawing stands on. Per-name, so the hand keeps its variety — but the
	/// variety lives in the art window, where variety belongs, rather than in the frame, where it
	/// was pretending to mean something.
	///
	/// **Every one of these is a mid-tone on purpose.** The drawings are near-black silhouettes, so
	/// a dark ground makes them vanish; these are chosen light enough that `#0C131B` reads on all
	/// five without a per-card check.
	/// </summary>
	public static Color ColourFor(string cardName)
	{
		Color[] options =
		[
			Color.FromHtml("#41627C"),
			Color.FromHtml("#8A554C"),
			Color.FromHtml("#43786F"),
			Color.FromHtml("#5A5588"),
			Color.FromHtml("#87703F"),
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
