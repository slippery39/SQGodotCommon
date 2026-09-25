using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **SURGE — more energy than the turn allows, at a price** (docs/paper/round-one-synergies.md).
/// Energy is MtgCore's temporary mana (`AddTemporaryManaAction` → <see cref="GainEnergyAction"/>);
/// every cost change goes through <see cref="PartyState.CostOf"/>, MtgCore's CostEngine rule.
/// </summary>
public record BorrowEnergyAction : GameAction
{
	/// <summary>Taken off next turn's energy.</summary>
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(s.UpdateObject(party.Id, party with { EnergyDebt = party.EnergyDebt + Amount }));
	}
}

/// <summary>Quicken: the next card you play this turn costs 0.</summary>
public record NextCardFreeAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(s.UpdateObject(party.Id, party with { NextCardFree = true }));
	}
}

/// <summary>Battle Cry: energy, but only if a foe died during this turn.</summary>
public record GainEnergyIfFoeDiedAction : GameAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return party.FoesDefeatedThisTurn > 0
			? new(s.UpdateObject(party.Id, party with { Energy = party.Energy + Amount }))
			: new(s);
	}
}

/// <summary>
/// **The first card you play each turn costs this much more (a foe's tax, the Hushcap) or less
/// (a monster's discount)** — MtgCore's taxes and battlefield reductions, read by `CostOf`.
/// </summary>
public record FirstCardCost : GameComponent
{
	public int Amount { get; init; }
}

/// <summary>
/// **An X card: it costs all your energy**, and remembers how much (`PartyBattle.XPaid`) for its
/// effect — MtgCore's XCostComponent.
/// </summary>
public record SpendsAllEnergy : GameComponent;

/// <summary>**The Glowmoth: at the start of your turn, if it was not hit last turn, +N energy.**</summary>
public record EnergyIfUnhit : GameComponent
{
	public int Amount { get; init; } = 1;
}

/// <summary>
/// **A foe died.** Staged by `HitFoe`, stamped with whether it was during YOUR turn — the end of
/// the turn resolves after the next turn is queued, so the stamp is the only safe way to tell.
/// </summary>
public record FoeDefeatedEvent : GameEvent
{
	public int FoeId { get; init; }
	public bool DuringYourTurn { get; init; }
}

/// <summary>The Stormbuck: a kill made with your hand (Hasten, Strike, a spell) — not the end of turn.</summary>
public record OnFoeDefeatedDuringYourTurn : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) =>
		e is FoeDefeatedEvent { DuringYourTurn: true };
}
