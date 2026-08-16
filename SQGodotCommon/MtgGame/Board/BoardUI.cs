using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public partial class BoardUI : Control
{
	public event Action? EndTurnPressed;
	public event Action<int>? PlayerCreatureClicked;
	public event Action<int>? PlayerCreatureRightClicked;
	public event Action<int>? OpponentCreatureClicked;
	public event Action? OpponentDirectAttacked;
	public event Action? GraveyardButtonPressed;
	public event Action? LogTogglePressed;
	public event Action<int>? CreatureHovered;
	public event Action<int>? CreatureHoverEnded;

	/// Fraction of the width the board keeps while the event log is open.
	private const float BoardWidthWithLog = 0.78f;

	/// Width of the left rail holding the player panels and the graveyard button. Taking these
	/// out of the vertical stack is what lets the battlefield rows be tall enough to read.
	private const float RailWidth = 300f;

	private VBoxContainer _mainColumn = null!;
	private Label _turnLabel = null!;
	private PlayerPanel _opponentPanel = null!;
	private PlayerPanel _playerPanel = null!;
	private BattlefieldZone _opponentBattlefield = null!;
	private BattlefieldZone _playerBattlefield = null!;
	private Button _graveyardButton = null!;
	private Button _endTurnButton = null!;
	private Button _logToggleButton = null!;

	public override void _Ready()
	{
		// Fill the full viewport — anchors don't resolve when parented under a Node2D
		Size = GetViewportRect().Size;
		Position = Vector2.Zero;
		// Pass mouse events through so hand cards (Node2D below) remain draggable
		MouseFilter = Control.MouseFilterEnum.Ignore;

		_mainColumn = GetNode<VBoxContainer>("MainColumn");
		_turnLabel = GetNode<Label>("MainColumn/TopBar/TurnLabel");
		_logToggleButton = GetNode<Button>("MainColumn/TopBar/LogToggleButton");
		_opponentPanel = GetNode<PlayerPanel>("MainColumn/OpponentRow/OpponentPanel");
		_opponentBattlefield = GetNode<BattlefieldZone>(
			"MainColumn/OpponentRow/OpponentBattlefield"
		);
		_playerPanel = GetNode<PlayerPanel>("MainColumn/PlayerRow/PlayerSidebar/PlayerPanel");
		_graveyardButton = GetNode<Button>("MainColumn/PlayerRow/PlayerSidebar/GraveyardButton");
		_playerBattlefield = GetNode<BattlefieldZone>("MainColumn/PlayerRow/PlayerBattlefield");
		_endTurnButton = GetNode<Button>("MainColumn/BottomBar/EndTurnButton");

		// Each panel sits beside its own battlefield row rather than above it, so it reads as
		// belonging to that half of the board and — more importantly — stops being a height
		// driver. See SetBoardWidth for the horizontal half of this.
		_opponentPanel.CustomMinimumSize = new Vector2(RailWidth, 0);
		_playerPanel.CustomMinimumSize = new Vector2(RailWidth, 0);

		_endTurnButton.Pressed += () => EndTurnPressed?.Invoke();
		_logToggleButton.Pressed += () => LogTogglePressed?.Invoke();

		_turnLabel.AddThemeFontSizeOverride("font_size", 25);
		_turnLabel.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		_turnLabel.HorizontalAlignment = HorizontalAlignment.Center;

		_endTurnButton.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_endTurnButton.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_endTurnButton.AddThemeStyleboxOverride("disabled", MtgUiStyles.ButtonDisabled());
		_endTurnButton.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		_endTurnButton.AddThemeColorOverride("font_disabled_color", MtgUiStyles.DimBorder);

		_playerBattlefield.CardClicked += id => PlayerCreatureClicked?.Invoke(id);
		_playerBattlefield.CardRightClicked += id => PlayerCreatureRightClicked?.Invoke(id);
		_playerBattlefield.CardHovered += id => CreatureHovered?.Invoke(id);
		_playerBattlefield.CardHoverEnded += id => CreatureHoverEnded?.Invoke(id);
		_opponentBattlefield.CardClicked += id => OpponentCreatureClicked?.Invoke(id);
		_opponentBattlefield.CardHovered += id => CreatureHovered?.Invoke(id);
		_opponentBattlefield.CardHoverEnded += id => CreatureHoverEnded?.Invoke(id);
		_opponentPanel.Clicked += () => OpponentDirectAttacked?.Invoke();

		_graveyardButton.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_graveyardButton.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_graveyardButton.AddThemeStyleboxOverride("disabled", MtgUiStyles.ButtonDisabled());
		_graveyardButton.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		_graveyardButton.AddThemeColorOverride("font_disabled_color", MtgUiStyles.DimBorder);
		_graveyardButton.Pressed += () => GraveyardButtonPressed?.Invoke();

		_logToggleButton.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_logToggleButton.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_logToggleButton.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
	}

	/// <summary>
	/// Gives the board the full width when the event log is closed and 78% when it is open. The
	/// log is an overlay on its own CanvasLayer, so nothing reclaims that space automatically.
	/// </summary>
	public void SetBoardWidth(bool logOpen)
	{
		_mainColumn.AnchorRight = logOpen ? BoardWidthWithLog : 1.0f;
		_mainColumn.OffsetRight = 0;
	}

	public void SetLogToggleText(string text) => _logToggleButton.Text = text;

	/// <summary>
	/// Where a dragged hand card has to be dropped to be played. Read from the live rect rather
	/// than hardcoded, so the drop target cannot drift out of step with the layout.
	/// </summary>
	public Rect2 GetPlayerBattlefieldRect() =>
		new(_playerBattlefield.GlobalPosition, _playerBattlefield.Size);

	public Vector2 GetPlayerLibraryPosition() =>
		_playerPanel.GlobalPosition
		+ new Vector2(_playerPanel.Size.X - 40f, _playerPanel.Size.Y * 0.5f);

	public void FlashLoss(int losingPlayerId, int humanPlayerId)
	{
		var panel = losingPlayerId == humanPlayerId ? _playerPanel : _opponentPanel;
		var tween = panel.CreateTween();
		tween.TweenProperty(panel, "modulate", new Color(1f, 0.15f, 0.15f, 1f), 0.15f);
		tween.TweenProperty(panel, "modulate", Colors.White, 0.35f);
	}

	public void RefreshAll(
		GameState state,
		int humanPlayerId,
		int aiPlayerId,
		int? selectedAttackerId = null,
		IEnumerable<int> targetHighlightIds = null,
		IEnumerable<int> additionalCostHighlightIds = null
	)
	{
		var game = state.GetGame();
		var human = state.GetPlayer(humanPlayerId);
		var ai = state.GetPlayer(aiPlayerId);

		var humanLibraryId = state.GetWellKnownId(MtgObjectKeys.Player1Library);
		var aiLibraryId = state.GetWellKnownId(MtgObjectKeys.Player2Library);
		var humanBattlefieldId = state.GetWellKnownId(MtgObjectKeys.Player1Battlefield);
		var aiBattlefieldId = state.GetWellKnownId(MtgObjectKeys.Player2Battlefield);

		_playerPanel.Refresh("You", human, state.GetCardsInZone(humanLibraryId).Count());
		_opponentPanel.Refresh("Opponent", ai, state.GetCardsInZone(aiLibraryId).Count());
		_playerBattlefield.Refresh(
			state.GetCardsInZone(humanBattlefieldId),
			state,
			selectedAttackerId,
			targetHighlightIds,
			additionalCostHighlightIds
		);
		_opponentBattlefield.Refresh(
			state.GetCardsInZone(aiBattlefieldId),
			state,
			targetHighlightIds: targetHighlightIds
		);

		var humanGraveyardId = state.GetWellKnownId(MtgObjectKeys.Player1Graveyard);
		var graveyardCount = state.GetCardsInZone(humanGraveyardId).Count();
		_graveyardButton.Text = $"Graveyard ({graveyardCount})";
		_graveyardButton.Disabled = graveyardCount == 0;

		var isHumanTurn = game.ActivePlayerId == humanPlayerId;
		_playerPanel.SetActive(isHumanTurn);
		_opponentPanel.SetActive(!isHumanTurn);

		// Highlight player panels that are valid spell/ability targets (overrides active state)
		if (targetHighlightIds != null)
		{
			if (targetHighlightIds.Contains(humanPlayerId))
				_playerPanel.Modulate = new Color(1f, 1f, 0.3f, 1f);
			if (targetHighlightIds.Contains(aiPlayerId))
				_opponentPanel.Modulate = new Color(1f, 1f, 0.3f, 1f);
		}

		_turnLabel.Text =
			$"Turn {game.TurnNumber} — {(isHumanTurn ? "Your turn" : "Opponent's turn")}";
		_endTurnButton.Disabled = !isHumanTurn;
	}
}
