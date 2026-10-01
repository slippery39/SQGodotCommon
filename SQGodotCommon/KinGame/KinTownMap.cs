using System;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE TOWN as a map** (`KinMapPlan.md` §3): a painted village green with each building standing
/// where the town's DATA puts it (`PartyRun.Town`), a name plate under it, and the team's lead monster
/// walking to the one you click. The board then opens that building's screen (hospital, shop, pen,
/// the leader's hall) — or, at the gate, sets out. Presentation only: what is shut says the run's
/// own refusal (`CannotLeaveTown`).
/// </summary>
public sealed class KinTownMap
{
	private const float BuildingSize = 280;

	private readonly ColorRect _root;
	private readonly Control _places;
	private readonly TextureRect _token;
	private readonly Label _title;
	private readonly Label _subtitle;
	private readonly Label _purse;
	private readonly Label _note;
	private readonly HBoxContainer _team;

	private PartyRun _run;
	private Action<BuildingKind> _open;
	private bool _walking;

	public KinTownMap(Node parent)
	{
		_root = new ColorRect { Color = KinPalette.Navy, Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		parent.AddChild(_root);

		var ground = new TextureRect
		{
			Texture = KinArt.RegionBackdrop("town_ground") ?? KinArt.RegionBackdrop("town"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Modulate = new Color(0.82f, 0.84f, 0.86f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(ground);

		_places = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_places.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(_places);

		_token = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Size = new Vector2(130, 110),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ZIndex = 10,
		};
		_root.AddChild(_token);

		(var header, _title, _subtitle) = KinMapKit.Header();
		_root.AddChild(header);
		(var purse, _purse) = KinMapKit.Purse();
		_root.AddChild(purse);

		_note = KinMapKit.Text(28, KinPalette.Gold, HorizontalAlignment.Center);
		_note.Position = new Vector2(460, 1010);
		_note.Size = new Vector2(1000, 44);
		_root.AddChild(_note);

		_team = new HBoxContainer { Position = new Vector2(32, 972) };
		_team.AddThemeConstantOverride("separation", 14);
		_root.AddChild(_team);
	}

	public void Hide() => _root.Visible = false;

	/// <summary>Shows the town as the run stands; a clicked building is walked to, then opened.</summary>
	public void Show(PartyRun run, Action<BuildingKind> open)
	{
		_run = run;
		_open = open;
		_walking = false;
		_root.Visible = true;
		_note.Text = "";

		_title.Text =
			$"{run.Region.Name.ToUpperInvariant()} — TOWN {run.RegionIndex + 1} OF {run.Regions.Count}";
		// The region's BOSS, named from the town on: the route is preparing for it.
		_subtitle.Text = $"BOSS: {run.Boss.Name.ToUpperInvariant()}";
		_purse.Text = $"GOLD {run.Gold}";

		foreach (var child in _places.GetChildren())
			child.QueueFree();
		foreach (var building in run.Town.Buildings)
			_places.AddChild(Place(building));

		var lead = run.Team.FirstOrDefault()?.Companion.Name;
		_token.Texture = lead is null ? null : KinArt.Sprite(lead) ?? KinArt.Drawing(lead);
		// The token waits by the well in the middle of the green.
		_token.Position = At(0.5, 0.56) - _token.Size / 2;

		foreach (var child in _team.GetChildren())
			child.QueueFree();
		foreach (var m in run.Team)
			_team.AddChild(KinMapKit.Member(m));
	}

	private static Vector2 At(double x, double y) => new((float)x * 1920, (float)y * 1080);

	/// <summary>
	/// **One building**: its sprite (`Art/buildings/<kind>.png`), its name plate, its hover. The
	/// leader's hall glows gold while the leader stands — it is the goal; a shut gate is dimmed.
	/// </summary>
	private Control Place(Building building)
	{
		var size = new Vector2(BuildingSize, BuildingSize);
		var button = new Button
		{
			Size = size,
			Position = At(building.X, building.Y) - size / 2,
			TooltipText = Tooltip(building),
			FocusMode = Control.FocusModeEnum.None,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			PivotOffset = size / 2,
		};
		foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
			button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());

		var shadow = KinMapKit.Fill(
			KinArt.Drawing("ui/contact"),
			new Vector2(size.X * 0.8f, 44),
			0
		);
		shadow.Position = new Vector2(size.X * 0.1f, size.Y - 62);
		var goal = building.Kind == BuildingKind.Gate;
		shadow.Modulate = goal ? new Color(KinPalette.Gold, 0.9f) : new Color(0, 0, 0, 0.55f);
		button.AddChild(shadow);

		var art = KinArt.Drawing("buildings/" + building.Kind.ToString().ToLowerInvariant());
		button.AddChild(
			art is not null
				? KinMapKit.Fill(art, size, 12)
				: KinMapKit.Fill(KinArt.Drawing("ui/node_bone"), size, 70)
		);

		var shut = building.Kind == BuildingKind.Gate && _run.CannotLeaveTown is not null;
		var plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		plate.AddThemeStyleboxOverride(
			"panel",
			KinUiKit.Plate(
				goal ? "gold"
				: shut ? "red"
				: "bone"
			)
		);
		var name = KinMapKit.Text(
			22,
			goal ? KinPalette.Gold : KinPalette.Bone,
			HorizontalAlignment.Center
		);
		name.Text = (shut ? "SHUT · " : "") + building.Name.ToUpperInvariant();
		plate.AddChild(name);
		button.AddChild(plate);
		// Centred under the sprite once the plate knows its own width.
		plate.Resized += () =>
			plate.Position = new Vector2((size.X - plate.Size.X) / 2, size.Y - 30);

		if (shut)
			button.Modulate = new Color(0.75f, 0.75f, 0.8f);

		var kind = building.Kind;
		button.Pressed += () => Walk(building, kind, shut);
		button.MouseEntered += () => button.Scale = new Vector2(1.05f, 1.05f);
		button.MouseExited += () => button.Scale = Vector2.One;
		return button;
	}

	/// <summary>The token walks to the building's door, then it opens — a shut gate says why instead.</summary>
	private void Walk(Building building, BuildingKind kind, bool shut)
	{
		if (_walking)
			return;
		if (shut)
		{
			_note.Text = _run.CannotLeaveTown + ".";
			return;
		}
		_walking = true;
		var door =
			At(building.X, building.Y) + new Vector2(-40, BuildingSize * 0.32f) - _token.Size / 2;
		var tween = _token.CreateTween();
		tween
			.TweenProperty(_token, "position", door, KinAnimator.Instant ? 0.0 : 0.45)
			.SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(() => _open(kind)));
	}

	private string Tooltip(Building building) =>
		building.Kind switch
		{
			BuildingKind.Hospital => $"Heal the team to full — {PartyRun.HospitalPrice} gold.",
			BuildingKind.Shop => "Cards, and taking a card out of your deck.",
			BuildingKind.Gate => _run.CannotLeaveTown
				?? $"Out onto the route — {_run.Boss.Name} waits at its end.",
			_ => "",
		};
}
