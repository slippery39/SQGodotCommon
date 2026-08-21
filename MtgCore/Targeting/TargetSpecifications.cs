using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Matches any MtgPlayer object that hasn't lost.
/// </summary>
public record IsPlayerSpecification : TargetSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context)
	{
		var state = context.GameState;
		return
		[
			state.GetWellKnownId(MtgObjectKeys.Player1),
			state.GetWellKnownId(MtgObjectKeys.Player2),
		];
	}

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.Find(candidateId);
		return obj is MtgPlayer player && !player.HasLost;
	}
}

/// <summary>
/// Matches any card with a CreatureComponent currently on any battlefield.
/// </summary>
public record IsCreatureSpecification : TargetSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context)
	{
		var state = context.GameState;
		return state
			.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player1Battlefield))
			.Concat(state.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player2Battlefield)));
	}

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.Find(candidateId);
		if (obj is not Card card)
			return false;

		if (!card.HasComponent<CreatureComponent>())
			return false;

		var zone = context.GameState.GetCardZone(candidateId);
		if (zone.ZoneType != ZoneType.Battlefield)
			return false;

		var creature = card.GetComponent<CreatureComponent>()!;

		if (!context.IsNonTargeted)
		{
			if (creature.HasShroud)
				return false;
			if (creature.HasHexproof && card.ControllerId != context.CastingPlayerId)
				return false;

			foreach (var kw in card.GetComponents<AppliedKeywordComponent>())
			{
				if (kw.GrantsShroud)
					return false;
				if (kw.GrantsHexproof && card.ControllerId != context.CastingPlayerId)
					return false;
			}

			// Protection from a creature type: untargetable by a source of that type. Sits with
			// Shroud/Hexproof because all three are "this creature can't be chosen" rules and
			// splitting them would let one drift out of sync with the others.
			if (context.GameState.IsProtectedFrom(candidateId, context.SourceCardId))
				return false;
		}

		return true;
	}
}

/// <summary>
/// Matches any card with a PlaneswalkerComponent currently on any battlefield.
///
/// Until this existed no spell could name a planeswalker at all: every targeting helper was built
/// from IsCreatureSpecification and IsPlayerSpecification, and a walker is neither. Combat could
/// attack one (AttackAction handles it) but no burn spell, exile effect or bounce could touch it,
/// which made every walker in the cube unanswerable except by attacking it.
/// </summary>
public record IsPlaneswalkerSpecification : TargetSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context)
	{
		var state = context.GameState;
		return state
			.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player1Battlefield))
			.Concat(state.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player2Battlefield)));
	}

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card card)
			return false;

		if (!card.HasComponent<PlaneswalkerComponent>())
			return false;

		return context.GameState.GetCardZone(candidateId).ZoneType == ZoneType.Battlefield;
	}
}

/// <summary>
/// Matches only objects controlled by the casting player.
/// </summary>
public record IsControlledByYouSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.Find(candidateId);

		return obj switch
		{
			MtgPlayer player => player.Id == context.CastingPlayerId,
			Card card => card.ControllerId == context.CastingPlayerId,
			_ => false,
		};
	}
}

/// <summary>
/// Matches only objects controlled by an opponent of the casting player.
/// </summary>
public record IsControlledByOpponentSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		var obj = context.Find(candidateId);

		return obj switch
		{
			MtgPlayer player => player.Id != context.CastingPlayerId,
			Card card => card.ControllerId != context.CastingPlayerId,
			_ => false,
		};
	}
}

/// <summary>
/// Matches any card currently in the casting player's hand.
/// Use with IsSubtypeSpecification to target e.g. "a Goblin in your hand."
/// </summary>
public record IsInHandSpecification : ZoneSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context) =>
		context.GameState.GetChildrenIds(
			context.GameState.GetPlayerZoneId(context.CastingPlayerId, ZoneType.Hand)
		);

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card)
			return false;

		var handId = context.GameState.GetPlayerZoneId(context.CastingPlayerId, ZoneType.Hand);
		return context.GameState.GetCardZoneId(candidateId) == handId;
	}
}

/// <summary>
/// Matches when the candidate ID equals the SourceCardId of the targeting context.
/// Used in EventTriggerCondition filters to express "when this specific card triggers"
/// (e.g. Goblin Lackey fires only when Lackey itself deals combat damage).
/// </summary>
public record IsSourceCardSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		candidateId == context.SourceCardId;
}

