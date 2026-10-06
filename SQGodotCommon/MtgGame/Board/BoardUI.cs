using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

/// <summary>
/// The board's layout (MtgVisualDesign.md §6): a column of [opponent strip] [opponent tokens]
/// [your tokens] [your strip], the hand fanned below it, END TURN and the turn banner bottom-right
/// where a thumb reaches them on a phone. Each strip is a <see cref="PlayerHud"/>.
/// </summary>
public partial class BoardUI : Control
{
	public event Action? EndTurnPressed;
	public event Action<int>? PlayerCreatureClicked;
	public event Action<int>? PlayerCreatureRightClicked;
	public event Action<int>? OpponentCreatureClicked;
	public event Action? OpponentDirectAttacked;
	public event Action? GraveyardButtonPressed;
	public event Action? OpponentGraveyardButtonPressed;
	public event Action? ExilePressed;
	public event Action? OpponentExilePressed;
	public event Action? LogTogglePressed;
	public event Action<int>? CreatureHovered;
	public event Action<int>? CreatureHoverEnded;

	/// Fraction of the width the board keeps while the event log is open.
	private const float BoardWidthWithLog = 0.78f;

	private VBoxContainer _mainColumn = null!;
	private Label _turnBanner = null!;
	private PlayerHud _opponentHud = null!;
	private PlayerHud _playerHud = null!;
	private BattlefieldZone _opponentBattlefield = null!;
	private BattlefieldZone _playerBattlefield = null!;
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
		_opponentHud = GetNode<PlayerHud>("MainColumn/OpponentHud");
		_opponentBattlefield = GetNode<BattlefieldZone>("MainColumn/OpponentBattlefield");
		_playerBattlefield = GetNode<BattlefieldZone>("MainColumn/PlayerBattlefield");
		_playerHud = GetNode<PlayerHud>("MainColumn/PlayerHud");
		_turnBanner = GetNode<Label>("TurnBanner");
		_endTurnButton = GetNode<Button>("EndTurnButton");
		_logToggleButton = GetNode<Button>("LogToggleButton");

		_endTurnButton.Pressed += () => EndTurnPressed?.Invoke();
		_logToggleButton.Pressed += () => LogTogglePressed?.Invoke();
		MtgUiStyles.StylePrimary(_endTurnButton, fontSize: 34);

		// A label over the backdrop needs its own outline (godot-frontend.md), on a parchment plate.
		_turnBanner.AddThemeStyleboxOverride("normal", MtgUiStyles.Banner());
		_turnBanner.LabelSettings = new LabelSettings
		{
			Font = MtgUiStyles.TitleFont,
			FontSize = 22,
			FontColor = MtgUiStyles.Ink,
		};

		_logToggleButton.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_logToggleButton.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_logToggleButton.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);

		_playerBattlefield.CardClicked += id => PlayerCreatureClicked?.Invoke(id);
		_playerBattlefield.CardRightClicked += id => PlayerCreatureRightClicked?.Invoke(id);
		_playerBattlefield.CardHovered += id => CreatureHovered?.Invoke(id);
		_playerBattlefield.CardHoverEnded += id => CreatureHoverEnded?.Invoke(id);
		_opponentBattlefield.CardClicked += id => OpponentCreatureClicked?.Invoke(id);
		_opponentBattlefield.CardHovered += id => CreatureHovered?.Invoke(id);
		_opponentBattlefield.CardHoverEnded += id => CreatureHoverEnded?.Invoke(id);

		_opponentHud.PortraitClicked += () => OpponentDirectAttacked?.Invoke();
		_playerHud.GraveyardClicked += () => GraveyardButtonPressed?.Invoke();
		_opponentHud.GraveyardClicked += () => OpponentGraveyardButtonPressed?.Invoke();
		_playerHud.ExileClicked += () => ExilePressed?.Invoke();
		_opponentHud.ExileClicked += () => OpponentExilePressed?.Invoke();
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

	public Vector2 GetPlayerLibraryPosition() => _playerHud.LibraryCenter;

	public void FlashLoss(int losingPlayerId, int humanPlayerId) =>
		(losingPlayerId == humanPlayerId ? _playerHud : _opponentHud).Flash(
			new Color(1f, 0.15f, 0.15f, 1f)
		);

	/// <summary>
	/// The cards in a player's exile that are worth showing: everything but LANDS. A played land
	/// becomes mana and is exiled on the way (PlayLandAction), so the raw pile is mostly lands —
	/// and the mana display already accounts for them.
	/// </summary>
	public static List<Card> ExileShown(GameState state, int playerId) =>
		state
			.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Exile))
			.Where(c => !c.HasSubtype("Land"))
			.ToList();

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
		var isHumanTurn = game.ActivePlayerId == humanPlayerId;

		RefreshHud(_playerHud, state, humanPlayerId, isHumanTurn, targetHighlightIds);
		RefreshHud(_opponentHud, state, aiPlayerId, !isHumanTurn, targetHighlightIds);

		_playerBattlefield.Refresh(
			state.GetCardsInZone(state.GetWellKnownId(MtgObjectKeys.Player1Battlefield)),
			state,
			selectedAttackerId,
			targetHighlightIds,
			additionalCostHighlightIds,
			canAttack: isHumanTurn
		);
		_opponentBattlefield.Refresh(
			state.GetCardsInZone(state.GetWellKnownId(MtgObjectKeys.Player2Battlefield)),
			state,
			targetHighlightIds: targetHighlightIds
		);

		_turnBanner.Text = $"Turn {game.TurnNumber} — {(isHumanTurn ? "Your turn" : "Opponent")}";
		_endTurnButton.Disabled = !isHumanTurn;
	}

	private static void RefreshHud(
		PlayerHud hud,
		GameState state,
		int playerId,
		bool active,
		IEnumerable<int>? targetHighlightIds
	)
	{
		var graveyard = state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Graveyard));
		var exile = ExileShown(state, playerId);
		hud.Refresh(
			state.GetPlayer(playerId),
			state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Hand)).Count(),
			state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Library)).Count(),
			graveyard.LastOrDefault(),
			graveyard.Count(),
			exile.LastOrDefault(),
			exile.Count
		);
		hud.SetActive(active);
		hud.SetTargetable(targetHighlightIds?.Contains(playerId) == true);
	}
}
