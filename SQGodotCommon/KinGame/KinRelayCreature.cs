using System.Collections.Immutable;
using System.Linq;
using Godot;

namespace KinGame;

/// <summary>
/// **A status as a symbol** — an icon, a number (or nothing), a tint (the declutter pass, 2026-09-30:
/// "a good UI would have a symbol plus a number"). What each means is the inspector's job, on hover.
/// </summary>
public sealed record Chip(Texture2D Icon, string Text, Color Tint, string Tip = "");

/// <summary>
/// **What a creature view shows — composed by `KinRelayField` from `PartyState` facts.** Strings and
/// colours only: nothing here decides anything.
/// </summary>
public sealed record CreatureLook(
	string Move,
	Texture2D MoveIcon,
	Texture2D MoveIcon2,
	Color MoveTint2,
	ImmutableList<bool> Pips,
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
	int Block,
	bool Rooted,
	ImmutableList<Chip> Chips,
	string BlockTip,
	string BadgeTip,
	string Note,
	Color NoteColour
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

	/// <summary>The badge's second symbol — what the move or bonus IS (a shield, a flame).</summary>
	private readonly TextureRect _icon2;

	/// <summary>An intent's aim: one dot a place in your line, back to front, filled where it lands.</summary>
	private readonly TextureRect[] _pips = new TextureRect[5];

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

	/// <summary>BLOCK: a shield on the bar's left end, its number on it — green when some is Rooted.</summary>
	private readonly TextureRect _shield;

	private readonly Label _block;

	/// <summary>The status row under the bar: a fixed pool of icon + number slots, reused every repaint.</summary>
	private readonly (TextureRect Icon, Label Text)[] _chips;

	private const int ChipIcon = 30;

	/// <summary>What each shown symbol means — read by <see cref="TipAt"/> on hover.</summary>
	private string _blockTip = "",
		_badgeTip = "";

	private readonly string[] _chipTips = new string[6];

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
		_move = Line(pill, 22, 0, 28, Width - 38, 48);
		_move.VerticalAlignment = VerticalAlignment.Center;
		_move.HorizontalAlignment = HorizontalAlignment.Left;
		_move.AutowrapMode = TextServer.AutowrapMode.Off;
		_icon2 = Add(
			pill,
			new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Position = new Vector2(30, 10),
				Size = new Vector2(28, 28),
			}
		);
		for (var i = 0; i < _pips.Length; i++)
			_pips[i] = Add(
				pill,
				new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Size = new Vector2(13, 13),
				}
			);

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

		_shield = Add(
			_content,
			// IgnoreSize BEFORE the texture and the size: set after, the texture's own 512 px had
			// already become the minimum, and the shield covered half the field.
			new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Texture = KinArt.GuardIcon,
				Position = new Vector2(-22, 256),
				Size = new Vector2(58, 60),
			}
		);
		_block = Line(_content, 24, 256, -22, 58, 60);
		_block.VerticalAlignment = VerticalAlignment.Center;
		_block.AutowrapMode = TextServer.AutowrapMode.Off;
		_block.LabelSettings.FontColor = KinPalette.Navy;
		_block.LabelSettings.OutlineSize = 0;
		_block.LabelSettings.ShadowSize = 0;

		_chips = new (TextureRect, Label)[6];
		for (var i = 0; i < _chips.Length; i++)
		{
			var icon = Add(
				_content,
				new TextureRect
				{
					Size = new Vector2(ChipIcon, ChipIcon),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				}
			);
			var text = Line(_content, 22, 0, 0, 40, ChipIcon);
			text.HorizontalAlignment = HorizontalAlignment.Left;
			text.VerticalAlignment = VerticalAlignment.Center;
			text.AutowrapMode = TextServer.AutowrapMode.Off;
			_chips[i] = (icon, text);
		}
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
		// A long move ("swap front two") steps down a size rather than run out of its pill.
		_move.LabelSettings.FontSize = look.Move.Length > 9 ? 17 : 22;
		_icon.Texture = look.MoveIcon;
		_icon.Visible = look.MoveIcon is not null;
		_icon.Modulate = side;
		_icon2.Texture = look.MoveIcon2;
		_icon2.Visible = look.MoveIcon2 is not null;
		_icon2.Modulate = look.MoveTint2;
		LayOutBadge(look);
		var rim = look.FacesLeft ? "red" : "gold";
		_pill.AddThemeStyleboxOverride("panel", KinUiKit.Plate(rim));
		// Nothing to say (a token has no first-attack bonus): no empty pill.
		_pill.Visible = look.Move.Length > 0 || look.MoveIcon is not null;
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
		// A long name ("★ ELDER MOSSHELL") steps down rather than wrap behind the HP bar.
		_name.LabelSettings.FontSize =
			look.Name.Length > 13 ? 16
			: look.Name.Length > 11 ? 18
			: 22;
		// Lit — a card can land here — the name goes gold with the ground: the drop's highlight
		// replaced its "HERE" words (2026-10-02), and the gold ground alone was faint on grass.
		_name.Modulate = look.Lit ? KinPalette.Gold : Colors.White;
		_hp.MaxValue = look.MaxHp;
		_hp.Value = look.Hp;
		_fill.ModulateColor = look.Bar;
		// No level shown (playtest, 2026-09-30: foes do not need one — a region's scaling is not news).
		_hpText.Text = $"{look.Hp}/{look.MaxHp}";

		_note.Text = look.Note;
		// Red is lifted for the forecast: the palette's red on a dark outline all but vanished.
		_note.LabelSettings.FontColor =
			look.NoteColour == KinPalette.Red ? KinPalette.Red.Lightened(0.3f) : look.NoteColour;

		_shield.Visible = _block.Visible = look.Block > 0;
		_shield.Modulate = look.Rooted
			? KinPalette.Family(KinCore.Party.Family.Grove).Lightened(0.35f)
			: KinPalette.Bone;
		_block.Text = look.Block.ToString();
		_blockTip = look.BlockTip;
		_badgeTip = look.BadgeTip;

		ShowChips(look.Chips);
	}

	/// <summary>
	/// **What the symbol under this point means** (Shayne, 2026-10-01: hover an icon, see what it is),
	/// or null. The view catches no mouse — the board hit-tests, as it does for the creature itself.
	/// </summary>
	public string TipAt(Vector2 global)
	{
		for (var i = 0; i < _chips.Length; i++)
		{
			var (icon, text) = _chips[i];
			if (
				icon.Visible
				&& _chipTips[i].Length > 0
				&& (icon.GetGlobalRect().HasPoint(global) || text.GetGlobalRect().HasPoint(global))
			)
				return _chipTips[i];
		}
		if (_shield.Visible && _blockTip.Length > 0 && _shield.GetGlobalRect().HasPoint(global))
			return _blockTip;
		if (_pill.Visible && _badgeTip.Length > 0 && _pill.GetGlobalRect().HasPoint(global))
			return _badgeTip;
		return null;
	}

	/// <summary>The chips, centred as one row under the bar; an icon with no number takes no text room.</summary>
	private void ShowChips(ImmutableList<Chip> chips)
	{
		const int gap = 8;
		int WidthOf(Chip c) => ChipIcon + (c.Text.Length > 0 ? 3 + 12 * c.Text.Length : 0);
		var shown = chips.Take(_chips.Length).ToList();
		var total = shown.Sum(WidthOf) + gap * Mathf.Max(0, shown.Count - 1);
		var x = (Width - total) / 2f;
		const float y = 336;
		for (var i = 0; i < _chips.Length; i++)
		{
			var (icon, text) = _chips[i];
			icon.Visible = text.Visible = i < shown.Count;
			if (i >= shown.Count)
				continue;
			var chip = shown[i];
			_chipTips[i] = chip.Tip;
			icon.Texture = chip.Icon;
			icon.Modulate = chip.Tint;
			icon.Position = new Vector2(x, y);
			text.Text = chip.Text;
			text.Position = new Vector2(x + ChipIcon + 3, y);
			text.Size = new Vector2(12 * chip.Text.Length + 4, ChipIcon);
			x += WidthOf(chip) + gap;
		}
	}

	/// <summary>
	/// **The badge as one centred row**: [the kind of move] [what it is] [the number] [where it lands]
	/// — symbols, not "7 → front two" (the declutter pass, 2026-09-30).
	/// </summary>
	private void LayOutBadge(CreatureLook look)
	{
		const float pill = Width - 8;
		var text = _move.LabelSettings.Font ?? ThemeDB.FallbackFont;
		var textWidth =
			look.Move.Length == 0
				? 0
				: text.GetStringSize(
					look.Move,
					HorizontalAlignment.Left,
					-1,
					_move.LabelSettings.FontSize
				).X + 4;
		var pips = look.Pips.Count;
		var total =
			(_icon.Visible ? 26 : 0)
			+ (_icon2.Visible ? 32 : 0)
			+ textWidth
			+ (pips > 0 ? 6 + pips * 15 : 0);
		var x = Mathf.Max(4, (pill - total) / 2);
		if (_icon.Visible)
		{
			_icon.Position = new Vector2(x, 13);
			x += 26;
		}
		if (_icon2.Visible)
		{
			_icon2.Position = new Vector2(x, 10);
			x += 32;
		}
		_move.Position = new Vector2(x, 0);
		_move.Size = new Vector2(textWidth + 2, 48);
		x += textWidth + 6;
		for (var i = 0; i < _pips.Length; i++)
		{
			_pips[i].Visible = i < pips;
			if (i >= pips)
				continue;
			_pips[i].Texture = look.Pips[i] ? KinArt.PipHit : KinArt.PipMiss;
			_pips[i].Position = new Vector2(x + i * 15, 17);
		}
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
