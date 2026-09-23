using Godot;

namespace KinGame;

/// <summary>
/// One space in the companion game — yours or the foe's. **Presentation only**: the board hands it
/// strings and a border colour, and every fact behind them came from `PartyState`.
///
/// Catches no mouse. Card hover is physics picking, which any mouse-catching Control silently
/// blocks (HANDOFF-KinCompanionGuard scar 2); clicks are hit-tested by the board instead.
/// </summary>
public sealed class KinPartyCell
{
	public const int Width = 220;
	public const int Height = 220;

	public PanelContainer Root { get; }

	private readonly Label _name;
	private readonly TextureRect _art;
	private readonly Label _stats;
	private readonly Label _detail;
	private readonly Label _move;
	private readonly Label _note;

	public KinPartyCell()
	{
		Root = new PanelContainer
		{
			CustomMinimumSize = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 2);
		Root.AddChild(column);

		_name = KinPalette.Text("", 22, KinPalette.Bone);
		_art = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(0, 92),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_stats = KinPalette.Text("", 18, KinPalette.Bone);
		_detail = KinPalette.Text("", 16, KinPalette.Bone);
		_move = KinPalette.Text("", 16, KinPalette.Gold);
		_note = KinPalette.Text("", 16, KinPalette.Red);

		foreach (var label in new[] { _name, _stats, _detail, _move, _note })
		{
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(1, 0);
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
		}

		column.AddChild(_name);
		column.AddChild(_art);
		column.AddChild(_stats);
		column.AddChild(_detail);
		column.AddChild(_move);
		column.AddChild(_note);
	}

	public void ShowEmpty() => Show("", null, "", "", "", "", KinPalette.EmptySlot, null);

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
		_name.Text = name;
		_art.Texture = art;
		_art.Visible = art is not null;
		_stats.Text = stats;
		_detail.Text = detail;
		_move.Text = move;
		_note.Text = note;
		Root.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(fill, border, border is null ? 2 : 4)
		);
	}
}
