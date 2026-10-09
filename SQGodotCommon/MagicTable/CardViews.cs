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

	public static Control Permanent(
		GameState s,
		Card c,
		int count,
		Glow glow,
		Action<string?> preview,
		Action onClick
	)
	{
		var (w, h) = (PermanentSize.X, PermanentSize.Y);
		var card = Frame(glow, PermanentSize);
		card.AddChild(Art(MagicArt.Crop(c.Name), new Vector2(w - 6, h * 0.55f), new Vector2(3, 3)));
		card.AddChild(Text(c.Name, 16, new Vector2(4, h * 0.55f + 3), w - 8, wrap: true));

		var stats = Stats(s, c);
		if (stats != "")
			card.AddChild(Badge(stats, new Vector2(w - 60, h - 36), c.Damage > 0));
		if (count > 1)
			card.AddChild(Badge($"×{count}", new Vector2(4, h - 36), false));
		if (Counters(c) is { Length: > 0 } counters)
			card.AddChild(Badge(counters, new Vector2(4, 4), false, 16, w - 8)); // on the art, clear of the name

		// A tapped card lies sideways in a slot as wide as it is tall (rotated about its corner).
		var slot = new Control
		{
			CustomMinimumSize = c.IsTapped ? new Vector2(h, w) : PermanentSize,
			TooltipText = Describe.Card(s, c),
		};
		if (c.IsTapped)
		{
			card.Rotation = Mathf.Pi / 2;
			card.Position = new Vector2(h, 0);
		}
		slot.AddChild(card);
		Hook(slot, c.Name, preview, onClick);
		return slot;
	}

	public static Control HandCard(Card c, Glow glow, Action<string?> preview, Action onClick)
	{
		var card = Frame(glow, HandSize);
		card.AddChild(Art(MagicArt.Full(c.Name), HandSize - new Vector2(6, 6), new Vector2(3, 3)));
		var slot = new Control { CustomMinimumSize = HandSize };
		slot.AddChild(card);
		Hook(slot, c.Name, preview, onClick);
		return slot;
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

	/// <summary>Hover shows the full card; a left click acts. Children ignore the mouse so the slot gets it.</summary>
	public static void Hook(Control slot, string cardName, Action<string?> preview, Action? onClick)
	{
		slot.MouseFilter = Control.MouseFilterEnum.Stop;
		slot.MouseEntered += () => preview(cardName);
		slot.MouseExited += () => preview(null);
		if (onClick is not null)
			slot.GuiInput += e =>
			{
				if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					onClick();
			};
	}

	/// <summary>Everything a crop shows — two lands that look alike may stack.</summary>
	public static string Looks(GameState s, Card c) =>
		$"{c.Name}|{Stats(s, c)}|{Counters(c)}|{c.IsTapped}|{c.Damage}|{c.AttachedToId}";

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

	private static string Counters(Card c)
	{
		var parts = new List<string>();
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
				_ => MtgUiStyles.DimBorder,
			},
		};
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(
			glow == Glow.None ? 1
			: glow is Glow.Choosable or Glow.Selected ? 5
			: 3
		);
		style.SetContentMarginAll(6);
		return style;
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
