using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>What the board knows that the field shows: the forecast, the order, what is lit, what is held.</summary>
public sealed record FieldContext(
	ImmutableDictionary<int, int> Forecast,
	Dictionary<int, int> Order,
	HashSet<int> Drops,
	KinCard Focus,
	bool Snaring,
	int SelectedId,
	int HeldId
);

/// <summary>
/// **THE RELAY's field: your line on the left, theirs on the right, the FRONTS meeting in the
/// middle** (`KinUI.md`, "THE RELAY screen"). Replaces the two rows of cells.
///
/// **A creature is a view keyed by its id**, standing at its line position and SLIDING when the line
/// changes; a creature that leaves fades where it stood. Reads `PartyState`, composes strings, decides
/// nothing — every drop it lights, the engine accepted.
///
/// **Drops are numbered as the board's always were: 0–4 your line, 5–9 theirs**, by PLACE, so an
/// empty place is a drop too (a Summon, a Gust).
/// </summary>
public sealed class KinRelayField
{
	public const int Slot = KinRelayCreature.Width;
	public const int Gap = 96;
	public const int Half = Slot * PartyBattle.MaxLine;
	public const int Width = Half * 2 + Gap;
	public const int Height = 400;

	public Control Root { get; }

	private readonly Dictionary<int, KinRelayCreature> _views = new();
	private readonly Dictionary<int, Vector2> _targets = new();
	private readonly Label[] _hints = new Label[PartyBattle.MaxLine * 2];

	/// <summary>A view lifted by the deploy drag: it follows the mouse and is not slid until dropped.</summary>
	private int _heldId;