/// <summary>
/// Matches any candidate that is NOT the source card of the targeting context.
/// Used with lord/anthem effects to express "other creatures" (e.g. Goblin Chieftain
/// buffs other Goblins, not itself).
/// </summary>
public record IsNotSelfSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) =>
		candidateId != context.SourceCardId;
}

/// <summary>
/// Never matches any candidate. Used as a safe default on StaticAbilityComponent
/// so the filter must be explicitly set on every concrete ability.
/// </summary>
public record AlwaysFalseSpecification : TargetSpecification
{
	public override bool IsSatisfiedBy(int candidateId, TargetingContext context) => false;
}

/// <summary>
/// Matches any instant or sorcery card currently in the casting player's own graveyard.
/// "Instant or sorcery" is identified by the presence of SpellComponent.
/// </summary>
public record IsInstantOrSorceryInOwnGraveyardSpecification : ZoneSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context) =>
		context.GameState.GetChildrenIds(
			context.GameState.GetPlayerZoneId(context.CastingPlayerId, ZoneType.Graveyard)
		);

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card card)
			return false;

		if (!card.HasComponent<SpellComponent>())
			return false;

		var graveyardId = context.GameState.GetPlayerZoneId(
			context.CastingPlayerId,
			ZoneType.Graveyard
		);
		return context.GameState.GetCardZoneId(candidateId) == graveyardId;
	}
}

/// <summary>
/// Matches any creature card currently in the casting player's own graveyard.
/// </summary>
public record IsCreatureInOwnGraveyardSpecification : ZoneSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context) =>
		context.GameState.GetChildrenIds(
			context.GameState.GetPlayerZoneId(context.CastingPlayerId, ZoneType.Graveyard)
		);

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card card)
			return false;

		if (!card.HasComponent<CreatureComponent>())
			return false;

		var graveyardId = context.GameState.GetPlayerZoneId(
			context.CastingPlayerId,
			ZoneType.Graveyard
		);
		return context.GameState.GetCardZoneId(candidateId) == graveyardId;
	}
}

/// <summary>
/// Matches any creature card in EITHER player's graveyard — "put a creature card from a
/// graveyard onto the battlefield under your control" (Endless Obedience).
///
/// Distinct from IsCreatureInOwnGraveyardSpecification, and the difference is the card: raiding
/// the opponent's graveyard turns their removal spell into your threat, and it is what makes
/// reanimation an answer to a board you are losing to rather than only a rebuy of your own dead
/// creatures.
///
/// The reanimated creature enters under the CASTING player's control regardless of whose
/// graveyard it came from; PutIntoBattlefieldAction owns that, not this spec.
/// </summary>
public record IsCreatureInAnyGraveyardSpecification : ZoneSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context) =>
		GraveyardIds(context).SelectMany(context.GameState.GetChildrenIds);

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card card)
			return false;

		if (!card.HasComponent<CreatureComponent>())
			return false;

		return GraveyardIds(context).Contains(context.GameState.GetCardZoneId(candidateId));
	}

	private static IEnumerable<int> GraveyardIds(TargetingContext context) =>
		new[] { MtgObjectKeys.Player1, MtgObjectKeys.Player2 }
			.Select(context.GameState.GetWellKnownId)
			.Select(pid => context.GameState.GetPlayerZoneId(pid, ZoneType.Graveyard))
			.Where(id => id != 0)
			.ToList();
}

/// <summary>
/// Matches any card currently on either player's battlefield.
/// Compose with other specs (e.g. IsSubtypeSpecification) to restrict to specific permanents.
/// Being a ZoneSpecification, AndSpecification will automatically prefer this side's
/// candidates over a full-map scan.
/// </summary>
public record IsOnBattlefieldSpecification : ZoneSpecification
{
	public override IEnumerable<int> GetCandidateIds(TargetingContext context)
	{
		var state = context.GameState;
		return state
			.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player1Battlefield))
			.Concat(state.GetChildrenIds(state.GetWellKnownId(MtgObjectKeys.Player2Battlefield)));
	}

	public override bool IsSatisfiedBy(int candidateId, TargetingContext context)
	{
		if (!context.GameState.HasObject(candidateId))
			return false;

		if (context.Find(candidateId) is not Card)
			return false;

		var zone = context.GameState.GetCardZone(candidateId);
		return zone.ZoneType == ZoneType.Battlefield;
	}
}
