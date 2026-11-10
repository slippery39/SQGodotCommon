using System;
using MtgGame;

namespace MagicTable;

/// <summary>
/// F6 (story AI-14.4, as MtgGame's): what the AI read at its last searched decision — every first move it
/// considered, best first, with the score of the line it found for it; then those lines, each a game the
/// search imagined, with its evaluator terms. Look before arguing a scoring change (root CLAUDE.md).
/// </summary>
public partial class AiInspector : PanelContainer
{
	private const int TextSize = 20;

	public AiInspector()
	{
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		CustomMinimumSize = new Vector2(1240, 900);
		Visible = false;
	}

	public void Show(MagicTableManager.Inspection? last, Func<string> lines, Action close)
	{
		CardViews.Clear(this);
		Visible = true;
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		AddChild(column);

		var header = new HBoxContainer();
		var title = MtgUiStyles.Title("AI inspector  (F6)", 30);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(title);
		var shut = new Button { Text = "Close  (Esc)", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(shut, 24);
		shut.Pressed += close;
		header.AddChild(shut);
		column.AddChild(header);

		if (last is null)
		{
			column.AddChild(
				SealedScene.Text(
					"No searched decision yet — the AI searches when it has two or more moves (not with --greedy).",
					TextSize
				)
			);
			return;
		}
		column.AddChild(
			SealedScene.Text($"Turn {last.Turn} · {last.Who} chose: {last.Chose}", TextSize + 2)
		);
		var ranked = new VBoxContainer();
		foreach (var (move, score) in last.Ranked)
			ranked.AddChild(SealedScene.Text($"{score, 9:0.00}    {move}", TextSize));
		var top = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 260),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		top.AddChild(ranked);
		column.AddChild(top);

		column.AddChild(SealedScene.Text("Each move's line, as the search imagined it:", TextSize));
		var text = new RichTextLabel
		{
			Text = lines(),
			ScrollActive = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SelectionEnabled = true,
		};
		text.AddThemeFontSizeOverride("normal_font_size", TextSize - 2);
		column.AddChild(text);
	}
}
