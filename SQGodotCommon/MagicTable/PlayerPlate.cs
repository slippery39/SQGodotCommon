using System;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MtgGame;

namespace MagicTable;

/// <summary>
/// One player at a glance: name and a big life total, hand and library counts, the graveyard,
/// exile and command zones as buttons that open the <see cref="ZoneViewer"/>, emblems as art,
/// floating mana. The whole plate is a click target too ("any target", "Attack AI").
/// </summary>
public partial class PlayerPlate : PanelContainer
{
	private const int TextSize = 22;

	/// <summary>Left click on the plate itself (not its buttons).</summary>
	public event Action? Clicked;

	public PlayerPlate()
	{
		GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				Clicked?.Invoke();
		};
	}

	public void Show(
		GameState s,
		int player,
		Glow glow,
		Action<string?> preview,
		Action<ZoneType> openZone,
		bool active = false
	)
	{
		CardViews.Clear(this);
		// Whose turn it is, at a glance (TBL-4.2): the active player's plate is edged in gold.
		AddThemeStyleboxOverride(
			"panel",
			glow == Glow.None && active ? CardViews.ActiveEdge() : CardViews.Outline(glow)
		);
		var p = s.GetPlayer(player);
		int Count(ZoneType z) => s.CardsIn(player, z).Count();

		var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 4);
		AddChild(column);

		var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		var priority = s.GetGame().PriorityPlayerId == player ? "  ◀ priority" : "";
		var name = Text($"{p.Name}{priority}", TextSize);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		top.AddChild(name);
		var life = MtgUiStyles.Title($"{p.Life}", 48);
		life.MouseFilter = MouseFilterEnum.Ignore;
		top.AddChild(life);
		column.AddChild(top);

		column.AddChild(
			Text($"hand {Count(ZoneType.Hand)} · library {Count(ZoneType.Library)}", TextSize)
		);

		var zones = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		foreach (
			var (zone, label) in new[]
			{
				(ZoneType.Graveyard, "Graveyard"),
				(ZoneType.Exile, "Exile"),
				(ZoneType.Command, "Command"),
			}
		)
		{
			var button = new Button
			{
				Text = $"{label} {Count(zone)}",
				FocusMode = FocusModeEnum.None,
				Disabled = Count(zone) == 0,
			};
			button.AddThemeFontSizeOverride("font_size", TextSize - 4);
			// While the player is a target, the whole plate is — the buttons step aside.
			if (glow is Glow.Choosable or Glow.Selected)
				button.MouseFilter = MouseFilterEnum.Ignore;
			MtgUiStyles.StyleChoice(button, selected: false);
			button.Pressed += () => openZone(zone);
			zones.AddChild(button);
		}
		column.AddChild(zones);

		// Emblems and "until your next turn" effects: always on show, they change the game.
		var command = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		foreach (var c in s.CardsIn(player, ZoneType.Command))
		{
			var thumb = CardViews.Thumb(MagicArt.Key(c), 70);
			thumb.TooltipText = c.Name;
			CardViews.Hook(thumb, MagicArt.Key(c), preview, null);
			command.AddChild(thumb);
		}
		if (command.GetChildCount() > 0)
			column.AddChild(command);

		if (Floating(p.ManaPool) is { Length: > 0 } floating)
			column.AddChild(Text($"floating {floating}", TextSize));
	}

	private static string Floating(MagicCore.ManaPool pool)
	{
		var plain = string.Join(
			" ",
			new[]
			{
				("W", pool.White),
				("U", pool.Blue),
				("B", pool.Black),
				("R", pool.Red),
				("G", pool.Green),
				("C", pool.Colorless),
			}
				.Where(x => x.Item2 > 0)
				.Select(x => $"{{{x.Item1}}}×{x.Item2}")
		);
		return pool.Restricted.IsEmpty ? plain : $"{plain} +{pool.Restricted.Count} restricted";
	}

	private static Label Text(string text, int size)
	{
		var label = new Label
		{
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 0),
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		return label;
	}
}
