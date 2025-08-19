using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Cards;

/// <summary>
/// Needs to inherit from Node2D because we are using GetGlobalMousePosition in our script.
/// </summary>
public partial class CardUIManager : Node2D
{
	private static CardUIManager _instance;
	public static CardUIManager Instance
	{
		get
		{
			if (_instance == null)
			{
				throw new InvalidOperationException(
					"Trying to access CardUIManager before it has been initialized. Please make sure it is setup in the GameManager and restart the game."
				);
			}
			return _instance;
		}
		set { _instance = value; }
	}

	/// <summary>
	/// All cards that are being moused over but not necessarily hovered, for example if there are multiple bunched up cards, we will only actually want to hover one of them.
	/// </summary>
	public static List<CardUI2D> MouseOveredCards { get; set; } = new List<CardUI2D>();

	/// <summary>
	/// The singular card that is being hovered and therefore being enlarged to view
	/// </summary>
	public static CardUI2D CurrentHoveredCard { get; set; } = null;

	private static CardUI2D _draggingCard = null;

	/// <summary>
	/// A card that we are currently dragging.
	/// </summary>
	public static CardUI2D DraggingCard
	{
		get { return _draggingCard; }
		set
		{
			_draggingCard = value;
			CurrentHoveredCard?.StopHover();
			CurrentHoveredCard = null;
		}
	}

	public override void _Ready()
	{
		if (Instance != null)
		{
			GD.PrintErr("There is already an instance of UI Manager");
			return;
		}

		Instance = this;
		GD.Print("UI Manager is ready");
	}

	public override void _Process(double delta)
	{
		if (DraggingCard != null)
		{
			return;
		}

		//remove any invalid instances from the mouseovered cards
		MouseOveredCards = MouseOveredCards
			.Where(x => IsInstanceValid(x))
			//since this runs every frame automatically, we need this to prevent trying to access objects that were marked as disposed.
			.Where(card => !card.IsQueuedForDeletion())
			.ToList();

		//We might be hovering over multiple cards, in which we want to hover the one which the mouse is closest to its position.
		var cardToHover = MouseOveredCards
			.Where(card => card != DraggingCard)
			.MinBy(card =>
			{
				return GetGlobalMousePosition().DistanceTo(card.GlobalPosition);
			});

		if (cardToHover == null)
		{
			CurrentHoveredCard?.StopHover();
			CurrentHoveredCard = null;
			return;
		}

		if (CurrentHoveredCard != cardToHover)
		{
			CurrentHoveredCard?.StopHover();

			CurrentHoveredCard = cardToHover;
			cardToHover.StartHover();
		}
	}

	public void AddMouseOvered(CardUI2D card)
	{
		if (!MouseOveredCards.Contains(card))
		{
			MouseOveredCards.Add(card);
		}
	}

	public void RemoveMouseOvered(CardUI2D card)
	{
		MouseOveredCards.Remove(card);
	}
}
