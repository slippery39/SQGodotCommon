using System;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE WILD ROUTE as a map** (`KinMapPlan.md` §2, option A): the route's places drawn where their
/// data says (`RouteNode.Row` bottom to top, `X` across), dashed paths along their links, the team's
/// lead monster standing where you are, and the places you can walk to lit gold — the battle's
/// "legal drop" language. Click a lit place: the token walks there, then the run moves
/// (`PartyRun.MoveTo`). Presentation only — every fact and every refusal is the run's.
///
/// **Tall grass shows "?"**: its fight is rolled with the route but not shown until you walk in.
/// </summary>
public sealed class KinRouteMap
{
	// The map's box on the 1920x1080 canvas: the town you left at the bottom, the next at the top.
	private const float Left = 160;
	private const float Right = 1760;
	private const float Top = 160;
	private const float Bottom = 920;
	private const int Node = 96;

	private readonly ColorRect _root;
	private readonly Control _paths;
	private readonly Control _places;
	private readonly TextureRect _token;
	private readonly Label _title;
	private readonly Label _subtitle;
	private readonly Label _purse;
	private readonly Label _note;
	private readonly HBoxContainer _team;

	private PartyRun _run;
	private Action<int> _move;
	private bool _walking;

	public bool IsShowing => _root.Visible;

	public KinRouteMap(Node parent)
	{
		_root = new ColorRect { Color = KinPalette.Navy, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		parent.AddChild(_root);

		var ground = new TextureRect
		{
			Texture = KinArt.RegionBackdrop("route") ?? KinArt.RegionBackdrop("map"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Modulate = new Color(0.72f, 0.75f, 0.8f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(ground);

		_paths = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_paths.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_paths.Draw += DrawPaths;
		_root.AddChild(_paths);

		_places = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_places.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_places);

		_token = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Size = new Vector2(120, 100),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 10,
		};
		_root.AddChild(_token);

		// Top-left: where you are. Top-right: the purse. Bottom-centre: what just happened.
		(var header, _title, _subtitle) = KinMapKit.Header();
		_root.AddChild(header);
		(var purse, _purse) = KinMapKit.Purse();
		_root.AddChild(purse);

		// Bottom-centre, under the town you left: at the top it sat on the next town's disc.
		_note = KinMapKit.Text(28, KinPalette.Gold, HorizontalAlignment.Center);
		_note.Position = new Vector2(460, 1010);
		_note.Size = new Vector2(1000, 44);
		_root.AddChild(_note);

		// Bottom-left: the team, as the pen will show it — face, name and HP.
		_team = new HBoxContainer { Position = new Vector2(32, 972) };
		_team.AddThemeConstantOverride("separation", 14);
		_root.AddChild(_team);
	}

	public void Hide() => _root.Visible = false;

	/// <summary>Shows the route as the run stands, and walks to a place when one is clicked.</summary>
	public void Show(PartyRun run, Action<int> move)
	{
		_run = run;
		_move = move;
		_walking = false;
		_root.Visible = true;

		var areas = run.Region.Areas;
		_title.Text = $"THE ROUTE — {run.Region.Name.ToUpperInvariant()}";
		_subtitle.Text =
			$"◀ {areas[0].Name}   ·   {areas[1].Name} ▶   ·   wild LV {run.Region.MinLevel}–{run.Region.MaxLevel}"
			+ "   ·   pick a lit place to walk to";
		_purse.Text = $"GOLD {run.Gold}   ·   SNARES {run.Snares}";
		_note.Text = Arrived(run.Here);

		foreach (var child in _places.GetChildren())
			child.QueueFree();
		var reachable = run.Route!.Next(run.NodeId).Select(n => n.Id).ToHashSet();
		foreach (var node in run.Route.Nodes)
			_places.AddChild(Place(node, reachable.Contains(node.Id)));

		var lead = run.Team.FirstOrDefault()?.Companion.Name;
		_token.Texture = lead is null ? null : KinArt.Sprite(lead) ?? KinArt.Drawing(lead);
		_token.Position = TokenAt(run.Here);

		foreach (var child in _team.GetChildren())
			child.QueueFree();
		foreach (var m in run.Team)
			_team.AddChild(KinMapKit.Member(m));

		_paths.QueueRedraw();
	}

	// ===== Geometry

	private Vector2 At(RouteNode node)
	{
		var rows = _run.Route!.End.Row;
		return new Vector2(
			Left + (float)node.X * (Right - Left),
			Bottom - node.Row * (Bottom - Top) / rows
		);
	}

	/// <summary>The token stands on the place, a little up and to the left of its centre.</summary>
	private Vector2 TokenAt(RouteNode node) => At(node) - new Vector2(150, 70);

	// ===== Drawing

	/// <summary>
	/// **Every link as a dashed path**: gold from where you stand to where you can walk, faint bone
	/// elsewhere. Paths behind you are dimmer still.
	/// </summary>
	private void DrawPaths()
	{
		if (_run?.Route is not { } route)
			return;
		foreach (var link in route.Links)
		{
			var (from, to) = (route.Nodes[link.From], route.Nodes[link.To]);
			var open = link.From == _run.NodeId && _run.HereIsCleared;
			var behind = to.Row <= _run.Here.Row;
			var colour =
				open ? KinPalette.Gold
				: behind ? new Color(KinPalette.Bone, 0.35f)
				: new Color(KinPalette.Bone, 0.9f);
			// A dark casing under every dash, so a path reads on forest, meadow and water alike.
			_paths.DrawDashedLine(
				At(from),
				At(to),
				new Color(0.03f, 0.05f, 0.08f, 0.8f),
				open ? 12 : 10,
				22
			);
			_paths.DrawDashedLine(At(from), At(to), colour, open ? 7 : 5, 22);
		}
	}

	/// <summary>
	/// **One place**: a disc (gold if you can walk to it, red for a fight, bone otherwise) holding what
	/// is there — the species of a visible fight, "?" for tall grass — and its name under it.
	/// </summary>
	private Control Place(RouteNode node, bool reachable)
	{
		var size = node.Kind is NodeKind.Start or NodeKind.End ? Node + 24 : Node;
		var button = new Button
		{
			Size = new Vector2(size, size),
			Position = At(node) - new Vector2(size, size) / 2,
			Disabled = !reachable,
			TooltipText = Tooltip(node),
			FocusMode = Control.FocusModeEnum.None,
		};
		foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
			button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());

		var rim =
			reachable ? "gold"
			: node.IsFight && node.Kind != NodeKind.Grass ? "red"
			: "bone";
		button.AddChild(
			KinMapKit.Fill(KinArt.Drawing("ui/node_" + rim), new Vector2(size, size), 0)
		);

		var (art, glyph) = Face(node);
		if (art is not null)
			button.AddChild(KinMapKit.Fill(art, new Vector2(size, size), size * 0.14f));
		else if (glyph.Length > 0)
		{
			var mark = KinMapKit.Text(size / 2, KinPalette.Bone, HorizontalAlignment.Center);
			mark.Text = glyph;
			mark.Size = new Vector2(size, size);
			mark.VerticalAlignment = VerticalAlignment.Center;
			button.AddChild(mark);
		}

		var name = KinMapKit.Text(
			18,
			reachable ? KinPalette.Gold : KinPalette.Bone,
			HorizontalAlignment.Center
		);
		name.Text = Caption(node);
		name.Position = new Vector2(-60, size - 4);
		name.Size = new Vector2(size + 120, 26);
		button.AddChild(name);

		// Behind you, or already dealt with: faded, so the way ahead stands out.
		var done = node.Row < _run.Here.Row || (node.Id == _run.NodeId && _run.HereIsCleared);
		button.Modulate = done && node.Id != _run.NodeId ? new Color(1, 1, 1, 0.45f) : Colors.White;

		if (reachable)
		{
			var id = node.Id;
			button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			button.Pressed += () => Walk(id);
			button.MouseEntered += () => button.Scale = new Vector2(1.08f, 1.08f);
			button.MouseExited += () => button.Scale = Vector2.One;
			button.PivotOffset = button.Size / 2;
		}
		return button;
	}

	/// <summary>The token walks to the place, then the run moves there — one walk at a time.</summary>
	private void Walk(int id)
	{
		if (_walking)
			return;
		_walking = true;
		var to = TokenAt(_run.Route!.Nodes[id]);
		var tween = _token.CreateTween();
		tween
			.TweenProperty(_token, "position", to, KinAnimator.Instant ? 0.0 : 0.4)
			.SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(() => _move(id)));
	}

