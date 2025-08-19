using System;
using Common.Core;

namespace Project;

public partial class GameUI : Node
{
	private bool isGameOver = false;
	private bool allowInput = false;

	[Export]
	public Control GameOverUI { get; set; }

	public override void _Ready()
	{
		GameOverUI.Visible = false;
	}

	public void ShowGameOver()
	{
		isGameOver = true;
		this.GetGameScene().World.Disable();
		GameOverUI.FadeIn(2);
		Observable
			.Timer(TimeSpan.FromSeconds(2))
			.Subscribe(t =>
			{
				allowInput = true;
			})
			.AddTo(this);
	}

	public override void _Process(double delta)
	{
		if (isGameOver && allowInput && Input.IsAnythingPressed())
		{
			GameManager.Instance.GoToMainMenu();
		}
	}
}
