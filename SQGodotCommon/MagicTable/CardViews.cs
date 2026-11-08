using System;
using System.Collections.Generic;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Cards;
using MtgGame;

namespace MagicTable;

/// <summary>
/// How an object is lit: playable (gold); in combat (red attacking, blue blocking); while a choice
/// or a target is asked — clickable (light blue), picked (green), what it is about (white).
/// </summary>
public enum Glow
{
	None,
	Playable,
	Attacking,
	Blocking,
	Choosable,
	Selected,
	About,
	Pays, // a land a dragged spell would tap (TBL-8.2)
}

/// <summary>
/// The table's card pictures, built fresh on every refresh (the state is immutable, so redraw
/// beats patching). A permanent is an Arena-style crop: art, name, P/T or loyalty, counters; a
/// tapped one lies sideways. Hand cards are the full Scryfall image. Hovering any of them shows
/// the full card in the preview, through <paramref name="preview"/>.
/// </summary>
public static class CardViews
{
	public static readonly Vector2 PermanentSize = new(126, 168);
	public static readonly Vector2 HandSize = new(144, 200);

	/// <param name="scale">Bigger when the board has room (TBL-4.2); the text keeps its size.</param>
	public static Control Permanent(
		GameState s,
		Card c,
		int count,
		Glow glow,
		Action<string?> preview,
		Action onClick,
		float scale = 1
	)
	{
		var size = PermanentSize * scale;
		var (w, h) = (size.X, size.Y);
		var card = Frame(glow, size);
		card.AddChild(
			Art(MagicArt.Crop(MagicArt.Key(c)), new Vector2(w - 6, h * 0.55f), new Vector2(3, 3))
		);
		card.AddChild(Text(c.Name, 16, new Vector2(4, h * 0.55f + 3), w - 8, wrap: true));

		var stats = Stats(s, c);
		if (stats != "")
			card.AddChild(Badge(stats, new Vector2(w - 60, h - 36), c.Damage > 0));
		if (count > 1)
			card.AddChild(Badge($"×{count}", new Vector2(4, h - 36), false));
		if (Status(s, c) is { Length: > 0 } status)
			card.AddChild(Badge(status, new Vector2(4, 4), false, 16, w - 8)); // on the art, clear of the name

		// A tapped card lies sideways in a slot as wide as it is tall (rotated about its corner).
		var slot = new Control
		{
			CustomMinimumSize = c.IsTapped ? new Vector2(h, w) : size,
			TooltipText = Describe.Card(s, c),
		};
		if (c.IsTapped)
		{
			card.Rotation = Mathf.Pi / 2;
			card.Position = new Vector2(h, 0);
		}
		if (IsPhasedOut(s, c))
			slot.Modulate = new Color(1, 1, 1, 0.4f); // still visible, clearly not there (Shayne, 2026-11-06)
		slot.AddChild(card);
		Hook(slot, MagicArt.Key(c), preview, onClick);
		return slot;
	}

	/// <param name="from">Where a card castable from outside the hand is ("graveyard", "exile"); null in hand.</param>
	public static Control HandCard(
		GameState s,
		Card c,
		Glow glow,
		Action<string?> preview,
		Action onClick,
		string? from = null
	)
	{
		var image = MagicArt.ImageName(s, c);
		var card = Frame(glow, HandSize);
		card.AddChild(Art(MagicArt.Full(image), HandSize - new Vector2(6, 6), new Vector2(3, 3)));
		if (from is not null)
			card.AddChild(
				Badge($"from {from}", new Vector2(4, HandSize.Y - 36), false, 18, HandSize.X - 8)
			);
		var slot = new Control { CustomMinimumSize = HandSize };
		slot.AddChild(card);
		Hook(slot, image, preview, onClick);
		return slot;
	}

	/// <summary>Vulcan Mind-Meld: a card exiled face down you haven't seen (Card.IsHiddenFrom) — a back, no hover.</summary>
	public static Control FaceDown()
	{
		var card = Frame(Glow.None, HandSize);
		card.AddChild(
			Badge("face down", new Vector2(4, HandSize.Y / 2 - 18), false, 18, HandSize.X - 8)
		);
		var slot = new Control { CustomMinimumSize = HandSize };
		slot.AddChild(card);
		return slot;
	}

