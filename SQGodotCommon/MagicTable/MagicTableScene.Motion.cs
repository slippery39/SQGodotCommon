using System;
using System.Collections.Generic;
using System.Linq;
using Common.Core;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Cards;
using static MagicTable.TableChanges;

namespace MagicTable;

/// <summary>
/// The table's motion (TBL-4.5; Shayne: "things happen but its not clear what"). Each move the manager makes is
/// a BEAT: the table draws the state after it, and everything that move changed moves together — three creatures
/// dying in one combat go at once, never one after another, and never two beats at once. While beats play the
/// table shows the state they have reached and takes no input, and the AI waits. A card flies from where it was
/// drawn to where it is drawn now (its zone's place on the plate when it has none of its own) and settles; a death
/// dims, then goes; damage and life float off as numbers, and a life total counts. Presentation only: what
/// changed is <see cref="TableChanges"/>'.
/// </summary>
public partial class MagicTableScene
{
	/// <summary>
	/// A beat's length, in Tools and <c>--beat=</c>; 0 is none. Shayne, 2026-11-13: 0.2 s at first, then after
	/// watching a game "even very slow can seem way too fast" — the old Very slow (0.8 s) is Normal now.
	/// </summary>
	private static readonly (string Label, double Seconds)[] Beats =
	[
		("Off", 0),
		("Fast", 0.4),
		("Normal", 0.8),
		("Slow", 1.2),
		("Very slow", 1.6),
	];

	private double _beat = 0.8;
	private readonly Queue<GameState> _beats = new();
	private bool _playing;

	/// <summary>While beats play: the state on the table. Null when it shows the game as it is.</summary>
	private GameState? _onScreen;

	/// <summary>After any input: the moves it made, queued as beats from the state drawn before them.</summary>
	private void QueueBeats()
	{
		var steps = _m.TakeSteps();
		if (
			_beat > 0
			&& _shownState is { } shown
			&& steps.Count > 0
			&& ReferenceEquals(steps[^1], _m.State)
		)
		{
			_onScreen ??= shown;
			foreach (var step in steps)
				_beats.Enqueue(step);
			if (!_playing)
				PlayBeats();
		}
		else if (!ReferenceEquals(_beats.LastOrDefault() ?? _onScreen, _m.State))
		{
			// An undo, a new game, the animations switched off: nothing to move towards — show it as it is.
			_beats.Clear();
			_onScreen = null;
		}
	}

	private async void PlayBeats()
	{
		_playing = true;
		try
		{
			// What is on screen may have been drawn this very frame (the AI's step as the last beat ended): wait
			// for its layout before reading where anything is.
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			while (_onScreen is { } shown && _beats.TryDequeue(out var next))
			{
				var changes = Between(shown, next);
				_onScreen = next;
				if (changes.Count == 0 && Fighting(shown).SetEquals(Fighting(next)))
					continue; // a pass: nothing to see, no time
				// Where each card starts, read BEFORE the redraw rebuilds the rows and plates (a zone button's place).
				var drawn = Rects(Nodes());
				var from = new Dictionary<int, Rect2>();
				foreach (var change in changes)
					if (change is Moved { From: { } zone } moved)
						from[moved.Card.Id] = drawn.TryGetValue(moved.Card.Id, out var r)
							? r
							: Anchor(moved.Was, zone);
					else if (change is Gone gone && drawn.TryGetValue(gone.Was.Id, out var at))
						from[gone.Was.Id] = at;
				Refresh();
				var lands = Hide(changes);
				foreach (var life in changes.OfType<Life>()) // counting from the old total, not the new one drawn
					(life.PlayerId == _m.HumanId ? _myPlate : _oppPlate).CountLife(
						life.From,
						life.To,
						_beat
					);
				var starts = Animate(changes, shown, next, from, lands);
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // the rows laid out
				var to = Rects(Nodes()); // afresh: a redraw meanwhile (a zone opened) built new rows
				foreach (var start in starts)
					start(to);
				await ToSignal(GetTree().CreateTimer(_beat), Timer.SignalName.Timeout);
			}
		}
		finally // caught up, undone meanwhile, or thrown: never left waiting — the AI and every click wait on it
		{
			_playing = false;
			_onScreen = null;
			if (IsInstanceValid(this) && IsInsideTree())
				Refresh();
		}
	}

	private const string RestMeta = "rest";

	/// <summary>
	/// Every attacker and blocker in this state — a beat when it changes, though no card changed zones. The combat
	/// row redraws with them in it at once, unanimated (Shayne, 2026-11-13: a flight there showed the full card,
	/// then the crop — "I would rather it just be instant").
	/// </summary>
	private static HashSet<int> Fighting(GameState s)
	{
		var attackers = MagicCore.Turns.Combat.Attackers(s).Select(a => a.Id).ToList();
		return
		[
			.. attackers,
			.. attackers.SelectMany(a => MagicCore.Turns.Combat.BlockersOf(s, a)).Select(b => b.Id),
		];
	}

