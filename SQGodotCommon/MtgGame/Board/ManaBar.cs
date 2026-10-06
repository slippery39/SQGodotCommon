using MtgCore;

namespace MtgGame;

/// <summary>
/// A player's mana as two tracks (MtgVisualDesign.md §6), because the engine pays them separately
/// and neither substitutes for the other: GENERIC as amber crystals with a "4/6" numeral (a phone
/// cannot count crystals), and each COLOUR the player's lands make as a gem with what is left this
/// turn. A spent crystal or an exhausted colour goes dark. Lands never stay in play, so this bar
/// is where a player's lands are seen at all.
/// </summary>
public partial class ManaBar : PanelContainer
{
	private const string UiDir = "res://MtgGame/Assets/UI/";
	private static readonly Texture2D Crystal = GD.Load<Texture2D>(UiDir + "crystal.png");
	private static readonly Texture2D Gem = GD.Load<Texture2D>(UiDir + "gem.png");

	/// Past this the crystals stop growing and the numeral carries the count.
	private const int MaxCrystals = 10;

	private static readonly Color Lit = new("f2b84a");
	private static readonly Color Spent = new("3a2c22");

	private HBoxContainer _crystals = null!;
	private Label _numeral = null!;
	private HBoxContainer _colours = null!;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 10);
		AddChild(row);

		_crystals = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_crystals.AddThemeConstantOverride("separation", 2);
		row.AddChild(_crystals);

		_numeral = Text(26, MtgUiStyles.GoldBorder);
		row.AddChild(_numeral);

		_colours = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		_colours.AddThemeConstantOverride("separation", 6);
		row.AddChild(_colours);
	}

	public void Refresh(MtgPlayer player)
	{
		foreach (var child in _crystals.GetChildren())
			child.QueueFree();
		for (var i = 0; i < System.Math.Min(player.MaxMana, MaxCrystals); i++)
			_crystals.AddChild(
				Picture(Crystal, new Vector2(22, 32), i < player.CurrentMana ? Lit : Spent)
			);
		_numeral.Text = $"{player.CurrentMana}/{player.MaxMana}";
		TooltipText = $"Mana: {player.CurrentMana} of {player.MaxMana} left this turn";

		foreach (var child in _colours.GetChildren())
			child.QueueFree();
		foreach (var colour in ManaPool.Colors)
		{
			var max = player.MaxColorMana[colour];
			if (max == 0)
				continue;
			var left = player.CurrentColorMana[colour];

			var stone = Picture(
				Gem,
				new Vector2(34, 34),
				left > 0 ? MtgCardSkin.GemColor(colour) : new Color(0.25f, 0.22f, 0.2f)
			);
			stone.TooltipText = $"{colour}: {left} of {max} left this turn";
			stone.MouseFilter = MouseFilterEnum.Pass;
			var count = Text(20, Colors.White);
			count.Text = $"{left}";
			count.Size = stone.CustomMinimumSize;
			count.HorizontalAlignment = HorizontalAlignment.Center;
			stone.AddChild(count);
			_colours.AddChild(stone);
		}
	}

	/// ExpandMode before Texture, or the texture's pixel size becomes the minimum (godot-frontend.md).
	private static TextureRect Picture(Texture2D texture, Vector2 size, Color tint) =>
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Texture = texture,
			CustomMinimumSize = size,
			SelfModulate = tint,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};

	private static Label Text(int size, Color color) =>
		new()
		{
			LabelSettings = new LabelSettings
			{
				Font = MtgUiStyles.TitleFont,
				FontSize = size,
				FontColor = color,
				OutlineSize = 6,
				OutlineColor = Colors.Black,
			},
			VerticalAlignment = VerticalAlignment.Center,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
}
