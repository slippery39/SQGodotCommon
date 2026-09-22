using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using Godot;
using KinCore;

namespace KinGame;

/// <summary>
/// The shop floor: cards to buy, life to buy back, and **a card to take out of the deck**.
///
/// Removal is why this screen exists. Combat v3 made the deck the whole of your per-turn output — you
/// draw five a turn and play two or three — so a card you would not play is actively crowding out one
/// you would. Until the shop, the only thing in the game that could remove a card from a run was an
/// apocalypse.
///
/// **Built in code, like `KinIntermission`, and following its hard-won layout rules**: a ColorRect
/// root rather than a container (a Container overrides its children's anchors), labels wrapped so
/// they cannot demand a width, and cards as Node2D positioned by hand because a Control container
/// will not lay a Node2D out.
/// </summary>
public sealed class KinShop
{
	private readonly ColorRect _root;
	private readonly Label _title;
	private readonly Label _body;
	private readonly Button _remove;
	private readonly Button _heal;
	private readonly Button _leave;
	private readonly Label _hint;

	/// <summary>Cards for sale, or the deck when picking one to remove. Node2D — see the class doc.</summary>
	private readonly Node2D _cards;

	private readonly Func<Run> _run;
	private readonly Action<RunCard> _onBuy;
	private readonly Action<int> _onRemove;
	private readonly Action _onHeal;

	private ShopOffer _offer = new();

	/// <summary>True while the deck is shown for removal rather than the stock.</summary>
	private bool _removing;

	/// <summary>
	/// Which of the offered cards have already been bought this visit. **Stock depletes** — a card
	/// can be bought once and then it is gone.
	///
	/// Per-visit view state, and legitimately so: a shop floor is entered once and never returned
	/// to, so what has been taken off the shelf does not outlive the screen. Anything that DOES have
	/// to outlive it — the gold, the deck — is read from the run and never copied here.
	/// </summary>
	private readonly HashSet<int> _bought = [];

	private const float CentreX = 960f;
	private const int ButtonY = 900;

	/// <summary>
	/// The space a card grid may occupy, in canvas pixels: below the panel and above the buttons.
	/// **Nothing may be drawn outside it** — the first removal grid ignored these and ran off the
	/// screen in both directions.
	/// </summary>
	private const float BandWidth = 1820f;

	private const float BandHeight = 610f;
	private const float BandCentreY = 545f;

	/// <summary>One card's footprint at scale 1, including the gap around it.</summary>
	private const float StepX = 300f;

	private const float StepY = 420f;

