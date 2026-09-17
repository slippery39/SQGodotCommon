using DoomCore;
using Godot;

namespace DoomGame;

/// <summary>
/// One lane slot — a silhouette with an attack pip and a life pip, or an empty socket.
///
/// **The lane is the matchup and the matchup is the whole game**, so this is where a player's eye
/// lives for the entire battle. It reads as shapes and numbers rather than a sentence: you should
/// be able to take in five lanes at a glance without reading any of them.
///
/// Positional language is shared with the cards on purpose — cost top-left, life bottom-right — so
/// the same number means the same thing wherever it appears.
/// </summary>
public sealed class DoomLaneCell
{
	// **Sized to fill the frame.** At 188 the five lanes plus gaps came to 988 of the 1920 canvas,
	// so the board crowded into half the screen with a third of the window empty beside it. The
	// lane is where the eye lives for the whole battle; it gets the width.
	public const int Width = 296;
	public const int Height = 156;

	/// <summary>How wide the five lanes plus their gaps come to — the board's content column.</summary>
	public const int RowWidth = (Width * DoomBattle.LaneCount) + (Gap * (DoomBattle.LaneCount - 1));
	public const int Gap = 12;

	public PanelContainer Root { get; }

	private readonly Label _name;
	private readonly TextureRect _figure;
	private readonly Label _attack;
	private readonly Label _life;

	// A MarginContainer, not a PanelContainer: the latter paints the theme's default panel, which
	// put a dark plate behind every attack number on the board. The number wants no background at
	// all — the sword beside it is what marks it.
	private readonly MarginContainer _attackPip;
	private readonly TextureRect _attackIcon;
	private readonly PanelContainer _lifePip;
	private readonly Label _telegraph; // null on the row that never shows one

	public DoomLaneCell(bool showsTelegraph)
	{
		// Near-SQUARE and a fixed size, not ExpandFill. Letting the container divide the viewport
		// gave five 363x132 letterboxes — a lane that wide reads as a row of banners rather than as
		// a slot something stands in, and it is nothing like the reference.
		Root = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(Width, Height),
		};

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 2);

		// CLIPPED, or the lane is not actually a fixed size. CustomMinimumSize is a MINIMUM: a label
		// wider than the slot drags the whole row out of line, and the companion's name grows with
		// every apocalypse it survives ("Ash — Barnacled, Glowing"), so this gets worse as a run
		// goes on. The intermission shows the full name where there is room for it.
		_name = DoomPalette.Text("", 24, DoomPalette.Bone);
		_name.ClipText = true;
		_name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_name.CustomMinimumSize = new Vector2(Width - 16, 0);
		rows.AddChild(_name);

		_figure = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 84),
		};
		rows.AddChild(_figure);

		// Attack on the left, life on the right, in every lane and on every card — and now in the
		// same two SHAPES as well. A sword with a bare number for what it deals, a red disc for what
		// it has left. Two identical pips made the eye stop and read both to tell them apart, which
		// is the one thing a five-lane board cannot afford.
		var pips = new HBoxContainer();
		pips.AddThemeConstantOverride("separation", 6);

		_attackIcon = new TextureRect
		{
			Texture = DoomArt.AttackIcon,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(30, 30),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};

		_attackPip = new MarginContainer();
		_attack = DoomPalette.Text("", 26, DoomPalette.Bone);
		_attackPip.AddChild(_attack);

		(_lifePip, _life) = DoomPalette.Pip(DoomPalette.Red, 26);

		pips.AddChild(_attackIcon);
		pips.AddChild(_attackPip);
		pips.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		pips.AddChild(_lifePip);
		rows.AddChild(pips);

		// Created ONLY for the row that can show one. A Label built and never added to the tree is
		// an orphaned node, and five of them leaked at exit — Godot reports it as
		// "ObjectDB instances leaked", which names nothing and is easy to blame on the engine.
		if (showsTelegraph)
		{
			_telegraph = DoomPalette.Text("INCOMING", 20, DoomPalette.Gold);
			_telegraph.Visible = false;
			rows.AddChild(_telegraph);
		}

		Root.AddChild(rows);
		ShowEmpty();
	}

	/// <summary>An empty lane: a recessed socket, and nothing to read.</summary>
	public void ShowEmpty()
	{
		_name.Text = "";
		_figure.Texture = null;
		_attackPip.Visible = false;
		_attackIcon.Visible = false;
		_lifePip.Visible = false;

		// A dashed outline over nothing, rather than a filled panel in a darker shade. An empty lane
		// is a different KIND of thing from a held one — a socket, not a quieter body.
		var dashed = new StyleBoxTexture { Texture = DoomArt.DashedSlot(Width, Height) };
		Root.AddThemeStyleboxOverride("panel", dashed);
	}

	public void ShowEnemy(Enemy enemy)
	{
		_name.Text = enemy.Name;
		_figure.Texture = DoomArt.Figure(DoomPalette.Navy, hostile: true);

		// The telegraphed intent, not a guess.
		//
		// **A waiting enemy shows that it is waiting.** It used to render no pip at all, which is
		// indistinguishable from a lane that failed to draw — in a game whose stated rule is "do
		// not hide an intent" (see Enemy.cs) and whose whole theme is that certainty is permission
		// to show the player everything. An absent number is not the same as a known nothing.
		var attacking = enemy.Intent == IntentKind.Attack;
		_attackPip.Visible = true;
		_attackIcon.Visible = true;
		_attack.Text = attacking ? enemy.IntentAmount.ToString() : "—";

		// A waiting enemy keeps its sword but greys it, so the lane still says "this one deals
		// damage, just not this turn" rather than going silent.
		_attackIcon.Modulate = attacking ? Colors.White : new Color(1, 1, 1, 0.35f);

		_lifePip.Visible = true;
		_life.Text = enemy.Health.ToString();

		Style(DoomPalette.Slate, DoomPalette.Red, 2);
	}

	public void ShowUnit(DoomCard card, bool isCompanion)
	{
		var unit = card.Unit();

		_name.Text = card.Name;
		_figure.Texture = DoomArt.Figure(
			isCompanion ? DoomPalette.Gold : DoomPalette.Bone,
			hostile: false
		);

		_attackPip.Visible = true;
		_attackIcon.Visible = true;
		_attackIcon.Modulate = Colors.White;
		_attack.Text = unit.Power.ToString();

		// REMAINING toughness, not printed toughness — a body in a lane is worth what it has left.
		_lifePip.Visible = true;
		_life.Text = unit.RemainingToughness.ToString();

		_name.AddThemeColorOverride(
			"font_color",
			isCompanion ? DoomPalette.Gold : DoomPalette.Bone
		);

		Style(
			DoomPalette.Slate,
			isCompanion ? DoomPalette.Gold : DoomPalette.Bone,
			isCompanion ? 3 : 2
		);
	}

	/// <summary>The Opponent has announced a body for this lane. One marker; it carries no stats.</summary>
	public void SetTelegraph(bool incoming)
	{
		if (_telegraph is not null)
			_telegraph.Visible = incoming;
	}

	private void Style(Color fill, Color border, int width) =>
		Root.AddThemeStyleboxOverride("panel", DoomPalette.Box(fill, border, width));
}