	public KinRelayField()
	{
		Root = new Control
		{
			CustomMinimumSize = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		for (var d = 0; d < _hints.Length; d++)
		{
			var hint = KinPalette.Text("", 22, KinPalette.Gold);
			hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			hint.CustomMinimumSize = new Vector2(1, 0);
			hint.Position = SlotRect(d).Position + new Vector2(0, 170);
			hint.Size = new Vector2(Slot, 80);
			hint.MouseFilter = Control.MouseFilterEnum.Ignore;
			Root.AddChild(hint);
			_hints[d] = hint;
		}

		// The one sentence the layout needs: which way is front.
		var caption = KinPalette.Text(
			"YOUR LINE  ▶  FRONT   ·   FRONT  ◀  THEIR LINE",
			18,
			KinPalette.Bone
		);
		caption.Modulate = new Color(1, 1, 1, 0.6f);
		caption.Position = new Vector2(0, Height - 4);
		caption.Size = new Vector2(Width, 24);
		caption.MouseFilter = Control.MouseFilterEnum.Ignore;
		Root.AddChild(caption);
	}

	// ===== Geometry

	/// <summary>The place a drop number names, in field coordinates: your front nearest the middle.</summary>
	public static Rect2 SlotRect(int drop) =>
		new(
			drop < PartyBattle.MaxLine
				? Half - (drop + 1) * Slot
				: Half + Gap + (drop - PartyBattle.MaxLine) * Slot,
			0,
			Slot,
			Height
		);

	private Rect2 GlobalSlot(int drop) =>
		new(Root.GetGlobalRect().Position + SlotRect(drop).Position, SlotRect(drop).Size);

	/// <summary>The drop number of the place under a point, or null.</summary>
	public int? DropAt(Vector2 global)
	{
		for (var d = 0; d < _hints.Length; d++)
			if (GlobalSlot(d).HasPoint(global))
				return d;
		return null;
	}

	public Vector2 CentreOf(int drop) => GlobalSlot(drop).GetCenter();

	/// <summary>What a blow flashes and a number rises off: the creature's sprite.</summary>
	public Control ViewOf(int creatureId) =>
		_views.TryGetValue(creatureId, out var view) ? view.Sprite : null;

	/// <summary>Where a card dropped at this place rises from: the creature there, or the place itself.</summary>
	public Control At(GameState s, int drop) =>
		CreatureIn(s, drop) is { } c ? ViewOf(c.Id) : _hints[drop];

	public Creature CreatureIn(GameState s, int drop) =>
		drop < PartyBattle.MaxLine ? s.AllyAt(drop) : s.FoeAt(drop - PartyBattle.MaxLine);

	/// <summary>The creature under a point and its sprite — for the inspector.</summary>
	public (Creature Creature, Control Sprite)? CreatureAt(GameState s, Vector2 global) =>
		DropAt(global) is { } d && CreatureIn(s, d) is { } c ? (c, ViewOf(c.Id)) : null;

	// ===== Rendering

	/// <summary>
	/// **Every creature in both lines, drawn from state.** New creatures appear at their place; one
	/// that has moved is only given its new place here — <see cref="Settle"/> slides it there, once
	/// the turn's blows have played.
	/// </summary>
	public void Render(GameState s, FieldContext ctx)
	{
		_heldId = ctx.HeldId;
		var standing = new HashSet<int>();

		foreach (var ally in s.LivingAllies())
			Place(ally, AllyLook(s, ally, ctx), ally.Position, standing);
		foreach (var foe in s.LivingFoes())
			Place(foe, FoeLook(s, foe, ctx), PartyBattle.MaxLine + foe.Position, standing);

		foreach (var id in _views.Keys.Where(id => !standing.Contains(id)).ToList())
			_targets.Remove(id);

		// A drop on an EMPTY place — a Summon on your line, a Gust on theirs — is said on the place.
		var allies = s.LivingAllies().Count();
		var foes = s.LivingFoes().Count();
		for (var d = 0; d < _hints.Length; d++)
		{
			var empty = d < PartyBattle.MaxLine ? d >= allies : d - PartyBattle.MaxLine >= foes;
			_hints[d].Text =
				empty && ctx.Focus is not null && ctx.Drops.Contains(d)
					? $"▲ {ctx.Focus.Name.ToUpperInvariant()} HERE"
					: "";
		}
	}

	private void Place(Creature c, CreatureLook look, int drop, HashSet<int> standing)
	{
		standing.Add(c.Id);
		var at = SlotRect(drop).Position;
		if (!_views.TryGetValue(c.Id, out var view))
		{
			view = _views[c.Id] = new KinRelayCreature();
			view.Root.Position = at;
			Root.AddChild(view.Root);
			KinAnimator.Pop(view.Root);
		}
		view.Show(look);
		_targets[c.Id] = at;
	}

	/// <summary>
	/// **The line closes up — after <paramref name="delay"/>**, the length of the turn's blows. Each
	/// view slides to its place; a creature that left fades where it stood.
	/// </summary>
	public void Settle(double delay)
	{
		foreach (var (id, view) in _views.ToList())
		{
			if (!_targets.TryGetValue(id, out var to))
			{
				_views.Remove(id);
				KinAnimator.FadeOut(view.Root, delay);
				continue;
			}
			if (id != _heldId && view.Root.Position != to)
				KinAnimator.Slide(view.Root, to, delay);
		}
	}

	/// <summary>A blow: the attacker's sprite jabs toward the other line; its words stay put.</summary>
	public void Lunge(GameState s, int attackerId)
	{
		if (_views.TryGetValue(attackerId, out var view))
			KinAnimator.Lunge(view.Sprite, s.GetObject(attackerId) is Ally ? 40 : -40);
	}

	/// <summary>DEPLOY: the held creature follows the mouse until it is dropped.</summary>
	public void Follow(int creatureId, Vector2 global)
	{
		if (_views.TryGetValue(creatureId, out var view))
			view.Root.GlobalPosition = global - new Vector2(Slot / 2f, 140);
	}

	// ===== Looks — strings from facts

	private static CreatureLook AllyLook(GameState s, Ally ally, FieldContext ctx)
	{
		var next = ally.Current;
		var drop = ctx.Drops.Contains(ally.Position) ? ctx.Focus : null;
		var loses = ctx.Forecast.GetValueOrDefault(ally.Id);
		var colour = KinPalette.Companion(ally.Name);

		return new CreatureLook(
			ally.HasActed
				? "ACTED THIS TURN"
				: KinMoveText
					.Says(
						next,
						next.Kind == IntentType.Attack ? ally.AttackFor(next.Amount) : next.Amount
					)
					.ToUpperInvariant(),
			KinPalette.Gold,
			Art(ally.Name, colour.Lightened(0.45f), hostile: false),
			KinArt.Drawing(ally.Name) is not null,
			FacesLeft: false,
			ally.HasActed ? 0 : ctx.Order.GetValueOrDefault(ally.Id),
			drop is not null || ally.Id == ctx.SelectedId || ally.Id == ctx.HeldId
				? KinPalette.Gold
				: colour,
			drop is not null || ally.Id == ctx.SelectedId || ally.Id == ctx.HeldId,
			ally.Name.ToUpperInvariant(),
			ally.Hp,
			ally.MaxHp,
			colour.Lightened(0.25f),
			Join(
				ally.Block > 0 ? $"BLOCK {ally.Block}" : "",
				ally.FadesIn > 0 ? $"TOKEN · FADES IN {ally.FadesIn}"
					: ally.BonusThorns > 0 ? $"THORNS {ally.TotalThorns}"
					: ally.Passive
			),
			drop is not null ? $"▲ {drop.Name.ToUpperInvariant()} HERE"
				: loses > 0 ? $"▼ −{loses} this turn"
				: "",
			drop is not null ? KinPalette.Gold : KinPalette.Red
		);
	}

	private static CreatureLook FoeLook(GameState s, Foe foe, FieldContext ctx)
	{
		var intent = foe.Current;
		var drop = ctx.Drops.Contains(PartyBattle.MaxLine + foe.Position) ? ctx.Focus : null;
		var snareHere = ctx.Snaring && s.CatchRefusal(foe) is null;
		var loses = ctx.Forecast.GetValueOrDefault(foe.Id);
		var attacks = intent.Kind == IntentType.Attack && !foe.Staggered;

		return new CreatureLook(
			foe.Staggered
				? "STAGGERED"
				: KinMoveText.Says(intent, intent.Amount).ToUpperInvariant(),
			attacks ? KinPalette.Red : KinPalette.Bone,
			Art(foe.Name, KinArt.ColourFor(foe.Name), hostile: true),
			KinArt.Drawing(foe.Name) is not null,
			FacesLeft: true,
			ctx.Order.GetValueOrDefault(foe.Id),
			drop is not null || snareHere ? KinPalette.Gold : KinPalette.Red,
			drop is not null || snareHere,
			foe.Name.ToUpperInvariant(),
			foe.Hp,
			foe.MaxHp,
			KinPalette.Red,
			Join(
				foe.Block > 0 ? $"BLOCK {foe.Block}" : "",
				foe.OffBalance > 0 ? $"OFF-BALANCE +{foe.OffBalance}" : "",
				foe.FadesIn > 0 ? $"FADES IN {foe.FadesIn}" : "",
				foe.Catchable && foe.Hp <= foe.CatchAt() ? "◆ CATCHABLE" : ""
			),
			snareHere ? "◆ SNARE IT HERE"
				: drop is not null ? $"▼ {drop.Name.ToUpperInvariant()} HERE"
				: loses > 0 ? $"−{loses} this turn"
				: "",
			snareHere || drop is not null ? KinPalette.Gold : KinPalette.Bone
		);
	}

	private static string Join(params string[] parts) =>
		string.Join(" · ", parts.Where(p => p.Length > 0));

	/// <summary>A drawing if one exists, else the silhouette in the creature's colour.</summary>
	private static Texture2D Art(string name, Color colour, bool hostile) =>
		KinArt.Drawing(name) ?? KinArt.Figure(colour, hostile);
}

/// <summary>**A move, as the player reads it** — on a creature's telegraph and on the starter screen.</summary>
public static class KinMoveText
{
	/// <summary>`amount` is what it will actually deal (a monster's attack already carries Power).</summary>
	public static string Says(Intent intent, int amount) =>
		intent.Kind switch
		{
			IntentType.Attack => $"{intent.Name} {amount} → {Where(intent.Target)}"
				+ (intent.Steals ? " + steals" : ""),
			IntentType.Block when intent.Target == Aim.Ahead =>
				$"{intent.Name}: +{amount} block ahead",
			IntentType.Block => $"{intent.Name}: +{amount} block",
			IntentType.Move => $"{intent.Name}: {(amount < 0 ? "forward" : "back")}",
			IntentType.Shove => $"{intent.Name}: swap front two",
			IntentType.Echo => $"{intent.Name}: last spell",
			IntentType.Summon => $"{intent.Name}: a {intent.Summons?.Creature.Name}",
			_ => intent.Name,
		};

	/// <summary>Where a move lands, in a word or two.</summary>
	public static string Where(Aim aim) =>
		aim switch
		{
			Aim.Back => "back",
			Aim.Pierce => "front two",
			Aim.Sweep => "all",
			Aim.Hunt => "weakest",
			Aim.Ahead => "ahead",
			_ => "front",
		};
}