	/// <summary>
	/// An Aura or Equipment, shown under the permanent it is on: a strip with its art and name,
	/// its own hover and click target.
	/// </summary>
	public static Control Attachment(Card a, Glow glow, Action<string?> preview, Action onClick)
	{
		var strip = new PanelContainer { CustomMinimumSize = new Vector2(PermanentSize.X, 0) };
		strip.AddThemeStyleboxOverride("panel", Outline(glow));
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddChild(Thumb(MagicArt.Key(a), 44));
		var name = Text(a.Name, 15, Vector2.Zero, PermanentSize.X - 56, wrap: false);
		name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // a clipped label in a row gets no width of its own
		row.AddChild(name);
		strip.AddChild(row);
		Hook(strip, MagicArt.Key(a), preview, onClick);
		return strip;
	}

	/// <summary>A small art crop for lists (the stack, choice options).</summary>
	public static TextureRect Thumb(string cardName, float width) =>
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Texture = MagicArt.Crop(cardName),
			CustomMinimumSize = new Vector2(width, width * 0.73f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

	/// <summary>Shows a card big while it is held (TBL-4.2), null to hide it — set by the table.</summary>
	public static Action<string?>? Zoom { get; set; }

	/// <summary>A hold this long zooms instead of clicking.</summary>
	private const double HoldSeconds = 0.4;

	/// <summary>A release this far from its press is a swipe, not a pick (design pixels).</summary>
	private const float SwipeDistance = 12;

	/// <summary>
	/// A card's hover, click and zoom. Hover shows the full card; children ignore the mouse so the slot
	/// gets it. Hold it (a mouse button or a finger — a touch has no hover) and it shows big until let
	/// go; a right button zooms at once — on a screen that shows a zoom. The click is on RELEASE, so a
	/// hold never also plays the card, and a release that moved is a swipe (godot-frontend.md).
	/// </summary>
	public static void Hook(Control slot, string cardName, Action<string?> preview, Action? onClick)
	{
		slot.MouseFilter = Control.MouseFilterEnum.Stop;
		slot.MouseEntered += () => preview(cardName);
		slot.MouseExited += () => preview(null);
		var (press, zoomed, from) = (0, false, Vector2.Zero);
		slot.GuiInput += e =>
		{
			if (
				e
				is not InputEventMouseButton
				{
					ButtonIndex: MouseButton.Left or MouseButton.Right
				} b
			)
				return;
			if (b.Pressed)
			{
				var mine = ++press;
				from = b.GlobalPosition;
				zoomed = b.ButtonIndex == MouseButton.Right && Zoom is not null;
				if (zoomed)
					Zoom?.Invoke(cardName);
				else
					slot.GetTree().CreateTimer(HoldSeconds).Timeout += () =>
					{
						if (mine == press && GodotObject.IsInstanceValid(slot) && Zoom is not null)
						{
							zoomed = true;
							Zoom?.Invoke(cardName);
						}
					};
				return;
			}
			press++; // a timer still running belongs to a press that is over
			if (zoomed)
				Zoom?.Invoke(null);
			else if (
				b.ButtonIndex == MouseButton.Left
				&& b.GlobalPosition.DistanceTo(from) < SwipeDistance
			)
				onClick?.Invoke();
			zoomed = false;
		};
	}

	/// <summary>Everything a crop shows — two lands that look alike may stack.</summary>
	public static string Looks(GameState s, Card c) =>
		$"{c.Name}|{Stats(s, c)}|{Status(s, c)}|{c.IsTapped}|{c.Damage}|{c.AttachedToId}";

	/// <summary>Removes at once, not only queued: a container still lays out a queued child this frame.</summary>
	public static void Clear(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static string Stats(GameState s, Card c) =>
		c.Is(CardType.Creature) ? $"{s.Power(c)}/{s.Toughness(c) - c.Damage}"
		: c.Is(CardType.Planeswalker) ? $"◆{c.Loyalty}"
		: "";

	/// <summary>Counters, and what a crop can't show otherwise: token, copy, prepared, no abilities.</summary>
	private static string Status(GameState s, Card c)
	{
		var parts = new List<string>();
		if (IsPhasedOut(s, c))
			parts.Add("PHASED OUT");
		if (c.IsToken)
			parts.Add("token");
		if (c.IsCopy)
			parts.Add("copy");
		if (c.IsPrepared)
			parts.Add("prepared");
		if (s.HasLostAllAbilities(c))
			parts.Add("no abilities");
		if (c.PlusOneCounters > 0)
			parts.Add($"+1×{c.PlusOneCounters}");
		if (c.MinusOneCounters > 0)
			parts.Add($"-1×{c.MinusOneCounters}");
		if (c.ChargeCounters > 0)
			parts.Add($"chg {c.ChargeCounters}");
		if (c.StunCounters > 0)
			parts.Add($"stun {c.StunCounters}");
		if (c.HasFinalityCounter)
			parts.Add("final");
		return string.Join(" ", parts);
	}

	/// <summary>The City on the Edge of Forever: a permanent in its controller's phased-out zone (MagicCore.Rules.Phasing).</summary>
	public static bool IsPhasedOut(GameState s, Card c) =>
		s.GetParent(c.Id) is { } zone && s.GetObject(zone) is Zone { ZoneType: ZoneType.PhasedOut };

	/// <summary>The one look of every glow — cards, player plates and stack rows alike.</summary>
	public static StyleBoxFlat Outline(Glow glow)
	{
		var style = new StyleBoxFlat
		{
			BgColor = MtgUiStyles.CardBg,
			BorderColor = glow switch
			{
				Glow.Playable => MtgUiStyles.GoldBorder,
				Glow.Attacking => new Color("d0583a"),
				Glow.Blocking => new Color("2f7fd0"),
				Glow.Choosable => new Color("8fd3ff"),
				Glow.Selected => new Color("5fd35f"),
				Glow.About => Colors.White,
				Glow.Pays => new Color("f0a030"),
				_ => MtgUiStyles.DimBorder,
			},
		};
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(
			glow == Glow.None ? 1
			: glow is Glow.Choosable or Glow.Selected or Glow.Pays ? 5
			: 3
		);
		style.SetContentMarginAll(6);
		return style;
	}

	/// <summary>The active player's plate (TBL-4.2): bright gold, wider than a glow, with a halo — never a card's playable gold.</summary>
	public static StyleBoxFlat ActiveEdge()
	{
		var style = Outline(Glow.None);
		Lit(style);
		return style;
	}

	/// <summary>
	/// One player's half of the battlefield (TBL-4.2): a dark wash that calms the leather behind the
	/// cards, edged in gold while it is that player's turn.
	/// </summary>
	public static StyleBoxFlat Board(bool active)
	{
		var style = new StyleBoxFlat { BgColor = new Color(0.04f, 0.02f, 0.01f, 0.55f) };
		style.SetCornerRadiusAll(8);
		style.SetContentMarginAll(4);
		if (active)
			Lit(style);
		return style;
	}

	private static void Lit(StyleBoxFlat style)
	{
		style.BorderColor = new Color("f4e29b");
		style.SetBorderWidthAll(4);
		style.ShadowColor = new Color(0.96f, 0.89f, 0.61f, 0.35f);
		style.ShadowSize = 10;
	}

	private static Panel Frame(Glow glow, Vector2 size)
	{
		var panel = new Panel { Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride("panel", Outline(glow));
		return panel;
	}

	private static TextureRect Art(Texture2D? texture, Vector2 size, Vector2 position) =>
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Texture = texture,
			Size = size,
			Position = position,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

	private static Label Text(string text, int size, Vector2 position, float width, bool wrap)
	{
		// Wrap and clip BEFORE Size: set first, the unwrapped line's width became the size.
		var label = new Label
		{
			AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
			ClipText = !wrap,
			MaxLinesVisible = 2,
			CustomMinimumSize = new Vector2(1, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Text = text,
			Position = position,
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.Size = new Vector2(width, 0);
		label.AddThemeColorOverride("font_color", new Color("f4e29b"));
		return label;
	}

	private static Label Badge(
		string text,
		Vector2 position,
		bool hurt,
		int size = 22,
		float width = 56
	)
	{
		var label = Text(text, size, position, width, wrap: false);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		var style = new StyleBoxFlat { BgColor = hurt ? new Color("6a1410") : new Color("1b120c") };
		style.SetCornerRadiusAll(4);
		label.AddThemeStyleboxOverride("normal", style);
		label.AddThemeColorOverride("font_color", Colors.White);
		return label;
	}
}
