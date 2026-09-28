using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>What the board knows that the field shows: the forecast, the steps, what is lit, what is held.</summary>
public sealed record FieldContext(
	ImmutableDictionary<int, int> Forecast,
	Dictionary<int, int> Steps,
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
	/// <summary>The narrowest a place gets: five a side.</summary>
	public const int Slot = KinRelayCreature.Width;
	public const int Gap = 96;
	public const int Half = Slot * PartyBattle.MaxLine;
	public const int Width = Half * 2 + Gap;

	/// <summary>A view grows to this with few creatures a side — the mockup's three-a-side size.</summary>
	private const float MaxScale = 1.3f;
	public const int Height = (int)(KinRelayCreature.Height * MaxScale) + 8;

	/// <summary>
	/// **The width of a place NOW: the longest line spread across its half**, as the style-D mockup
	/// spreads three a side over the screen (never under five a side's 176). Safe because no drop
	/// lands on an empty place beyond a line's end — a summon lands on your front, Gust swaps their
	/// front two, deploy reorders who is there.
	/// </summary>
	private float _slot = Slot;

	private float ViewScale => Mathf.Min(MaxScale, _slot / Slot);

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
			hint.Size = new Vector2(Slot, 80);
			hint.MouseFilter = Control.MouseFilterEnum.Ignore;
			Root.AddChild(hint);
			_hints[d] = hint;
		}

		// No "YOUR LINE ▶ FRONT" caption any more: the sprites stand facing each other, which says it.
	}

	// ===== Geometry

	/// <summary>The place a drop number names, in field coordinates: your front nearest the middle.</summary>
	public Rect2 SlotRect(int drop) =>
		new(
			drop < PartyBattle.MaxLine
				? Half - (drop + 1) * _slot
				: Half + Gap + (drop - PartyBattle.MaxLine) * _slot,
			0,
			_slot,
			Height
		);

	/// <summary>Where a view stands in a place: centred, at the view's current scale.</summary>
	private Vector2 ViewAt(int drop) =>
		SlotRect(drop).Position + new Vector2((_slot - KinRelayCreature.Width * ViewScale) / 2, 0);

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
		var longest = Mathf.Max(s.LivingAllies().Count(), s.LivingFoes().Count());
		_slot = Mathf.Max(Slot, Half / (float)Mathf.Max(3, longest));

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
			_hints[d].Position = SlotRect(d).Position + new Vector2((_slot - Slot) / 2, 220);
			_hints[d].Text =
				empty && ctx.Focus is not null && ctx.Drops.Contains(d)
					? $"▲ {ctx.Focus.Name.ToUpperInvariant()} HERE"
					: "";
		}
	}

	private void Place(Creature c, CreatureLook look, int drop, HashSet<int> standing)
	{
		standing.Add(c.Id);
		var at = ViewAt(drop);
		if (!_views.TryGetValue(c.Id, out var view))
		{
			view = _views[c.Id] = new KinRelayCreature();
			view.Root.Position = at;
			Root.AddChild(view.Root);
			KinAnimator.Pop(view.Root);
		}
		view.Fit(ViewScale);
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
			KinAnimator.Lunge(view.Sprite, s.GetObject(attackerId) is Ally ? 48 : -48);
	}

	/// <summary>DEPLOY: the held creature follows the mouse until it is dropped.</summary>
	public void Follow(int creatureId, Vector2 global)
	{
		if (_views.TryGetValue(creatureId, out var view))
			view.Root.GlobalPosition = global - new Vector2(_slot / 2f, 140 * ViewScale);
	}

	// ===== Looks — strings from facts

	private static CreatureLook AllyLook(GameState s, Ally ally, FieldContext ctx)
	{
		var next = ally.Current;
		var drop = ctx.Drops.Contains(ally.Position) ? ctx.Focus : null;
		var loses = ctx.Forecast.GetValueOrDefault(ally.Id);
		var colour = KinPalette.Family(ally.Family, ally.Name);

		var (move, icon) = ally.HasActed
			? ("acted", null)
			: KinMoveText.Short(
				next,
				next.Kind == IntentType.Attack ? ally.AttackFor(next.Amount) : next.Amount
			);

		return new CreatureLook(
			move,
			icon,
			Art(ally.Name, colour.Lightened(0.45f), hostile: false),
			KinArt.Sprite(ally.Name) is not null,
			FacesLeft: false,
			ally.HasActed ? 0 : ctx.Steps.GetValueOrDefault(ally.Id),
			drop is not null || ally.Id == ctx.SelectedId || ally.Id == ctx.HeldId
				? KinPalette.Gold
				: colour,
			drop is not null || ally.Id == ctx.SelectedId || ally.Id == ctx.HeldId,
			ally.Name.ToUpperInvariant(),
			ally.Hp,
			ally.MaxHp,
			colour.Lightened(0.25f),
			Join(
				FamilyWord(ally.Family),
				ally.Block > 0
					? $"BLOCK {ally.Block}" + (ally.Rooted > 0 ? $" ({ally.Rooted} ROOTED)" : "")
					: "",
				ally.HasComponent<Grow>() ? "GROW" : "",
				ally.FadesIn > 0 ? $"TOKEN · FADES IN {ally.FadesIn}"
					: ally.BonusThorns > 0 ? $"THORNS {ally.TotalThorns}"
					: ally.Passive
			),
			drop is not null ? $"▲ {drop.Name.ToUpperInvariant()} HERE"
				: loses > 0 ? $"−{loses}"
				: "",
			drop is not null ? KinPalette.Gold : KinPalette.Red,
			ally.Level
		);
	}

	private static CreatureLook FoeLook(GameState s, Foe foe, FieldContext ctx)
	{
		var intent = foe.Current;
		var drop = ctx.Drops.Contains(PartyBattle.MaxLine + foe.Position) ? ctx.Focus : null;
		var snareHere = ctx.Snaring && s.CatchRefusal(foe) is null;
		var loses = ctx.Forecast.GetValueOrDefault(foe.Id);
		var (move, icon) = foe.Staggered
			? ("staggered", null)
			: KinMoveText.Short(intent, intent.Amount);

		return new CreatureLook(
			move,
			icon,
			Art(foe.Name, KinArt.ColourFor(foe.Name), hostile: true),
			KinArt.Sprite(foe.Name) is not null,
			FacesLeft: true,
			ctx.Steps.GetValueOrDefault(foe.Id),
			drop is not null || snareHere ? KinPalette.Gold : KinPalette.Red,
			drop is not null || snareHere,
			foe.Name.ToUpperInvariant(),
			foe.Hp,
			foe.MaxHp,
			KinPalette.Red,
			Join(
				FamilyWord(foe.Family),
				foe.Block > 0 ? $"BLOCK {foe.Block}" : "",
				foe.OffBalance > 0 ? $"OFF-BALANCE +{foe.OffBalance}" : "",
				foe.FadesIn > 0 ? $"FADES IN {foe.FadesIn}" : "",
				foe.Catchable && foe.Hp <= foe.CatchAt() ? "◆ CATCHABLE" : ""
			),
			snareHere ? "◆ SNARE IT HERE"
				: drop is not null ? $"▼ {drop.Name.ToUpperInvariant()} HERE"
				: loses > 0 ? $"−{loses}"
				: "",
			snareHere || drop is not null ? KinPalette.Gold : KinPalette.Red,
			foe.Level
		);
	}

	/// <summary>The family, as the status line's first word — "" for none (`KinFamiliesPlan.md`).</summary>
	private static string FamilyWord(Family family) =>
		family == Family.None ? "" : family.ToString().ToUpperInvariant();

	private static string Join(params string[] parts) =>
		string.Join(" · ", parts.Where(p => p.Length > 0));

	/// <summary>
	/// A standing sprite if one exists, else the portrait drawing, else the silhouette in the
	/// creature's colour — the last two sit in the medallion.
	/// </summary>
	private static Texture2D Art(string name, Color colour, bool hostile) =>
		KinArt.Sprite(name) ?? KinArt.Drawing(name) ?? KinArt.Figure(colour, hostile);
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

	/// <summary>
	/// **A move on the badge above a creature's head** — an icon and "6 → front", as the style-D
	/// mockup has it. The move's NAME is dropped here (it did not fit a place at a readable size) and
	/// kept in the inspector and on the starter screen, which use <see cref="Says"/>.
	/// </summary>
	public static (string Text, Texture2D Icon) Short(Intent intent, int amount) =>
		intent.Kind switch
		{
			IntentType.Attack => (
				$"{amount} → {Where(intent.Target)}" + (intent.Steals ? " + steal" : ""),
				KinArt.AttackIcon
			),
			IntentType.Block => (
				$"+{amount}" + (intent.Target == Aim.Ahead ? " ahead" : ""),
				KinArt.GuardIcon
			),
			IntentType.Move => (amount < 0 ? "forward" : "back", null),
			IntentType.Shove => ("swap front two", null),
			IntentType.Echo => ("echo spell", null),
			IntentType.Summon => ($"summon {intent.Summons?.Creature.Name}", null),
			_ => (intent.Name, null),
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
