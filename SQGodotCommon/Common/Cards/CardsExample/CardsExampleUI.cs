using System;
using Common.Cards;
using Project;

namespace Cards;

public partial class CardsExampleUI : GameUI
{
	private Hand2D Hand { get; set; }

	private CardsExampleScene CardsExampleScene => this.GetGameScene() as CardsExampleScene;

	public override void _Ready()
	{
		Hand =
			GetNodeOrNull<Hand2D>("Hand")
			?? throw new InvalidOperationException("Could not find node Hand");
	}
}