	public KinShop(
		CanvasLayer parent,
		Func<Run> run,
		Action<RunCard> onBuy,
		Action<int> onRemove,
		Action onHeal,
		Action onLeave
	)
	{
		_run = run;
		_onBuy = onBuy;
		_onRemove = onRemove;
		_onHeal = onHeal;

		_root = new ColorRect { Visible = false, Color = new Color(0, 0, 0, 0.82f) };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(KinPalette.Slate, KinPalette.Gold, 3)
		);
		panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		panel.OffsetLeft = 420;
		panel.OffsetRight = -420;
		panel.OffsetTop = 36;
		panel.OffsetBottom = 36;
		_root.AddChild(panel);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 14);
		panel.AddChild(rows);

		_title = KinPalette.Text("", 42, KinPalette.Bone);
		_body = KinPalette.Text("", 20, KinPalette.Bone);
		_hint = KinPalette.Text("", 22, KinPalette.Gold);
		rows.AddChild(_title);
		rows.AddChild(_body);
		rows.AddChild(_hint);
		Wrap(_title);
		Wrap(_body);
		Wrap(_hint);

		// The three ways to spend, in a row below the stock. Removal first because it is the reason
		// the screen exists and the thing a player will not think to look for.
		_remove = MakeButton("", new Vector2(-660, ButtonY));
		_remove.Pressed += ToggleRemoving;

		_heal = MakeButton("", new Vector2(-220, ButtonY));
		_heal.Pressed += () =>
		{
			_onHeal();
			Refresh();
		};

		_leave = MakeButton("DESCEND", new Vector2(220, ButtonY));
		_leave.Pressed += onLeave;

		parent.AddChild(_root);

		// AFTER the panel, so cards draw over the dimmed background rather than under it.
		_cards = new Node2D { Visible = false };
		parent.AddChild(_cards);
	}

	private Button MakeButton(string text, Vector2 position)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(420, 66) };
		button.AddThemeFontSizeOverride("font_size", 22);
		button.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		button.Position = position;

		// **Above the cards.** `ZIndex` beats tree order, and the card grid sets 400+ — without this
		// a full deck drew straight over the BACK button and there was no way out of the screen.
		button.ZIndex = 500;
		_root.AddChild(button);
		return button;
	}

	/// <summary>
	/// Lets a label wrap, and stops it demanding a width of its own. `CustomMinimumSize.X = 1` is the
	/// part that matters — autowrap alone still reports a minimum width and a container honours it,
	/// which is what once pushed the intermission panel off the side of the screen.
	/// </summary>
	private static void Wrap(Label label)
	{
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(1, 0);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
	}

	public void Hide()
	{
		_root.Visible = false;
		_cards.Visible = false;
	}

	/// <summary>
	/// Opens the shop on a floor. The offer is fixed for the visit.
	///
	/// `removing` exists for the capture flag: the removal grid is the layout most likely to break —
	/// it sizes itself to a deck that can be forty cards — and a screenshot cannot press a button.
	/// </summary>
	public void Show(ShopOffer offer, bool removing = false)
	{
		_offer = offer;
		_removing = removing;
		_bought.Clear();
		_root.Visible = true;
		Refresh();
	}

	private void ToggleRemoving()
	{
		_removing = !_removing;
		Refresh();
	}

	/// <summary>
	/// Redraws everything from the RUN as it is now, after every transaction.
	///
	/// **Nothing on this screen tracks its own copy of the gold or the deck.** A shop that remembered
	/// what it had sold would be a second account of the run's state, and it would drift from the one
	/// the game actually uses — the same rule that keeps the doom preview running the real transform.
	/// </summary>
	private void Refresh()
	{
		var run = _run();
		var canRemove = run.Deck.Count > Run.MinDeckSize && run.Gold >= _offer.RemovalPrice;

		_title.Text = _removing ? "TAKE ONE OUT" : $"FLOOR {run.Floor} — SOMEONE IS STILL TRADING";

		_body.Text = _removing
			? string.Join(
				"\n",
				"Click a card to leave it behind. It is gone for the rest of the run.",
				"",
				$"Gold  {run.Gold}        Deck  {run.Deck.Count} cards"
					+ $"   (never below {Run.MinDeckSize})"
			)
			: string.Join(
				"\n",
				"You draw five a turn from this deck. What is in it is what you get to do.",
				"",
				$"Gold  {run.Gold}        Life  {run.Life} / {run.MaxLife}"
					+ $"        Deck  {run.Deck.Count} cards"
			);

		// **Prices on the buttons, not in a table.** A cost you have to look up somewhere else is a
		// cost the player does not weigh.
		_remove.Text = _removing ? "BACK" : $"REMOVE A CARD — {_offer.RemovalPrice}";
		_remove.Disabled = !_removing && !canRemove;

		_heal.Text = $"PATCH UP +{_offer.HealAmount} — {_offer.HealPrice}";
		_heal.Visible = !_removing;
		_heal.Disabled = run.Gold < _offer.HealPrice || run.Life >= run.MaxLife;

		_leave.Visible = !_removing;

		_hint.Text =
			_removing ? ""
			: run.Deck.Count <= Run.MinDeckSize
				? $"Your deck is down to {Run.MinDeckSize}. Nothing more can be taken out of it."
			: _bought.Count == _offer.Cards.Length && _offer.Cards.Length > 0
				? "You have taken everything worth taking."
			: run.Gold < _offer.CardPrice && !canRemove ? "Not enough gold for anything here."
			: "";

		if (_removing)
			ShowDeck(run);
		else
			ShowStock(run);
	}

	/// <summary>The cards for sale, drawn as real cards — the same rule the reward screen follows.</summary>
	private void ShowStock(Run run)
	{
		ClearCards();

		var stock = _offer.Cards.ToList();

		// **Bought cards leave the shelf**, and the ones left keep their original slot rather than
		// the row re-centring — buying one must not slide the others out from under the cursor.
		for (var i = 0; i < stock.Count; i++)
		{
			if (_bought.Contains(i))
				continue;

			var card = stock[i];
			var affordable = run.Gold >= _offer.CardPrice;

			var ui = Build(card.ToKinCard(), i);
			ui.Position = new Vector2(CentreX + ((i - ((stock.Count - 1) / 2f)) * 340f), 620f);
			ui.Scale *= 1.0f;

			// **Unaffordable stock is shown dimmed rather than hidden.** What you cannot buy yet is
			// information: it is what the gold in your pocket is for.
			if (!affordable)
				ui.Modulate = new Color(1, 1, 1, 0.45f);
			else
			{
				var slot = i;
				KinCardTap.OnFirstPress(
					ui,
					() =>
					{
						_onBuy(card);
						_bought.Add(slot);
						Refresh();
					}
				);
			}

			// **Beside the card, never INSIDE it.** `card_2d_canvasgroup.tscn` carries a scale of
			// its own and this screen adds another, so a raw point size parented to a card is
			// multiplied by both — the chain belongs to `KinCardFace.Pt()` and nothing else should
			// author against it. As a sibling in `_cards` the label is at canvas scale, full stop.
			var price = KinPalette.Text($"{_offer.CardPrice} GOLD", 24, KinPalette.Gold);
			price.Position = ui.Position + new Vector2(-46, 200);
			price.ZIndex = 420 + i;
			_cards.AddChild(price);
		}

		_cards.Visible = true;
	}

	/// <summary>
	/// The whole deck, to pick one to leave behind.
	///
	/// Laid out by hand in a grid because these are Node2D and no Control container will position
	/// them. Sorted so the deck reads as a deck rather than as draw order — you are looking for the
	/// worst card in it, and that is far easier when copies sit together.
	/// </summary>
	private void ShowDeck(Run run)
	{
		ClearCards();

		var deck = run
			.Deck.OrderBy(c => c.Cost)
			.ThenBy(c => c.Name, StringComparer.Ordinal)
			.ToList();

		// **The grid is FITTED to the band, not guessed at.** The first version picked a column
		// count from the card count and clamped a scale — with a 46-card deck it produced five rows
		// of near-full-size cards that ran off the top and bottom of the screen, drew over the panel
		// and buried the BACK button. A deck can be forty cards; the layout has to solve for that
		// rather than hope.
		//
		// Every column count is tried and the one that lets the cards be LARGEST wins, because both
		// constraints bind at different deck sizes: width for a small deck, height for a large one.
		var best = 6;
		var bestScale = 0f;

		for (var columns = 5; columns <= 18; columns++)
		{
			var rows = (int)Math.Ceiling(deck.Count / (double)columns);
			var scale = Math.Min(BandWidth / (columns * StepX), BandHeight / (rows * StepY));

			if (scale > bestScale)
			{
				bestScale = scale;
				best = columns;
			}
		}

		// Never bigger than a card in hand — a deck view is for scanning, not for reading one card.
		var chosen = Math.Min(bestScale, 0.58f);
		var cols = best;
		var rowCount = (int)Math.Ceiling(deck.Count / (double)cols);
		var stepX = StepX * chosen;
		var stepY = StepY * chosen;

		for (var i = 0; i < deck.Count; i++)
		{
			var card = deck[i];
			var ui = Build(card.ToKinCard(), i);

			var column = i % cols;
			var row = i / cols;

			ui.Position = new Vector2(
				CentreX + ((column - ((cols - 1) / 2f)) * stepX),
				BandCentreY + ((row - ((rowCount - 1) / 2f)) * stepY)
			);
			ui.Scale *= chosen;

			var id = card.RunCardId;
			KinCardTap.OnFirstPress(
				ui,
				() =>
				{
					_onRemove(id);
					_removing = false;
					Refresh();
				}
			);
		}

		_cards.Visible = true;
	}

	/// <summary>
	/// One card face, wired for clicking.
	///
	/// **Dragging off and the hover LIFT off**, both for the reasons `KinIntermission` records:
	/// `CardUI2D` raises Clicked only while it is the hovered card and starting a drag clears that on
	/// the same press, so a draggable card can never be clicked; and `StartHover` yanks the card to
	/// the bottom of the viewport, which is right for a hand and wrong for anything else.
	///
	/// The press itself is wired by the callers through <see cref="KinCardTap"/>, which is what
	/// makes a single tap enough on a phone.
	/// </summary>
	private CardUI2D Build(KinCard shown, int index)
	{
		var ui = GD.Load<PackedScene>("res://Common/Cards/2D/Card2D/card_2d_canvasgroup.tscn")
			.Instantiate<CardUI2D>();

		_cards.AddChild(ui);

		// AFTER AddChild, and in this order: Style reaches into the card's own tree by node name.
		KinCardFace.Style(ui);
		ui.ApplyTo(KinCardFace.For(shown));
		KinCardFace.ApplyStats(ui, shown);

		ui.ZIndex = 400 + index;
		ui.DragEnabled = () => false;
		ui.IsPosLerping = true;

		if (ui.FindChild("HoverArea", true, false) is Area2D area)
		{
			area.MouseEntered += ui.Highlight;
			area.MouseExited += ui.NoHighlight;
		}

		return ui;
	}

	private void ClearCards()
	{
		foreach (var child in _cards.GetChildren())
		{
			_cards.RemoveChild(child);
			child.QueueFree();
		}

		_cards.Visible = false;
	}
}
