using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;

namespace KinGame;

/// <summary>
/// **Run start: you pick the thing that comes with you.**
///
/// This is the first screen of a run and the only build declaration it has. It replaces nothing —
/// `KinThemeSelect` was deleted long before this existed, because picking an act was a choice that
/// did nothing once the acts were chained. `KinBoard` recorded the fix at the time: *"if a run start
/// ever needs a screen again it should pick the COMPANION"*, which is what this is.
///
/// **The ability is the whole reason to choose**, so the ability is the biggest text on each tile.
/// A companion's stat line is the least interesting thing about it — two of the roster differ by
/// three points of toughness and play nothing alike — so the stats are a footnote and the rules
/// text is the headline. Reading a name and a 5/12 and picking one would be a coin toss.
///
/// It reads <see cref="StarterContent.Roster"/> rather than holding a list of its own. A second
/// roster is a roster that drifts, and the first time it did the screen would be offering a
/// companion the run cannot build.
/// </summary>
public sealed class KinCompanionSelect
{
	private readonly ColorRect _root;
	private readonly HBoxContainer _tiles;
	private readonly Action<Companion> _onChosen;

	/// <summary>
	/// How tall a tile is, in canvas pixels. Fixed, because the tiles hold different amounts of
	/// text and a row of panels at five different heights reads as five broken panels.
	/// </summary>
	private const int TileHeight = 420;

	public KinCompanionSelect(CanvasLayer parent, Action<Companion> onChosen)
	{
		_onChosen = onChosen;

		// A ColorRect and not a Container, for the reason KinIntermission records: a Container
		// overrides its children's anchors every layout pass.
		_root = new ColorRect { Visible = false, Color = KinPalette.Navy };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 18);
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.OffsetLeft = 80;
		column.OffsetRight = -80;
		column.OffsetTop = 90;
		column.AddChild(KinPalette.Text("WHO COMES WITH YOU", 52, KinPalette.Gold));

		var blurb = KinPalette.Text(
			"It is on the board free, every battle, and it is the only thing that is. "
				+ "What it does decides which cards are worth taking.",
			22,
			KinPalette.Bone
		);
		blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		blurb.CustomMinimumSize = new Vector2(1, 0);
		column.AddChild(blurb);

		// **ShrinkBegin, not ExpandFill.** Expanding made every tile as tall as the screen, which
		// pushed each stat line to the very bottom edge and left a field of empty panel above it.
		_tiles = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		_tiles.AddThemeConstantOverride("separation", 16);
		column.AddChild(_tiles);

		_root.AddChild(column);
		parent.AddChild(_root);
	}

	public void Show()
	{
		foreach (var child in _tiles.GetChildren())
		{
			_tiles.RemoveChild(child);
			child.QueueFree();
		}

		foreach (var companion in StarterContent.Roster)
			_tiles.AddChild(Tile(companion));

		_root.Visible = true;
	}

	public void Hide() => _root.Visible = false;

	/// <summary>
	/// One companion: its drawing, its name, what it DOES, and its stats last.
	///
	/// The whole tile is the button. A small "CHOOSE" button under a panel makes the panel look
	/// like decoration and gives a phone a target a thumb keeps missing — the same reason the
	/// reward screen offers real cards rather than buttons describing cards.
	/// </summary>
	private Control Tile(Companion companion)
	{
		// **`Flat` is NOT set, and that is the whole tile.** A flat Button draws no stylebox at all,
		// so the panel, its border and its hover highlight all silently vanished and the screen
		// rendered as five columns of loose text on the background.
		var button = new Button
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, TileHeight),
		};
		button.AddThemeStyleboxOverride(
			"normal",
			KinPalette.Box(KinPalette.Slate, KinPalette.Bone, 3)
		);
		button.AddThemeStyleboxOverride(
			"hover",
			KinPalette.Box(KinPalette.Slate, KinPalette.Gold, 5)
		);
		button.AddThemeStyleboxOverride(
			"pressed",
			KinPalette.Box(KinPalette.Slate, KinPalette.Gold, 5)
		);
		button.Pressed += () => _onChosen(companion);

		var rows = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		rows.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rows.OffsetLeft = 16;
		rows.OffsetRight = -16;
		rows.OffsetTop = 16;
		rows.OffsetBottom = -16;
		rows.AddThemeConstantOverride("separation", 10);
		button.AddChild(rows);

		// By NAME, the same convention every card and enemy uses — so a companion gets its drawing
		// the moment someone draws one, with no registration step.
		var art = new TextureRect
		{
			// The same "drawing by name, else the generated silhouette" path a card takes, so a
			// companion with no art is a figure rather than a hole.
			Texture = KinArt.CardArt(companion.Name, KinPalette.Gold, 150),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(0, 150),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		rows.AddChild(art);

		rows.AddChild(KinPalette.Text(companion.Name.ToUpperInvariant(), 30, KinPalette.Gold));

		// **The headline, because it is the actual decision.** Every line the companion declares,
		// not just the first: Ash has two effects and the second is the unwind, which carries no
		// text on purpose — so empty text is skipped rather than rendered as a blank row.
		var ability = KinPalette.Text(
			string.Join("\n", companion.Effects.Select(e => e.Text).Where(t => t.Length > 0)),
			22,
			KinPalette.Bone
		);
		ability.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		ability.CustomMinimumSize = new Vector2(1, 0);
		ability.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		ability.VerticalAlignment = VerticalAlignment.Top;
		rows.AddChild(ability);

		var stats = KinPalette.Text(
			$"{companion.Power} / {companion.Toughness}",
			20,
			KinPalette.Bone
		);
		stats.Modulate = new Color(1, 1, 1, 0.7f);
		rows.AddChild(stats);

		return button;
	}
}
