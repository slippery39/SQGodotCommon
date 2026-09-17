using System;
using DoomCore;
using Godot;

namespace DoomGame;

/// <summary>
/// The first screen: which apocalypse you are walking into.
///
/// **This is the picker, the difficulty preview and the pitch in one.** The doom is a SCHEDULE, not
/// a roll (see <see cref="ThemeLibrary"/>), and certainty is permission to show the player
/// everything — so the whole act is laid out before a card is drawn. There is nothing to hide: a
/// run of a theme always faces the same escalation, which is what makes it a story rather than a
/// shuffle.
///
/// Until this existed the front end always got the <see cref="DoomTheme.LongEmergency"/> default and
/// two of the three acts were reachable only from the simulator.
/// </summary>
public sealed class DoomThemeSelect
{
	private readonly PanelContainer _root;

	public DoomThemeSelect(CanvasLayer parent, int seed, Action<DoomTheme> onPick)
	{
		_root = new PanelContainer { Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Navy));

		var centre = new CenterContainer();
		_root.AddChild(centre);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 18);
		centre.AddChild(rows);

		rows.AddChild(DoomPalette.Text("CHOOSE YOUR APOCALYPSE", 46, DoomPalette.Bone));
		rows.AddChild(
			DoomPalette.Text(
				$"Twenty floors. The schedule is fixed, and this is all of it.        SEED {seed}",
				20,
				DoomPalette.Bone
			)
		);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 16);
		columns.Alignment = BoxContainer.AlignmentMode.Center;
		rows.AddChild(columns);

		foreach (var theme in ThemeLibrary.All)
			columns.AddChild(BuildColumn(theme, onPick));

		parent.AddChild(_root);
	}

	public void Show() => _root.Visible = true;

	public void Hide() => _root.Visible = false;

	private static PanelContainer BuildColumn(ThemeDefinition theme, Action<DoomTheme> onPick)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride(
			"panel",
			DoomPalette.Box(DoomPalette.Slate, DoomPalette.Gold, 3)
		);

		var rows = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
		rows.AddThemeConstantOverride("separation", 10);
		panel.AddChild(rows);

		rows.AddChild(DoomPalette.Text(theme.Name.ToUpperInvariant(), 30, DoomPalette.Gold));
		rows.AddChild(DoomPalette.Text(theme.Description, 19, DoomPalette.Bone));
		rows.AddChild(new HSeparator());

		foreach (var band in ThemeLibrary.BandsOf(theme.Theme))
		{
			var permanent = StarterContent.ScopeOf(band.Doom) == DoomScope.Permanent;

			// Red for a doom that REWRITES THE DECK, bone for one that only takes the fight in
			// front of you. That distinction is the whole bargain the game is built on, and it is
			// the one thing a player picking an act is actually choosing between.
			rows.AddChild(
				DoomPalette.Text(
					(
						band.IsOneFloor
							? $"FLOOR {band.FirstFloor}"
							: $"FLOORS {band.FirstFloor}-{band.LastFloor}"
					)
						+ $"   {DoomPalette.Caps(band.Doom.ToString())}"
						+ (permanent ? "   — rewrites your deck" : ""),
					17,
					permanent ? DoomPalette.Red : DoomPalette.Bone,
					HorizontalAlignment.Left
				)
			);

			var flavour = DoomPalette.Text(
				$"      {StarterContent.DescriptionFor(band.Doom)}",
				15,
				DoomPalette.Bone,
				HorizontalAlignment.Left
			);
			flavour.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			rows.AddChild(flavour);
		}

		var choose = new Button { Text = "WALK INTO IT", CustomMinimumSize = new Vector2(0, 60) };
		choose.AddThemeFontSizeOverride("font_size", 22);

		var picked = theme.Theme;
		choose.Pressed += () => onPick(picked);
		rows.AddChild(choose);

		return panel;
	}
}
