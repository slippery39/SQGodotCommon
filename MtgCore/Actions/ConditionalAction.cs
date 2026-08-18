using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Runs an inner action only if a condition holds — "if you have less life than an opponent,
/// you gain 6 life" (Timely Reinforcements).
///
/// Reuses ActivationCondition rather than inventing a parallel condition hierarchy for spells:
/// the question ("is this board state true for this player?") is identical, and the subclasses
/// are shared.
///
/// The condition is evaluated at RESOLUTION, which is what an intervening-if clause requires —
/// a card that checks life totals must check them when it resolves, not when it was cast.
/// </summary>
public record ConditionalAction : GameAction, ITargetedAction
{
	public ActivationCondition? Condition { get; init; }
	public GameAction? Action { get; init; }
	public string PlayerIdContextKey { get; init; } = ContextKeys.CastingPlayerId;

	/// <summary>
	/// Targets chosen for this effect, forwarded to the inner action.
	///
	/// Without this a conditional effect could only ever be NoTarget, because ResolveEffectAction
	/// injects targets solely into an ITargetedAction. That blocked "spell mastery — that
	/// creature enters with two +1/+1 counters" (Necromantic Summons), where the conditional half
	/// must land on the SAME creature the unconditional half chose. Giving both effects the same
	/// targeting strategy is what pairs them: MtgActionGenerator fills one chosen target into
	/// every user-select effect on the spell.
	/// </summary>
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		if (Action == null)
			return new ActionResult(gameState);

		var playerId = GetInput<int>(PlayerIdContextKey, 0);
		if (playerId == 0)
			return new ActionResult(gameState);

		if (Condition != null && !Condition.IsSatisfied(gameState, 0, playerId))
			return new ActionResult(gameState);

		// The inner action inherits this action's context so it can still read CastingPlayerId
		// and SourceCardId — without that a nested GainLifeAction has no idea who to target.
		var inner = Action with
		{
			InputContext = InputContext,
		};

		if (!TargetIds.IsEmpty && inner is ITargetedAction targeted)
			inner = targeted.WithTargets(TargetIds);
		// EffectActions resolve their targets from context; a nested one that targets "you"
		// needs the player id handed to it explicitly.
		else if (inner is EffectAction effect && effect.TargetIds.IsEmpty)
			inner = effect with { TargetIds = ImmutableList.Create(playerId) };

		return new ActionResult(gameState.SpawnAction(inner));
	}
}

/// <summary>"You have less life than an opponent." — Timely Reinforcements.</summary>
public record HasLessLifeThanOpponentCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		Compare(state, playerId, (you, opponent) => you < opponent);

	public override string Describe() => "You must have less life than an opponent";

	internal static bool Compare(GameState state, int playerId, Func<int, int, bool> predicate)
	{
		if (state.GetObject(playerId) is not MtgPlayer player)
			return false;

		var p1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var opponentId = playerId == p1Id ? state.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;

		return state.GetObject(opponentId) is MtgPlayer opponent
			&& predicate(player.Life, opponent.Life);
	}
}

/// <summary>"You control fewer creatures than an opponent." — Timely Reinforcements.</summary>
public record ControlsFewerCreaturesCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		var p1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var opponentId = playerId == p1Id ? state.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;

		return CountCreatures(state, playerId) < CountCreatures(state, opponentId);
	}

	public override string Describe() => "You must control fewer creatures than an opponent";

	private static int CountCreatures(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		return battlefieldId == 0
			? 0
			: state.GetCardsInZone(battlefieldId).Count(c => c.HasComponent<CreatureComponent>());
	}
}

/// <summary>
/// "As long as your life total is greater than or equal to your starting life total."
/// — Path of Bravery. The at-or-above counterpart to LifeAboveStartingCondition, which needs a
/// strict surplus.
/// </summary>
public record LifeAtOrAboveStartingCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		state.GetObject(playerId) is MtgPlayer player && player.Life >= player.StartingLife;

	public override string Describe() => "Your life must be at or above your starting total";
}
