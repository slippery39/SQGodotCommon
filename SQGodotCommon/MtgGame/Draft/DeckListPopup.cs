using System.Collections.Generic;
using System.Linq;
using MtgCore;
using MtgSimulator;

namespace MtgGame;

/// <summary>
/// Shows what a seat actually played, split the way <c>Draft.BuildDeck</c> splits it: the first
/// <c>DefaultMaxSpells</c> non-lands in pick order are the deck, the rest of the pool never left
/// the sideboard. Reading the two side by side is the point — it is how you tell whether a seat
/// drafted badly or just drew badly.
/// </summary>
public partial class DeckListPopup : CanvasLayer
{
	private Label _title = null!;
	private Label _deckHeader = null!;
	private Label _sideHeader = null!;
	private VBoxContainer _deckList = null!;
	private VBoxContainer _sideList = null!;

	public override void _Ready()
	{
		Layer = 8;

		var scrim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
		scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scrim.MouseFilter = Control.MouseFilterEnum.Stop;
		// Click anywhere outside the panel to dismiss — no close button to hunt for.
		scrim.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true })
				Hide();
		};
		AddChild(scrim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(center);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		panel.CustomMinimumSize = new Vector2(720, 620);
		center.AddChild(panel);

		var margin = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 18);
		panel.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_title = new Label();
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.AddThemeFontSizeOverride("font_size", 28);
		_title.Modulate = MtgUiStyles.GoldBorder;
		root.AddChild(_title);

		var hint = new Label { Text = "Click anywhere to close" };
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.Modulate = new Color(0.55f, 0.55f, 0.6f, 1f);
		root.AddChild(hint);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddChild(columns);

		columns.AddChild(BuildColumn(out _deckHeader, out _deckList));
		columns.AddChild(BuildColumn(out _sideHeader, out _sideList));

		Hide();
	}

	private static VBoxContainer BuildColumn(out Label header, out VBoxContainer list)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		header = new Label();
		header.AddThemeFontSizeOverride("font_size", 18);
		header.Modulate = MtgUiStyles.GoldBorder;
		col.AddChild(header);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		col.AddChild(scroll);

		list = new VBoxContainer();
		list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(list);

		return col;
	}

	public void ShowPool(string drafterName, IReadOnlyList<Card> pool)
	{
		// Mirrors Draft.BuildDeck's split so this shows the deck that was really played, not a
		// second guess at it. Owner stamping is irrelevant here — nothing is entering a game.
		var spells = pool.Where(c => !c.HasSubtype("Land")).ToList();
		var maindeck = spells.Take(Draft.DefaultMaxSpells).ToList();
		var sideboard = spells.Skip(Draft.DefaultMaxSpells).ToList();
		var lands = 40 - maindeck.Count;

		_title.Text = $"{drafterName} — drafted deck";
		_deckHeader.Text = $"Deck ({maindeck.Count} spells + {lands} Plains)";
		_sideHeader.Text = $"Sideboard ({sideboard.Count})";

		CardListView.Fill(_deckList, maindeck);
		CardListView.Fill(_sideList, sideboard, dim: true);

		Show();
	}
}
