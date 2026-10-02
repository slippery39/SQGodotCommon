using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>What the board knows that the field shows: the forecast, the steps, what is lit.</summary>
public sealed record FieldContext(
	ImmutableDictionary<int, int> Forecast,
	Dictionary<int, int> Steps,
	HashSet<int> Drops,
	KinCard Focus,
	int SelectedId
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

	/// <summary>Each place's gold ground mark — lit where the held card can land on an EMPTY place.</summary>
	private readonly TextureRect[] _hints = new TextureRect[PartyBattle.MaxLine * 2];

	public KinRelayField()
	{
		Root = new Control
		{
			CustomMinimumSize = new Vector2(Width, Height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		for (var d = 0; d < _hints.Length; d++)
		{
			// The creature's own drop mark (`ui/contact`, gold) on bare ground: a highlight, not words.
			var hint = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Texture = KinArt.Drawing("ui/contact"),
				Size = new Vector2(Slot * 0.7f, 40),
				Modulate = new Color(KinPalette.Gold, 0.95f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false,
			};
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
		var standing = new HashSet<int>();
		var longest = Mathf.Max(s.LivingAllies().Count(), s.LivingFoes().Count());
		_slot = Mathf.Max(Slot, Half / (float)Mathf.Max(3, longest));

		foreach (var ally in s.LivingAllies())
			Place(ally, AllyLook(s, ally, ctx), ally.Position, standing);
		foreach (var foe in s.LivingFoes())
			Place(foe, FoeLook(s, foe, ctx), PartyBattle.MaxLine + foe.Position, standing);

		foreach (var id in _views.Keys.Where(id => !standing.Contains(id)).ToList())
			_targets.Remove(id);

		// A drop on an EMPTY place — a Summon on your line, a Gust on theirs — lights the place.
		var allies = s.LivingAllies().Count();
		var foes = s.LivingFoes().Count();
		for (var d = 0; d < _hints.Length; d++)
		{
			var empty = d < PartyBattle.MaxLine ? d >= allies : d - PartyBattle.MaxLine >= foes;
			_hints[d].Position = SlotRect(d).Position + new Vector2((_slot - Slot * 0.7f) / 2, 220);
			_hints[d].Visible = empty && ctx.Focus is not null && ctx.Drops.Contains(d);
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
			if (view.Root.Position != to)
				KinAnimator.Slide(view.Root, to, delay);
		}
	}

	/// <summary>A blow: the attacker's sprite jabs toward the other line; its words stay put.</summary>
	public void Lunge(GameState s, int attackerId)
	{
		if (_views.TryGetValue(attackerId, out var view))
			KinAnimator.Lunge(view.Sprite, s.GetObject(attackerId) is Ally ? 48 : -48);
	}

	// ===== Looks — strings from facts

	private static CreatureLook AllyLook(GameState s, Ally ally, FieldContext ctx)
	{
		var drop = ctx.Drops.Contains(ally.Position) ? ctx.Focus : null;
		var loses = ctx.Forecast.GetValueOrDefault(ally.Id);
		var colour = KinPalette.Family(ally.Family, ally.Name);

		// **Round 4: a monster has no move — its badge is its FIRST-ATTACK bonus**, lit until the first
		// attack card on it this turn spends it.
		// Spent (or none): no badge at all — "attacked" was a word for an absence.
		var (move, icon2, tint2) =
			ally.GetComponent<FirstAttack>() is { } bonus && PartyMonsters.BonusReady(ally)
				? KinMoveText.BonusSymbol(bonus)
				: ("", null, KinPalette.Bone);
		var icon = icon2 is null && move.Length == 0 ? null : KinArt.AttackIcon;

		return new CreatureLook(
			move,
			icon,
			icon2,
			tint2,
			[],
			Art(ally.Name, colour.Lightened(0.45f), hostile: false),
			KinArt.Sprite(ally.Name) is not null,
			FacesLeft: false,
			0,
			drop is not null || ally.Id == ctx.SelectedId ? KinPalette.Gold : colour,
			drop is not null || ally.Id == ctx.SelectedId,
			ally.Name.ToUpperInvariant(),
			ally.Hp,
			ally.MaxHp,
			colour.Lightened(0.25f),
			ally.Block,
			PartyFamilies.RootedOf(s, ally) > 0,
			Chips(
				// A token's Power matters too (Grove draft 1: tokens attack).
				new Chip(
					KinArt.PowerIcon,
					$"{ally.Power + ally.BonusPower}",
					KinPalette.Bone,
					$"Power {ally.Power + ally.BonusPower}: {KinSymbols.Power.Meaning}"
				),
				ally.SpellPower > 0
					? new Chip(
						KinArt.SpellPowerIcon,
						$"{ally.SpellPower}",
						EmberTint,
						$"Spell Power {ally.SpellPower}: it adds this to your team's {KinSymbols.SpellPower.Name}."
					)
					: null,
				ally.TotalThorns > 0
					? new Chip(
						KinArt.ThornsIcon,
						$"{ally.TotalThorns}",
						GroveTint,
						$"Thorns {ally.TotalThorns}: {KinSymbols.Thorns.Meaning}"
					)
					: null,
				ally.IsToken
					? ally.FadesIn > 0
						? new Chip(
							KinArt.ClockIcon,
							$"{ally.FadesIn}",
							KinPalette.Bone,
							$"A token that fades in {ally.FadesIn} turn{(ally.FadesIn == 1 ? "" : "s")}."
						)
						: new Chip(KinArt.GrowIcon, "", GroveTint, KinSymbols.Token.Line)
					: null,
				!ally.IsToken && ally.Passive.Length > 0
					? new Chip(
						KinArt.PassiveIcon,
						"",
						KinPalette.Gold,
						$"{ally.Passive}: {ally.PassiveRule}"
					)
					: null
			),
			BlockTip(s, ally),
			ally.GetComponent<FirstAttack>() is { } firstBonus && PartyMonsters.BonusReady(ally)
				? $"First attack this turn: {firstBonus.Text}."
				: "",
			loses > 0 ? $"−{loses}" : "",
			KinPalette.Red
		);
	}

	private static CreatureLook FoeLook(GameState s, Foe foe, FieldContext ctx)
	{
		var intent = foe.Current;
		var drop = ctx.Drops.Contains(PartyBattle.MaxLine + foe.Position) ? ctx.Focus : null;
		var loses = ctx.Forecast.GetValueOrDefault(foe.Id);
		var (move, icon, icon2) =
			foe.Staggered ? ("dazed", null, null)
			: intent.Kind == IntentType.WindUp ? KinMoveText.WindingUp(foe)
			: KinMoveText.Short(intent, intent.Amount);
		// Where an attack lands, as the ENGINE aims it (the forecast's own account): one dot a place
		// in your line, back to front as it stands on screen.
		var hit =
			intent.Kind == IntentType.Attack && !foe.Staggered
				? s.IntentTargets(foe).ToHashSet()
				: [];
		var line = s.LivingAllies().OrderByDescending(a => a.Position).ToList();
		ImmutableList<bool> pips = hit.Count > 0 ? [.. line.Select(a => hit.Contains(a.Id))] : [];

		return new CreatureLook(
			move,
			icon,
			icon2,
			KinPalette.Red.Lightened(0.3f),
			pips,
			Art(foe.Name, KinArt.ColourFor(foe.Name), hostile: true),
			KinArt.Sprite(foe.Name) is not null,
			FacesLeft: true,
			ctx.Steps.GetValueOrDefault(foe.Id),
			drop is not null ? KinPalette.Gold : KinPalette.Red,
			drop is not null,
			foe.Name.ToUpperInvariant(),
			foe.Hp,
			foe.MaxHp,
			KinPalette.Red,
			foe.Block,
			false,
			Chips(
				foe.Burn > 0
					? new Chip(
						KinArt.BurnIcon,
						$"{foe.Burn}",
						EmberTint,
						$"Burn {foe.Burn}: {KinSymbols.Burn.Meaning}"
					)
					: null,
				foe.OffBalance > 0
					? new Chip(
						KinArt.AttackIcon,
						$"+{foe.OffBalance}",
						KinPalette.Red.Lightened(0.3f),
						$"Off-Balance: every hit on it this round deals {foe.OffBalance} more."
					)
					: null,
				foe.FadesIn > 0
					? new Chip(
						KinArt.ClockIcon,
						$"{foe.FadesIn}",
						KinPalette.Bone,
						$"Fades in {foe.FadesIn} turn{(foe.FadesIn == 1 ? "" : "s")}."
					)
					: null,
				foe.Trait.Length > 0
					? new Chip(KinArt.PassiveIcon, "", KinPalette.Red.Lightened(0.3f), foe.Trait)
					: null
			),
			foe.Block > 0 ? $"Block {foe.Block}: it stops that much damage." : "",
			foe.Staggered ? "Dazed: it loses this move."
				: intent.Kind == IntentType.WindUp
					? $"Winding up. Next: {KinMoveText.Says(foe.Pattern[(foe.PatternIndex + 1) % foe.Pattern.Count], foe.Pattern[(foe.PatternIndex + 1) % foe.Pattern.Count].Amount)}."
				: KinMoveText.Says(intent, intent.Amount)
					+ (
						intent.Kind == IntentType.Attack
							? ". The dots: your line, back to front; filled is hit."
							: "."
					),
			loses > 0 ? $"−{loses}" : "",
			KinPalette.Red
		);
	}

	// The palette's Ember brown read as mud at chip size over a meadow: the brighter flame orange.
	/// <summary>A monster's Block, said with its Rooted part.</summary>
	private static string BlockTip(GameState s, Ally ally)
	{
		if (ally.Block <= 0)
			return "";
		var rooted = PartyFamilies.RootedOf(s, ally);
		return $"Block {ally.Block}: {KinSymbols.Block.Meaning}"
			+ (rooted > 0 ? $" {rooted} of it is Rooted: it stays one more turn." : "");
	}

	/// <summary>
	/// **The meaning of the symbol under the mouse, on whichever creature it is over** — or null.
	/// </summary>
	public string TipAt(GameState s, Vector2 global) =>
		CreatureAt(s, global) is { } hit && _views.TryGetValue(hit.Creature.Id, out var view)
			? view.TipAt(global)
			: null;

	private static readonly Color EmberTint = Color.FromHtml("#FF9A3C");
	private static readonly Color GroveTint = KinPalette.Family(Family.Grove).Lightened(0.35f);

	/// <summary>
	/// **The status row** — the ones that apply, in a fixed order. No family word: the creature's
	/// colour already says it, and a foe's family means nothing to you (the declutter pass).
	/// </summary>
	private static ImmutableList<Chip> Chips(params Chip?[] chips) => [.. chips.OfType<Chip>()];

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
				+ (intent.Crushes ? ", ignores Block" : "")
				+ (intent.Steals ? " + steals" : ""),
			IntentType.Block when intent.Target == Aim.Ahead =>
				$"{intent.Name}: +{amount} block ahead",
			IntentType.Block => $"{intent.Name}: +{amount} block",
			IntentType.Move => $"{intent.Name}: {(amount < 0 ? "forward" : "back")}",
			IntentType.Shove => $"{intent.Name}: swap front two",
			IntentType.Echo => $"{intent.Name}: last spell",
			IntentType.Summon => $"{intent.Name}: a {intent.Summons?.Creature.Name}",
			IntentType.WindUp => $"{intent.Name}: winds up the next move",
			IntentType.Pull => $"{intent.Name}: your back monster to the front",
			_ => intent.Name,
		};

	/// <summary>A FIRST-ATTACK bonus, short enough for a badge: "+4 dmg, +5 block".</summary>
	public static string Bonus(FirstAttack b) =>
		string.Join(
			", ",
			new[]
			{
				b.Damage > 0 ? $"+{b.Damage} dmg" : "",
				b.Block > 0 ? $"+{b.Block} block" : "",
				b.Rooted > 0 ? $"+{b.Rooted} rooted" : "",
				b.Thorns > 0 ? $"+{b.Thorns} thorns" : "",
				b.SpellPower > 0 ? $"+{b.SpellPower} SP" : "",
				b.FightSpellPower > 0 ? $"+{b.FightSpellPower} SP (fight)" : "",
				b.Burn > 0 ? $"{b.Burn} burn" : "",
				b.Energy > 0 ? $"+{b.Energy} energy" : "",
				b.Draw > 0 ? $"draw {b.Draw}" : "",
				b.Grow > 0 ? $"grows {b.Grow}" : "",
				b.Summons is { } t ? $"+{t.Creature.Name.ToLowerInvariant()}" : "",
			}.Where(p => p.Length > 0)
		);

	/// <summary>
	/// **A wind-up's badge shows the move it is winding up to** — "next: 18 → front" — so the big
	/// blow is on screen a whole turn before it lands.
	/// </summary>
	/// <summary>A wind-up: the clock, then the blow it is winding up to.</summary>
	public static (string Text, Texture2D Icon, Texture2D Icon2) WindingUp(Foe foe)
	{
		var next = foe.Pattern[(foe.PatternIndex + 1) % foe.Pattern.Count];
		var (text, icon, _) = Short(next, next.Amount);
		return (text, KinArt.ClockIcon, icon);
	}

	/// <summary>
	/// **A first-attack bonus as a symbol and a number** — "+4" beside a shield, not "1st: +4 block".
	/// The first thing it gives; the inspector has every word.
	/// </summary>
	public static (string Text, Texture2D Icon, Color Tint) BonusSymbol(FirstAttack b)
	{
		var ember = KinPalette.Family(Family.Ember).Lightened(0.4f);
		var grove = KinPalette.Family(Family.Grove).Lightened(0.4f);
		return b switch
		{
			{ Damage: > 0 } => ($"+{b.Damage}", KinArt.PowerIcon, KinPalette.Bone),
			{ Block: > 0 } => ($"+{b.Block}", KinArt.GuardIcon, KinPalette.Bone),
			{ Rooted: > 0 } => ($"+{b.Rooted}", KinArt.GuardIcon, grove),
			{ Thorns: > 0 } => ($"+{b.Thorns}", KinArt.ThornsIcon, grove),
			{ SpellPower: > 0 } => ($"+{b.SpellPower}", KinArt.SpellPowerIcon, ember),
			{ FightSpellPower: > 0 } => (
				$"+{b.FightSpellPower}",
				KinArt.SpellPowerIcon,
				KinPalette.Gold
			),
			{ Burn: > 0 } => ($"{b.Burn}", KinArt.BurnIcon, ember),
			{ Energy: > 0 } => ($"+{b.Energy}", KinArt.EnergyIcon, KinPalette.Gold),
			{ Draw: > 0 } => ($"+{b.Draw}", KinArt.DrawIcon, KinPalette.Bone),
			{ Grow: > 0 } => ($"+{b.Grow}", KinArt.PowerIcon, grove),
			{ Summons: { } t } => ("", KinArt.GrowIcon, grove),
			_ => ("", null, KinPalette.Bone),
		};
	}

	/// <summary>
	/// **A move on the badge above a creature's head** — an icon and "6 → front", as the style-D
	/// mockup has it. The move's NAME is dropped here (it did not fit a place at a readable size) and
	/// kept in the inspector and on the starter screen, which use <see cref="Says"/>.
	/// </summary>
	public static (string Text, Texture2D Icon, Texture2D Icon2) Short(Intent intent, int amount) =>
		intent.Kind switch
		{
			// Where it lands is the badge's dots, not a word; CRUSH is its cracked shield.
			IntentType.Attack => (
				$"{amount}" + (intent.Steals ? " +steal" : ""),
				intent.Crushes ? KinArt.CrushIcon : KinArt.AttackIcon,
				null
			),
			IntentType.Block => (
				$"+{amount}" + (intent.Target == Aim.Ahead ? " ahead" : ""),
				KinArt.GuardIcon,
				null
			),
			IntentType.Move => (amount < 0 ? "forward" : "back", null, null),
			IntentType.Shove => ("swap", null, null),
			IntentType.Echo => ("echo", null, null),
			IntentType.Summon => ($"+{intent.Summons?.Creature.Name}", null, null),
			IntentType.Pull => ("pull", null, null),
			_ => (intent.Name, null, null),
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
