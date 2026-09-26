using System.Collections.Generic;
using Godot;

namespace KinGame;

/// <summary>
/// **The style-D card, as textures** (`KinVisualDesign.md`, `docs/mockups/round1/chatgpt_D_S1.png`):
/// a dark slate body edged in the OWNER's colour (neutral steel for a trainer card) with a thin
/// bright inner line, a darker name plate, a pale parchment text box with dark text, a diamond
/// cost gem, the ACTION illustration filling the art window, and the owner's face in a medallion
/// at the foot. Only pixels — `KinCardFace` hands these to the shared card, whose interaction is
/// untouched.
/// </summary>
public static class KinCardKit
{
	private const int FrameW = 312;
	private const int FrameH = 445;

	public static readonly Color Body = Color.FromHtml("#1B2530");
	public static readonly Color Neutral = Color.FromHtml("#7C8A99");
	public static readonly Color Parchment = Color.FromHtml("#E4DDC8");
	public static readonly Color Ink = Color.FromHtml("#1C2631");

	private static readonly Dictionary<Color, Texture2D> Frames = new();
	private static readonly Dictionary<string, Texture2D> Medallions = new();
	private static readonly Dictionary<(string, int), Texture2D> Illustrations = new();
	private static Texture2D _plate;
	private static Texture2D _parchment;
	private static Texture2D _gem;

	/// <summary>The card body: dark, edged in <paramref name="edge"/>, with a thin bright line inside it.</summary>
	public static Texture2D Frame(Color edge)
	{
		if (Frames.TryGetValue(edge, out var cached))
			return cached;

		var image = KinArt.RoundedRectImage(FrameW, FrameH, Body, edge, 8, 22);

		// Depth, as the mockup's cards have: the body lit from above (a vertical gradient) and the
		// edge bevelled — lighter along the top, darker along the bottom. A flat fill read as paper.
		for (var y = 0; y < FrameH; y++)
		{
			var t = y / (float)(FrameH - 1);
			var body = Body.Lightened(0.16f * (1 - t)).Darkened(0.2f * t);
			var rim =
				t < 0.5f ? edge.Lightened(0.35f * (1 - 2 * t)) : edge.Darkened(0.35f * (2 * t - 1));
			for (var x = 0; x < FrameW; x++)
			{
				var px = image.GetPixel(x, y);
				if (px.A == 0)
					continue;
				if (px.IsEqualApprox(Body))
					image.SetPixel(x, y, body);
				else if (px.IsEqualApprox(edge))
					image.SetPixel(x, y, rim);
			}
		}
		var inner = KinArt.RoundedRectImage(
			FrameW - 22,
			FrameH - 22,
			Colors.Transparent,
			new Color(KinPalette.Bone, 0.45f),
			2,
			14
		);
		image.BlendRect(
			inner,
			new Rect2I(0, 0, inner.GetWidth(), inner.GetHeight()),
			new Vector2I(11, 11)
		);
		return Frames[edge] = ImageTexture.CreateFromImage(image);
	}

	public static Texture2D NamePlate =>
		_plate ??= ImageTexture.CreateFromImage(
			KinArt.RoundedRectImage(
				279,
				53,
				Color.FromHtml("#101820"),
				Color.FromHtml("#101820"),
				0,
				10
			)
		);

	public static Texture2D ParchmentBox =>
		_parchment ??= ImageTexture.CreateFromImage(
			KinArt.RoundedRectImage(279, 158, Parchment, Color.FromHtml("#9A8F74"), 2, 10)
		);

	/// <summary>The cost: a blue diamond with a gold rim — the first thing the eye goes to.</summary>
	public static Texture2D CostGem
	{
		get
		{
			if (_gem is not null)
				return _gem;

			const int size = 84;
			var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
			var c = (size - 1) / 2f;
			for (var y = 0; y < size; y++)
			for (var x = 0; x < size; x++)
			{
				var d = Mathf.Abs(x - c) + Mathf.Abs(y - c); // a diamond is a circle in the L1 norm
				image.SetPixel(
					x,
					y,
					d > c ? Colors.Transparent
						: d > c - 5 ? KinPalette.Gold
						: d > c - 8 ? Color.FromHtml("#0E1A2E")
						: y < c ? Color.FromHtml("#3A7BD5")
						: Color.FromHtml("#2A5CA8")
				);
			}
			return _gem = ImageTexture.CreateFromImage(image);
		}
	}

