using System;
using Godot;
using MtgCore;

namespace MtgGame;

public partial class PlayerPanel : PanelContainer
{
	private Label _nameLabel = null!;
	private Label _lifeLabel = null!;
	private Label _libraryLabel = null!;
	private HBoxContainer _manaContainer = null!;

	public event Action? Clicked;

	public override void _Ready()
	{
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		margin.AddChild(vbox);

		// Top row: player name (left) + library count (right)
		var topRow = new HBoxContainer();
		vbox.AddChild(topRow);

		_nameLabel = new Label();
		_nameLabel.AddThemeFontSizeOverride("font_size", 25);
		_nameLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.70f, 0.55f, 1f));
		_nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		topRow.AddChild(_nameLabel);

		// Two short lines rather than one long one: the rail is 300px and the name shares the row,
		// so "Hand 7  Lib. 33" at the name's size would not fit.
		_libraryLabel = new Label();
		_libraryLabel.AddThemeFontSizeOverride("font_size", 16);
		_libraryLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.70f, 0.55f, 1f));
		_libraryLabel.HorizontalAlignment = HorizontalAlignment.Right;
		topRow.AddChild(_libraryLabel);

		// Life total — dominant visual element
		_lifeLabel = new Label();
		_lifeLabel.AddThemeFontSizeOverride("font_size", 40);
		_lifeLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f, 1f));
		_lifeLabel.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(_lifeLabel);

		// Mana pips row
		_manaContainer = new HBoxContainer();
		_manaContainer.AddThemeConstantOverride("separation", 4);
		vbox.AddChild(_manaContainer);

		GuiInput += inputEvent =>
		{
			if (
				inputEvent is InputEventMouseButton mb
				&& mb.Pressed
				&& mb.ButtonIndex == MouseButton.Left
			)
				Clicked?.Invoke();
		};
	}

	public void Refresh(string playerName, MtgPlayer player, int libraryCount, int handCount)
	{
		_nameLabel.Text = playerName;
		_lifeLabel.Text = $"♥ {player.Life}";
		_libraryLabel.Text = $"Hand {handCount}\nLib. {libraryCount}";

		foreach (var child in _manaContainer.GetChildren())
			child.QueueFree();

		for (var i = 0; i < player.MaxMana; i++)
		{
			var pip = new Panel();
			pip.CustomMinimumSize = new Vector2(14, 14);
			pip.AddThemeStyleboxOverride("panel", MtgUiStyles.ManaPip(i < player.CurrentMana));
			_manaContainer.AddChild(pip);
		}
	}

	public void SetActive(bool isActive)
	{
		Modulate = isActive ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
	}
}
