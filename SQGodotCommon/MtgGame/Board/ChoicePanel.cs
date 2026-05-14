using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgGame;

public partial class ChoicePanel : CanvasLayer
{
	private Label _promptLabel = null!;
	private Label _hintLabel = null!;
	private VBoxContainer _optionContainer = null!;
	private Button _confirmButton = null!;

	private int _minChoices;
	private int _maxChoices;
	private readonly HashSet<int> _selected = new();
	private readonly List<(int Id, Button Btn)> _optionButtons = new();

	public event Action<ImmutableList<int>>? Confirmed;

	public override void _Ready()
	{
		Layer = 5;

		var bg = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(440, 0);
		center.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 24);
		margin.AddThemeConstantOverride("margin_right", 24);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		_promptLabel = new Label();
		_promptLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_promptLabel.AddThemeFontSizeOverride("font_size", 18);
		vbox.AddChild(_promptLabel);

		_hintLabel = new Label();
		_hintLabel.AddThemeFontSizeOverride("font_size", 13);
		_hintLabel.Modulate = new Color(0.75f, 0.75f, 0.75f, 1f);
		vbox.AddChild(_hintLabel);

		var separator = new HSeparator();
		vbox.AddChild(separator);

		_optionContainer = new VBoxContainer();
		_optionContainer.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(_optionContainer);

		var sep2 = new HSeparator();
		vbox.AddChild(sep2);

		_confirmButton = new Button { Text = "Confirm" };
		_confirmButton.Disabled = true;
		_confirmButton.CustomMinimumSize = new Vector2(0, 40);
		_confirmButton.Pressed += OnConfirmPressed;
		vbox.AddChild(_confirmButton);

		Hide();
	}

	public void ShowChoice(
		string prompt,
		ImmutableList<ChoiceOption> options,
		int minChoices,
		int maxChoices
	)
	{
		_promptLabel.Text = prompt.Length > 0 ? prompt : "Choose:";
		_minChoices = minChoices;
		_maxChoices = maxChoices;
		_selected.Clear();
		_optionButtons.Clear();

		foreach (var child in _optionContainer.GetChildren())
			child.QueueFree();

		foreach (var option in options)
		{
			var btn = new Button { Text = option.DisplayText };
			btn.Alignment = HorizontalAlignment.Left;
			btn.CustomMinimumSize = new Vector2(0, 36);
			var optId = option.Id;
			btn.Pressed += () => OnOptionPressed(optId, btn);
			_optionContainer.AddChild(btn);
			_optionButtons.Add((optId, btn));
		}

		UpdateHintAndConfirm();
		Show();
	}

	private void OnOptionPressed(int optionId, Button btn)
	{
		if (_selected.Contains(optionId))
		{
			_selected.Remove(optionId);
			btn.Modulate = Colors.White;
		}
		else if (_selected.Count < _maxChoices)
		{
			_selected.Add(optionId);
			btn.Modulate = new Color(0.4f, 1f, 0.4f, 1f);
		}
		UpdateHintAndConfirm();
	}

	private void UpdateHintAndConfirm()
	{
		var range = _minChoices == _maxChoices ? $"{_minChoices}" : $"{_minChoices}–{_maxChoices}";
		_hintLabel.Text = $"Select {range}  ({_selected.Count} selected)";
		_confirmButton.Disabled = _selected.Count < _minChoices || _selected.Count > _maxChoices;
	}

	private void OnConfirmPressed()
	{
		var selection = _selected.ToImmutableList();
		Hide();
		_selected.Clear();
		Confirmed?.Invoke(selection);
	}
}
