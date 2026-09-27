using Godot;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **The pieces the two map screens share** (`KinRouteMap`, `KinTownMap`): outlined words over a
/// painted map, a picture fitted into a box, and a team member as face, name and HP.
/// </summary>
public static class KinMapKit
{
	public static Label Text(int size, Color colour, HorizontalAlignment align) =>
		new()
		{
			HorizontalAlignment = align,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings
			{
				FontSize = size,
				FontColor = colour,
				OutlineSize = 6,
				OutlineColor = new Color(0.04f, 0.06f, 0.09f),
				ShadowSize = 4,
				ShadowColor = new Color(0, 0, 0, 0.6f),
				ShadowOffset = new Vector2(2, 3),
			},
		};

	public static TextureRect Fill(Texture2D texture, Vector2 size, float inset) =>
		new()
		{
			Texture = texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Position = new Vector2(inset, inset),
			Size = size - new Vector2(2 * inset, 2 * inset),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

	/// <summary>A team member: its face in a medallion, its name and its HP.</summary>
	public static Control Member(RunCompanion m)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(
			new TextureRect
			{
				Texture = KinCardKit.Medallion(m.Companion.Name),
				CustomMinimumSize = new Vector2(64, 64),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			}
		);
		var hp = Text(20, KinPalette.Bone, HorizontalAlignment.Left);
		hp.Text = $"{m.Companion.Name.ToUpperInvariant()}  LV {m.Level}\n{m.Hp}/{m.MaxHp}";
		row.AddChild(hp);
		return row;
	}

	/// <summary>The top-left plate: a gold title over a bone subtitle.</summary>
	public static (PanelContainer Plate, Label Title, Label Subtitle) Header()
	{
		var plate = new PanelContainer { Position = new Vector2(32, 18) };
		plate.AddThemeStyleboxOverride("panel", KinUiKit.Plate("bone"));
		var lines = new VBoxContainer();
		lines.AddThemeConstantOverride("separation", 0);
		var title = Text(30, KinPalette.Gold, HorizontalAlignment.Left);
		var subtitle = Text(18, KinPalette.Bone, HorizontalAlignment.Left);
		lines.AddChild(title);
		lines.AddChild(subtitle);
		plate.AddChild(lines);
		return (plate, title, subtitle);
	}

	/// <summary>The top-right plate: the purse.</summary>
	public static (PanelContainer Plate, Label Text) Purse()
	{
		var plate = new PanelContainer { Position = new Vector2(1520, 18) };
		plate.AddThemeStyleboxOverride("panel", KinUiKit.Plate("gold"));
		var text = Text(24, KinPalette.Bone, HorizontalAlignment.Center);
		plate.AddChild(text);
		return (plate, text);
	}
}
