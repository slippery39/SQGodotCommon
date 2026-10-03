using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;
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

	public KinPartyRunScreens(Node parent)
	{
		_root = new ColorRect { Color = KinPalette.Navy, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop; // nothing under it takes a click
		// Hover any symbol on these screens for what it means (Shayne, 2026-10-01).
		_root.Theme = KinSymbols.TooltipTheme();
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

	/// <summary>
	/// **Choose the FAMILY** — each shown by its first monster for now; the trio and its REROLL come
	/// with the family screen (`KinJam.md`, top: the run's new shape).
	/// </summary>
	public void ShowStarters(Action<Family> choose)
	{
		Begin("CHOOSE YOUR FAMILY", "", "title");

		var row = Row();
		foreach (var family in PartyContent.Families)
		{
			var face = PartyContent.PoolOf(family)[0];
			row.AddChild(MonsterTile(face, new Vector2(340, 480), () => choose(family)));
		}
	}

	public void ShowBetween(
		PartyRun run,
		RunReport report,
		string beaten,
		Action<KinCard> take,
		Action skip
	)
	{
		Begin($"VICTORY — {beaten.ToUpperInvariant()}", $"+{report.Gold} GOLD");

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
			row.AddChild(CardButton(reward, () => take(reward), Rarity(reward)));

		var buttons = Row();
		buttons.AddChild(Button("SKIP", skip));
	}

	/// <summary>**A boss beaten: three BOSS RELICS — keep one for the rest of the run.**</summary>
	public void ShowRelicChoice(PartyRun run, string beaten, Action<Relic> choose, Action skip)
	{
		Begin(
			$"{beaten.ToUpperInvariant()} IS BEATEN",
			$"The team heals {(int)(PartyRun.BossHeal * 100)}%."
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
	/// **A boss beaten: EVOLVE one of your monsters** (round 5) — each that can, shown as the form it
	/// becomes. No skip: an evolution is never worse.
	/// </summary>
	public void ShowEvolution(PartyRun run, Action<int> evolve)
	{
		Begin("EVOLVE ONE", "");
		var row = Row();
		foreach (var index in run.Evolvable)
			row.AddChild(
				MonsterTile(
					run.Team[index].Companion.EvolvesInto,
					new Vector2(340, 480),
					() => evolve(index)
				)
			);
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
	/// <summary>The gold, relics and team — front first. With `toFront`, pressing a monster sends it there.</summary>
	private void ShowTeam(PartyRun run, Action<int> toFront = null)
	{
		_column.AddChild(Label($"GOLD {run.Gold}", 20, new Color(KinPalette.Bone, 0.8f)));

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
		for (var i = 0; i < run.Team.Count; i++)
		{
			var index = i;
			var tile = Monster(run.Team[i], () => toFront?.Invoke(index));
			if (toFront is not null)
				tile.TooltipText = i == 0 ? "The front: foes hit it first." : "Send to the front.";
			team.AddChild(tile);
		}
	}

	private static Button Monster(RunCompanion m, Action pressed)
	{
		var button = Button($"{m.Companion.Name.ToUpperInvariant()}\n{m.Hp}/{m.MaxHp}", pressed);
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
		if (subtitle.Length > 0)
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

	/// <summary>
	/// **A row that scrolls sideways** when its tiles outgrow the screen — the spring's upgrades ran off
	/// it once a deck had grown (playtest, 2026-09-30). Centred while they fit.
	/// </summary>
	private HBoxContainer ScrollRow(float height)
	{
		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(1700, height + 24),
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_column.AddChild(scroll);
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 24);
		scroll.AddChild(row);
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
	/// **A card AS ITSELF** — the hand's own face (`KinCardFace`), drawn once into an off-screen
	/// viewport and shown as a picture (the declutter pass, 2026-09-30: one look everywhere). The
	/// shared card listens for hover and drag through its own collision area; as a picture it takes
	/// no input, so the button alone takes the click.
	/// </summary>
	private static Button CardButton(KinCard card, Action pressed, string footer = "")
	{
		var size = new Vector2(250, 360);
		var button = new Button
		{
			CustomMinimumSize = size + new Vector2(0, footer.Length > 0 ? 36 : 0),
		};
		var clear = new StyleBoxEmpty();
		button.AddThemeStyleboxOverride("normal", clear);
		button.AddThemeStyleboxOverride("focus", clear);
		button.AddThemeStyleboxOverride("disabled", clear);
		button.AddThemeStyleboxOverride("hover", KinUiKit.Plate("gold", new Color(1, 1, 1, 0.35f)));
		button.AddThemeStyleboxOverride("pressed", KinUiKit.Plate("gold", KinPalette.Gold));
		button.Pressed += pressed;
		button.TooltipText = KinSymbols.PlainTip(card);

		var view = new SubViewport
		{
			Size = new Vector2I((int)size.X, (int)size.Y),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		button.AddChild(view);
		var ui = GD.Load<PackedScene>("res://Common/Cards/2D/Card2D/card_2d_canvasgroup.tscn")
			.Instantiate<CardUI2D>();
		ui.Position = size / 2;
		ui.Scale *= 1.15f;
		// **Filled once it is READY, not when it is made.** The button is built before the screen adds
		// it, so the card is not in the tree yet; the shared card finds its own parts in `_Ready`, and
		// `ApplyTo` before that threw — the whole row and the SKIP after it vanished (capture).
		ui.Ready += () =>
		{
			KinCardFace.Style(ui);
			ui.ApplyTo(KinCardFace.For(card));
			KinCardFace.ApplyStats(ui, card);
		};
		view.AddChild(ui);

		button.AddChild(
			new TextureRect
			{
				Texture = view.GetTexture(),
				Size = size,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			}
		);
		if (footer.Length > 0)
		{
			var label = Label(footer, 22, KinPalette.Gold);
			label.Position = new Vector2(0, size.Y);
			label.Size = new Vector2(size.X, 34);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			button.AddChild(label);
		}
		return button;
	}

	/// <summary>Uncommon and rare said under a card where it is chosen; nothing for a common.</summary>
	private static string Rarity(KinCard card) =>
		card.Rarity == KinCore.Party.Rarity.Common ? "" : card.Rarity.ToString().ToUpperInvariant();

	/// <summary>
	/// **A monster to choose** — its art, its passive (a star, its name, its one-line rule), its
	/// first-attack bonus and its stats as symbols. No "GROVE FAMILY": the tile is in its colour.
	/// </summary>
	private static Button MonsterTile(PartyCompanion monster, Vector2 size, Action pressed)
	{
		var bonus = monster.Abilities.OfType<FirstAttack>().FirstOrDefault();
		var (bonusText, bonusIcon, bonusTint) = bonus is null
			? ("", null, KinPalette.Bone)
			: KinMoveText.BonusSymbol(bonus);
		return Tile(
			KinPalette.Family(monster.Family, monster.Name),
			KinArt.Sprite(monster.Name) ?? KinArt.Drawing(monster.Name),
			monster.Name.ToUpperInvariant(),
			[monster.PassiveRule],
			size,
			pressed,
			[
				[
					new Chip(
						KinArt.PassiveIcon,
						monster.Passive,
						KinPalette.Gold,
						$"{KinSymbols.Passive.Line} {monster.Passive}: {monster.PassiveRule}"
					),
				],
				bonusIcon is null
					? []
					:
					[
						new Chip(
							KinArt.AttackIcon,
							"",
							KinPalette.Gold,
							KinSymbols.FirstAttack.Line
						),
						new Chip(bonusIcon, bonusText, bonusTint, $"First attack: {bonus!.Text}."),
					],
				[
					new Chip(
						KinArt.LifeIcon,
						$"{monster.Hp}",
						KinPalette.Red.Lightened(0.3f),
						$"Health {monster.Hp}: {KinSymbols.Health.Meaning}"
					),
					new Chip(
						KinArt.PowerIcon,
						$"{monster.Power}",
						KinPalette.Bone,
						$"Power {monster.Power}: {KinSymbols.Power.Meaning}"
					),
					.. monster.SpellPower > 0
						? new[]
						{
							new Chip(
								KinArt.SpellPowerIcon,
								$"{monster.SpellPower}",
								Color.FromHtml("#FF9A3C"),
								$"Spell Power {monster.SpellPower}: it adds this to your team's Spell Power."
							),
						}
						: [],
				],
			]
		);
	}

	/// <summary>One row of symbols with their numbers, centred — a tile's stats.</summary>
	private static HBoxContainer ChipRow(IEnumerable<Chip> chips)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 6);
		foreach (var chip in chips)
		{
			row.AddChild(
				new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Texture = chip.Icon,
					Modulate = chip.Tint,
					CustomMinimumSize = new Vector2(32, 32),
					// PASS, not Ignore: it shows its tip, and the click still reaches the tile.
					MouseFilter = Control.MouseFilterEnum.Pass,
					TooltipText = chip.Tip,
				}
			);
			if (chip.Text.Length > 0)
			{
				var number = Label(chip.Text, 24, KinPalette.Bone);
				number.MouseFilter = Control.MouseFilterEnum.Pass;
				number.TooltipText = chip.Tip;
				row.AddChild(number);
			}
		}
		return row;
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
		Action pressed,
		Chip[][] chipRows = null
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
		foreach (var chips in (chipRows ?? []).Where(r => r.Length > 0).Take(1))
			column.AddChild(ChipRow(chips));
		foreach (var line in lines.Where(l => l.Length > 0))
		{
			var label = Label(line, 20, KinPalette.Bone);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(1, 0);
			column.AddChild(label);
		}
		foreach (var chips in (chipRows ?? []).Where(r => r.Length > 0).Skip(1))
			column.AddChild(ChipRow(chips));

		return tile;
	}
}
