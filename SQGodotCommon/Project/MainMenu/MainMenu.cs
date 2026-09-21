using System.Collections.Generic;
using Godot;
using KinGame;

namespace Project;

/// <summary>
/// The way into the game, and the first thing anyone sees.
///
/// **Built in code, like every other DOOMJAM screen**, and for the same reason: the layout is a
/// contract and a contract reviews better as text than as a scene diff. The scene is now a bare root
/// Control — it used to carry a MarginContainer, a CenterContainer and a default-themed
/// `PanelContainer`, and that untouched panel theme was the grey box the menu sat in.
///
/// **It reads DOOMJAM's own palette rather than inventing one.** Gold means "yours" everywhere else
/// in the game — the Companion, your energy — so gold is what a highlighted option is. Red is
/// reserved for the enemy and for life, so it carries the one line about the doom and nothing else.
/// See KinUI.md.
/// </summary>
public partial class MainMenu : Control
{
	/// <summary>
	/// The menu, in order, with the first selected on open.
	///
	/// **The MTG entries are gone (2026-09-18).** They opened `MtgGame` scenes that this jam branch
	/// does not use, and a menu is the wrong place to keep a bookmark for other work. **Nothing in
	/// `MtgGame` was touched** — the scenes are all still there and still load; they simply have no
	/// entry from here. Putting the two lines back is a two-line change.
	///
	/// **"Options" went too, and that is not the same kind of deletion.** It printed to the console
	/// and did nothing else. A menu entry that looks like a choice and is not is the same lie the
	/// theme picker was telling, and it goes for the same reason. Put it back when there is
	/// something behind it — the animation speed dial (F4 on the board) is the obvious first thing.
	/// </summary>
	/// <summary>
	/// **The game's name, and it is the only place the player sees one.**
	///
	/// The codebase carries a `Kin*` prefix — `KinCore`, `KinBoard`, `Kin.md` — which is a codename
	/// and deliberately NOT this title: the prefix should survive a re-theme, and `Doom*` did not.
	/// This constant and `project.godot`'s `config/name` are the two places that face outward.
	/// </summary>
	private const string Title = "ENDLING";

	private static readonly string[] Options = ["DESCEND", "QUIT"];

	private readonly List<Label> _labels = [];
	private int _index;

	public override void _Ready()
	{
		BuildBackdrop();
		BuildMenu();
		UpdateVisuals();
	}

	/// <summary>
	/// The painted backdrop the board already uses, darkened, with a flat navy fall-back.
	///
	/// **The ground is a plain `ColorRect` underneath rather than the image's own background**, so a
	/// missing or not-yet-imported PNG degrades to the right colour instead of to nothing. New art
	/// does not exist until `--headless --import` has run, and `ResourceLoader.Exists` returns false
	/// silently until it does.
	/// </summary>
	private void BuildBackdrop()
	{
		var ground = new ColorRect { Color = KinPalette.Navy };
		ground.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(ground);

		if (KinArt.Backdrop is { } texture)
		{
			var art = new TextureRect
			{
				Texture = texture,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,

				// Pushed well down so the title and the options read cleanly over it. The board
				// dims its own copy for the same reason.
				Modulate = new Color(1, 1, 1, 0.35f),
			};
			art.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(art);
		}
	}

	private void BuildMenu()
	{
		// **A VBox at FullRect with centred alignment**, rather than a container nested three deep.
		// A Container overrides its children's anchors on every layout pass, which is what made the
		// old structure hard to reason about — here there is one, and it owns the whole screen.
		var rows = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		rows.AddThemeConstantOverride("separation", 10);
		rows.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(rows);

		// **The title, and nothing under it.** There were two lines of description here — the hook
		// and a summary of the run's shape — and both were the kind of copy that explains a game to
		// someone who has not started it yet. A menu is not where that argument gets won.
		rows.AddChild(Centred(KinPalette.Text(Title, 104, KinPalette.Bone)));

		rows.AddChild(new Control { CustomMinimumSize = new Vector2(0, 64) });

		foreach (var option in Options)
		{
			var label = KinPalette.Text(option, 40, KinPalette.Bone);

			// **Each option takes mouse input, and the VBox above does not.** Hit-testing used to
			// walk every label's rect on every mouse move; a Control that answers for itself is
			// both less code and correct when the layout changes.
			label.MouseFilter = MouseFilterEnum.Stop;
			rows.AddChild(Centred(label));
			_labels.Add(label);
		}
	}

	/// <summary>
	/// Makes a label span the row so it centres against the SCREEN rather than against its own text
	/// width — without this every line is centred on a box exactly as wide as the words in it, and
	/// a VBox stacks them left-aligned to each other.
	/// </summary>
	private static Label Centred(Label label)
	{
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		return label;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true } key)
		{
			switch (key.Keycode)
			{
				case Key.Down
				or Key.S:
					Move(1);
					break;
				case Key.Up
				or Key.W:
					Move(-1);
					break;
				case Key.Enter
				or Key.KpEnter
				or Key.Space:
					Select();
					break;
				case Key.Escape:
					GetTree().Quit();
					break;
			}
		}

		if (@event is InputEventMouseMotion)
			HoverUnderMouse();

		if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
		{
			if (HoverUnderMouse())
				Select();
		}
	}

	/// <summary>Moves the highlight to whatever the cursor is over. True if it found something.</summary>
	private bool HoverUnderMouse()
	{
		for (var i = 0; i < _labels.Count; i++)
		{
			if (!_labels[i].GetGlobalRect().HasPoint(GetGlobalMousePosition()))
				continue;

			if (_index != i)
			{
				_index = i;
				UpdateVisuals();
			}

			return true;
		}

		return false;
	}

	private void Move(int direction)
	{
		_index = (_index + direction + _labels.Count) % _labels.Count;
		UpdateVisuals();
	}

	private void UpdateVisuals()
	{
		for (var i = 0; i < _labels.Count; i++)
		{
			var selected = i == _index;

			// Gold for the selection, and a marker either side of it. Colour alone is a poor cue at
			// a glance and the board already uses gold for several things that are not this.
			_labels[i]
				.AddThemeColorOverride("font_color", selected ? KinPalette.Gold : KinPalette.Bone);
			_labels[i].Text = selected ? $"[  {Options[i]}  ]" : Options[i];
		}
	}

	private void Select()
	{
		switch (Options[_index])
		{
			case "DESCEND":
				QueueFree();
				GameManager.Instance.ChangeScene("res://KinGame/kin_board.tscn");
				break;

			case "QUIT":
				GetTree().Quit();
				break;
		}
	}
}