	/// <summary>
	/// What each moving card will land as, hidden at once — before a frame shows it — so it appears as it lands:
	/// its picture on the table, or its card in the fan. Not a land stack it joins: its other lands were there.
	/// </summary>
	private Dictionary<int, CanvasItem> Hide(List<Change> changes)
	{
		var nodes = Nodes();
		var lands = new Dictionary<int, CanvasItem>();
		foreach (var moved in changes.OfType<Moved>())
		{
			var id = moved.Card.Id;
			CanvasItem? item =
				nodes.GetValueOrDefault(id) is { } node
				&& node.GetMeta(CardViews.IdsMeta).AsInt32Array().Length == 1
					? node
				: moved.To == ZoneType.Hand ? _hand.Card(id)
				: null;
			if (item is null)
				continue;
			item.SetMeta(RestMeta, item.Modulate); // a phased-out card's dim, back as it settles
			item.Modulate = Colors.Transparent;
			lands[id] = item;
		}
		return lands;
	}

	/// <summary>
	/// The beat's motion, in two halves: what flies is made NOW, where it was — no frame shows it gone before it
	/// leaves — and each part starts once the new rows are laid out, given where everything is drawn then.
	/// </summary>
	private List<Action<Dictionary<int, Rect2>>> Animate(
		List<Change> changes,
		GameState before,
		GameState after,
		Dictionary<int, Rect2> from,
		Dictionary<int, CanvasItem> lands
	)
	{
		var starts = new List<Action<Dictionary<int, Rect2>>>();
		foreach (var change in changes)
			switch (change)
			{
				case Moved { From: null } made: // a token, a copy: nowhere to come from
					if (lands.GetValueOrDefault(made.Card.Id) is { } item)
						starts.Add(_ => Settle(item));
					break;
				case Moved { From: ZoneType.Library, To: ZoneType.Hand } drawn
					when drawn.Card.OwnerId == _m.HumanId:
					if (lands.GetValueOrDefault(drawn.Card.Id) is { } card)
						card.Modulate = card.GetMeta(RestMeta).AsColor(); // the fan draws it in itself
					break;
				case Moved moved:
					var id = moved.Card.Id;
					var fly = Fly(
						Visible(moved.Was, moved.From!.Value) || Visible(moved.Card, moved.To)
							? MagicArt.Full(MagicArt.ImageName(after, moved.Card))
							: PlayerPlate.Back,
						from[id],
						dies: moved is { From: ZoneType.Battlefield, To: ZoneType.Graveyard },
						lands.GetValueOrDefault(id)
					);
					starts.Add(to =>
						fly(to.TryGetValue(id, out var end) ? end : Anchor(moved.Card, moved.To))
					);
					break;
				case Gone gone when from.TryGetValue(gone.Was.Id, out var where): // a token: dims where it was, and goes
					var fade = Fly(
						MagicArt.Full(MagicArt.ImageName(before, gone.Was)),
						where,
						dies: true,
						null
					);
					starts.Add(_ => fade(where));
					break;
				case Triggered triggered: // its stack row and its source pulse (A7)
					starts.Add(_ =>
					{
						var now = Nodes();
						foreach (var id in new[] { triggered.StackId, triggered.SourceId })
							if (now.GetValueOrDefault(id) is { } node)
								Settle(node);
					});
					break;
				case Damaged damaged:
					starts.Add(to =>
					{
						if (to.TryGetValue(damaged.CardId, out var at))
							Float($"-{damaged.Amount}", at.GetCenter(), Red);
					});
					break;
				case Life life:
					var plate = life.PlayerId == _m.HumanId ? _myPlate : _oppPlate;
					var by = life.To - life.From;
					starts.Add(_ =>
						Float(
							by < 0 ? $"{by}" : $"+{by}",
							plate.LifeRect.GetCenter(),
							by < 0 ? Red : Green
						)
					);
					break;
			}
		return starts;
	}

	private static readonly Color Red = new(1, 0.35f, 0.3f);
	private static readonly Color Green = new(0.45f, 1, 0.45f);

	/// <summary>A card's picture where it was, now; given where it lands, it flies there over the beat and what it lands as settles.</summary>
	private Action<Rect2> Fly(Texture2D? face, Rect2 start, bool dies, CanvasItem? lands)
	{
		var size = CardViews.HandSize; // a whole card's shape
		float Fit(Rect2 r) => Mathf.Min(r.Size.X / size.X, r.Size.Y / size.Y);
		var ghost = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // before Texture (godot-frontend.md)
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = face,
			Size = size,
			PivotOffset = size / 2,
			Position = start.GetCenter() - size / 2,
			Scale = Vector2.One * Fit(start),
			TopLevel = true,
			ZIndex = 310, // over the overlays (300) and the fan
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(ghost);
		return end => Launch(ghost, end, Fit(end), dies, lands);
	}

