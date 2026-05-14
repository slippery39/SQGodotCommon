using System.Collections.Generic;
using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class EventLogPanel : CanvasLayer
{
	private VBoxContainer _logContainer = null!;
	private ScrollContainer _scroll = null!;
	private readonly Dictionary<int, string> _cardNames = new();
	private int _humanPlayerId;
	private const int MaxEntries = 100;

	public override void _Ready()
	{
		Layer = 1;

		var panel = new PanelContainer();
		panel.AnchorLeft = 0.78f;
		panel.AnchorRight = 1.0f;
		panel.AnchorTop = 0.0f;
		panel.AnchorBottom = 1.0f;
		AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(vbox);

		var title = new Label { Text = "Event Log" };
		title.AddThemeFontSizeOverride("font_size", 14);
		vbox.AddChild(title);

		vbox.AddChild(new HSeparator());

		_scroll = new ScrollContainer();
		_scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_scroll.FollowFocus = false;
		vbox.AddChild(_scroll);

		_logContainer = new VBoxContainer();
		_logContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_logContainer.AddThemeConstantOverride("separation", 2);
		_scroll.AddChild(_logContainer);
	}

	public void AppendEvents(ImmutableList<GameEvent> events, GameState state, int humanPlayerId)
	{
		_humanPlayerId = humanPlayerId;
		UpdateCardNameCache(state);

		foreach (var e in events)
		{
			var line = FormatEvent(e);
			if (line == null)
				continue;
			AddLogLabel(line, isTurnSeparator: e is TurnStartedEvent);
		}

		int excess = _logContainer.GetChildCount() - MaxEntries;
		for (int i = 0; i < excess; i++)
		{
			var child = _logContainer.GetChild(0);
			_logContainer.RemoveChild(child);
			child.Free();
		}

		CallDeferred(nameof(ScrollToBottom));
	}

	private void ScrollToBottom() => _scroll.ScrollVertical = int.MaxValue;

	private void AddLogLabel(string text, bool isTurnSeparator = false)
	{
		var label = new Label { Text = text };
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeFontSizeOverride("font_size", 11);
		if (isTurnSeparator)
			label.Modulate = new Color(0.75f, 0.85f, 1f, 1f);
		_logContainer.AddChild(label);
	}

	private void UpdateCardNameCache(GameState state)
	{
		var zoneKeys = new[]
		{
			MtgObjectKeys.Player1Hand,
			MtgObjectKeys.Player1Library,
			MtgObjectKeys.Player1Battlefield,
			MtgObjectKeys.Player1Graveyard,
			MtgObjectKeys.Player2Hand,
			MtgObjectKeys.Player2Library,
			MtgObjectKeys.Player2Battlefield,
			MtgObjectKeys.Player2Graveyard,
		};
		foreach (var key in zoneKeys)
		{
			var zoneId = state.GetWellKnownId(key);
			foreach (var card in state.GetCardsInZone(zoneId))
				_cardNames[card.Id] = card.Name;
		}
		var stackId = state.GetStackId();
		foreach (var card in state.GetCardsInZone(stackId))
			_cardNames[card.Id] = card.Name;
	}

	private string P(int playerId) => playerId == _humanPlayerId ? "You" : "Opponent";

	private string C(int cardId) =>
		_cardNames.TryGetValue(cardId, out var name) ? name : $"[#{cardId}]";

	private static string Bonus(int v) => v >= 0 ? $"+{v}" : $"{v}";

	private string FormatEvent(GameEvent e) =>
		e switch
		{
			TurnStartedEvent ts => $"--- {P(ts.PlayerId)}'s turn ---",
			TurnEndedEvent => null,
			CardDrawnEvent draw => $"{P(draw.PlayerId)} drew {C(draw.CardId)}",
			SpellCastEvent cast => $"{P(cast.CastingPlayerId)} cast {C(cast.CardId)}",
			CreaturePlayedEvent played => $"{P(played.PlayerId)} played {C(played.CardId)}",
			PermanentPlayedEvent played => $"{P(played.PlayerId)} played {C(played.CardId)}",
			SpellResolvedEvent resolved => $"{C(resolved.CardId)} resolved",
			CreatureEnteredBattlefieldEvent entered =>
				$"{C(entered.CardId)} entered the battlefield",
			CreatureAttackedEvent attacked => $"{C(attacked.CreatureId)} attacked",
			CombatDamageDealtToPlayerEvent combatDmg =>
				$"{C(combatDmg.AttackerId)} dealt {combatDmg.Amount} combat damage to {P(combatDmg.DefendingPlayerId)}",
			PlayerDamagedEvent dmg => $"{P(dmg.PlayerId)} took {dmg.Amount} damage",
			PlayerGainedLifeEvent gained => $"{P(gained.PlayerId)} gained {gained.Amount} life",
			PlayerLostLifeEvent lost => $"{P(lost.PlayerId)} lost {lost.Amount} life",
			CreatureDamagedEvent cdmg => $"{C(cdmg.CreatureId)} took {cdmg.Amount} damage",
			CreatureDestroyedEvent destroyed => $"{C(destroyed.CreatureId)} was destroyed",
			CreatureModifiedEvent mod =>
				$"{C(mod.CreatureId)} got {Bonus(mod.PowerBonus)}/{Bonus(mod.ToughnessBonus)}",
			CardDiscardedEvent disc => $"{P(disc.PlayerId)} discarded {C(disc.CardId)}",
			CardRevealedEvent rev =>
				$"{P(rev.PlayerId)} revealed {C(rev.CardId)} (cost {rev.ManaCost})",
			CardExiledEvent exiled => $"{C(exiled.CardId)} was exiled",
			LibraryEmptyEvent empty => $"{P(empty.PlayerId)}'s library is empty",
			PlayerLostEvent lost => $"{P(lost.PlayerId)} lost ({lost.Reason})",
			GameOverEvent { WinnerPlayerId: -1 } => "=== Draw! ===",
			GameOverEvent over => $"=== {P(over.WinnerPlayerId)} wins! ===",
			PermanentLeftBattlefieldEvent => null,
			_ => null,
		};
}
