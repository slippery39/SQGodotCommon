using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using MtgCore;

namespace MtgGame;

public partial class MtgGameScene : Node2D
{
	private MtgGameManager _manager = null!;
	private BoardUI _boardUI = null!;
	private Hand2D _hand = null!;
	private Area2D _battlefieldDropZone = null!;

	// Tracks game card IDs currently represented in the UI hand
	private readonly List<int> _handCardIds = new();

	public override void _Ready()
	{
		_manager = new MtgGameManager();
		_boardUI = GetNode<BoardUI>("BoardUI");
		_hand = GetNode<Hand2D>("Hand2D");
		_battlefieldDropZone = GetNode<Area2D>("BattlefieldDropZone");

		_boardUI.EndTurnPressed += OnEndTurnPressed;

		_hand.IsDragSuccess = context => context.SelectedAreas.Contains(_battlefieldDropZone);

		_hand.OnDragSuccess = context =>
		{
			if (!int.TryParse(context.CardUI2D.Id, out var cardId))
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			var (success, _) = _manager.CastCreature(cardId);
			if (!success)
			{
				_hand.LerpCardTransform(context.CardUI2D);
				return;
			}

			Refresh();
		};

		_manager.StartGame();
		Refresh();
	}

	private async void OnEndTurnPressed()
	{
		_manager.EndTurn();
		Refresh();

		while (_manager.IsAiTurn && !_manager.IsWaitingForChoice)
		{
			await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
			_manager.RunAiTurnStep();
			Refresh();
		}
	}

	private void Refresh()
	{
		_boardUI.RefreshAll(_manager.State, _manager.HumanPlayerId, _manager.AiPlayerId);
		SyncHand();
	}

	private void SyncHand()
	{
		var state = _manager.State;
		var humanHandId = state.GetWellKnownId(MtgObjectKeys.Player1Hand);
		var handCards = state.GetCardsInZone(humanHandId).ToList();
		var currentCardIds = handCards.Select(c => c.Id).ToHashSet();

		// Remove cards no longer in hand
		var toRemove = _handCardIds.Where(id => !currentCardIds.Contains(id)).ToList();
		foreach (var id in toRemove)
		{
			_hand.DiscardCard(id.ToString());
			_handCardIds.Remove(id);
		}

		// Add cards newly drawn into hand
		var existingIds = _handCardIds.ToHashSet();
		foreach (var card in handCards.Where(c => !existingIds.Contains(c.Id)))
		{
			var cardUI = _hand.DrawCard();
			cardUI.Id = card.Id.ToString();
			_handCardIds.Add(card.Id);
		}

		// Update visuals for all hand cards (UI order matches _handCardIds insertion order)
		var uiCards = _hand.GetCards();
		if (uiCards.Count == 0)
			return;

		var cardLookup = handCards.ToDictionary(c => c.Id);
		var details = uiCards
			.Select(ui =>
				int.TryParse(ui.Id, out var id) && cardLookup.TryGetValue(id, out var card)
					? MtgCardMapper.ToDetails(card, state, _manager.HumanPlayerId)
					: new InternalCardUI2D.Details()
			)
			.ToList();

		_hand.SetCardsDetails(details);
	}
}
