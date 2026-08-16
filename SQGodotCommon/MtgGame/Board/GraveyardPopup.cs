using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Full-screen overlay that displays the player's graveyard as a scrollable row of mini-cards.
/// Flashback-eligible cards glow purple. Clicking one emits CardClicked so the caller can
/// initiate the flashback cast flow. Opened on demand; closed via the Close button or when
/// the caller calls Hide().
/// </summary>
public partial class GraveyardPopup : CanvasLayer
{
	/// Large enough that a graveyard card can be read, not just recognised.
	private const float GraveyardCardScale = 0.7f;

	private ScrollContainer _scroll = null!;
	private BattlefieldZone _zone = null!;
	private CardPreviewPopup _preview = null!;
	private GameState? _state;

	public event Action<int>? CardClicked;

	public override void _Ready()
	{
		Layer = 6;

		var bg = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Pass;
		AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(1150, 500);
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		center.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		// Header row: title + close button
		var header = new HBoxContainer();
		vbox.AddChild(header);

		var title = new Label { Text = "Graveyard" };
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(title);

		var closeBtn = new Button { Text = "✕" };
		closeBtn.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		closeBtn.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		closeBtn.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		closeBtn.Pressed += Hide;
		header.AddChild(closeBtn);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		// Scrollable card area
		_scroll = new ScrollContainer();
		_scroll.CustomMinimumSize = new Vector2(0, 380);
		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		_scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(_scroll);

		_zone = new BattlefieldZone();
		_zone.CardScale = GraveyardCardScale;
		_zone.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_scroll.AddChild(_zone);

		_zone.CardClicked += id =>
		{
			Hide();
			CardClicked?.Invoke(id);
		};

		// This popup sits on layer 6, so its preview needs a higher one or it renders behind.
		_preview = new CardPreviewPopup
		{
			InternalCardScene = ResourceLoader.Load<PackedScene>(
				"res://Common/Cards/2D/Card2D/internal_cardui2d_canvasgroup.tscn"
			),
			PreviewScale = 1.1f,
			PreviewLayer = 7,
		};
		AddChild(_preview);

		_zone.CardHovered += OnCardHovered;
		_zone.CardHoverEnded += _ => _preview.HideCard();

		// A child CanvasLayer is its own layer and does not inherit this one's visibility, so the
		// preview has to be dismissed explicitly however the popup gets closed.
		VisibilityChanged += () =>
		{
			if (!Visible)
				_preview.HideCard();
		};

		Hide();
	}

	private void OnCardHovered(int cardId)
	{
		if (_state?.GetObject(cardId) is not Card card)
			return;

		_preview.ShowCard(
			new InternalCardUI2D.Details
			{
				CardName = card.Name,
				ManaCost = card.ManaCost.ToString(),
				TypeLine = MtgCardMapper.GetTypeLine(card),
				PowerToughness = MtgCardMapper.GetPowerToughness(card, _state),
				RulesText = MtgCardMapper.GetRulesText(card),
				ArtworkTexture = CardArtLoader.Load(card.Name),
				FrameColor = MtgCardTheme.FrameColor(card),
				NamePlateColor = MtgCardTheme.NamePlateColor(card),
			},
			GetViewport().GetMousePosition()
		);
	}

	public void ShowGraveyard(
		IEnumerable<Card> cards,
		GameState state,
		IEnumerable<int> flashbackIds,
		IEnumerable<int>? targetHighlightIds = null
	)
	{
		_state = state;
		_zone.Refresh(
			cards,
			state,
			flashbackHighlightIds: flashbackIds,
			targetHighlightIds: targetHighlightIds
		);
		Show();
	}
}
