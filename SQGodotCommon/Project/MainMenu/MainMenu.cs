using System;
using System.Collections.Generic;
using MtgGame;

namespace Project;

/// <summary>
/// The title screen, in the MTG kit (MtgVisualDesign.md): "Arcane Duel" over the backdrop, one
/// plate per entry. Mouse, touch and keyboard (up / down / enter) all drive the same selection.
/// </summary>
public partial class MainMenu : Control
{
	private static readonly (string Label, Action Go)[] Entries =
	{
		(
			"Start Game",
			() => GameManager.Instance.ChangeScene("res://MtgGame/DeckSelect/DeckSelectScene.tscn")
		),
		("Draft", () => GameManager.Instance.ChangeScene("res://MtgGame/Draft/DraftScene.tscn")),
		("Quit", () => GameManager.Instance.GetTree().Quit()),
	};

	private readonly List<Button> _buttons = new();
	private int _current;

	public override void _Ready()
	{
		// On THIS node: the screen's root is still adding children during this _Ready, and Godot
		// refuses (with only a logged error) to add to a busy parent. A CanvasLayer draws at its
		// own layer wherever it sits in the tree.
		MtgUiStyles.AddBackdrop(this);
		GetNode<PanelContainer>("../..").AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

		var title = GetNode<Label>("../Title");
		title.Text = "Arcane Duel";
		title.LabelSettings = MtgUiStyles.Title("", 96).LabelSettings;

		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 16);
		AddChild(list);

		for (var i = 0; i < Entries.Length; i++)
		{
			var index = i;
			var button = new Button
			{
				Text = Entries[i].Label,
				CustomMinimumSize = new Vector2(420, 72),
			};
			button.AddThemeFontOverride("font", MtgUiStyles.TitleFont);
			button.AddThemeFontSizeOverride("font_size", 30);
			button.MouseEntered += () => Select(index);
			button.Pressed += () => Entries[index].Go();
			_buttons.Add(button);
			list.AddChild(button);
		}
		Select(0);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true } key)
			return;
		if (key.Keycode == Key.Down)
			Select((_current + 1) % Entries.Length);
		else if (key.Keycode == Key.Up)
			Select((_current + Entries.Length - 1) % Entries.Length);
		else if (key.Keycode == Key.Enter)
			Entries[_current].Go();
	}

	private void Select(int index)
	{
		_current = index;
		for (var i = 0; i < _buttons.Count; i++)
			MtgUiStyles.StyleChoice(_buttons[i], selected: i == index);
	}
}
