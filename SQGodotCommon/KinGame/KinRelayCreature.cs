using Godot;

namespace KinGame;

/// <summary>
/// **What a creature view shows — composed by `KinRelayField` from `PartyState` facts.** Strings and
/// colours only: nothing here decides anything.
/// </summary>
public sealed record CreatureLook(
	string Move,
	Color MoveColour,
	Texture2D Art,
	bool Drawn,
	bool FacesLeft,
	int Order,
	Color Ring,
	bool Lit,
	string Name,
	int Hp,
	int MaxHp,
	Color Bar,
	string Status,
	string Note,
	Color NoteColour
);

/// <summary>
/// **One creature in a line — THE RELAY's sprite** (`KinUI.md`, "THE RELAY screen"). Top to bottom:
/// its next move and where it lands, the sprite (a round medallion: the portrait cropped, or the
/// silhouette of a creature not drawn yet), the name, an HP bar, one status line and one note.
///
/// **A plain Control with hand-placed children — no containers**, so the field can slide it and the
/// animator can lunge it without a layout pass undoing either (kin-frontend rule). Catches no mouse:
/// card hover is physics picking, which a mouse-catching Control silently blocks; the board
/// hit-tests instead.
/// </summary>
public sealed class KinRelayCreature
{
	public const int Width = 176;
	public const int Height = 392;
	private const int Medal = 150;

	public Control Root { get; }

	/// <summary>
	/// The sprite — the medallion, its ring and its order badge, moved as one. What a blow flashes, a
	/// number rises off, and a lunge moves: lunging the whole view dragged its name and HP bar into
	/// the neighbour's.
	/// </summary>
	public Control Sprite { get; }

	private readonly Label _move;
	private readonly Panel _clip;
	private readonly TextureRect _art;
	private readonly Panel _ring;
	private readonly StyleBoxFlat _ringBox;
	private readonly PanelContainer _orderPip;
	private readonly Label _order;
	private readonly Label _name;
	private readonly ProgressBar _hp;
	private readonly StyleBoxFlat _fill;
	private readonly Label _hpText;
	private readonly Label _status;
	private readonly Label _note;

	public KinRelayCreature()
	{
		Root = new Control
		{
			Size = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		_move = Line(20, KinPalette.Bone, 0, 0, 58);
		_move.VerticalAlignment = VerticalAlignment.Bottom;

		Sprite = new Control
		{
			Position = new Vector2((Width - Medal) / 2f, 62),
			Size = new Vector2(Medal, Medal),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		Root.AddChild(Sprite);

		// The medallion: a round mask holding the art, and a ring drawn OVER it (a border drawn by the
		// mask itself would be hidden by the art it clips).
		var round = Round(KinPalette.Slate);
		_clip = new Panel
		{
			Size = new Vector2(Medal, Medal),
			ClipChildren = CanvasItem.ClipChildrenMode.AndDraw,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_clip.AddThemeStyleboxOverride("panel", round);
		Sprite.AddChild(_clip);

		_art = new TextureRect
		{
			Size = new Vector2(Medal, Medal),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_clip.AddChild(_art);

		_ringBox = Round(Colors.Transparent);
		_ring = new Panel { Size = _clip.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		_ring.AddThemeStyleboxOverride("panel", _ringBox);
		Sprite.AddChild(_ring);

		(_orderPip, _order) = KinPalette.Pip(KinPalette.Navy, 20);
		_orderPip.Position = new Vector2(-6, -4);
		_orderPip.MouseFilter = Control.MouseFilterEnum.Ignore;
		Sprite.AddChild(_orderPip);

		_name = Line(22, KinPalette.Bone, 216, 0, 28);

		_fill = new StyleBoxFlat { CornerRadiusTopLeft = 4, CornerRadiusBottomLeft = 4 };
		_hp = new ProgressBar
		{
			Position = new Vector2(10, 248),
			Size = new Vector2(Width - 20, 26),
			ShowPercentage = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_hp.AddThemeStyleboxOverride(
			"background",
			KinPalette.Box(KinPalette.Navy, KinPalette.Slate, 1)
		);
		_hp.AddThemeStyleboxOverride("fill", _fill);
		Root.AddChild(_hp);
		_hpText = Line(20, KinPalette.Bone, 247, 0, 28);

		_status = Line(20, KinPalette.Bone, 280, 0, 52);
		_note = Line(20, KinPalette.Red, 334, 0, 56);
	}

	/// <summary>Kept off the view's edges: at full width a neighbour's note ran straight into this one's.</summary>
	private const int Pad = 8;

	private Label Line(int size, Color colour, float y, float x, float height)
	{
		var label = KinPalette.Text("", size, colour);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.Position = new Vector2(x + Pad, y);
		label.Size = new Vector2(Width - 2 * Pad, height);
		label.CustomMinimumSize = new Vector2(1, 0);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		Root.AddChild(label);
		return label;
	}

	private static StyleBoxFlat Round(Color fill)
	{
		var box = new StyleBoxFlat { BgColor = fill };
		box.SetCornerRadiusAll(Medal / 2);
		return box;
	}

	public void Show(CreatureLook look)
	{
		_move.Text = look.Move;
		_move.AddThemeColorOverride("font_color", look.MoveColour);

		_art.Texture = look.Art;
		_art.FlipH = look.FacesLeft;
		// A drawing fills the medallion; a silhouette is transparent, so covering would crop it into
		// a meaningless corner — it stays centred over the ground (KinUI.md, "the two art cases").
		_art.StretchMode = look.Drawn
			? TextureRect.StretchModeEnum.KeepAspectCovered
			: TextureRect.StretchModeEnum.KeepAspectCentered;

		_ringBox.BorderColor = look.Ring;
		_ringBox.SetBorderWidthAll(look.Lit ? 7 : 4);

		_orderPip.Visible = look.Order > 0;
		_order.Text = look.Order.ToString();

		_name.Text = look.Name;
		_hp.MaxValue = look.MaxHp;
		_hp.Value = look.Hp;
		_fill.BgColor = look.Bar;
		_hpText.Text = $"{look.Hp}/{look.MaxHp}";

		_status.Text = look.Status;
		_note.Text = look.Note;
		_note.AddThemeColorOverride("font_color", look.NoteColour);
	}
}
