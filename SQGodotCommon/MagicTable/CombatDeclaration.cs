using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicTable;

/// <summary>
/// Arena's combat declaration, on the table: click your creatures to attack (click again to take one
/// back; click a planeswalker to send the last one there), or click your creature and then the
/// attacker it blocks — then submit the whole declaration once
/// (<see cref="MagicTableManager.DeclareCombat"/>). Plain C#: who may attack or block is the
/// engine's (<see cref="Combat.AttackCandidates"/>, <see cref="Combat.BlockCandidates"/>,
/// <see cref="Combat.CanBlock"/>).
/// </summary>
public sealed class CombatDeclaration(bool attacking)
{
	private readonly Dictionary<int, int> _declared = new();

	public bool Attacking { get; } = attacking;

	/// <summary>Creature → the player or planeswalker it attacks, or the attacker it blocks.</summary>
	public IReadOnlyDictionary<int, int> Declared => _declared;

	/// <summary>Attacking: the last creature picked (a planeswalker click redirects it). Blocking: the blocker awaiting its attacker.</summary>
	public int Selected { get; private set; }

	/// <summary>Why the last submit was refused (the engine's verdict); empty when it wasn't.</summary>
	public IReadOnlyList<string> Refused { get; set; } = [];

	/// <summary>
	/// What may be clicked. Attack targets are the ones the engine offers the creature it is asking
	/// about now (<paramref name="choice"/>) — the player and the planeswalkers it may attack.
	/// </summary>
	public IReadOnlySet<int> Clickable(GameState s, ChoiceAction choice)
	{
		if (Attacking)
		{
			var attack = Combat.AttackCandidates(s).Select(c => c.Id).ToHashSet();
			if (Selected != 0)
				attack.UnionWith(
					choice
						.Options.Where(o => o.IsEnabled && o.Id != CombatOption.None)
						.Select(o => o.Id)
				);
			return attack;
		}
		var block = Combat.BlockCandidates(s).Select(c => c.Id).ToHashSet();
		if (Selected != 0)
			block.UnionWith(
				Combat
					.Attackers(s)
					.Where(a => Combat.CanBlock(s, s.GetCard(Selected), a))
					.Select(a => a.Id)
			);
		return block;
	}

	public void Click(GameState s, ChoiceAction choice, int id)
	{
		Refused = [];
		if (Attacking && Combat.AttackCandidates(s).Any(c => c.Id == id))
		{
			if (_declared.Remove(id))
				Selected = Selected == id ? 0 : Selected;
			else
			{
				_declared[id] = Combat.Defender(s);
				Selected = id;
			}
		}
		else if (!Attacking && Combat.BlockCandidates(s).Any(c => c.Id == id))
		{
			if (_declared.Remove(id))
				Selected = Selected == id ? 0 : Selected; // taking one block back keeps another selected
			else
				Selected = Selected == id ? 0 : id;
		}
		else if (Selected != 0 && Clickable(s, choice).Contains(id))
		{
			_declared[Selected] = id; // attacking: a planeswalker or back to the player; blocking: the attacker
			if (!Attacking)
				Selected = 0;
		}
	}

	/// <summary>"All attack": every creature that can, at the player — then submit or adjust.</summary>
	public void AllAttack(GameState s)
	{
		Refused = [];
		foreach (var c in Combat.AttackCandidates(s))
			if (_declared.TryAdd(c.Id, Combat.Defender(s)))
				Selected = c.Id; // the last one may still be sent at a planeswalker
	}

	/// <param name="clickable">This redraw's <see cref="Clickable"/>, computed once.</param>
	public Glow GlowOf(int id, Card? c, IReadOnlySet<int> clickable) =>
		_declared.ContainsKey(id) ? (Attacking ? Glow.Attacking : Glow.Blocking)
		: id == Selected ? Glow.Selected
		: clickable.Contains(id) ? Glow.Choosable
		: c?.AttackingId > 0 ? Glow.Attacking
		: Glow.None;

	public string Status(GameState s)
	{
		var how = Attacking
			? "Declare attackers: click a creature to attack, click it again to take it back."
				+ (Planeswalkers(s).Any() ? " Then click a planeswalker to send it there." : "")
			: "Declare blockers: click your creature, then the attacker it blocks.";
		var lines = _declared.Select(d => $"{s.GetCard(d.Key).Name} → {s.GetObject(d.Value).Name}");
		return string.Join("\n", lines.Prepend(how).Concat(Refused));
	}

	private static IEnumerable<int> Planeswalkers(GameState s) =>
		s.CardsIn(Combat.Defender(s), ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Planeswalker))
			.Select(c => c.Id);
}
