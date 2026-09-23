using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;

namespace KinGame;

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
public sealed class KinLaneCell
{
	// **SQUARE, and that is the whole point of the shape.** Generated art arrives 1024x1024, so a
	// square slot takes it edge to edge with no crop, no letterbox and no transparency pass — which
	// is why this shape was chosen over the card's 0.70 portrait. Card ratio at the old width
	// measured 493px OVER the 1080 canvas (two rows is what makes portrait so expensive); square at
	// 175 fits with no other band touched.
	//
	// It was 296x156, a landscape strip, back when every drawing was an authored transparent
	// silhouette standing on a plinth. An opaque photo in that strip read as a postage stamp stuck
	// on a tile, which is what sent the shape square.
	public const int Width = 175;
	public const int Height = 175;

	/// <summary>How wide the five lanes plus their gaps come to — the board's content column.</summary>
	public const int RowWidth = (Width * KinBattle.LaneCount) + (Gap * (KinBattle.LaneCount - 1));
	public const int Gap = 12;

	public PanelContainer Root { get; }

	private readonly Label _name;
	private readonly TextureRect _figure;

	/// <summary>
	/// Sits UNDER the figure and exists only for the transparent fallback silhouette.
	///
	/// **Not painted onto `Root`'s stylebox, and that is deliberate.** `Style()` runs after
	/// `Stand()` in both ShowEnemy and ShowUnit and writes that same stylebox for the border — so a
	/// ground set there is silently overwritten every single time, leaving the fallback figure
	/// floating on slate with no error anywhere.
	/// </summary>
	private readonly ColorRect _ground;

	/// <summary>Dark strips behind the name and the pips, so text stays readable over any art.</summary>
	private readonly ColorRect _topScrim;
	private readonly ColorRect _bottomScrim;

	/// <summary>
	/// **The keywords that decide a matchup — FLIER, THORNS 6, STRIKES TWICE — one per line, under
	/// the name.** Enemies had no text on the board at all, so a Flier and a Wretch looked
	/// identical and a counter to either was luck. KinUI.md: a fact needed to choose a lane is on
	/// the board, never only on hover.
	///
	/// One line per trait rather than one joined line, because two traits on one body do not fit
	/// 175px and the rule is that nothing carrying a number is ever dropped.
	/// </summary>
	private readonly Label _traits;
	private readonly ColorRect _traitScrim;
	private const int TraitLine = 24;
	private readonly Label _attack;
	private readonly Label _life;

	// A MarginContainer, not a PanelContainer: the latter paints the theme's default panel, which
	// put a dark plate behind every attack number on the board. The number wants no background at
	// all — the sword beside it is what marks it.
	private readonly MarginContainer _attackPip;
	private readonly TextureRect _attackIcon;
	private readonly PanelContainer _lifePip;

	/// <summary>
	/// **The companion's GUARD: a shield and a number, mirroring the sword and attack on the left.**
	/// The first build reused the red toughness disc, so "Guard 16" read as "toughness 16" and
	/// nothing on the companion looked different from a unit (playtest: "it felt exactly the
	/// same"). The second wrote the word GUARD on the pip and the cell clipped it to "GUARD 1" — a
	/// wrong number, which is worse than an ugly one. An icon never outgrows the slot.
	/// </summary>
	private readonly HBoxContainer _guardPip;
	private readonly Label _guard;

	/// <summary>Where a number about the companion's Guard floats up from.</summary>
	public Control GuardAnchor => _guardPip;
	private readonly Label _telegraph; // null on the row that never shows one

	public KinLaneCell(bool showsTelegraph)
	{
		// Near-SQUARE and a fixed size, not ExpandFill. Letting the container divide the viewport
		// gave five 363x132 letterboxes — a lane that wide reads as a row of banners rather than as
		// a slot something stands in, and it is nothing like the reference.
		Root = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(Width, Height),
		};

		// **A plain Control, not a Container, and that is load-bearing.** A Container rewrites its
		// children's anchors on every layout pass, so the art could not be anchored full-rect
		// underneath anchored overlays. PanelContainer sizes THIS to fill, and because a bare
		// Control lays nothing out, everything inside it keeps the anchors it was given.
		var inner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		inner.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		inner.ClipContents = true;

		// CLIPPED, or the lane is not actually a fixed size. CustomMinimumSize is a MINIMUM: a label
		// wider than the slot drags the whole row out of line, and the companion's name grows with
		// every apocalypse it survives ("Ash — Barnacled, Glowing"), so this gets worse as a run
		// goes on. The intermission shows the full name where there is room for it.
		_ground = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		inner.AddChild(_ground);

		// THE ART, ON TOP OF THE GROUND AND UNDER EVERY OVERLAY. `KeepAspectCovered` is what makes a square
		// drawing fill a square slot exactly; `inner.ClipContents` catches anything that is not
		// square rather than letting it bleed over the border.
		_figure = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_figure.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		inner.AddChild(_figure);

