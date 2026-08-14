using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using MtgCore;
using MtgSimulator;
using Project;

namespace MtgGame;

/// <summary>
/// The human seat in a booster draft: renders this seat's pack, takes a click, and drives
/// <see cref="Draft.ApplyPicks"/> with the bots' picks alongside.
///
/// The draft library has no human picker by design — the caller supplies the index. This is
/// that caller, and it is why no library change was needed to make drafting playable.
/// </summary>
public partial class DraftScene : Control
{
	private const int SeatCount = 8;
	private const int PackSize = 15;
	private const int PackCount = 3;

	/// Which set this scene drafts. Change this one line to draft a different set —
	/// SetRegistry.Default is the Legacy pool, Hollowmere.Set is the graveyard set.
	private static readonly CardSet DraftedSet = Hollowmere.Set;

	/// Derived from DraftTrainingStore.PathFor so the asset filename and the filename the
	/// trainer writes cannot drift apart — only the directory differs (res:// vs sim_results/).
	private static string ModelPath =>
		"res://MtgGame/Assets/"
		+ System.IO.Path.GetFileName(DraftTrainingStore.PathFor(DraftedSet.Code));

	/// Draft cards are read, not glanced at — the battlefield's 0.42 leaves the rules box
	/// illegible. This is ~250x310px per card, so a 15-card pack wraps to 2-3 rows.
	private const float PackCardScale = 0.7f;

	/// Comfortably above PackCardScale — a preview that is not clearly bigger than the card it
	/// previews is worse than none.
	private const float PreviewCardScale = 1.15f;

	private DraftState _state = null!;
	private List<DraftPicker> _pickers = null!;
	private int _seed;
	private int _humanSeat;

	private Label _header = null!;
	private Label _pickerLabel = null!;
	private HFlowContainer _packGrid = null!;
	private VBoxContainer _poolList = null!;
	private Label _poolHeader = null!;
	private CardPreviewPopup _preview = null!;
	private PackedScene _boardCardScene = null!;

	public override void _Ready()
	{
		_seed = (int)(DateTime.Now.Ticks & 0x7FFFFFFF);
		_humanSeat = 0;

		_boardCardScene = ResourceLoader.Load<PackedScene>("res://MtgGame/Board/board_card.tscn");
		_state = Draft.Create(
			DraftFormat.Booster,
			DraftedSet.Cards,
			_seed,
			SeatCount,
			PackSize,
			PackCount
		);
		_pickers = BuildPickers(out var pickerName);

		BuildUi(pickerName);
		Refresh();
	}

	/// <summary>
	/// Bots use the trained model when it is available and Curve when it is not. Those are not
	/// equivalent — on Hollowmere, Trained measures 75% against Curve's 37.5% — so the
	/// difference is shown in the header rather than hidden.
	/// </summary>
	private List<DraftPicker> BuildPickers(out string pickerName)
	{
		var model = LoadModel();
		pickerName = model == null ? "Curve (no trained model found)" : "Trained";

		var rng = new Random(_seed + 100);
		var picker = model == null ? DraftPickers.Curve : DraftPickers.Trained(model, rng);

		// Every seat gets the same picker so the pod is a level field; the human's only edge
		// should be their own picks. The human's slot is never called — it is filled from the
		// click — but keeping the list seat-aligned avoids index juggling in OnPicked.
		return Enumerable.Range(0, SeatCount).Select(_ => picker).ToList();
	}

	/// System.IO cannot read res:// inside an exported build, so the model is read through
	/// Godot's FileAccess and parsed by the library.
	private static DraftTrainingData? LoadModel()
	{
		if (!FileAccess.FileExists(ModelPath))
		{
			GD.PushWarning($"Draft: no trained model at {ModelPath}; bots fall back to Curve.");
			return null;
		}
		return DraftTrainingStore.FromJson(FileAccess.GetFileAsString(ModelPath));
	}

	// ===== UI =====

	private void BuildUi(string pickerName)
	{
		var bg = new ColorRect { Color = MtgUiStyles.DarkBg };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		_preview = new CardPreviewPopup
		{
			InternalCardScene = ResourceLoader.Load<PackedScene>(
				"res://Common/Cards/2D/Card2D/internal_cardui2d_canvasgroup.tscn"
			),
			PreviewScale = PreviewCardScale,
		};
		AddChild(_preview);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 20);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_header = new Label();
		_header.AddThemeFontSizeOverride("font_size", 32);
		_header.Modulate = MtgUiStyles.GoldBorder;
		root.AddChild(_header);

