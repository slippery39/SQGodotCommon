using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;
using MtgGame;

namespace MagicTable;

/// <summary>
/// Every pending choice, generically: <see cref="Describe.Question"/>, one toggle per option, and
/// Confirm once the count is within Min..Max. A choice of exactly one answers on the click. An
/// option whose id is a card shows its art and previews on hover.
/// </summary>
public partial class ChoicePanel : PanelContainer
{
	public ChoicePanel()
	{
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		Visible = false;
	}

	public void Show(
		GameState s,
		ChoiceAction choice,
		Action<string?> preview,
		Action<IEnumerable<int>> answer
	)
	{
		CardViews.Clear(this);
		Visible = true;

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 6);
		AddChild(column);
		var range = BoardChoice.Range(choice);
		var question = new Label
		{
			Text = $"{Describe.Question(s, choice)}  ({range})",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 0),
		};
		question.AddThemeFontSizeOverride("font_size", 27);
		question.AddThemeColorOverride("font_color", new Color("f4e29b"));
		column.AddChild(question);

		// Several picks, or none allowed: toggles + Confirm. Exactly one: the click answers.
		var multi = BoardChoice.IsMulti(choice);
		var picked = new List<int>();
		var confirm = new Button { Text = "Confirm", Disabled = choice.MinChoices > 0 };
		MtgUiStyles.StylePrimary(confirm, 28);
		confirm.Pressed += () => answer(picked);

		var list = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("h_separation", 6);
		list.AddThemeConstantOverride("v_separation", 6);
		if (choice.Options.Count > 24)
		{
			// Kai Winn's "choose a nonland card name": hundreds — a search box over a scrolling list.
			var search = new LineEdit { PlaceholderText = "Type to search…" };
			search.AddThemeFontSizeOverride("font_size", 22);
			search.TextChanged += text =>
			{
				foreach (var b in list.GetChildren().OfType<Button>())
					b.Visible = b.Text.Contains(text, StringComparison.OrdinalIgnoreCase);
			};
			column.AddChild(search);
			var scroll = new ScrollContainer
			{
				CustomMinimumSize = new Vector2(0, 420),
				HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			};
			scroll.AddChild(list);
			column.AddChild(scroll);
		}
		else
			column.AddChild(list);
		var cardIds = MagicRules.OptionsAreObjects(choice);
		foreach (var option in choice.Options)
		{
			var card =
				cardIds && s.HasObject(option.Id) && s.GetObject(option.Id) is Card c ? c : null;
			var button = new Button
			{
				Text = option.DisplayText,
				ToggleMode = multi,
				Disabled = !option.IsEnabled,
				CustomMinimumSize = new Vector2(280, 60),
				FocusMode = FocusModeEnum.None,
				// Wrapped at the button's width: a long option once grew off the panel.
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			button.AddThemeFontSizeOverride("font_size", 20);
			MtgUiStyles.StyleChoice(button, selected: false);
			if (card is not null)
			{
				var image = MagicArt.ImageName(s, card);
				button.Icon = MagicArt.Crop(image);
				button.ExpandIcon = true;
				button.CustomMinimumSize = new Vector2(270, 90);
				button.MouseEntered += () => preview(image);
				button.MouseExited += () => preview(null);
			}
			var id = option.Id;
			if (!button.ToggleMode)
				button.Pressed += () => answer([id]);
			else
				button.Toggled += on =>
				{
					if (on)
						picked.Add(id);
					else
						picked.Remove(id);
					MtgUiStyles.StyleChoice(button, selected: on);
					confirm.Disabled =
						picked.Count < choice.MinChoices || picked.Count > choice.MaxChoices;
				};
			list.AddChild(button);
		}
		if (multi)
			column.AddChild(confirm);
	}

	public void Close()
	{
		Visible = false;
		CardViews.Clear(this);
	}
}