		_topScrim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
		_topScrim.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_topScrim.OffsetBottom = 30;
		inner.AddChild(_topScrim);

		_bottomScrim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
		_bottomScrim.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		_bottomScrim.OffsetTop = -38;
		inner.AddChild(_bottomScrim);

		_traitScrim = new ColorRect { Color = new Color(0, 0, 0, 0.62f), Visible = false };
		_traitScrim.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_traitScrim.OffsetTop = 30;
		inner.AddChild(_traitScrim);

		// 20 canvas px is ~16.7 real at 1600x900 — the floor for anything a player must read.
		_traits = KinPalette.Text("", 20, KinPalette.Gold);
		_traits.Visible = false;
		_traits.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_traits.OffsetLeft = 6;
		_traits.OffsetRight = -6;
		_traits.OffsetTop = 30;
		inner.AddChild(_traits);

		_name = KinPalette.Text("", 19, KinPalette.Bone);
		_name.ClipText = true;
		_name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		_name.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_name.OffsetLeft = 6;
		_name.OffsetRight = -6;
		_name.OffsetBottom = 30;
		inner.AddChild(_name);

		// Attack on the left, life on the right, in every lane and on every card — and now in the
		// same two SHAPES as well. A sword with a bare number for what it deals, a red disc for what
		// it has left. Two identical pips made the eye stop and read both to tell them apart, which
		// is the one thing a five-lane board cannot afford.
		var pips = new HBoxContainer();
		pips.AddThemeConstantOverride("separation", 6);

		_attackIcon = new TextureRect
		{
			Texture = KinArt.AttackIcon,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(24, 24),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};

		_attackPip = new MarginContainer();
		_attack = KinPalette.Text("", 22, KinPalette.Bone);
		_attackPip.AddChild(_attack);

		(_lifePip, _life) = KinPalette.Pip(KinPalette.Red, 22);

		_guardPip = new HBoxContainer
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_guardPip.AddThemeConstantOverride("separation", 4);
		_guardPip.AddChild(
			new TextureRect
			{
				Texture = KinArt.GuardIcon,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(24, 24),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			}
		);
		_guard = KinPalette.Text("", 22, KinPalette.Bone);
		_guardPip.AddChild(_guard);

		pips.AddChild(_attackIcon);
		pips.AddChild(_attackPip);
		pips.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		pips.AddChild(_lifePip);
		pips.AddChild(_guardPip);
		pips.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		pips.OffsetLeft = 6;
		pips.OffsetRight = -6;
		pips.OffsetTop = -36;
		inner.AddChild(pips);

		// Created ONLY for the row that can show one. A Label built and never added to the tree is
		// an orphaned node, and five of them leaked at exit — Godot reports it as
		// "ObjectDB instances leaked", which names nothing and is easy to blame on the engine.
		if (showsTelegraph)
		{
			_telegraph = KinPalette.Text("INCOMING", 17, KinPalette.Gold);
			_telegraph.Visible = false;
			_telegraph.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
			_telegraph.OffsetLeft = -70;
			_telegraph.OffsetRight = 70;
			_telegraph.OffsetTop = Height / 2 - 12;
			inner.AddChild(_telegraph);
		}

		Root.AddChild(inner);

