using System.Collections.Generic;

namespace Project;

public partial class MainMenu : Control
{
	// Exported array to define menu options
	[Export]
	public string[] MenuOptions = { "Start Game", "Options", "Quit" };

	private int _currentOptionIndex = 0; // Tracks the currently selected option
	private List<Label> _menuLabels = new List<Label>(); // Holds references to menu option labels

	// Colors for selected and unselected options
	private Color _selectedColor = new Color(1, 1, 0); // Yellow
	private Color _defaultColor = new Color(1, 1, 1); // White

	public override void _Ready()
	{
		// Create menu option labels
		var vBox = new VBoxContainer();
		AddChild(vBox);

		for (int i = 0; i < MenuOptions.Length; i++)
		{
			var label = new Label
			{
				Text = MenuOptions[i],
				Modulate = i == _currentOptionIndex ? _selectedColor : _defaultColor,
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			label.LabelSettings = new LabelSettings { FontSize = 40 };
			_menuLabels.Add(label);
			vBox.AddChild(label);
		}

		// Center menu
		vBox.AnchorLeft = 0.5f;
		vBox.AnchorRight = 0.5f;
		vBox.AnchorTop = 0.5f;
		vBox.AnchorBottom = 0.5f;
		vBox.Alignment = BoxContainer.AlignmentMode.Center;
	}

	public override void _Input(InputEvent @event)
	{
		// Handle keyboard input
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.Down)
			{
				ChangeOption(1);
			}
			else if (keyEvent.Keycode == Key.Up)
			{
				ChangeOption(-1);
			}
			else if (keyEvent.Keycode == Key.Enter)
			{
				SelectOption();
			}
		}

		if (@event is InputEventMouseMotion)
		{
			UpdateOptionOnHover();
		}

		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				for (int i = 0; i < _menuLabels.Count; i++)
				{
					if (_menuLabels[i].GetGlobalRect().HasPoint(GetGlobalMousePosition()))
					{
						_currentOptionIndex = i;
						UpdateMenuVisuals();
						SelectOption();
						break;
					}
				}
			}
		}
	}

	private void UpdateOptionOnHover()
	{
		for (int i = 0; i < _menuLabels.Count; i++)
		{
			if (_menuLabels[i].GetGlobalRect().HasPoint(GetGlobalMousePosition()))
			{
				if (_currentOptionIndex != i)
				{
					_currentOptionIndex = i;
					UpdateMenuVisuals();
				}
				break;
			}
		}
	}

	private void ChangeOption(int direction)
	{
		_currentOptionIndex += direction;

		// Wrap around the options
		if (_currentOptionIndex < 0)
			_currentOptionIndex = MenuOptions.Length - 1;
		else if (_currentOptionIndex >= MenuOptions.Length)
			_currentOptionIndex = 0;

		UpdateMenuVisuals();
	}

	private void UpdateMenuVisuals()
	{
		for (int i = 0; i < _menuLabels.Count; i++)
		{
			_menuLabels[i].Modulate = i == _currentOptionIndex ? _selectedColor : _defaultColor;
		}
	}

	private void SelectOption()
	{
		GD.Print($"Selected option: {MenuOptions[_currentOptionIndex]}");

		// Perform actions based on selected option
		switch (MenuOptions[_currentOptionIndex])
		{
			case "Start Game":
				QueueFree();
				// Load the game scene
				GameManager.Instance.ChangeScene("res://Common/Cards/cards_example.tscn");
				break;

			case "Options":
				// Load the options scene or handle options logic
				GD.Print("Options selected!");
				break;

			case "Quit":
				// Quit the game
				GetTree().Quit();
				break;
		}
	}
}
