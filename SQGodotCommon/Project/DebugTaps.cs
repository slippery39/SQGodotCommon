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
/// a click DOES. <c>--tap=X,Y@F~N</c> holds the button N frames before letting go — a long press;
/// <c>--tap=X,Y@F~N&gt;X2,Y2</c> moves to X2,Y2 while it holds, and lets go there — a drag.
/// </summary>
public partial class DebugTaps : Node
{
	private readonly List<(Vector2 At, int Frame, int Hold, Vector2 To)> _taps = new();
	private int _frame;

	public static DebugTaps? FromArgs()
	{
		var taps = new DebugTaps();
		foreach (var arg in OS.GetCmdlineUserArgs().Where(a => a.StartsWith("--tap=")))
		{
			var (point, rest) = (arg[6..].Split('@')[0], arg.Split('@')[1].Split('>'));
			var when = rest[0].Split('~');
			var at = Point(point);
			taps._taps.Add(
				(
					at,
					int.Parse(when[0]),
					// A drag needs frames to move in: ten unless it says how many.
					when.Length > 1
						? int.Parse(when[1])
					: rest.Length > 1 ? 10
					: 0,
					rest.Length > 1 ? Point(rest[1]) : at
				)
			);
		}
		return taps._taps.Count > 0 ? taps : null;
	}

	private static Vector2 Point(string xy) =>
		xy.Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray() switch
		{
			[var x, var y] => new Vector2(x, y),
			_ => throw new System.FormatException($"--tap: '{xy}' is not X,Y"),
		};

	public override void _Process(double delta)
	{
		_frame++;
		foreach (var (at, frame, hold, to) in _taps)
		{
			if (_frame == frame)
				Push(new InputEventMouseMotion { Position = at, GlobalPosition = at });
			if (_frame == frame + 1)
				Push(Button(at, pressed: true));
			// A drag: the pointer moves toward the target while the button is held.
			if (to != at && _frame > frame + 1 && _frame <= frame + 1 + hold)
			{
				var p = at.Lerp(to, (_frame - frame - 1) / (float)hold);
				Push(
					new InputEventMouseMotion
					{
						Position = p,
						GlobalPosition = p,
						ButtonMask = MouseButtonMask.Left,
					}
				);
			}
			if (_frame == frame + 1 + hold)
				Push(Button(to, pressed: false));
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
