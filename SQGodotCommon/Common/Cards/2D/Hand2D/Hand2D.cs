using System;
using System.Collections.Generic;
using System.Linq;
using Common.Core;

namespace Common.Cards;

[Tool]
public partial class Hand2D : Node2D
{
	[Export]
	public Curve CardXPositionCurve { get; set; }

	[Export]
	public Curve CardYPositionCurve { get; set; }

	[Export]
	public Curve CardRotationCurve { get; set; }

	private Node2D LeftMostPoint { get; set; }

	private Node2D RightMostPoint { get; set; }

	private Node2D CardsContainer { get; set; }

	[Export]
	public float YMagnitude { get; set; } = 50;

	[Export]
	public float MaxRotationDegrees { get; set; } = 15;

	[Export]
	public PackedScene CardScene { get; set; }

	// Track active card tweens to update them when new cards are added
	private Dictionary<Node2D, Tween> activeCardTweens = new Dictionary<Node2D, Tween>();

	// Drag handling actions - can be overridden by external clients
	public Func<DragEndContext, bool> IsDragSuccess;
	public Action<DragEndContext> OnDragSuccess;
	public Action<DragEndContext> OnDragFail;

	public override void _Ready()
	{
		Init();

		// Set up default drag handling
		SetupDefaultDragHandling();

		CardDragEnd = _HandleCardDragEnd;

		if (!Engine.IsEditorHint())
		{
			CardsContainer.ClearChildren();
		}
	}

	private void SetupDefaultDragHandling()
	{
		// Default implementation: always fail (can be overridden by external clients)
		if (IsDragSuccess == null)
		{
			IsDragSuccess = _DefaultIsDragSuccess;
		}

		// Default success: print to console (can be overridden)
		if (OnDragSuccess == null)
		{
			OnDragSuccess = _DefaultOnDragSuccess;
		}

		// Default fail: return card to hand (can be overridden)
		if (OnDragFail == null)
		{
			OnDragFail = _DefaultOnDragFail;
		}
	}

	// Default implementations
	private bool _DefaultIsDragSuccess(DragEndContext context)
	{
		// Default: always return false (drag always fails)
		// External clients can override this with their own logic
		return false;
	}

	private void _DefaultOnDragSuccess(DragEndContext context)
	{
		// Default success behavior: just print to console
		GD.Print($"Card {context.CardUI2D.Id} drag succeeded!");
	}