	private void Launch(TextureRect ghost, Rect2 end, float scale, bool dies, CanvasItem? lands)
	{
		var size = ghost.Size;
		var tween = ghost.CreateTween();
		var flight = _beat * 0.7;
		if (dies) // dimmed, then to the graveyard
		{
			tween.TweenProperty(ghost, "modulate", new Color(1, 0.3f, 0.3f, 0.75f), _beat * 0.3);
			flight = _beat * 0.4;
		}
		tween
			.TweenProperty(ghost, "position", end.GetCenter() - size / 2, flight)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
		tween.Parallel().TweenProperty(ghost, "scale", Vector2.One * scale, flight);
		tween.TweenCallback(
			Callable.From(() =>
			{
				ghost.QueueFree();
				if (lands is not null)
					Settle(lands);
			})
		);
	}

	/// <summary>Where it lands (A3): shown, a little bright (and big, on the table), easing back over the rest of the beat.</summary>
	private void Settle(CanvasItem item)
	{
		if (!IsInstanceValid(item))
			return; // redrawn meanwhile
		var rest = item.HasMeta(RestMeta) ? item.GetMeta(RestMeta).AsColor() : Colors.White;
		item.Modulate = new Color(rest.R * 1.6f, rest.G * 1.6f, rest.B * 1.6f, rest.A);
		var tween = item.CreateTween().SetParallel();
		tween.TweenProperty(item, "modulate", rest, _beat * 0.3);
		if (item is Control node) // a fan card's size is the fan's
		{
			node.PivotOffset = node.Size / 2;
			node.Scale = Vector2.One * 1.15f;
			tween.TweenProperty(node, "scale", Vector2.One, _beat * 0.3);
		}
	}

	/// <summary>A number floating off what it happened to: Common's IndicatorLabel (rises and fades over a second).</summary>
	private void Float(string text, Vector2 centre, Color colour)
	{
		var label = new IndicatorLabel
		{
			Text = text,
			TopLevel = true,
			ZIndex = 311,
			MouseFilter = MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings
			{
				FontSize = 44,
				FontColor = colour,
				OutlineSize = 10,
				OutlineColor = Colors.Black,
			},
		};
		AddChild(label);
		label.Position = centre - label.GetCombinedMinimumSize() / 2;
	}

	/// <summary>Whether the human may see this card where it is: a library and an unknown card in their hand are backs.</summary>
	private bool Visible(Card c, ZoneType zone) =>
		zone switch
		{
			ZoneType.Library => false,
			ZoneType.Hand => c.OwnerId == _m.HumanId || _showHand || c.IsKnownTo(_m.HumanId),
			_ => !c.IsHiddenFrom(_m.HumanId),
		};

	/// <summary>A card with no picture of its own here: its zone's place — the plate's button, its hand, the stack.</summary>
	private Rect2 Anchor(Card c, ZoneType zone)
	{
		var owner = zone == ZoneType.Battlefield ? c.ControllerId : c.OwnerId;
		var plate = owner == _m.HumanId ? _myPlate : _oppPlate;
		var place = zone switch
		{
			ZoneType.Stack => _stack.GetGlobalRect(),
			ZoneType.Hand when owner == _m.HumanId => _handSpace.GetGlobalRect(),
			_ => plate.ZoneRect(zone),
		};
		var size = CardViews.HandSize * 0.45f;
		return new Rect2(place.GetCenter() - size / 2, size);
	}

	/// <summary>Every control showing an object (CardViews.Tag), by id — a land stack's for each land in it.</summary>
	private Dictionary<int, Control> Nodes()
	{
		var nodes = new Dictionary<int, Control>();
		void Walk(Node n)
		{
			if (n is Control c && c.HasMeta(CardViews.IdsMeta) && c.IsVisibleInTree())
				foreach (var id in c.GetMeta(CardViews.IdsMeta).AsInt32Array())
					nodes[id] = c;
			foreach (var child in n.GetChildren())
				Walk(child);
		}
		Walk(this);
		return nodes;
	}

	/// <summary>Where each object is drawn now: the tagged controls, and the hand's fan.</summary>
	private Dictionary<int, Rect2> Rects(Dictionary<int, Control> nodes)
	{
		var rects = nodes.ToDictionary(n => n.Key, n => n.Value.GetGlobalRect());
		foreach (var (id, rect) in _hand.Rects())
			rects[id] = rect;
		return rects;
	}
}
