using Godot;

namespace DoomGame;

/// <summary>
/// The doom clock, as a ring of segments — one per turn of the countdown, going dark as they are
/// spent, with the turns remaining in the middle.
///
/// **This is the one number the whole game is about**, and the banner said it in words at the end
/// of a sentence: "CIVIL UNREST — 4 TURNS". Words are read; a ring emptying is seen. The mockup puts
/// it in the banner's top-right corner and it is the only thing up there, which is the right weight
/// for it.
///
/// **It is a picture of state, not a second copy of it.** It draws `CountdownRemaining` out of
/// `CountdownTotal` and computes nothing — the UI reads facts, it does not derive them (DoomUI.md).
/// Note that the total is the RELOAD length, not a maximum: the doom is a metronome, so the ring
/// refills every time it fires rather than running out once.
/// </summary>
public sealed partial class DoomClockDial : Control
{
	private const float Radius = 40f;
	private const float Thickness = 11f;

	/// <summary>Turns before the doom lands. Redraws on change; nothing else does.</summary>
	public int Remaining
	{
		get => _remaining;
		set
		{
			if (_remaining == value)
				return;

			_remaining = value;
			QueueRedraw();
		}
	}

	public int Total
	{
		get => _total;
		set
		{
			if (_total == value)
				return;

			_total = value;
			QueueRedraw();
		}
	}

	private int _remaining;
	private int _total;

	private readonly Label _count;

	public DoomClockDial()
	{
		CustomMinimumSize = new Vector2(Radius * 2 + 12, Radius * 2 + 12);
		MouseFilter = MouseFilterEnum.Ignore;

		_count = DoomPalette.Text("", 34, DoomPalette.Bone);
		_count.SetAnchorsPreset(LayoutPreset.FullRect);
		_count.VerticalAlignment = VerticalAlignment.Center;
		_count.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_count);
	}

	public override void _Draw()
	{
		var centre = Size / 2f;

		// The seated ring, so a spent segment still reads as a segment rather than as a gap. An
		// emptying dial has to show what it is emptying OUT OF or it is just a shrinking arc.
		DrawArc(centre, Radius, 0, Mathf.Tau, 64, DoomPalette.EmptySlot, Thickness, true);

		if (_total <= 0)
			return;

		// A gap between segments, in radians, so five turns read as five things and not as one ring
		// with hairlines. Scaled to the count: at two turns a fixed gap is invisible, at twelve it
		// eats the segment.
		var gap = Mathf.Min(0.16f, 1.2f / _total);
		var slice = Mathf.Tau / _total;

		for (var i = 0; i < _total; i++)
		{
			// From the top, clockwise — the direction every clock a player has ever seen turns.
			var start = -Mathf.Pi / 2f + (i * slice) + (gap / 2f);
			var end = start + slice - gap;

			// **Gold is legal here and this is the one place it stretches.** It is reserved for
			// "yours, and precious" (DoomUI.md), and the turns you have left before the doom lands
			// are exactly that — they are the resource the whole screen is spending.
			var spent = i >= _remaining;
			DrawArc(
				centre,
				Radius,
				start,
				end,
				24,
				spent ? DoomPalette.Slate : DoomPalette.Gold,
				Thickness,
				true
			);
		}

		// **Red at one turn left.** The last turn before a doom is a different decision from every
		// turn before it, and it is worth spending the other reserved colour to say so.
		if (_remaining <= 1)
			DrawArc(centre, Radius - Thickness, 0, Mathf.Tau, 48, DoomPalette.Red, 3f, true);
	}

	/// <summary>Points the dial at a battle's clock. Called from the board's render, like everything else.</summary>
	public void Show(int remaining, int total)
	{
		Remaining = remaining;
		Total = total;
		_count.Text = remaining.ToString();
		_count.AddThemeColorOverride(
			"font_color",
			remaining <= 1 ? DoomPalette.Red : DoomPalette.Bone
		);
	}
}