		_pickerLabel = new Label { Text = $"7 AI drafters · {pickerName} · seed {_seed}" };
		_pickerLabel.AddThemeFontSizeOverride("font_size", 13);
		_pickerLabel.Modulate = new Color(0.65f, 0.65f, 0.7f, 1f);
		root.AddChild(_pickerLabel);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 20);
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddChild(columns);

		// Pack — scrolls because 15 cards at 150px wide will not fit every window width.
		var packScroll = new ScrollContainer();
		packScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		packScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		packScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		columns.AddChild(packScroll);

		_packGrid = new HFlowContainer();
		_packGrid.AddThemeConstantOverride("h_separation", 10);
		_packGrid.AddThemeConstantOverride("v_separation", 10);
		_packGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		packScroll.AddChild(_packGrid);

		columns.AddChild(BuildPoolSidebar());
	}

	private PanelContainer BuildPoolSidebar()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		panel.CustomMinimumSize = new Vector2(260, 0);

		var inner = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			inner.AddThemeConstantOverride($"margin_{side}", 10);
		panel.AddChild(inner);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		inner.AddChild(col);

		_poolHeader = new Label();
		_poolHeader.AddThemeFontSizeOverride("font_size", 18);
		_poolHeader.Modulate = MtgUiStyles.GoldBorder;
		col.AddChild(_poolHeader);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		col.AddChild(scroll);

		_poolList = new VBoxContainer();
		_poolList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_poolList);

		return panel;
	}

	private void Refresh()
	{
		var seat = _state.Seats[_humanSeat];
		var pick = PackSize - seat.Offer.Count + 1;
		_header.Text = $"Pack {_state.Round + 1} · Pick {pick}";

		RefreshPack(seat.Offer);
		RefreshPool(seat.Pool);
	}

	private void RefreshPack(IReadOnlyList<Card> offer)
	{
		foreach (var child in _packGrid.GetChildren())
			child.QueueFree();

		for (var i = 0; i < offer.Count; i++)
		{
			// Captured per iteration: the pick is this card's POSITION in the offer, not its
			// identity. Card is a record, so duplicate templates in one pack compare equal and
			// picking by value would remove the wrong copy.
			var index = i;
			var card = offer[i];

			var node = _boardCardScene.Instantiate<BoardCard>();
			// Set before entering the tree: BoardCard applies it in _Ready.
			node.CardScale = PackCardScale;
			_packGrid.AddChild(node);
			node.Refresh(card, state: null);
			node.Clicked += _ => OnPicked(index);
			node.Hovered += _ =>
				_preview.ShowCard(ToDetails(card), GetViewport().GetMousePosition());
			node.HoverEnded += _ => _preview.HideCard();
		}
	}

	private void RefreshPool(IReadOnlyList<Card> pool)
	{
		_poolHeader.Text = $"Your Pool ({pool.Count})";
		CardListView.Fill(_poolList, pool);
	}

	private static InternalCardUI2D.Details ToDetails(Card card) =>
		new()
		{
			CardName = card.Name,
			ManaCost = card.ManaCost.ToString(),
			RulesText = MtgCardMapper.GetRulesText(card),
			ArtworkTexture = CardArtLoader.Load(card.Name),
		};

	// ===== Pick loop =====

	private void OnPicked(int humanIndex)
	{
		_preview.HideCard();

		var picks = _state
			.Seats.Select(
				(seat, i) => i == _humanSeat ? humanIndex : _pickers[i](seat.Offer, seat.Pool)
			)
			.ToList();
		_state = Draft.ApplyPicks(_state, picks);

		if (_state.IsComplete)
			FinishDraft();
		else
			Refresh();
	}

	private void FinishDraft()
	{
		var pools = _state.Seats.Select(s => (IReadOnlyList<Card>)s.Pool).ToList();
		GameManager.Instance.RegisterService(new DraftTournament(pools, _seed, _humanSeat));
		GameManager.Instance.ChangeScene("res://MtgGame/Draft/TournamentScene.tscn");
	}
}
