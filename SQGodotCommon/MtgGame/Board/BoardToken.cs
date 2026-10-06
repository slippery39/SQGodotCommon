using System;
using System.Collections.Generic;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

/// <summary>
/// A permanent on the battlefield as a COMPACT TOKEN (MtgVisualDesign.md §6), not a full card:
/// arched art crop, name plate, power and toughness in two big discs, keywords along the top, a
/// rim in the card's colour. The full card is the hover preview. Built in code from the
/// <c>tools/make_mtg_ui.py</c> textures; the battlefield uses this, draft and deck grids keep
/// <see cref="BoardCard"/>.
///
/// Toughness shows what is LEFT (toughness minus damage), red when hurt — the number that decides
/// whether a hit kills it, read without arithmetic.
/// </summary>
public partial class BoardToken : Control
{
	public static readonly Vector2 TokenSize = new(150, 190);

	private const string UiDir = "res://MtgGame/Assets/UI/";
	private static readonly Texture2D MaskTex = GD.Load<Texture2D>(UiDir + "token_mask.png");
	private static readonly Texture2D RimTex = GD.Load<Texture2D>(UiDir + "token_rim.png");
	private static readonly Texture2D GlowTex = GD.Load<Texture2D>(UiDir + "token_glow.png");
	private static readonly Texture2D DiscTex = GD.Load<Texture2D>(UiDir + "card_cost.png");

	private static readonly Color Hurt = new("ff6a5a");
	private static readonly Color Cream = new("fff0c8");

	private static readonly Dictionary<BoardCardHighlight, Color> GlowColors =
		new()
		{
			[BoardCardHighlight.Target] = new Color("ffd24a"),
			[BoardCardHighlight.Selected] = new Color("7dff6a"),
			[BoardCardHighlight.AdditionalCost] = new Color("ff9a2a"),
			[BoardCardHighlight.Flashback] = new Color("c58aff"),
		};

	/// The "can attack" glow is softer than a selection: it is ambient, not a question.
	private static readonly Color ReadyGlow = new(0.45f, 1f, 0.4f, 0.55f);

	private int _cardId;
	private TextureRect _glow = null!;
	private TextureRect _art = null!;
	private TextureRect _rim = null!;
	private Label _name = null!;
	private Label _keywords = null!;
	private Label _power = null!;
	private Label _toughness = null!;
	private Control _powerDisc = null!;
	private Control _toughnessDisc = null!;

	public event Action<int>? Clicked;
	public event Action<int>? RightClicked;
	public event Action<int>? Hovered;
	public event Action<int>? HoverEnded;

	public override void _Ready()
	{
		CustomMinimumSize = TokenSize;
		PivotOffset = TokenSize / 2;
		MouseFilter = MouseFilterEnum.Stop;

		_glow = Picture(GlowTex);
		_glow.Position = new Vector2(-24, -24);
		_glow.Size = TokenSize + new Vector2(48, 48);

		// The mask draws nothing itself; it only clips the art to the arch.
		var mask = Picture(MaskTex);
		mask.ClipChildren = ClipChildrenMode.Only;
		// Behind the art: a token with no picture (the Clue) still reads as a token, not a hole.
		mask.AddChild(
			new ColorRect
			{
				Color = MtgUiStyles.CardBg,
				Size = TokenSize,
				MouseFilter = MouseFilterEnum.Ignore,
			}
		);
		_art = Picture(null, mask);
		_art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		_rim = Picture(RimTex);

		var plate = new Panel { MouseFilter = MouseFilterEnum.Ignore };
		plate.AddThemeStyleboxOverride("panel", MtgUiStyles.Banner());
		plate.Position = new Vector2(8, 118);
		plate.Size = new Vector2(TokenSize.X - 16, 30);
		AddChild(plate);
		_name = Text(
			plate,
			15,
			MtgUiStyles.Ink,
			outline: 0,
			new Vector2(6, 0),
			new Vector2(plate.Size.X - 12, 30)
		);

		_keywords = Text(this, 13, Cream, outline: 5, new Vector2(10, 8), new Vector2(130, 22));

		(_powerDisc, _power) = Disc(new Vector2(-10, 142));
		(_toughnessDisc, _toughness) = Disc(new Vector2(TokenSize.X - 46, 142));

		GuiInput += OnGuiInput;
		MouseEntered += () => Hovered?.Invoke(_cardId);
		MouseExited += () => HoverEnded?.Invoke(_cardId);
	}

