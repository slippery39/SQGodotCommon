using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE RUN's screens, over the battle board** (KinJam.md "THE RUN"): choose a starter, between
/// battles (HP, who revived, who joined, the rest, a reward), and the run's end. Presentation only —
/// every fact comes from `PartyRun` and its `RunReport`.
///
/// **Built of Buttons, and the board hides the hand while it shows.** Card hover is physics picking
/// and the fan's cards carry their own ZIndex, so a hand left visible draws over any overlay.
/// </summary>
public sealed partial class KinPartyRunScreens
{
	private readonly ColorRect _root;
	private readonly TextureRect _scene;
	private readonly VBoxContainer _column;

	public bool IsShowing => _root.Visible;

	/// <summary>What each starter WANTS from the board — the one line that tells them apart.</summary>
	private static readonly Dictionary<string, string> Wants =
		new()
		{
			["Bramble"] = "Wants to be HIT",
			["Pike"] = "Wants never to be where the hit lands",
			["Gale"] = "Wants the FOES where it chooses",
		};

	public KinPartyRunScreens(Node parent)
	{
		_root = new ColorRect { Color = KinPalette.Navy, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop; // nothing under it takes a click
		parent.AddChild(_root);

		// **A PLACE behind every screen** (style D) — the town, the region's map, the title valley —
		// not the navy void every run screen used to be. `Art/backdrops/<scene>.png`, dimmed so the
		// kit's plates read over it; navy stays underneath for a scene not drawn yet.
		_scene = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Modulate = new Color(0.55f, 0.58f, 0.64f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_scene.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_scene);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(centre);

		_column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_column.AddThemeConstantOverride("separation", 18);
		centre.AddChild(_column);
	}

	public void Hide() => _root.Visible = false;

	// ===== The three screens

	public void ShowStarters(Action<PartyCompanion> choose)
	{
		Begin(
			"CHOOSE YOUR STARTER",
			"Your starter's FAMILY is the run's: the cards you are offered, and the monsters you can catch.",
			"title"
		);

		var row = Row();
		foreach (var companion in PartyContent.Roster)
			row.AddChild(
				Tile(
					KinPalette.Family(companion.Family, companion.Name),
					KinArt.Sprite(companion.Name) ?? KinArt.Drawing(companion.Name),
					companion.Name.ToUpperInvariant(),
					[
						$"{companion.Family.ToString().ToUpperInvariant()} FAMILY",
						Wants.GetValueOrDefault(companion.Name, ""),
						companion.Passive,
						companion.PassiveRule,
						companion.Abilities.OfType<FirstAttack>().FirstOrDefault() is { } bonus
							? $"First attack each turn: {bonus.Text}."
							: "",
						$"HP {companion.Hp} · POW {companion.Power}",
					],
					new Vector2(340, 640),
					() => choose(companion)
				)
			);
	}

	public void ShowBetween(
		PartyRun run,
		RunReport report,
		string beaten,
		Action<KinCard> take,
		Action skip
	)
	{
		Begin(
			$"VICTORY — {beaten.ToUpperInvariant()}",
			$"+{report.Gold} gold.   Next: {Ahead(run)}"
		);

		var news = new List<string>();
		if (report.Relic is { } relic)
			news.Add($"RELIC: {PartyRelics.Name(relic)} — {PartyRelics.Text(relic)}");
		foreach (var name in report.Revived)
			news.Add($"{name} was knocked out, and is back at 1 HP.");
		foreach (var line in news)
			_column.AddChild(Label(line, 24, KinPalette.Gold));

		ShowTeam(run);

		_column.AddChild(Label("TAKE A CARD", 28, KinPalette.Bone));
		var row = Row();
		foreach (var reward in run.RewardOffer())
			row.AddChild(
				Tile(
					KinPalette.Family(reward.Family),
					CardArt(reward.Name),
					$"{reward.Name.ToUpperInvariant()}  ({reward.Cost})",
					[KinCardFace.Tag(reward), string.Join(" ", KinRulesText.Lines(reward))],
					new Vector2(280, 430),
					() => take(reward)
				)
			);

		var buttons = Row();
		buttons.AddChild(Button("SKIP", skip));
	}

	/// <summary>**A boss beaten: three BOSS RELICS — keep one for the rest of the run.**</summary>
	public void ShowRelicChoice(PartyRun run, string beaten, Action<Relic> choose, Action skip)
	{
		Begin(
			$"{beaten.ToUpperInvariant()} IS BEATEN",
			"Choose a BOSS RELIC to keep for the rest of the run. The team is healed in full."
		);
		var row = Row();
		foreach (var relic in run.RelicChoice)
			row.AddChild(
				Tile(
					KinPalette.Slate,
					null,
					PartyRelics.Name(relic).ToUpperInvariant(),
					[PartyRelics.Text(relic)],
					new Vector2(320, 240),
					() => choose(relic)
				)
			);
		Row().AddChild(Button("SKIP", skip));
	}

	/// <summary>
	/// **A boss beaten: three MONSTERS of your family — one joins the team** (round 4: monsters come
	/// from bosses; the first two bosses offer them).
	/// </summary>
	public void ShowMonsterChoice(PartyRun run, Action<PartyCompanion> choose, Action skip)
	{
		Begin(
			"A MONSTER JOINS YOU",
			"Choose one to join the team for the rest of the run. Its passive shapes the cards you want."
		);
		var row = Row();
		foreach (var monster in run.MonsterChoice)
			row.AddChild(
				Tile(
					KinPalette.Family(monster.Family, monster.Name),
					KinArt.Sprite(monster.Name) ?? KinArt.Drawing(monster.Name),
					monster.Name.ToUpperInvariant(),
					[
						$"{monster.Family.ToString().ToUpperInvariant()} FAMILY",
						monster.Passive,
						monster.PassiveRule,
						$"HP {monster.Hp} · POW {monster.Power}",
					],
					new Vector2(340, 560),
					() => choose(monster)
				)
			);
		Row().AddChild(Button("SKIP", skip));
	}

	public void ShowOver(PartyRun run, Action newRun, Action menu)
	{
		Begin(
			run.IsWon ? "THE RUN IS WON" : "DEFEAT",
			run.IsWon
				? $"All {run.Regions.Count} bosses beaten, with {run.Team.Count} monsters to your name."
				: $"The team fell in {run.Region.Name} — region {run.RegionIndex + 1} of {run.Regions.Count}."
		);

		var buttons = Row();
		buttons.AddChild(Button("NEW RUN", newRun));
		buttons.AddChild(Button("MENU", menu));
	}

	/// <summary>**The team, as one line of HP**, and the relics held.</summary>
	private void ShowTeam(PartyRun run)
	{
		_column.AddChild(Label($"TEAM     GOLD {run.Gold}", 20, new Color(KinPalette.Bone, 0.8f)));

		// The RELICS held, by name — each one's rule is on the victory screen that paid it.
		if (!run.Relics.IsEmpty)
			_column.AddChild(
				Label(
					"RELICS: " + string.Join(", ", run.Relics.Select(PartyRelics.Name)),
					20,
					KinPalette.Gold
				)
			);

		var team = Row();
		foreach (var member in run.Team)
			team.AddChild(Monster(member));
	}

	private static Button Monster(RunCompanion m)
	{
		var button = Button($"{m.Companion.Name.ToUpperInvariant()}\n{m.Hp}/{m.MaxHp}", () => { });
		var tint = KinPalette.Family(m.Companion.Family, m.Companion.Name).Lightened(0.35f);
		KinUiKit.Style(button, 22);
		button.AddThemeStyleboxOverride("normal", KinUiKit.Plate("bone", tint));
		button.AddThemeStyleboxOverride("hover", KinUiKit.Plate("gold", tint.Lightened(0.15f)));
		// PRESSED is the picked team member in a swap: it must stay unmistakably lit.
		button.AddThemeStyleboxOverride("pressed", KinUiKit.Plate("gold", KinPalette.Gold));
		button.Icon = KinCardKit.Medallion(m.Companion.Name);
		button.CustomMinimumSize = new Vector2(260, 88);
		return button;
	}

	// ===== Pieces


	private void Begin(string title, string subtitle, string scene = "greenwood")
	{
		foreach (var child in _column.GetChildren())
			child.QueueFree();
		_scene.Texture = KinArt.RegionBackdrop(scene) ?? KinArt.RegionBackdrop("greenwood");
		_column.AddChild(Label(title, 56, KinPalette.Gold));
		_column.AddChild(Label(subtitle, 24, KinPalette.Bone));
		_root.Visible = true;
	}

	private HBoxContainer Row()
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 24);
		_column.AddChild(row);
		return row;
	}

	private static Label Label(string text, int size, Color colour)
	{
		var label = KinPalette.Text(text, size, colour);
		label.LabelSettings = new LabelSettings
		{
			FontSize = size,
			FontColor = colour,
			OutlineSize = size >= 40 ? 10 : 6,
			OutlineColor = new Color(0.04f, 0.06f, 0.09f),
			ShadowSize = 4,
			ShadowColor = new Color(0, 0, 0, 0.6f),
			ShadowOffset = new Vector2(2, 3),
		};
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		return label;
	}

	/// <summary>A card's action illustration for a tile, or its old subject art.</summary>
	private static Texture2D CardArt(string card) =>
		KinCardKit.Illustration(card, 172) ?? KinArt.Drawing(card);

	private static Button Button(string text, Action pressed)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(220, 60) };
		KinUiKit.Style(button, 24);
		button.Pressed += pressed;
		return button;
	}

	/// <summary>
	/// A big clickable tile: the companion's colour, its portrait, a title and a few lines. A Button
	/// with ignoring children, so the whole tile takes the click.
	/// </summary>
	private static Button Tile(
		Color fill,
		Texture2D art,
		string title,
		string[] lines,
		Vector2 size,
		Action pressed
	)
	{
		// The kit's bevelled plate, TINTED toward the tile's colour (a starter's own), so a tile is a
		// material and still says whose it is. Slate means "no colour of its own": left untinted.
		var tint = fill == KinPalette.Slate ? Colors.White : fill.Lightened(0.35f);
		var tile = new Button { CustomMinimumSize = size };
		tile.AddThemeStyleboxOverride("normal", KinUiKit.Plate("bone", tint));
		tile.AddThemeStyleboxOverride("hover", KinUiKit.Plate("gold", tint.Lightened(0.15f)));
		tile.AddThemeStyleboxOverride("pressed", KinUiKit.Plate("gold", tint.Darkened(0.1f)));
		tile.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		// A tile you cannot take (a shop card you cannot afford) is dimmed, not Godot's dark default.
		tile.AddThemeStyleboxOverride(
			"disabled",
			KinUiKit.Plate("bone", new Color(0.5f, 0.5f, 0.55f, 0.8f))
		);
		tile.Pressed += pressed;

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.OffsetLeft = 12;
		column.OffsetRight = -12;
		column.OffsetTop = 10;
		column.OffsetBottom = -10;
		column.AddThemeConstantOverride("separation", 6);
		tile.AddChild(column);

		// Every line wraps, the title included: an unwrapped label's minimum width is its whole line,
		// and "WHIRLING STRIKE (2)" dragged the column wider than the tile.
		var heading = Label(title, 26, KinPalette.Bone);
		heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		heading.CustomMinimumSize = new Vector2(1, 0);
		column.AddChild(heading);
		// No art, no art gap: an empty window pushed a shop tile's text out of its bottom.
		if (art is not null)
			column.AddChild(
				new TextureRect
				{
					Texture = art,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					// A tile with little to say gives its art the room (the gym's foes were a small figure over an
					// empty half tile).
					CustomMinimumSize = new Vector2(
						0,
						Mathf.Min(size.Y * (lines.Length <= 1 ? 0.6f : 0.4f), 220)
					),
					MouseFilter = Control.MouseFilterEnum.Ignore,
				}
			);
		foreach (var line in lines.Where(l => l.Length > 0))
		{
			var label = Label(line, 20, KinPalette.Bone);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(1, 0);
			column.AddChild(label);
		}

		return tile;
	}
}
