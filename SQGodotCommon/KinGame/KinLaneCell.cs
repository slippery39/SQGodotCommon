using KinCore;
using Godot;

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
	private readonly Label _attack;
	private readonly Label _life;

	// A MarginContainer, not a PanelContainer: the latter paints the theme's default panel, which
	// put a dark plate behind every attack number on the board. The number wants no background at
	// all — the sword beside it is what marks it.
	private readonly MarginContainer _attackPip;
	private readonly TextureRect _attackIcon;
	private readonly PanelContainer _lifePip;
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

		pips.AddChild(_attackIcon);
		pips.AddChild(_attackPip);
		pips.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		pips.AddChild(_lifePip);
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
		_attack.Text = attacking ? enemy.IntentAmount.ToString() : "—";

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

		_name.Text = card.Name;
		Stand(
			card.Name,
			KinArt.Figure(isCompanion ? KinPalette.Gold : KinPalette.Bone, hostile: false),
			KinArt.ColourFor(card.Name)
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
			isCompanion ? KinPalette.Gold : KinPalette.Bone
		);

		Style(
			KinPalette.Slate,
			isCompanion ? KinPalette.Gold : KinPalette.Bone,
			isCompanion ? 3 : 2
		);
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

		_figure.StretchMode =
			drawing is null
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
