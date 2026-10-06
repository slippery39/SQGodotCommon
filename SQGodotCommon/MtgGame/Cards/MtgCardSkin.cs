using Common.Cards;
using MtgCore;

namespace MtgGame;

/// <summary>
/// The Dark Arcane card face (MtgVisualDesign.md §6), applied to the SHARED card scene's instance
/// through <see cref="InternalCardUI2D.Details.Decorate"/> — the scene itself is never edited.
///
/// Textures come from <c>tools/make_mtg_ui.py</c> at the shared parts' pixel sizes. The frame and
/// name plate are near-white because <see cref="MtgCardTheme"/> MULTIPLIES a colour onto them; the
/// old dark brown frame turned all five colours into the same brown.
///
/// Cost sits top-right as a brass disc (the GENERIC track) with a gem per pip beneath it (the
/// COLOUR track) — the two never substitute, so both must show.
/// </summary>
public static class MtgCardSkin
{
	private const string UiDir = "res://MtgGame/Assets/UI/";
	private const string PipsNode = "MtgPips";

	public static readonly Texture2D Frame = GD.Load<Texture2D>(UiDir + "card_frame.png");
	public static readonly Texture2D NamePlate = GD.Load<Texture2D>(UiDir + "card_name.png");
	public static readonly Texture2D RulesBox = GD.Load<Texture2D>(UiDir + "card_rules.png");
	public static readonly Texture2D CostDisc = GD.Load<Texture2D>(UiDir + "card_cost.png");
	private static readonly Texture2D PtPlate = GD.Load<Texture2D>(UiDir + "card_pt.png");
	private static readonly Texture2D Gem = GD.Load<Texture2D>(UiDir + "gem.png");

	private static readonly Color Ink = MtgUiStyles.Ink;

	/// The mana symbol on a gem — card pips and the mana bar: near-black, so it reads on all five.
	public static readonly Color ManaSymbolInk = new(0.12f, 0.08f, 0.05f, 0.9f);

	/// Brighter than the frame colours — small symbols need it (godot-frontend.md).
	private static readonly System.Collections.Generic.Dictionary<ManaColor, Color> GemColors =
		new()
		{
			[ManaColor.White] = new("f4e29b"),
			[ManaColor.Blue] = new("4f9ef0"),
			[ManaColor.Black] = new("8a7f8c"),
			[ManaColor.Red] = new("f06a48"),
			[ManaColor.Green] = new("58c06c"),
		};

	/// The bright colour of a mana colour's gem — card pips and the mana bar share it.
	public static Color GemColor(ManaColor colour) => GemColors[colour];

	private static readonly LabelSettings CostText =
		new()
		{
			Font = MtgUiStyles.TitleFont,
			FontSize = 34,
			FontColor = MtgUiStyles.GoldBorder,
			OutlineSize = 6,
			OutlineColor = new Color(0, 0, 0, 0.9f),
		};

	private static readonly LabelSettings PtText =
		new()
		{
			Font = MtgUiStyles.TitleFont,
			FontSize = 30,
			FontColor = new Color("fff0c8"),
			OutlineSize = 6,
			OutlineColor = new Color(0, 0, 0, 0.9f),
		};

	public static void Apply(InternalCardUI2D ui, ManaPool pips)
	{
		var container = ui.GetNode<Node2D>("%CardContainer");

		// Cost: top-right, mirroring where the scene put it top-left. The name plate stops short
		// of the disc rather than running under it — the old layout clipped "Knight of the White
		// Orchid" to "night of the White Orchid".
		var cost = container.GetNode<Sprite2D>("ManaCost");
		cost.Position = new Vector2(132, -196);
		// A land has no cost; an empty disc reads as "costs nothing".
		cost.Visible = !string.IsNullOrEmpty(ui.ManaCost);
		ui.GetNode<Label>("%ManaCostLabel").LabelSettings = CostText;
		Pips(cost, pips);

		var name = ui.GetNode<Label>("%NameLabel");
		name.OffsetLeft = -128;
		name.OffsetRight = 98;
		name.HorizontalAlignment = HorizontalAlignment.Left;
		// Already a per-card copy (InternalCardUI2D._Ready duplicates it), so this is safe.
		DarkInk(name.LabelSettings);

		// Dark ink on cream needs the outline AND the size-10 shadow zeroed (match-mockup traps).
		DarkInk(ui.GetNode<Label>("%RulesTextLabel").LabelSettings);

		ui.GetNode<Sprite2D>("%PowerToughnessBadge").Texture = PtPlate;
		ui.GetNode<Label>("%PowerToughnessLabel").LabelSettings = PtText;

		ui.GetNode<ColorRect>("%TypeLineBand").Color = new Color(0.16f, 0.10f, 0.06f, 0.85f);
	}

	private static void DarkInk(LabelSettings settings)
	{
		if (settings == null)
			return;
		settings.FontColor = Ink;
		settings.OutlineSize = 0;
		settings.ShadowSize = 0;
		settings.ShadowColor = Colors.Transparent;
	}

	/// <summary>One gem per pip, in a row under the disc, right-aligned to the card's edge.</summary>
	private static void Pips(Sprite2D cost, ManaPool pips)
	{
		cost.GetNodeOrNull(PipsNode)?.Free();

		var row = new Node2D { Name = PipsNode, Position = new Vector2(16, 40) };
		cost.AddChild(row);

		const float step = 26;
		var x = 0f;
		foreach (var colour in System.Linq.Enumerable.Reverse(ManaPool.Colors))
		{
			for (var i = 0; i < pips[colour]; i++)
			{
				// SelfModulate on the gem: Modulate would also tint the symbol sitting on it.
				var gem = new Sprite2D
				{
					Texture = Gem,
					SelfModulate = GemColors[colour],
					Position = new Vector2(x, 0),
				};
				var symbol = MtgSymbols.Mana(colour);
				gem.AddChild(
					new Sprite2D
					{
						Texture = symbol,
						Scale = Vector2.One * (18f / symbol.GetWidth()),
						Modulate = ManaSymbolInk,
					}
				);
				row.AddChild(gem);
				x -= step;
			}
		}
	}
}
