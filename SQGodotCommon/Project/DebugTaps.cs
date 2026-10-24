using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Project;

/// <summary>
/// DEBUG: <c>-GameArgs '--tap=X,Y@F'</c> (repeat the arg for more) moves the mouse to window
/// pixel X,Y on frame F and clicks there on frame F+1 — the coordinates of a capture frame, so a
/// tap can be aimed straight from a screenshot. Sent through <see cref="Viewport.PushInput"/> as
/// REAL input, so mouse filters, hover and GuiInput run exactly as for a player; calling a handler
/// directly would verify something no player can do (godot-frontend.md). Lets a capture show what
/// a click DOES. <c>--tap=X,Y@F~N</c> holds the button N frames before letting go — a long press.
/// </summary>
public partial class DebugTaps : Node
{
	private readonly List<(Vector2 At, int Frame, int Hold)> _taps = new();
	private int _frame;

	public static DebugTaps? FromArgs()
	{
		var taps = new DebugTaps();
		foreach (var arg in OS.GetCmdlineUserArgs().Where(a => a.StartsWith("--tap=")))
		{
			var (point, when) = (arg[6..].Split('@')[0], arg.Split('@')[1].Split('~'));
			var xy = point
				.Split(',')
				.Select(v => float.Parse(v, CultureInfo.InvariantCulture))
				.ToArray();
			taps._taps.Add(
				(
					new Vector2(xy[0], xy[1]),
					int.Parse(when[0]),
					when.Length > 1 ? int.Parse(when[1]) : 0
				)
			);
		}
		return taps._taps.Count > 0 ? taps : null;
	}

	public override void _Process(double delta)
	{
		_frame++;
		foreach (var (at, frame, hold) in _taps)
		{
			if (_frame == frame)
				Push(new InputEventMouseMotion { Position = at, GlobalPosition = at });
			if (_frame == frame + 1)
				Push(Button(at, pressed: true));
			if (_frame == frame + 1 + hold)
				Push(Button(at, pressed: false));
		}
	}

	private void Push(InputEvent e) => GetViewport().PushInput(e);

	private static InputEventMouseButton Button(Vector2 at, bool pressed) =>
		new()
		{
			Position = at,
			GlobalPosition = at,
			ButtonIndex = MouseButton.Left,
			Pressed = pressed,
		};
}
