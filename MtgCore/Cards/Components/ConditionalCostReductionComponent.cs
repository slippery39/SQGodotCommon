using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "This spell costs {N} less to cast if …" — Winged Words, Stormwing Entity.
///
/// Read by CostEngine.ComputeEffectiveCost, which is the single place a mana cost is adjusted, so
/// this composes correctly with affinity, convoke, X and taxes rather than fighting them.
///
/// Reuses ActivationCondition for the "if" — the question ("is this board state true for this
/// player?") is identical to the one an activated-ability gate asks, and the subclasses are shared.
///
/// TWO PLACES THIS CAN LIVE, and they mean different things:
///   - on the card being cast, gated by Condition — "THIS spell costs {1} less if …"
///   - on a permanent on your battlefield, gated by AppliesTo — "creature spells you cast with
///     power 4 or greater cost {2} less" (Goreclaw). AppliesTo is a question about the CARD being
///     cast, which is the part an ActivationCondition cannot ask: it only sees a player.
/// CostEngine scans both. See ComputeReductions there for the caster-only asymmetry.
/// </summary>
public record ConditionalCostReductionComponent : GameComponent
{
	public int Amount { get; init; } = 1;
	public ActivationCondition? Condition { get; init; }

	/// <summary>
	/// A specification the card being cast must satisfy, for a reduction that lives on a
	/// battlefield permanent rather than on the card itself. Null means "any card", which is what
	/// a self-reduction wants.
	/// </summary>
	public TargetSpecification? AppliesTo { get; init; }
}

/// <summary>"If you control a creature with flying." — Winged Words.</summary>
public record ControlsFlyingCreatureCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			if (card.ControllerId != playerId)
				continue;
			if (!card.HasComponent<CreatureComponent>())
				continue;
			if (state.GetEffectiveStats(card.Id).HasFlying)
				return true;
		}

		return false;
	}

	public override string Describe() => "You must control a creature with flying";
}

/// <summary>
/// "If you've cast an instant or sorcery spell this turn." — Stormwing Entity.
///
/// Approximated by MtgGame.SpellsCastThisTurn, which counts spells of every type: the engine
/// keeps no per-type cast history. In practice a deck casting anything before its four-drop is
/// casting a cheap spell, so the two coincide almost always.
/// </summary>
public record CastSpellThisTurnCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		(state.TryGetGame()?.SpellsCastThisTurn ?? 0) >= 1;

	public override string Describe() => "You must have cast a spell this turn";
}