	// ===== Words

	/// <summary>What a place shows in its disc: a picture, or a glyph.</summary>
	private static (Texture2D Art, string Glyph) Face(RouteNode node) =>
		node.Kind switch
		{
			NodeKind.Wild or NodeKind.Rare => (Creature(node.Encounter!.Foes[0].Name), ""),
			NodeKind.Trainer => (KinArt.AttackIcon, ""),
			NodeKind.Rest => (KinArt.Drawing("icons/life"), ""),
			NodeKind.Find when node.Find == FindKind.Snare => (KinArt.Drawing("cards/snare"), ""),
			NodeKind.Find => (null, "$"),
			NodeKind.Grass => (null, "?"),
			NodeKind.End => (null, "▲"),
			_ => (null, "●"),
		};

	private static Texture2D Creature(string name) =>
		KinArt.Sprite(name) ?? KinArt.Drawing(name) ?? KinArt.Figure(KinArt.ColourFor(name), true);

	private static string Caption(RouteNode node) =>
		node.Kind switch
		{
			NodeKind.Start => "TOWN",
			NodeKind.End => "NEXT TOWN",
			NodeKind.Wild => Species(node),
			NodeKind.Rare => "RARE · " + node.Encounter!.Foes[0].Name.ToUpperInvariant(),
			NodeKind.Grass => "TALL GRASS",
			NodeKind.Trainer => "TRAINER",
			NodeKind.Rest => "SPRING",
			NodeKind.Find => "FIND",
			_ => "",
		};

	private static string Species(RouteNode node) =>
		string.Join(", ", node.Encounter!.Foes.Select(f => f.Name.ToUpperInvariant()).Distinct());

	private static string Tooltip(RouteNode node) =>
		node.Kind switch
		{
			NodeKind.Wild =>
				$"A wild fight: {Species(node)}. Weaken one and throw a Snare to catch it.",
			NodeKind.Grass =>
				"Tall grass: a wild fight, but you will not know what until you walk in.",
			NodeKind.Rare =>
				$"The rare's lair: {Species(node)}. Harder — the only place this rare lives.",
			NodeKind.Trainer =>
				"A trainer: a harder fight that pays more. Nothing here can be caught.",
			NodeKind.Rest => $"A spring: every monster heals {(int)(PartyRun.RestHeal * 100)}%.",
			NodeKind.Find => "Something lying on the path.",
			NodeKind.End => "The next town: a hospital, a shop — and, from here on, a leader.",
			_ => "",
		};

	/// <summary>What happened on arriving here — said once, above the map.</summary>
	private static string Arrived(RouteNode here) =>
		here.Kind switch
		{
			NodeKind.Find when here.Find == FindKind.Snare => "You found a Snare.",
			NodeKind.Find => $"You found a purse: +{PartyRun.FoundGold} gold.",
			NodeKind.Rest => "The spring heals everyone.",
			_ => "",
		};
}
