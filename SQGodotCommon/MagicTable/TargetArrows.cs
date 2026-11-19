using System;
using System.Collections.Generic;
using System.Linq;

namespace MagicTable;

/// <summary>
/// An arrow from each spell or ability on the stack to what it targets (TBL-4.5; Shayne: it "was unclear ... what it
/// was actually targeting"), only while it is on the stack. Drawn over the table; the ends are read every frame
/// while there are arrows, so a row re-laid or a card flying is followed. A target not on screen (a graveyard's
/// card) gets none. Presentation only: the pairs come from the state (<see cref="MagicTableScene"/>).
/// </summary>
public partial class TargetArrows : Control
{
	private static readonly Color Colour = new("f0a030");

	/// <summary>(stack object, target) by id.</summary>
	public List<(int From, int To)> Pairs { get; set; } = [];

	/// <summary>Where each object is drawn now, by id.</summary>
	public Func<Dictionary<int, Rect2>> Rects { get; set; } = () => new();

	private List<(Vector2 From, Vector2 To)> _lines = [];

	public TargetArrows()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		TopLevel = true; // its own coordinates are the table's: a rect read anywhere draws where it is
		ZIndex = 305; // over the overlays' plates (300), under a flying card (310)
	}

	public override void _Process(double delta)
	{
		if (Pairs.Count == 0 && _lines.Count == 0)
			return;
		var rects = Pairs.Count == 0 ? [] : Rects();
		_lines =
		[
			.. Pairs
				.Where(p => rects.ContainsKey(p.From) && rects.ContainsKey(p.To))
				.Select(p =>
					(
						new Vector2(rects[p.From].Position.X, rects[p.From].GetCenter().Y), // the stack row's left edge
						rects[p.To].GetCenter()
					)
				),
		];
		QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var (from, to) in _lines)
		{
			// A curve bowing up, so two arrows to one target don't lie on each other's line.
			var mid = (from + to) / 2 + new Vector2(0, -Mathf.Min(160, from.DistanceTo(to) / 4));
			var points = Enumerable
				.Range(0, 25)
				.Select(i => i / 24f)
				.Select(t => from.Lerp(mid, t).Lerp(mid.Lerp(to, t), t))
				.ToArray();
			DrawPolyline(points, Colors.Black, 9, antialiased: true); // an outline: it crosses busy art
			DrawPolyline(points, Colour, 5, antialiased: true);
			var dir = (to - points[^3]).Normalized();
			var side = new Vector2(-dir.Y, dir.X) * 12;
			DrawColoredPolygon([to, to - dir * 24 + side, to - dir * 24 - side], Colour);
		}
	}
}
