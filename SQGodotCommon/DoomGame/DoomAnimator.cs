using Godot;

namespace DoomGame;

/// <summary>
/// Every moving thing on the board, and the one dial that controls all of it.
///
/// **Nothing on this board may change without saying so.** A card game where the numbers simply
/// differ after you click is a game where the player reconstructs what happened instead of playing
/// it — see DoomUI.md, "Motion".
///
/// **An animation never decides anything.** It plays what the engine already resolved: the board
/// repaints from state first, and these run over the top of the finished picture. If a tween could
/// change what is true, the rule that the UI reads facts rather than computing them would already
/// be broken.
///
/// **This does NOT replay intermediate states**, deliberately. `Render` is a full repaint from
/// current state, so there is no half-resolved board to animate through and inventing one would
/// mean a second rules engine keeping its own account of the turn — the exact failure `DoomUI.md`
/// exists to prevent. What these do instead is mark WHAT CHANGED on the settled board: a number
/// floats off the thing it came out of, a lane that gained a body pops, a lane that lost one
/// flashes. That is the part a player actually reads.
/// </summary>
public static class DoomAnimator
{
	/// <summary>
	/// How fast the whole board moves. 1 is authored speed; 2 is twice as fast; **0 is instant**.
	///
	/// One multiplier for everything, because pacing is the thing most likely to need tuning late
	/// and the thing a player is most likely to want faster on a second run.
	/// </summary>
	public static float Speed { get; set; } = 1f;

	/// <summary>
	/// True when animation is off entirely.
	///
	/// **Zero means SKIPPED, not fast.** A queue that merely runs quickly is a flake generator — a
	/// 0.01s tween still takes frames, and whatever asserts after it races the tween. At zero
	/// nothing is scheduled at all, and the board is already correct because `Render` painted it
	/// before any of this ran.
	///
	/// **It does not stop the HAND, and cannot.** `Hand2D` runs its own fan tweens on hard-coded
	/// durations (0.2s to settle, 0.5s for a drawn card) and it is shared with the MTG card scenes,
	/// which this project is required to leave alone. Measured at speed 0: the floats, pops, flashes
	/// and the shake all stop dead, and the cards still slide into the fan for about a fifth of a
	/// second. That is the honest scope of this switch — do not claim the board is still.
	/// </summary>
	public static bool Instant => Speed <= 0f;

	private static double Seconds(double authored) => authored / Mathf.Max(Speed, 0.0001f);

	/// <summary>The settings key, so the speed survives a restart. See DoomBoard for the live toggle.</summary>
	public const string SettingsKey = "doom/animation_speed";

	/// <summary>
	/// Reads the configured speed once at boot. Absent or nonsense leaves the authored default —
	/// a bad config value must not make the game look frozen.
	/// </summary>
	public static void LoadConfiguredSpeed()
	{
		if (!ProjectSettings.HasSetting(SettingsKey))
			return;

		var configured = (float)ProjectSettings.GetSetting(SettingsKey, 1f);
		if (configured >= 0f && configured <= 8f)
			Speed = configured;
	}

	/// <summary>
	/// A thing arriving: it snaps in slightly oversized and settles. Used where a lane GAINED a
	/// body — a card you played, or an enemy the Opponent dropped in.
	/// </summary>
	public static void Pop(Control node)
	{
		if (node is null || !GodotObject.IsInstanceValid(node) || Instant)
			return;

		// Controls scale about their top-left unless told otherwise, which throws a popping cell
		// down and to the right instead of growing it in place.
		node.PivotOffset = node.Size / 2f;
		node.Scale = new Vector2(0.82f, 0.82f);

		node.CreateTween()
			.TweenProperty(node, "scale", Vector2.One, Seconds(0.25))
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
	}

	/// <summary>
	/// A thing leaving or being struck: one bright pulse, then back. Used where a lane LOST its
	/// body, and on a bar that just took damage.
	/// </summary>
	public static void Flash(CanvasItem node, Color tint)
	{
		if (node is null || !GodotObject.IsInstanceValid(node) || Instant)
			return;

		node.Modulate = tint;

		node.CreateTween()
			.TweenProperty(node, "modulate", Colors.White, Seconds(0.30))
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}

	/// <summary>
	/// The number that just moved, rising off the thing it came out of and fading.
	///
	/// **This is the single most valuable animation in a card game.** A health bar that drops from
	/// 44 to 38 tells you the result; a `-6` climbing off it tells you what happened, and it is the
	/// difference between a player who is following the game and one who is auditing it.
	///
	/// The label is parented to <paramref name="overlay"/> rather than to the thing it describes,
	/// so it can rise past that thing's bounds and cannot be clipped by a container.
	/// </summary>
	public static void Float(Control overlay, Control from, string text, Color colour)
	{
		if (overlay is null || from is null || Instant)
			return;

		if (!GodotObject.IsInstanceValid(overlay) || !GodotObject.IsInstanceValid(from))
			return;

		var label = DoomPalette.Text(text, 40, colour);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.ZIndex = 200;

		// Started ABOVE the thing it came out of, not on top of it. Centred on the health bar the
		// number sat directly over the bar's own "38 / 44" and the two were unreadable together.
		var centre = from.GetGlobalRect().GetCenter();
		label.Position = centre - new Vector2(60, 68);
		label.CustomMinimumSize = new Vector2(120, 0);
		overlay.AddChild(label);

		var tween = label.CreateTween();
		tween.SetParallel();
		tween
			.TweenProperty(label, "position", label.Position + new Vector2(0, -54), Seconds(0.85))
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "modulate:a", 0f, Seconds(0.85)).SetDelay(Seconds(0.25));

		// **QueueFree, and bound to the tween.** These spawn several times a turn for a whole run;
		// left behind they are an unbounded pile of Labels on top of the board.
		tween.Chain().TweenCallback(Callable.From(label.QueueFree));
	}

	/// <summary>
	/// A hit landing on YOU — the whole board jolts.
	///
	/// **It shakes the CanvasLayer, not a Control.** The obvious target was the status strip that
	/// holds your life, but every Control on this board is inside a container, and a container
	/// rewrites its children's positions on the next layout pass — so the tween fights the layout
	/// and the shake either does nothing or leaves the strip permanently off-centre. A CanvasLayer's
	/// `Offset` is owned by nobody, which makes it the one thing on this screen that can safely be
	/// moved. Shaking the whole screen is also the better read: losing life is the one counter that
	/// cannot be rebuilt, so it is worth the whole board flinching.
	/// </summary>
	public static void Shake(CanvasLayer layer, float pixels = 10f)
	{
		if (layer is null || !GodotObject.IsInstanceValid(layer) || Instant)
			return;

		var home = layer.Offset;
		var tween = layer.CreateTween();

		foreach (var offset in new[] { pixels, -pixels * 0.72f, pixels * 0.4f, 0f })
			tween.TweenProperty(layer, "offset", home + new Vector2(offset, 0), Seconds(0.05));

		// Snapped home rather than trusted to land there. A shake interrupted by the next render
		// leaves the board permanently off-centre, and it only shows after several turns.
		tween.TweenCallback(Callable.From(() => layer.Offset = home));
	}
}
