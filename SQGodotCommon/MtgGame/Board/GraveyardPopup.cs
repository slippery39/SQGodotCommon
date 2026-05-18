using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Full-screen overlay that displays the player's graveyard as a scrollable row of mini-cards.
/// Flashback-eligible cards glow purple. Clicking one emits CardClicked so the caller can
/// initiate the flashback cast flow. Opened on demand; closed via the Close button or when
/// the caller calls Hide().
/// </summary>
public partial class GraveyardPopup : CanvasLayer
{
	private ScrollContainer _scroll = null!;
	private BattlefieldZone _zone = null!;

	public event Action<int>? CardClicked;

	public override void _Ready()
	{
		Layer = 6;

		var bg = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(600, 280);
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		center.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		// Header row: title + close button
		var header = new HBoxContainer();
		vbox.AddChild(header);

		var title = new Label { Text = "Graveyard" };
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);

		var closeBtn = new Button { Text = "✕" };
		closeBtn.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		closeBtn.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		closeBtn.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		closeBtn.Pressed += Hide;
		header.AddChild(closeBtn);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		// Scrollable card area
		_scroll = new ScrollContainer();
		_scroll.CustomMinimumSize = new Vector2(0, 210);
		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		_scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(_scroll);

		_zone = new BattlefieldZone();
		_zone.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_scroll.AddChild(_zone);

		_zone.CardClicked += id =>
		{
			Hide();
			CardClicked?.Invoke(id);
		};

		Hide();
	}

	public void ShowGraveyard(
		IEnumerable<Card> cards,
		GameState state,
		IEnumerable<int> flashbackIds
	)
	{
		_zone.Refresh(cards, state, flashbackHighlightIds: flashbackIds);
		Show();
	}
}
