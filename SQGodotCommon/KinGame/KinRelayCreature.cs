using Godot;

namespace KinGame;

/// <summary>
/// **What a creature view shows — composed by `KinRelayField` from `PartyState` facts.** Strings and
/// colours only: nothing here decides anything.
/// </summary>
public sealed record CreatureLook(
	string Move,
	Texture2D MoveIcon,
	Texture2D Art,
	bool Standing,
	bool FacesLeft,
	int Step,
	Color Ring,
	bool Lit,
	string Name,
	int Hp,
	int MaxHp,
	Color Bar,
	string Status,
	string Note,
	Color NoteColour,
	int Level = 0
);

/// <summary>
/// **One creature in a line — THE RELAY's sprite, laid out as the style-D mockup**
/// (`KinVisualDesign.md`, `docs/mockups/round1/chatgpt_D_S1.png`). Top to bottom: a BADGE ROW (the
/// next move in a dark pill, and the STEP it acts in — shared with whoever stands at its depth on
/// the other side), the creature STANDING on a shadow, its name, an HP bar, the forecast and one
/// status line.
///
/// **Two kinds of art.** A transparent standing sprite (`Art/sprites/`) stands on its shadow, feet
/// on the ground line, and may be wider than its place. A creature with no sprite yet keeps the
/// round portrait medallion — the art arrives one creature at a time.
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

	/// <summary>The sprite box: feet sit on its bottom edge — the ground line.</summary>
	private const int ArtTop = 56;
	private const int ArtHeight = 180;

	/// <summary>A standing sprite may spill past its place, as a wide boar does in the mockup.</summary>
	private const int MaxArtWidth = 270;

	public Control Root { get; }

	/// <summary>
	/// The creature itself — sprite or medallion, with its shadow, moved as one. What a blow flashes,
	/// a number rises off, and a lunge moves: lunging the whole view dragged its name and HP bar into
	/// the neighbour's.
	/// </summary>
	public Control Sprite { get; }

	/// <summary>
	/// Everything the view draws, SCALED by <see cref="Fit"/>. Not the Root: `KinAnimator.Pop`
	/// tweens the Root's scale back to 1, which would undo a scaled Root on every arrival.
	/// </summary>
	private readonly Control _content;

	private readonly Panel _pill;
	private readonly TextureRect _icon;
	private readonly Label _move;
	private readonly Panel _disc;

	private readonly Label _step;
	private readonly TextureRect _shadow;
	private readonly TextureRect _standing;
	private readonly Control _medal;
	private readonly TextureRect _portrait;
	private readonly StyleBoxFlat _ringBox;
	private readonly Label _name;
	private readonly ProgressBar _hp;
	private readonly StyleBoxTexture _fill;
	private readonly Label _hpText;
	private readonly Label _note;
	private readonly Label _status;

	public KinRelayCreature()
	{
		Root = new Control
		{
			Size = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_content = Add(Root, new Control { Size = new Vector2(Width, Height) });

		// ===== The badge row: an icon and the move in a pill, the step as a disc on its corner.
		var pill = _pill = Add(
			_content,
			new Panel { Position = new Vector2(4, 8), Size = new Vector2(Width - 8, 48) }
		);
		_icon = Add(
			pill,
			new TextureRect
			{
				Position = new Vector2(6, 13),
				Size = new Vector2(22, 22),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			}
		);
		_move = Line(pill, 20, 0, 28, Width - 38, 48);
		_move.VerticalAlignment = VerticalAlignment.Center;

		_disc = Add(
			_content,
			new Panel { Position = new Vector2(Width - 30, -6), Size = new Vector2(32, 32) }
		);
		_step = Line(_disc, 20, 0, 0, 32, 32);
		_step.VerticalAlignment = VerticalAlignment.Center;

		// ===== The creature, standing on its shadow.
		Sprite = Add(
			_content,
			new Control { Position = new Vector2(0, ArtTop), Size = new Vector2(Width, ArtHeight) }
		);

		// The CONTACT shadow (`Art/ui/contact.png`, a soft white ellipse tinted here) is what makes a
		// creature SIT on the ground; a hard-edged bar read as hovering. It is also the "you can drop
		// here" mark: it turns gold, the way the ring did. Sized to the sprite in `Stand`.
		_shadow = Add(
			Sprite,
			new TextureRect
			{
				Texture = KinArt.Drawing("ui/contact"),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
			}
		);

		_standing = Add(
			Sprite,
			new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
			}
		);

		// The medallion: a round mask holding the portrait, and a ring drawn OVER it (a border drawn
		// by the mask itself would be hidden by the art it clips).
		_medal = Add(
			Sprite,
			new Control
			{
				Position = new Vector2((Width - Medal) / 2f, ArtHeight - Medal - 8),
				Size = new Vector2(Medal, Medal),
			}
		);
		var clip = Add(
			_medal,
			new Panel
			{
				Size = new Vector2(Medal, Medal),
				ClipChildren = CanvasItem.ClipChildrenMode.AndDraw,
			}
		);
		clip.AddThemeStyleboxOverride("panel", Rounded(KinPalette.Slate, Medal / 2));
		_portrait = Add(
			clip,
			new TextureRect
			{
				Size = new Vector2(Medal, Medal),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			}
		);
		_ringBox = Rounded(Colors.Transparent, Medal / 2);
		Add(_medal, new Panel { Size = new Vector2(Medal, Medal) })
			.AddThemeStyleboxOverride("panel", _ringBox);

		// ===== Name, HP, forecast, status — on a soft dark BACKSHADOW, as the mockup has it: over a
		// bright meadow, outlined words alone were still hard to read.
		// A blurred ellipse (`Art/ui/scrim.png`), not a box: a StyleBoxFlat panel read as a dark card.
		Add(
			_content,
			new TextureRect
			{
				Texture = KinArt.Drawing("ui/scrim"),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Position = new Vector2(-40, 212),
				Size = new Vector2(Width + 80, 150),
			}
		);

		_name = Line(_content, 22, 240, 0, Width, 30);

		_fill = (StyleBoxTexture)KinUiKit.Nine("bar_fill", 14, 0, null, 0, 0);
		_hp = Add(
			_content,
			new ProgressBar
			{
				Position = new Vector2(10, 272),
				Size = new Vector2(Width - 20, 28),
				ShowPercentage = false,
			}
		);
		var trough = KinUiKit.Nine("bar_trough", 14, 0, null, 0, 0);
		_hp.AddThemeStyleboxOverride("background", trough);
		_hp.AddThemeStyleboxOverride("fill", _fill);
		_hpText = Line(_content, 20, 272, 0, Width, 28);
		_hpText.VerticalAlignment = VerticalAlignment.Center;

		_note = Line(_content, 26, 302, 0, Width, 32);
		_status = Line(_content, 18, 336, 0, Width, 56);
	}

	private static T Add<T>(Node parent, T child)
		where T : Control
	{
		child.MouseFilter = Control.MouseFilterEnum.Ignore;
		parent.AddChild(child);
		return child;
	}

	/// <summary>
	/// **Every word here is outlined**: the view now stands on a painted backdrop, not on navy, and
	/// bare text over a forest is unreadable. Through a `LabelSettings` of its own — the theme's
	/// `outline_size` override drew NOTHING here (seen on a capture), and LabelSettings is how the
	/// project's working outlines (`indicator_label.tscn`, the cards) are done.
	/// </summary>
	private static Label Line(Node parent, int size, float y, float x, float width, float height)
	{
		var label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Position = new Vector2(x, y),
			Size = new Vector2(width, height),
			CustomMinimumSize = new Vector2(1, 0),
			LabelSettings = new LabelSettings
			{
				FontSize = size,
				FontColor = KinPalette.Bone,
				OutlineSize = 6,
				OutlineColor = new Color(0.04f, 0.06f, 0.09f),
				ShadowSize = 4,
				ShadowColor = new Color(0, 0, 0, 0.6f),
				ShadowOffset = new Vector2(2, 3),
			},
		};
		return Add(parent, label);
	}

	private static StyleBoxFlat Rounded(Color fill, int radius)
	{
		var box = new StyleBoxFlat { BgColor = fill };
		box.SetCornerRadiusAll(radius);
		return box;
	}

	/// <summary>The view at a scale — bigger when few creatures share a line (the field decides).</summary>
	public void Fit(float scale)
	{
		_content.Scale = new Vector2(scale, scale);
		Root.Size = new Vector2(Width, Height) * scale;
	}

	public void Show(CreatureLook look)
	{
		var side = look.FacesLeft ? KinPalette.Red : KinPalette.Gold;

		_move.Text = look.Move;
		// A long move ("next: 21 → front") steps down a size rather than wrap out of its pill.
		_move.LabelSettings.FontSize = look.Move.Length > 13 ? 16 : 20;
		_icon.Texture = look.MoveIcon;
		_icon.Visible = look.MoveIcon is not null;
		// No icon, no icon gap: "swap front two" wrapped while leaving room for an icon it lacked.
		_move.Position = new Vector2(_icon.Visible ? 28 : 6, 0);
		_move.Size = new Vector2(Width - (_icon.Visible ? 38 : 20), 48);
		_icon.Modulate = side;
		var rim = look.FacesLeft ? "red" : "gold";
		_pill.AddThemeStyleboxOverride("panel", KinUiKit.Plate(rim));
		_disc.Visible = look.Step > 0;
		_disc.AddThemeStyleboxOverride("panel", KinUiKit.Nine("disc_" + rim, 0, 0, null, 0, 0));
		_step.Text = look.Step.ToString();

		_shadow.Modulate = look.Lit ? new Color(KinPalette.Gold, 0.95f) : new Color(0, 0, 0, 0.6f);

		_standing.Visible = _shadow.Visible = look.Standing;
		_medal.Visible = !look.Standing;
		if (look.Standing)
			Stand(look.Art, look.FacesLeft);
		else
		{
			_portrait.Texture = look.Art;
			_portrait.FlipH = look.FacesLeft;
			_ringBox.BorderColor = look.Ring;
			_ringBox.SetBorderWidthAll(look.Lit ? 7 : 4);
		}

		_name.Text = look.Name;
		_hp.MaxValue = look.MaxHp;
		_hp.Value = look.Hp;
		_fill.ModulateColor = look.Bar;
		// The level rides in the bar: on the name line it cut long names ("BROODVINE LV").
		_hpText.Text =
			look.Level > 0 ? $"LV{look.Level} · {look.Hp}/{look.MaxHp}" : $"{look.Hp}/{look.MaxHp}";

		_note.Text = look.Note;
		// Red is lifted for the forecast: the palette's red on a dark outline all but vanished.
		_note.LabelSettings.FontColor =
			look.NoteColour == KinPalette.Red ? KinPalette.Red.Lightened(0.3f) : look.NoteColour;
		_status.Text = look.Status;
	}

	/// <summary>Scaled to fit, feet on the ground line, centred on its place — mirrored for a foe.</summary>
	private void Stand(Texture2D art, bool facesLeft)
	{
		var size = art.GetSize();
		var scale = Mathf.Min(MaxArtWidth / size.X, (ArtHeight - 10) / size.Y);
		var drawn = size * scale;
		_standing.Texture = art;
		_standing.FlipH = facesLeft;
		_standing.Size = drawn;
		_standing.Position = new Vector2((Width - drawn.X) / 2, ArtHeight - 6 - drawn.Y);

		// The shadow spans most of the body, centred under the feet.
		var span = Mathf.Clamp(drawn.X * 0.85f, 90, MaxArtWidth);
		_shadow.Size = new Vector2(span, 30);
		_shadow.Position = new Vector2((Width - span) / 2, ArtHeight - 22);
	}
}
