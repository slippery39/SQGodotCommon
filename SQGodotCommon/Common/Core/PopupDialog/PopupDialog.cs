using System;
using System.Collections.Generic;
using Godot;

namespace Common.Core;

public partial class PopupDialog : Control
{
	private Control ContentContainer { get; set; }

	private Control ButtonsContainer { get; set; }

	/// <summary>
	/// The default button that will be used for our bottom dialog buttons.
	/// </summary>
	[Export]
	public PackedScene ButtonPackedScene { get; set; }

	public override void _Ready()
	{
		ContentContainer = GetNode<Control>("CenterContainer/PanelContainer/VBoxContainer/Content");
		ButtonsContainer = GetNode<Control>("CenterContainer/PanelContainer/VBoxContainer/Buttons");
	}

	/// <summary>
	/// Sets the specified content inside the popup dialog.
	/// </summary>
	/// <param name="content"></param>
	public void SetContent(Control content)
	{
		ContentContainer.ClearChildren();
		ContentContainer.AddChild(content);
	}

	/// <summary>
	/// Sets the content as a label with the specified text
	/// </summary>
	/// <param name="text"></param>
	public void SetContent(string text)
	{
		ContentContainer.ClearChildren();
		var label = new Label();
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.Word;
		label.CustomMinimumSize = new Vector2(300, 300);
		label.LabelSettings = new();
		label.LabelSettings.FontSize = 35;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		ContentContainer.AddChild(label);
	}

	public void CreateButtons(List<ButtonConfig> configs)
	{
		ButtonsContainer.ClearChildren();

		foreach (var config in configs)
		{
			var button = ButtonPackedScene.Instantiate<Button>();
			button.Text = config.Label;

			if (config.CallBack != null)
			{
				button.Pressed += config.CallBack;
			}

			ButtonsContainer.AddChild(button);
		}
	}

	public class ButtonConfig
	{
		public string Label { get; set; }
		public Action CallBack { get; set; }
		public ButtonType Type { get; set; } = ButtonType.None;

		public ButtonConfig() { }

		public ButtonConfig(string label, Action callback)
		{
			Label = label;
			CallBack = callback;
		}

		public static List<ButtonConfig> OK(string okLabel = "OK")
		{
			return new List<ButtonConfig>
			{
				new() { Label = okLabel, Type = ButtonType.Submit },
			};
		}

		public static List<ButtonConfig> OKCancel(
			string okLabel = "OK",
			string cancelLabel = "Cancel"
		)
		{
			return new List<ButtonConfig>
			{
				new() { Label = okLabel, Type = ButtonType.Submit },
				new() { Label = cancelLabel, Type = ButtonType.Cancel },
			};
		}

		/// <summary>
		/// To allow to automatically apply our custom styling in the case of OK/Cancel buttons
		/// </summary>
		public enum ButtonType
		{
			None,
			Submit,
			Cancel,
		}
	}
}
