using System;
using System.Collections.Generic;
using ImmutableGameObjects;
using MagicCore.Cards;
using MtgGame;

namespace MagicTable;

/// <summary>
/// A graveyard, exile or command zone laid out as full cards — opened from a player plate, redrawn
/// with the table while open. Hover previews; a click goes to the table's router (a card castable
/// from there is played the usual way).
/// </summary>
public partial class ZoneViewer : PanelContainer
{
	public ZoneViewer()
	{
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		CustomMinimumSize = new Vector2(1000, 0);
		Visible = false;
	}

	public void Show(
		GameState s,
		string title,
		IEnumerable<Card> cards,
		Func<Card, Glow> glow,
		Action<string?> preview,
		Action<int> onClick,
		Action close
	)
	{
		CardViews.Clear(this);
		Visible = true;
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		AddChild(column);

		var header = new HBoxContainer();
		var label = MtgUiStyles.Title(title, 30);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(label);
		var closeButton = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(closeButton, 24);
		closeButton.Pressed += close;
		header.AddChild(closeButton);
		column.AddChild(header);

		var flow = new HFlowContainer();
		flow.AddThemeConstantOverride("h_separation", 6);
		flow.AddThemeConstantOverride("v_separation", 6);
		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, CardViews.HandSize.Y * 2 + 20),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		flow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(flow);
		column.AddChild(scroll);
		foreach (var c in cards)
		{
			var id = c.Id;
			flow.AddChild(CardViews.HandCard(s, c, glow(c), preview, () => onClick(id)));
		}
		if (flow.GetChildCount() == 0)
			flow.AddChild(MtgUiStyles.Title("(empty)", 24));
	}

	public void Close()
	{
		Visible = false;
		CardViews.Clear(this);
	}
}