	/// <param name="canAttack">
	/// Whether this side may attack now (its own turn). Combined here with the creature's own
	/// state; the engine's attack legality stays the authority when the click lands.
	/// </param>
	public void Refresh(Card card, GameState state, BoardCardHighlight highlight, bool canAttack)
	{
		_cardId = card.Id;
		_art.Texture = CardArtLoader.Load(card.Name);
		_rim.SelfModulate = MtgCardTheme.FrameColor(card);
		_name.Text = card.Name;
		Fit(_name, 15, 10);

		var creature = card.GetComponent<CreatureComponent>();
		var walker = card.GetComponent<PlaneswalkerComponent>();
		var stats = creature != null ? state.GetEffectiveStats(card.Id) : null;

		_powerDisc.Visible = creature != null;
		_toughnessDisc.Visible = creature != null || walker != null;
		if (creature != null)
		{
			_power.Text = $"{stats!.Power}";
			_toughness.Text = $"{stats.Toughness - creature.Damage}";
			_toughness.LabelSettings.FontColor = creature.Damage > 0 ? Hurt : Cream;
		}
		else if (walker != null)
		{
			_toughness.Text = $"{walker.Loyalty}";
			_toughness.LabelSettings.FontColor = Cream;
		}

		var keywords =
			creature != null ? MtgCardMapper.KeywordNames(creature, stats!) : new List<string>();
		if (creature is { CoverTurns: > 0 })
			keywords.Add($"Cover {creature.CoverTurns}");
		// ponytail: text until step 6 swaps in an icon per keyword.
		_keywords.Text = string.Join(" · ", keywords);
		Fit(_keywords, 13, 9);

		var ready =
			canAttack
			&& creature is { HasSummoningSickness: false, IsExhausted: false }
			&& highlight == BoardCardHighlight.None;
		_glow.Visible = ready || GlowColors.ContainsKey(highlight);
		_glow.SelfModulate = GlowColors.TryGetValue(highlight, out var c) ? c : ReadyGlow;

		// Exhausted: greyed and tilted (the mockup's read); summoning sick: dimmed only.
		Rotation = highlight == BoardCardHighlight.Exhausted ? Mathf.DegToRad(12) : 0;
		Modulate = highlight switch
		{
			BoardCardHighlight.Exhausted => new Color(0.55f, 0.55f, 0.6f),
			BoardCardHighlight.SummoningSick => new Color(0.75f, 0.75f, 0.75f),
			_ => Colors.White,
		};
	}

	private void OnGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton { Pressed: true } mb)
			return;
		if (mb.ButtonIndex == MouseButton.Left)
			Clicked?.Invoke(_cardId);
		else if (mb.ButtonIndex == MouseButton.Right)
			RightClicked?.Invoke(_cardId);
	}

	/// ExpandMode before Texture, or the texture's pixel size becomes the minimum (godot-frontend.md).
	private TextureRect Picture(Texture2D? texture, Control? parent = null)
	{
		var rect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Texture = texture,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = TokenSize,
		};
		(parent ?? this).AddChild(rect);
		return rect;
	}

	private (Control, Label) Disc(Vector2 position)
	{
		var disc = Picture(DiscTex);
		disc.Position = position;
		disc.Size = new Vector2(56, 56);
		var label = Text(disc, 30, Cream, outline: 7, Vector2.Zero, disc.Size);
		label.LabelSettings.Font = MtgUiStyles.TitleFont;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		return (disc, label);
	}

	/// Every label over a picture gets its OWN LabelSettings with an outline (godot-frontend.md).
	private static Label Text(
		Control parent,
		int size,
		Color color,
		int outline,
		Vector2 position,
		Vector2 boxSize
	)
	{
		var label = new Label
		{
			LabelSettings = new LabelSettings
			{
				FontSize = size,
				FontColor = color,
				OutlineSize = outline,
				OutlineColor = new Color(0, 0, 0, 0.9f),
			},
			Position = position,
			Size = boxSize,
			ClipText = true,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		parent.AddChild(label);
		return label;
	}

	/// Shrinks a label's font until its text fits its box — names run to 28 characters.
	private static void Fit(Label label, int max, int min)
	{
		var font = label.LabelSettings.Font ?? label.GetThemeFont("font");
		var size = max;
		while (size > min && font.GetStringSize(label.Text, fontSize: size).X > label.Size.X)
			size--;
		label.LabelSettings.FontSize = size;
	}
}
