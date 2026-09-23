using Godot;

namespace KinGame;

/// <summary>
/// One space in the companion game — yours or the foe's. **Presentation only**: the board hands it
/// strings and a border colour, and every fact behind them came from `PartyState`.
///
/// **The monster IS the cell.** Its art covers the whole space, the name sits on a band across the
/// top and the numbers on a band across the foot, tinted in the companion's colour. The first build
/// stacked the art as an 80px thumbnail between six lines of text — in a monster game the monster
/// was the smallest thing on the board. Same shape as `KinLaneCell`, and for the same reason.
///
/// Catches no mouse. Card hover is physics picking, which any mouse-catching Control silently
/// blocks (HANDOFF-KinCompanionGuard scar 2); clicks are hit-tested by the board instead.
/// </summary>
public sealed class KinPartyCell
{
	public const int Width = 220;
	public const int Height = 236;

	public PanelContainer Root { get; }

	private readonly TextureRect _art;
	private readonly PanelContainer _top;
	private readonly PanelContainer _foot;
	private readonly Label _name;
	private readonly Label _stats;
	private readonly Label _detail;
	private readonly Label _passive;
	private readonly Label _move;
	private readonly Label _note;

	public KinPartyCell()
	{
		Root = new PanelContainer
		{
			CustomMinimumSize = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		// **A plain Control, not a Container** — a Container rewrites its children's anchors every
		// layout pass, so the art could not sit full-rect UNDER the anchored bands (KinLaneCell).
		var inner = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ClipContents = true,
		};
		Root.AddChild(inner);

		_art = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		inner.AddChild(_art);

		// The bands stack in a full-rect VBox — top band, a spacer taking what is left, foot band — so
		// the foot is always AT the foot and exactly as tall as its lines. Anchoring the foot to the
		// bottom and growing it upward did not: it grew DOWN and the clip ate the forecast line.
		var bands = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		bands.AddThemeConstantOverride("separation", 0);
		bands.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		inner.AddChild(bands);

		_name = Line(22, KinPalette.Bone);
		_top = Band(_name);
		bands.AddChild(_top);

		bands.AddChild(
			new Control
			{
				SizeFlagsVertical = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			}
		);

		_stats = Line(18, KinPalette.Bone);
		_detail = Line(16, KinPalette.Bone);
		_passive = Line(16, KinPalette.Bone);
		_move = Line(16, KinPalette.Gold);
		_note = Line(16, KinPalette.Red);
		_foot = Band(_stats, _detail, _passive, _move, _note);
		bands.AddChild(_foot);
	}

	private static Label Line(int size, Color colour)
	{
		var label = KinPalette.Text("", size, colour);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(1, 0);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		return label;
	}

	private static PanelContainer Band(params Label[] lines)
	{
		var band = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 0);
		foreach (var line in lines)
			column.AddChild(line);
		band.AddChild(column);
		return band;
	}

	public void ShowEmpty() => Show("", null, "", "", "", "", KinPalette.EmptySlot, null);

	/// <summary>A companion's passive, live — "THORNS 5 this turn", "MOMENTUM: next hit +4".</summary>
	public string Passive
	{
		set => Set(_passive, value);
	}

	/// <summary>`move` is gold (something you can do); `note` is red (something coming at you).</summary>
	public void Show(
		string name,
		Texture2D art,
		string stats,
		string detail,
		string move,
		string note,
		Color fill,
		Color? border
	)
	{
		_art.Texture = art;
		_art.Visible = art is not null;

		Set(_name, name);
		Set(_stats, stats);
		Set(_detail, detail);
		Set(_move, move);
		Set(_note, note);
		Set(_passive, "");

		// The bands are dark over art so the text reads on any picture; with no art they vanish into
		// the cell. The foot carries the companion's colour, so identity survives the art covering
		// the fill.
		var scrim = art is null ? Colors.Transparent : new Color(0, 0, 0, 0.55f);
		_top.AddThemeStyleboxOverride("panel", KinPalette.Box(scrim));
		_foot.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(art is null ? Colors.Transparent : new Color(fill.Darkened(0.45f), 0.9f))
		);
		_top.Visible = name.Length > 0;
		_foot.Visible =
			art is not null || stats.Length + detail.Length + move.Length + note.Length > 0;

		Root.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(fill, border, border is null ? 2 : 4)
		);
	}

	/// <summary>An empty line takes no room, so the foot shrinks and shows more of the monster.</summary>
	private static void Set(Label label, string text)
	{
		label.Text = text;
		label.Visible = text.Length > 0;
	}
}