		ShowEmpty();
	}

	/// <summary>An empty lane: a recessed socket, and nothing to read.</summary>
	public void ShowEmpty()
	{
		ShowTraits([]);
		_guardPip.Visible = false;
		_name.Text = "";
		_figure.Texture = null;
		_ground.Visible = false;
		_topScrim.Visible = false;
		_bottomScrim.Visible = false;
		_attackPip.Visible = false;
		_attackIcon.Visible = false;
		_lifePip.Visible = false;

		// A dashed outline over nothing, rather than a filled panel in a darker shade. An empty lane
		// is a different KIND of thing from a held one — a socket, not a quieter body.
		var dashed = new StyleBoxTexture { Texture = KinArt.DashedSlot(Width, Height) };
		Root.AddThemeStyleboxOverride("panel", dashed);
	}

	public void ShowEnemy(Enemy enemy)
	{
		ShowTraits(KinRulesText.Traits(enemy));
		_guardPip.Visible = false;
		_name.Text = enemy.Name;
		// **One plinth colour for every enemy.** Your cards carry a per-name hue because a deck is
		// something you build and recognise; the enemy row is a wall of threats and giving each one
		// its own colour made it read as a second hand of cards.
		Stand(enemy.Name, KinArt.Figure(KinPalette.Navy, hostile: true), KinArt.EnemyGround);

		// The telegraphed intent, not a guess.
		//
		// **A waiting enemy shows that it is waiting.** It used to render no pip at all, which is
		// indistinguishable from a lane that failed to draw — in a game whose stated rule is "do
		// not hide an intent" (see Enemy.cs) and whose whole theme is that certainty is permission
		// to show the player everything. An absent number is not the same as a known nothing.
		var attacking = enemy.Intent == IntentKind.Attack;
		_attackPip.Visible = true;
		_attackIcon.Visible = true;
		// **"6×2", not "6", for a double-striker** — the pip is the number read across the board at
		// speed, and 6 on a Flail Knight that lands 12 is the wrong number to read at a glance. Both
		// facts are shown as they are; nothing is multiplied here, so the UI still computes none.
		_attack.Text = attacking ? Swing(enemy.IntentAmount, enemy.Strikes) : "—";

		// A waiting enemy keeps its sword but greys it, so the lane still says "this one deals
		// damage, just not this turn" rather than going silent.
		_attackIcon.Modulate = attacking ? Colors.White : new Color(1, 1, 1, 0.35f);

		_lifePip.Visible = true;
		_life.Text = enemy.Health.ToString();

		Style(KinPalette.Slate, KinPalette.Red, 2);
	}

	public void ShowUnit(KinCard card, bool isCompanion)
	{
		var unit = card.Unit();

		ShowTraits(KinRulesText.Traits(card));
		_name.Text = card.Name;
		Stand(
			card.Name,
			KinArt.Figure(isCompanion ? KinPalette.Gold : KinPalette.Bone, hostile: false),
			KinArt.ColourFor(card.Name)
		);

		_attackPip.Visible = true;
		_attackIcon.Visible = true;
		_attackIcon.Modulate = Colors.White;
		_attack.Text = Swing(unit.Power, unit.Strikes);

		// REMAINING toughness, not printed toughness — a body in a lane is worth what it has left.
		// **The companion shows its GUARD instead**, on its own bone pip: it has no body to wear
		// down, and what matters this turn is how much of the attack in its lane it will soak.
		_lifePip.Visible = !isCompanion;
		_guardPip.Visible = isCompanion;
		_life.Text = unit.RemainingToughness.ToString();
		_guard.Text = unit.Guard.ToString();

		_name.AddThemeColorOverride("font_color", isCompanion ? KinPalette.Gold : KinPalette.Bone);

		Style(
			KinPalette.Slate,
			isCompanion ? KinPalette.Gold : KinPalette.Bone,
			isCompanion ? 3 : 2
		);
	}

	private static string Swing(int amount, int strikes) =>
		strikes > 1 ? $"{amount}×{strikes}" : amount.ToString();

	/// <summary>Read from KinCore's own account of the body, so the lane and the card cannot disagree.</summary>
	private void ShowTraits(IEnumerable<string> traits)
	{
		var lines = traits.Select(t => t.ToUpperInvariant()).ToList();

		_traits.Text = string.Join("\n", lines);
		_traits.Visible = lines.Count > 0;
		_traitScrim.Visible = lines.Count > 0;
		_traitScrim.OffsetBottom = 30 + TraitLine * lines.Count + 4;
		_traits.OffsetBottom = 30 + TraitLine * lines.Count + 4;
	}

	/// <summary>The Opponent has announced a body for this lane. One marker; it carries no stats.</summary>
	public void SetTelegraph(bool incoming)
	{
		if (_telegraph is not null)
			_telegraph.Visible = incoming;
	}

	/// <summary>
	/// Puts a body in the lane: its authored drawing if one exists, else the generated silhouette.
	///
	/// **THE TWO CASES ARE FRAMED DIFFERENTLY, and skipping that is the whole trap.** A real drawing
	/// is an opaque square and FILLS the slot — covered, edge to edge, no ground behind it because
	/// none of it would ever show. A fallback silhouette is a transparent figure, so covering with
	/// it would crop a shape that is mostly empty space into a meaningless corner; it is CENTRED
	/// over the lane's own colour instead, exactly as it was before this slot went square.
	///
	/// Treating both the same looks fine on whichever case you happen to test and broken on the
	/// other — and the pool grows faster than the drawings do, so the fallback is the common case
	/// for a long time yet.
	/// </summary>
	private void Stand(string subject, Texture2D fallback, Color ground)
	{
		var drawing = KinArt.Drawing(subject);

		_topScrim.Visible = true;
		_bottomScrim.Visible = true;
		_figure.Texture = drawing ?? fallback;

		_figure.StretchMode = drawing is null
			? TextureRect.StretchModeEnum.KeepAspectCentered
			: TextureRect.StretchModeEnum.KeepAspectCovered;

		// The ground only exists to sit behind a transparent fallback. A filled drawing hides it
		// completely, so painting it there would be dead pixels.
		_ground.Visible = drawing is null;
		_ground.Color = ground;
	}

	private void Style(Color fill, Color border, int width) =>
		Root.AddThemeStyleboxOverride("panel", KinPalette.Box(fill, border, width));
}