	/// <summary>
	/// **The action picture** — `Art/cards/<name>.png`, cover-cropped to the art window — or null.
	/// A card shows what it DOES, not who owns it; the owner is the medallion.
	/// </summary>
	public static Texture2D Illustration(string cardName, int height)
	{
		if (Illustrations.TryGetValue((cardName, height), out var cached))
			return cached;
		if (KinArt.Drawing("cards/" + cardName) is not { } drawing)
			return Illustrations[(cardName, height)] = null;

		var art = drawing.GetImage();
		art.Convert(Image.Format.Rgba8);
		var scale = Mathf.Max(
			KinArt.ArtWidth / (float)art.GetWidth(),
			height / (float)art.GetHeight()
		);
		var w = Mathf.CeilToInt(art.GetWidth() * scale);
		var h = Mathf.CeilToInt(art.GetHeight() * scale);
		art.Resize(w, h, Image.Interpolation.Lanczos);
		var window = art.GetRegion(
			new Rect2I((w - KinArt.ArtWidth) / 2, (h - height) / 2, KinArt.ArtWidth, height)
		);
		return Illustrations[(cardName, height)] = ImageTexture.CreateFromImage(window);
	}

	/// <summary>
	/// The owner's face in a ring of its colour, for the card's foot — or null for a trainer card.
	/// Cut from the standing sprite when there is one, else the portrait.
	/// </summary>
	public static Texture2D Medallion(string owner)
	{
		if (string.IsNullOrEmpty(owner))
			return null;
		if (Medallions.TryGetValue(owner, out var cached))
			return cached;

		const int size = 76;
		const int ring = 5;
		var colour = KinPalette.Companion(owner);
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);

		var sprite = KinArt.Sprite(owner);
		var face = (sprite ?? KinArt.Drawing(owner))?.GetImage();
		if (face is not null)
		{
			face.Convert(Image.Format.Rgba8);
			// The HEAD, not the body: the whole creature in a 66px circle was a speck. A square
			// centred on where the upper body's opaque pixels are — a top-right corner crop found
			// Pike's spear tip and Gale's wing instead (seen on a capture).
			if (sprite is not null)
				face = Head(face);
			var inner = size - 2 * ring;
			var scale = inner / (float)Mathf.Max(face.GetWidth(), face.GetHeight());
			face.Resize(
				Mathf.Max(1, (int)(face.GetWidth() * scale)),
				Mathf.Max(1, (int)(face.GetHeight() * scale)),
				Image.Interpolation.Lanczos
			);
			var disc = KinArt.CircleImage(size, size, colour.Darkened(0.55f), colour, ring);
			disc.BlendRect(
				face,
				new Rect2I(0, 0, face.GetWidth(), face.GetHeight()),
				new Vector2I((size - face.GetWidth()) / 2, (size - face.GetHeight()) / 2)
			);
			// Clip anything that spilled past the ring, then redraw the ring over it.
			var c = (size - 1) / 2f;
			for (var y = 0; y < size; y++)
			for (var x = 0; x < size; x++)
			{
				var d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
				if (d > c)
					disc.SetPixel(x, y, Colors.Transparent);
				else if (d > c - ring)
					disc.SetPixel(x, y, colour);
			}
			image = disc;
		}
		else
			image = KinArt.CircleImage(size, size, colour.Darkened(0.4f), colour, ring);

		return Medallions[owner] = ImageTexture.CreateFromImage(image);
	}

	/// <summary>A square around the opaque mass of a sprite's upper 55%, weighted hard to its front (right).</summary>
	private static Image Head(Image sprite)
	{
		int w = sprite.GetWidth(),
			h = sprite.GetHeight();
		var top = (int)(h * 0.55f);
		double sx = 0,
			sy = 0,
			n = 0;
		for (var y = 0; y < top; y += 2)
		for (var x = 0; x < w; x += 2)
		{
			if (sprite.GetPixel(x, y).A < 0.5f)
				continue;
			var weight = 0.2 + 2 * (x / (double)w) * (x / (double)w); // a right-facing creature's front
			sx += x * weight;
			sy += y * weight;
			n += weight;
		}
		if (n == 0)
			return sprite;
		var side = (int)(Mathf.Min(w, h) * 0.55f);
		var cx = Mathf.Clamp((int)(sx / n) - side / 2, 0, w - side);
		var cy = Mathf.Clamp((int)(sy / n) - side / 2, 0, h - side);
		return sprite.GetRegion(new Rect2I(cx, cy, side, side));
	}
}
