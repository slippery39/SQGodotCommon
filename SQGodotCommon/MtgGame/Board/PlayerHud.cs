using System;
using MtgCore;

namespace MtgGame;

/// <summary>
/// One side's strip at the top or bottom of the board (MtgVisualDesign.md §6): the round hero
/// portrait with its life on an oxblood seal, the mana, and the zone piles. The opponent's strip
/// also shows their hand as card backs. Replaces the old left-rail PlayerPanel and the text
/// graveyard buttons.
///
/// The portrait is the player as a TARGET: clicking it attacks or aims at that player.
/// </summary>
public partial class PlayerHud : HBoxContainer
{
	private const string UiDir = "res://MtgGame/Assets/UI/";
	private static readonly Texture2D CardBack = GD.Load<Texture2D>(UiDir + "card_back.png");

	private static readonly Color TargetRing = new("ffe066");
	private static readonly Color ActiveRing = new("fff2c0");
	private static readonly Color IdleRing = new("a08a6a");

	[Export]
	public bool IsOpponent { get; set; }

	private HBoxContainer? _handBacks;
	private TextureRect _ring = null!;
	private Label _life = null!;
	private HBoxContainer _mana = null!;
	private ZonePile _library = null!;
	private ZonePile _graveyard = null!;
	private ZonePile _exile = null!;
	private bool _active;

	public event Action? PortraitClicked;
	public event Action? GraveyardClicked;
	public event Action? ExileClicked;

	public override void _Ready()
	{
		Alignment = AlignmentMode.Center;
		MouseFilter = MouseFilterEnum.Ignore;
		AddThemeConstantOverride("separation", 18);

		if (IsOpponent)
		{
			// ShrinkCenter on everything in this row: an HBox child otherwise fills the strip's full
			// height, which turned card backs into slivers and mana pips into bars.
			_handBacks = new HBoxContainer
			{
				MouseFilter = MouseFilterEnum.Ignore,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			};
			_handBacks.AddThemeConstantOverride("separation", -30);
			AddChild(_handBacks);
		}

		AddChild(BuildPortrait());

		_mana = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_mana.AddThemeConstantOverride("separation", 4);
		AddChild(_mana);

		_library = Pile();
		_graveyard = Pile();
		_graveyard.Clicked += () => GraveyardClicked?.Invoke();
		_exile = Pile();
		_exile.Clicked += () => ExileClicked?.Invoke();
	}

	/// <summary>
	/// <paramref name="exileTop"/> and <paramref name="exileCount"/> should leave out LANDS: a
	/// played land is exiled as it becomes mana, so the raw exile pile is mostly lands.
	/// </summary>
	public void Refresh(
		MtgPlayer player,
		int handCount,
		int libraryCount,
		Card? graveyardTop,
		int graveyardCount,
		Card? exileTop,
		int exileCount
	)
	{
		_life.Text = $"{player.Life}";

		if (_handBacks != null)
		{
			foreach (var child in _handBacks.GetChildren())
				child.QueueFree();
			for (var i = 0; i < handCount; i++)
				_handBacks.AddChild(
					new TextureRect
					{
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						Texture = CardBack,
						CustomMinimumSize = new Vector2(52, 73),
						MouseFilter = MouseFilterEnum.Ignore,
					}
				);
			_handBacks.TooltipText = $"Hand: {handCount}";
		}

		// ponytail: the old pip row until step 5's two-track mana display.
		foreach (var child in _mana.GetChildren())
			child.QueueFree();
		for (var i = 0; i < player.MaxMana; i++)
		{
			var pip = new Panel { CustomMinimumSize = new Vector2(14, 14) };
			pip.AddThemeStyleboxOverride("panel", MtgUiStyles.ManaPip(i < player.CurrentMana));
			_mana.AddChild(pip);
		}

		_library.Refresh(CardBack, libraryCount, "Library", clickable: false);
		_graveyard.Refresh(Art(graveyardTop), graveyardCount, "Graveyard", clickable: true);
		_exile.Refresh(Art(exileTop), exileCount, "Exile", clickable: true);
	}

	public void SetActive(bool isActive)
	{
		_active = isActive;
		_ring.SelfModulate = isActive ? ActiveRing : IdleRing;
	}

	/// A valid target for the spell or ability being aimed — overrides the turn ring.
	public void SetTargetable(bool targetable) =>
		_ring.SelfModulate =
			targetable ? TargetRing
			: _active ? ActiveRing
			: IdleRing;

	/// Where a drawn card flies in from.
	public Vector2 LibraryCenter => _library.GlobalPosition + _library.Size / 2;

	public void Flash(Color color)
	{
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate", color, 0.15f);
		tween.TweenProperty(this, "modulate", Colors.White, 0.35f);
	}

	private Control BuildPortrait()
	{
		var size = new Vector2(150, 150);
		var root = new Control
		{
			CustomMinimumSize = size,
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		root.MouseDefaultCursorShape = CursorShape.PointingHand;
		root.TooltipText = IsOpponent ? "Opponent" : "You";
		root.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				PortraitClicked?.Invoke();
		};

		var mask = Picture(UiDir + "portrait_mask.png", root, size);
		mask.ClipChildren = ClipChildrenMode.Only;
		Picture(UiDir + (IsOpponent ? "portrait_opponent.jpg" : "portrait_player.jpg"), mask, size);
		_ring = Picture(UiDir + "portrait_ring.png", root, size);

		var seal = Picture(UiDir + "seal.png", root, new Vector2(64, 64));
		seal.Position = new Vector2(98, 92);
		_life = new Label
		{
			LabelSettings = new LabelSettings
			{
				Font = MtgUiStyles.TitleFont,
				FontSize = 30,
				FontColor = new Color("fff0c8"),
				OutlineSize = 6,
				OutlineColor = Colors.Black,
			},
			Size = seal.Size,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		seal.AddChild(_life);
		return root;
	}

	/// ExpandMode before Texture, or the texture's pixel size becomes the minimum (godot-frontend.md).
	private static TextureRect Picture(string path, Control parent, Vector2 size)
	{
		var rect = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Texture = GD.Load<Texture2D>(path),
			Size = size,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		parent.AddChild(rect);
		return rect;
	}

	private ZonePile Pile()
	{
		var pile = new ZonePile { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		AddChild(pile);
		return pile;
	}

	private static Texture2D? Art(Card? card) =>
		card == null ? null : CardArtLoader.Load(card.Name);
}