	private void _DefaultOnDragFail(DragEndContext context)
	{
		// Default fail behavior: return card to its position in hand
		GD.Print($"Card {context.CardUI2D.Id} drag failed, returning to hand");
		LerpCardTransform(context.CardUI2D);
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			var isOk = Init();
			if (isOk)
				SetCardTransforms();
		}
	}

	public class DragEndContext
	{
		public Hand2D Hand2D { get; set; }
		public CardUI2D CardUI2D { get; set; }
		public List<Area2D> SelectedAreas { get; set; }
		public Vector2 DragEndPoint { get; set; }
	}

	public Action<DragEndContext> CardDragEnd;

	private void _HandleCardDragEnd(DragEndContext context)
	{
		// Use the configurable drag success logic
		bool isDragSuccessful = IsDragSuccess(context);

		if (isDragSuccessful)
		{
			OnDragSuccess(context);
		}
		else
		{
			OnDragFail(context);
		}
	}

	private void OnCardDragEnd(CardUI2D.DragEndContext context)
	{
		if (CardDragEnd != null)
		{
			CardDragEnd(
				new DragEndContext
				{
					Hand2D = this,
					CardUI2D = context.CardUI2D,
					SelectedAreas = context.SelectedAreas,
					DragEndPoint = context.DragEndPoint,
				}
			);
		}
	}

	private bool Init()
	{
		LeftMostPoint = this.GetNodeWithGuard<Node2D>("LeftMostPoint");
		RightMostPoint = this.GetNodeWithGuard<Node2D>("RightMostPoint");
		CardsContainer = this.GetNodeWithGuard<Node2D>("CardsContainer");

		bool isOK = true;

		if (LeftMostPoint == null)
		{
			GD.PrintErr("Could not find left most point node");
			isOK = false;
		}

		if (RightMostPoint == null)
		{
			GD.PrintErr("Could not find right most point node");
			isOK = false;
		}

		if (CardsContainer == null)
		{
			GD.PrintErr("Could not find cards container node");
			isOK = false;
		}

		return isOK;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.D)
		{
			GD.Print("Drawing card");
			DrawCard(new Vector2(0, GlobalPosition.Y));
		}
	}

	private float GetNormalizedPositionValue(int index, int totalCards = -1)
	{
		var cardCount = totalCards == -1 ? GetNumberOfCardsInHand() : totalCards;
		if (cardCount == 0)
		{
			return 0;
		}
		if (cardCount == 1)
		{
			return 0.5f;
		}

		//We don't want to position our cards at 0 and 1 when we have only a few cards in hand
		//So the spacing and the minPos and maxPos are designed to gradually increase the boundary
		//of where we will position the cards as more cards are added into the hand.
		var maxCardSpacing = Math.Min(1.0 / cardCount, 0.08);

		var minPos = Math.Max(0, 0.5 - (cardCount - 1) * maxCardSpacing);
		var maxPos = Math.Min(1, 0.5 + (cardCount - 1) * maxCardSpacing);

		var posVal = Mathf.Lerp(minPos, maxPos, (double)index / (cardCount - 1));

		return (float)posVal;
	}

	public int GetNumberOfCardsInHand()
	{
		//The godot editor doesn't like it when we try to access our cardUI2D inside the editor, but if we use node2d its fine
		if (Engine.IsEditorHint())
		{
			return CardsContainer.GetChildren().OfType<Node2D>().Count();
		}
		else
		{
			return GetCards().Count;
		}
	}

	public List<CardUI2D> GetCards()
	{
		return CardsContainer.GetChildren().OfType<CardUI2D>().ToList();
	}

	public void DiscardCard(string id)
	{
		var discardedCard = GetCards().FirstOrDefault(x => x.Id == id);

		// Clean up any active tween for this card
		if (activeCardTweens.ContainsKey(discardedCard))
		{
			if (
				activeCardTweens[discardedCard] != null
				&& activeCardTweens[discardedCard].IsValid()
			)
			{
				activeCardTweens[discardedCard].Kill();
			}
			activeCardTweens.Remove(discardedCard);
		}

		CardsContainer.RemoveChild(discardedCard);
		discardedCard.QueueFree();

		// Update remaining cards to their new positions
		var totalCards = GetNumberOfCardsInHand();
		UpdateAllCardPositions(totalCards);
	}

	public CardUI2D DrawCard()
	{
		return DrawCard(new Vector2(0, GlobalPosition.Y));
	}

	[Export]
	public float CardSizeX { get; set; } = 200;

	public CardUI2D DrawCard(Vector2 from)
	{
		var drawnCard = CreateCardUI2D();
		CardsContainer.AddChild(drawnCard);

		// Set the starting position for the new card
		drawnCard.GlobalPosition = from;
		drawnCard.IsPosLerping = true;

		// Get current card count - this is the final count with the new card
		var totalCards = GetNumberOfCardsInHand();

		// Update ALL cards to their correct final positions
		// This ensures that even cards currently animating will redirect to the right place
		UpdateAllCardPositions(totalCards, drawnCard);

		return drawnCard;
	}

	private void UpdateAllCardPositions(int totalCards, Node2D newCard = null)
	{
		var allCards = CardsContainer.GetChildren().OfType<Node2D>().ToList();

		// Stop any existing tweens and start fresh ones
		foreach (var kvp in activeCardTweens.ToList())
		{
			if (kvp.Value != null && kvp.Value.IsValid())
			{
				kvp.Value.Kill();
			}
		}
		activeCardTweens.Clear();

		// Animate all cards to their correct final positions
		for (int i = 0; i < allCards.Count; i++)
		{
			var card = allCards[i];
			var info = GetCardTransformInfo(i, totalCards);

			// Set Z-index immediately
			if (card is CardUI2D cardUI2D)
			{
				cardUI2D.SavedZIndex = info.ZIndex;
				cardUI2D.IsPosLerping = true;
			}

			// Create individual tween for this card
			var tween = CreateTween();
			tween.BindNode(card);
			tween.SetEase(Tween.EaseType.Out);
			tween.SetTrans(Tween.TransitionType.Cubic);

			// Store the tween so we can manage it
			activeCardTweens[card] = tween;

			// Determine animation duration - new cards take longer, existing cards are quicker
			float duration = (card == newCard) ? 0.5f : 0.3f;

			tween.TweenProperty(card, "position", info.Position, duration);
			tween.Parallel();
			tween.TweenProperty(
				card,
				"global_rotation",
				Mathf.DegToRad(info.GlobalRotationDegrees),
				duration
			);

			// Clean up when done
			tween.TweenCallback(
				Callable.From(() =>
				{
					if (card is CardUI2D cardUI)
						cardUI.IsPosLerping = false;

					// Remove from active tweens
					if (activeCardTweens.ContainsKey(card))
						activeCardTweens.Remove(card);
				})
			);
		}
	}

	public void SetCardTransforms()
	{
		var index = 0;
		var totalCards = GetNumberOfCardsInHand();

		//Temporary hack while we figure out why we cant use CardUI2D with this in Tool mode.
		var cards = CardsContainer.GetChildren().OfType<Node2D>();
		foreach (var card in cards)
		{
			var info = GetCardTransformInfo(index, totalCards);

			card.GlobalRotationDegrees = info.GlobalRotationDegrees;
			card.Position = info.Position;

			//Temporary hack while we figure out why we cant use CardUI2D with this in Tool mode.
			if (card is CardUI2D cardUI2D)
			{
				cardUI2D.SavedZIndex = info.ZIndex;
			}

			index++;
		}
	}

	public void SetCardsDetails(List<InternalCardUI2D.Details> details)
	{
		var cards = GetCards();

		if (details.Count != cards.Count)
		{
			throw new Exception(
				"Something went wrong in SetCardDetails, the cards and the details are not the same size"
			);
		}

		for (var i = 0; i < details.Count; i++)
		{
			details[i].ApplyTo(cards[i]);
		}
	}

	private CardUI2D CreateCardUI2D()
	{
		var card = CardScene.Instantiate<CardUI2D>();
		card.DragEnd = OnCardDragEnd;
		return card;
	}

	/// <summary>
	/// Lerps a single card transform. Used if we want to return a card to our hand, for example if we try to play a card and invalid targets were chosen
	/// We might want to play an animation to return it to our hand.
	/// </summary>
	/// <param name="cardToLerp"></param>
	public void LerpCardTransform(Node2D cardToLerp)
	{
		var ignoreList = CardsContainer
			.GetChildren()
			.OfType<Node2D>()
			.Where(c => c != cardToLerp)
			.ToList();

		LerpCardTransforms(ignoreList);
	}

	private void LerpCardTransforms()
	{
		LerpCardTransforms(new List<Node2D>());
	}

	private void LerpCardTransforms(List<Node2D> ignoreList)
	{
		var totalCards = GetNumberOfCardsInHand();
		var index = 0;

		//Teporary hack while we figure out why we cant use CardUI2D with this in Tool mode.
		var cards = CardsContainer.GetChildren().OfType<Node2D>();
		foreach (var card in cards)
		{
			if (ignoreList.Contains(card))
			{
				index++;
				continue;
			}

			var info = GetCardTransformInfo(index, totalCards);

			//Teporary hack while we figure out why we cant use CardUI2D with this in Tool mode.
			if (card is CardUI2D cardUI2D)
			{
				cardUI2D.SavedZIndex = info.ZIndex;
				cardUI2D.IsPosLerping = true;
			}

			var tween = CreateTween(); //We want to lerp it towards its position and rotation in the hand.
			tween.BindNode(card);
			tween.SetEase(Tween.EaseType.Out);
			tween.SetTrans(Tween.TransitionType.Cubic);

			tween.TweenProperty(
				card,
				"global_rotation",
				Mathf.DegToRad(info.GlobalRotationDegrees),
				0.2f
			);
			tween.Parallel();
			tween.TweenProperty(card, "position", info.Position, 0.2f);
			tween.TweenCallback(
				Callable.From(() =>
				{
					if (card is CardUI2D cardUI)
						cardUI.IsPosLerping = false;
				})
			);

			index++;
		}
	}

	private CardTransformInfo GetCardTransformInfo(int index, int totalCards = -1)
	{
		if (totalCards == -1)
			totalCards = GetNumberOfCardsInHand();

		var info = new CardTransformInfo
		{
			GlobalRotationDegrees =
				CardRotationCurve.Sample(GetNormalizedPositionValue(index, totalCards))
				* MaxRotationDegrees,
		};

		var startingPoint = LeftMostPoint.Position.X;
		var endPoint = RightMostPoint.Position.X;

		var pos = new Vector2
		{
			X =
				CardXPositionCurve.Sample(GetNormalizedPositionValue(index, totalCards))
				+ Mathf.Lerp(
					startingPoint,
					endPoint,
					GetNormalizedPositionValue(index, totalCards)
				),
			Y =
				CardYPositionCurve.Sample(GetNormalizedPositionValue(index, totalCards))
				* -YMagnitude,
		};
		info.Position = pos;

		info.ZIndex = index;

		return info;
	}

	private sealed class CardTransformInfo
	{
		public Vector2 Position { get; set; }
		public float GlobalRotationDegrees { get; set; }
		public int ZIndex { get; set; }
	}
}
