using System.Linq;
using Godot;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **The hover panel for a creature — yours or a foe** (Shayne's first AUTO-BATTLE run: "I was
/// playing kind of blind"). Stats, what Speed means THIS turn, the passive's rule, and the whole move
/// cycle in plain words with the next move marked. The cell only has room for the next move; this is
/// where the rest lives.
///
/// Presentation only: every number comes from the creature and `PartyState`. Catches no mouse — the
/// board hit-tests the hover itself, as it does clicks.
/// </summary>
public sealed class KinPartyInspector
{
	public const int Width = 400;

	private readonly PanelContainer _root;
	private readonly VBoxContainer _lines;

	public KinPartyInspector(Node parent)
	{
		_root = new PanelContainer
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(Width, 0),
			// Above the hand's fan, whose cards carry their own ZIndex.
			ZIndex = 100,
		};
		parent.AddChild(_root);

		var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 12);
		_root.AddChild(margin);

		_lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_lines.AddThemeConstantOverride("separation", 4);
		margin.AddChild(_lines);
	}

	public void Hide() => _root.Visible = false;

	private Rect2 _cell;
	private Vector2 _screen;

	/// <summary>Shows the panel beside `cell` — to its right, or its left if it would run off screen.</summary>
	public void Show(Creature creature, int order, Rect2 cell, Vector2 screen)
	{
		foreach (var child in _lines.GetChildren())
		{
			_lines.RemoveChild(child);
			child.QueueFree();
		}

		var ally = creature as Ally;
		var colour = ally is null ? KinPalette.Red : KinPalette.Companion(ally.Name);
		_root.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Navy, colour, 3));

		Line(creature.Name.ToUpperInvariant(), 26, KinPalette.Bone);
		Line(
			$"HP {creature.Hp}/{creature.MaxHp}"
				+ (ally is null ? "" : $" · POWER {ally.Power + ally.BonusPower}")
				+ $" · SPEED {creature.Speed}",
			18,
			KinPalette.Bone
		);
		Line(
			ally is { HasActed: true } ? "Has already acted this turn."
				: order > 0 ? $"Acts {Ordinal(order)} when you end the turn — faster goes first."
				: "",
			16,
			KinPalette.Bone
		);

		if (ally is not null && ally.Passive.Length > 0)
		{
			Line(ally.Passive, 18, KinPalette.Gold);
			Line(ally.PassiveRule, 16, KinPalette.Bone);
		}

		foreach (var status in Statuses(creature))
			Line(status, 16, KinPalette.Gold);

		Line("MOVES — in this order, then round again:", 18, KinPalette.Bone);
		var next = creature.PatternIndex % creature.Pattern.Count;
		for (var i = 0; i < creature.Pattern.Count; i++)
		{
			var move = creature.Pattern[i];
			var amount =
				ally is not null && move.Kind == IntentType.Attack
					? move.Amount + ally.Power + ally.BonusPower
					: move.Amount;
			var isNext = i == next && ally is not { HasActed: true };
			Line(
				$"{(isNext ? "▶ NEXT  " : "")}{move.Name}: {Explain(move, amount, ally is not null)}",
				16,
				isNext ? KinPalette.Gold : new Color(KinPalette.Bone, 0.85f)
			);
		}

		_cell = cell;
		_screen = screen;
		_root.Visible = true;
		Fit();
	}

	/// <summary>
	/// **Sizes and places the panel — called every frame it shows.** A wrapped label's height is only
	/// known once its container has laid it out, a frame later; measured at once, every word took a
	/// line and the panel ran the full height of the screen. A panel outside a container never
	/// shrinks by itself, so it is reset each frame.
	/// </summary>
	public void Fit()
	{
		if (!_root.Visible)
			return;
		_root.ResetSize();
		var x = _cell.End.X + 12;
		if (x + Width > _screen.X)
			x = _cell.Position.X - Width - 12;
		var y = Mathf.Clamp(_cell.Position.Y, 8, Mathf.Max(8, _screen.Y - _root.Size.Y - 8));
		_root.Position = new Vector2(x, y);
	}

	/// <summary>What is true of it right now that the cycle does not say.</summary>
	private static string[] Statuses(Creature creature) =>
		creature switch
		{
			Ally a =>
			[
				.. new[]
				{
					a.Block > 0 ? $"BLOCK {a.Block} — soaks damage until your next turn." : "",
					a.BonusThorns > 0 ? $"THORNS {a.TotalThorns} this turn." : "",
					a.Momentum > 0 ? $"MOMENTUM: its next attack deals +{a.Momentum}." : "",
					a.StepsLeft > 0
						? $"Can step {a.StepsLeft} more time{(a.StepsLeft == 1 ? "" : "s")} this turn."
						: "",
				}.Where(s => s.Length > 0),
			],
			Foe f =>
			[
				.. new[]
				{
					f.Block > 0 ? $"BLOCK {f.Block} — drops when it next acts." : "",
					f.OffBalance > 0
						? $"OFF-BALANCE: takes +{f.OffBalance} from every hit this turn."
						: "",
					f.Staggered ? "STAGGERED: loses its next move." : "",
				}.Where(s => s.Length > 0),
			],
			_ => [],
		};

	/// <summary>A move in words — the cell's short form (`KinPartyCell.Says`) spelled out.</summary>
	private static string Explain(Intent move, int amount, bool mine)
	{
		var victims = mine ? "foe" : "monster";
		return move.Kind switch
		{
			IntentType.Attack when move.Homing =>
				$"{amount} damage to the lowest-HP {victims}, wherever it stands.",
			IntentType.Attack => $"{amount} damage {Shape([.. move.Offsets])}.",
			IntentType.Block => mine
				? $"gains {amount} Block."
				: $"gains {amount} Block, which holds through your turn.",
			IntentType.Move => $"steps {Mathf.Abs(amount)} {(amount < 0 ? "left" : "right")}.",
			IntentType.Push =>
				$"pushes the foe ahead of it {Mathf.Abs(amount)} {(amount < 0 ? "left" : "right")}.",
			_ => "",
		};
	}

	/// <summary>Which columns an attack covers, relative to the attacker's own.</summary>
	private static string Shape(int[] offsets) =>
		offsets switch
		{
			[0] => "straight ahead",
			[-1, 0, 1] => "ahead and to both sides (3 wide)",
			_ => string.Join(
				" and ",
				offsets.Select(o =>
					o == 0 ? "ahead" : $"{Mathf.Abs(o)} {(o < 0 ? "left" : "right")}"
				)
			) + $" ({offsets.Length} wide)",
		};

	private static string Ordinal(int n) =>
		n switch
		{
			1 => "1st",
			2 => "2nd",
			3 => "3rd",
			_ => $"{n}th",
		};

	private void Line(string text, int size, Color colour)
	{
		if (text.Length == 0)
			return;
		var label = KinPalette.Text(text, size, colour, HorizontalAlignment.Left);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		// A FIXED width, not 1: a wrapped label measures its height at the width it is given.
		label.CustomMinimumSize = new Vector2(Width - 24, 0);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		_lines.AddChild(label);
	}
}
