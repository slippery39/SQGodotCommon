using System.Collections.Generic;
using System.Linq;
using Common.Cards;
using DoomCore;
using Godot;

namespace DoomGame;

/// <summary>
/// The panel that explains the card under the cursor.
///
/// **Two layers, and a card never carries the second one** (DoomUI.md). The card says what it does
/// in as few words as the effect can be said in; everything else — what a word means, what the
/// numbers are — arrives here, on hover, and only on hover.
///
/// **The card already enlarges by itself.** `CardUIManager` runs a hover pass every frame and
/// `CardUI2D.StartHover` scales the winner up, so the "hover to enlarge" half of this was free. All
/// that was missing was somewhere for the words to go.
///
/// **It polls `CardUIManager.CurrentHoveredCard` rather than subscribing.** That static is already
/// recomputed every frame by the shared manager, which owns the decision about WHICH of several
/// overlapping cards is the hovered one — and reading it costs nothing and changes no shared code.
/// A second hover system here would disagree with the one that does the scaling.
/// </summary>
public sealed class DoomCardInspector
{
	private readonly PanelContainer _root;
	private readonly Label _title;
	private readonly VBoxContainer _entries;

	/// <summary>What the panel is currently describing, so it is not rebuilt every frame.</summary>
	private string _showing;

	/// <summary>
	/// <paramref name="at"/> is in CANVAS coordinates (1920x1080), not window pixels — the same
	/// space every other position on this board is authored in.
	/// </summary>
	public DoomCardInspector(CanvasLayer parent, Vector2 at)
	{
		_root = new PanelContainer { Visible = false };
		_root.AddThemeStyleboxOverride(
			"panel",
			DoomPalette.Box(DoomPalette.Navy, DoomPalette.Slate, 3)
		);

		// **TopLeft, and an absolute position.** With a TopRight preset the offsets are measured
		// from the right edge, so setting Position to 1420 put the panel 1420px PAST the screen —
		// it was being built and filled correctly every frame and drawn where nobody could see it.
		//
		// The board passes the one region that is genuinely free: right of the fan and below the
		// status strip. The lane grid now spans nearly the full width, so there is no room beside
		// it, and a panel over the lanes would hide the board while explaining a card.
		_root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_root.Position = at;
		_root.CustomMinimumSize = new Vector2(440, 0);

		// **Ignores the mouse, all of it.** A panel that takes clicks under the cursor stops the
		// card's Area2D from seeing the mouse, the hover ends, the panel hides, the hover starts
		// again — a flicker loop that reads as a broken card rather than a broken panel.
		_root.MouseFilter = Control.MouseFilterEnum.Ignore;

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 10);

		_title = DoomPalette.Text("", 28, DoomPalette.Gold, HorizontalAlignment.Left);
		rows.AddChild(_title);

		_entries = new VBoxContainer();
		_entries.AddThemeConstantOverride("separation", 12);
		rows.AddChild(_entries);

		_root.AddChild(rows);
		parent.AddChild(_root);
	}

	/// <summary>
	/// Called every frame from the board. Shows the hovered card's keywords, or nothing.
	///
	/// <paramref name="cardsById"/> is the hand as the ENGINE has it — the panel reads the card's
	/// real text rather than scraping the labels off the card face, so a rendering bug cannot
	/// quietly become an explanation bug too.
	/// </summary>
	public void Follow(IReadOnlyDictionary<string, DoomCard> cardsById)
	{
		var hovered = CardUIManager.CurrentHoveredCard;

		// Dragging is not reading. The manager already clears the hovered card when a drag starts,
		// but being explicit costs a line and the panel hanging over a dragged card looks broken.
		if (
			hovered is null
			|| CardUIManager.DraggingCard is not null
			|| hovered.Id is null
			|| !cardsById.TryGetValue(hovered.Id, out var card)
		)
		{
			Hide();
			return;
		}

		Show(card);
	}

	private void Hide()
	{
		_showing = null;
		_root.Visible = false;
	}

	/// <summary>
	/// Describes a card directly, with no hover involved.
	///
	/// **This is how the panel gets LOOKED at.** Hover cannot be captured — `--write-movie` does not
	/// move a mouse, and driving the desktop with synthetic input is the one thing the last handoff
	/// says not to do. So the card preview scene calls this, and the panel is judged on a screenshot
	/// like everything else on this board.
	/// </summary>
	public void Describe(DoomCard card) => Show(card);

	private static Label Body(string text, Color colour, int size, float alpha = 1f)
	{
		var label = DoomPalette.Text(text, size, colour, HorizontalAlignment.Left);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(400, 0);
		label.Modulate = new Color(1, 1, 1, alpha);
		return label;
	}

	private void Show(DoomCard card)
	{
		var key = card.Id.ToString();
		if (_showing == key)
			return; // Same card as last frame. Rebuilding it would restart nothing and cost everything.

		_showing = key;

		foreach (var child in _entries.GetChildren())
		{
			_entries.RemoveChild(child);
			child.QueueFree();
		}

		var unit = card.GetComponent<UnitComponent>();
		var rules = DoomCardFace.RulesTextFor(card);

		// **The card's own text comes FIRST, in full.** The small card clips to what fits at the
		// 16px floor, and the whole promise of hover is that a card too complicated for its own face
		// can still be read somewhere. A panel that showed only keywords would leave the one card
		// that actually needed explaining showing nothing but a glossary.
		_title.Text = card.Name?.ToUpperInvariant() ?? "";

		var line = unit is null
			? $"Rite  ·  costs {card.Cost}"
			: $"Unit  ·  costs {card.Cost}  ·  {unit.Power} power, {unit.Toughness} toughness";

		_entries.AddChild(Body(line, DoomPalette.Gold, 19));

		if (!string.IsNullOrWhiteSpace(rules))
			_entries.AddChild(Body(rules, DoomPalette.Bone, 21));

		// A Rite says so nowhere on its face — the absence of a stat badge is the only tell — so the
		// word is fed in here rather than hoped for in the text.
		foreach (
			var keyword in KeywordLibrary.In(
				rules,
				string.Join(" ", card.Tags),
				unit is null ? "Rite" : ""
			)
		)
		{
			var entry = new VBoxContainer();
			entry.AddThemeConstantOverride("separation", 2);
			entry.AddChild(
				DoomPalette.Text(keyword.Name, 21, DoomPalette.Gold, HorizontalAlignment.Left)
			);
			entry.AddChild(Body(keyword.Text, DoomPalette.Bone, 18, 0.72f));
			_entries.AddChild(entry);
		}

		_root.Visible = true;
	}
}
