using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using MtgCore;
using MtgSimulator;

namespace MtgGame;

/// <summary>
/// Shows what a seat actually played, split the way <c>Draft.BuildDeck</c> splits it: the first
/// <c>DefaultMaxSpells</c> non-lands in pick order are the deck, the rest of the pool never left
/// the sideboard. Reading the two side by side is the point — it is how you tell whether a seat
/// drafted badly or just drew badly.
///
/// Two views of the same split: a text list, which is the one that shows the sideboard and reads
/// fastest, and a card view, which is the one that shows what the deck actually looks like.
/// </summary>
public partial class DeckListPopup : CanvasLayer
{
	private static readonly Vector2 TextViewSize = new(720, 620);
	private static readonly Vector2 CardViewSize = new(1280, 700);

	private PanelContainer _panel = null!;
	private Label _title = null!;
	private Button _toggle = null!;
	private Label _deckHeader = null!;
	private Label _sideHeader = null!;
	private VBoxContainer _deckList = null!;
	private VBoxContainer _sideList = null!;
	private HBoxContainer _textView = null!;
	private ScrollContainer _cardView = null!;
	private HBoxContainer _cardGrid = null!;
	private CardPreviewPopup _preview = null!;

	private bool _cardMode;
	private string _drafterName = "";
	private IReadOnlyList<Card> _pool = Array.Empty<Card>();

	public override void _Ready()
	{
		Layer = 8;

		var scrim = new ColorRect { Color = new Color(0, 0, 0, 0.75f) };
		scrim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scrim.MouseFilter = Control.MouseFilterEnum.Stop;
		// Click anywhere outside the panel to dismiss — no close button to hunt for.
		scrim.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true })
				Hide();
		};
		AddChild(scrim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(center);

		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		_panel.CustomMinimumSize = TextViewSize;
		center.AddChild(_panel);

		var margin = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 18);
		_panel.AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		var titleRow = new HBoxContainer();
		root.AddChild(titleRow);

		_title = new Label();
		_title.HorizontalAlignment = HorizontalAlignment.Center;
		_title.AddThemeFontSizeOverride("font_size", 28);
		_title.Modulate = MtgUiStyles.GoldBorder;
		_title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(_title);

		_toggle = new Button();
		_toggle.CustomMinimumSize = new Vector2(140, 0);
		_toggle.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_toggle.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_toggle.AddThemeColorOverride("font_color", MtgUiStyles.GoldBorder);
		_toggle.Pressed += () =>
		{
			_cardMode = !_cardMode;
			_preview.HideCard();
			Render();
		};
		titleRow.AddChild(_toggle);

		var hint = new Label { Text = "Click anywhere outside to close" };
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.Modulate = new Color(0.55f, 0.55f, 0.6f, 1f);
		root.AddChild(hint);

		_textView = new HBoxContainer();
		_textView.AddThemeConstantOverride("separation", 24);
		_textView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		root.AddChild(_textView);

		_textView.AddChild(BuildColumn(out _deckHeader, out _deckList));
		_textView.AddChild(BuildColumn(out _sideHeader, out _sideList));

		// The columns of a full curve run wider than any sensible panel, so the card view scrolls
		// sideways rather than shrinking the cards past the point of recognition.
		_cardView = new ScrollContainer();
		_cardView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		_cardView.HorizontalScrollMode = ScrollContainer.ScrollMode.Auto;
		_cardView.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		_cardView.Visible = false;
		root.AddChild(_cardView);

		_cardGrid = new HBoxContainer();
		_cardGrid.AddThemeConstantOverride("separation", 10);
		_cardView.AddChild(_cardGrid);

		// This popup sits on layer 8, so its preview needs a higher one or it renders behind.
		_preview = new CardPreviewPopup
		{
			InternalCardScene = ResourceLoader.Load<PackedScene>(
				"res://Common/Cards/2D/Card2D/internal_cardui2d_canvasgroup.tscn"
			),
			PreviewScale = 1.15f,
			PreviewLayer = 9,
		};
		AddChild(_preview);

		// A child CanvasLayer is its own layer and does not inherit this one's visibility.
		VisibilityChanged += () =>
		{
			if (!Visible)
				_preview.HideCard();
		};

		Hide();
	}

	private static VBoxContainer BuildColumn(out Label header, out VBoxContainer list)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		header = new Label();
		header.AddThemeFontSizeOverride("font_size", 18);
		header.Modulate = MtgUiStyles.GoldBorder;
		col.AddChild(header);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		col.AddChild(scroll);

		list = new VBoxContainer();
		list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(list);

		return col;
	}

	public void ShowPool(string drafterName, IReadOnlyList<Card> pool)
	{
		_drafterName = drafterName;
		_pool = pool;
		Render();
		Show();
	}

	private void Render()
	{
		// Mirrors Draft.BuildDeck's split so this shows the deck that was really played, not a
		// second guess at it. Owner stamping is irrelevant here — nothing is entering a game.
		var spells = _pool.Where(c => !c.HasSubtype("Land")).ToList();
		var maindeck = spells.Take(Draft.DefaultMaxSpells).ToList();
		var sideboard = spells.Skip(Draft.DefaultMaxSpells).ToList();
		var lands = 40 - maindeck.Count;

		_title.Text = $"{_drafterName} — drafted deck";
		_toggle.Text = _cardMode ? "Text view" : "Card view";
		_panel.CustomMinimumSize = _cardMode ? CardViewSize : TextViewSize;
		_textView.Visible = !_cardMode;
		_cardView.Visible = _cardMode;

		if (_cardMode)
		{
			// Maindeck only. The sideboard is what you skim, not what you look at, and it is one
			// toggle away in the text view.
			CardGridView.Fill(_cardGrid, maindeck, ShowPreview, () => _preview.HideCard());
			return;
		}

		_deckHeader.Text = $"Deck ({maindeck.Count} spells + {lands} Plains)";
		_sideHeader.Text = $"Sideboard ({sideboard.Count})";
		CardListView.Fill(_deckList, maindeck);
		CardListView.Fill(_sideList, sideboard, dim: true);
	}

	// state: null — a drafted pool card is not in a game, so P/T is the printed value.
	private void ShowPreview(Card card) =>
		_preview.ShowCard(
			new InternalCardUI2D.Details
			{
				CardName = card.Name,
				ManaCost = card.ManaCost.ToString(),
				TypeLine = MtgCardMapper.GetTypeLine(card),
				PowerToughness = MtgCardMapper.GetPowerToughness(card, state: null),
				RulesText = MtgCardMapper.GetRulesText(card),
				ArtworkTexture = CardArtLoader.Load(card.Name),
				FrameColor = MtgCardTheme.FrameColor(card),
				NamePlateColor = MtgCardTheme.NamePlateColor(card),
			},
			GetViewport().GetMousePosition()
		);
}
