using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Filters;

namespace MagicCore.Effects;

// Costs beyond mana, lifted from MtgCore's AdditionalCost (valid payments / can pay / pay) — one
// list serving activated abilities, a spell's additional costs, flashback and ward. A cost that
// picks a card names it in one of the action's payment ids (CastSpellAction / ActivateAbilityAction
// .SacrificeId …), one legal action per choice — so the AI, Describe and MagicTable read the same
// four ids as before. ponytail: one slot per kind, so a cost list can't hold two sacrifices that
// pick; give the actions MtgCore's per-cost payment map if a card ever needs that.

/// <summary>Which of an action's payment ids names a cost's chosen card.</summary>
public enum CostSlot
{
	None, // nothing to choose: "{T}", "sacrifice this"
	Sacrifice,
	Discard,
	Exile,
	Behold,
}

/// <summary>A cost beyond mana — what may be chosen to pay it, whether it can be paid, and paying it.</summary>
public abstract record AdditionalCost
{
	/// <summary>"… or pay {N}" — Silence the Echo: choosing nothing (id 0) pays N more generic instead.</summary>
	public int OrPay { get; init; }

	/// <summary>The action's payment id that names the chosen card; None when nothing is chosen.</summary>
	public virtual CostSlot Slot => CostSlot.None;

	/// <summary>What may be chosen to pay it, in order — one legal action each.</summary>
	public virtual IEnumerable<int> Candidates(GameState s, int playerId, int sourceId) => [];

	/// <summary>Countersculpt's behold moves nothing: any one candidate is the same play, so only one is offered.</summary>
	public virtual bool AnyOneWillDo => false;

	/// <summary>Whether it can be paid at all — "{T}": untapped, and not summoning sick.</summary>
	public virtual bool CanPay(GameState s, int playerId, int sourceId) => true;

	/// <summary>Paid first, with {T}: the source as activated is seen tapped (Execute snapshots it after).</summary>
	public virtual bool Taps => false;

	/// <summary>The permanents it uses, which can't also tap for the mana of the same cost.</summary>
	public virtual IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		chosen == 0 ? [] : [chosen];

	public abstract GameState Pay(GameState s, int playerId, int sourceId, int chosen);

	/// <summary>"Discard a card" — ward's question ("Discard a card to keep …?"). ponytail: says the kind, not the filter.</summary>
	public abstract string Describe();

	/// <summary>Ward: "counter it unless its controller [pays]" — paid by choosing as it resolves. Null: this cost can't be.</summary>
	public virtual GameAction? PaidByChoosing() => null;

	/// <summary>Ward: can the payer pay it by choosing?</summary>
	public virtual bool CanPayByChoosing(GameState s, int payerId) => false;
}

/// <summary>"{T}:" — tap this permanent.</summary>
public sealed record TapSelfCost : AdditionalCost
{
	public override bool CanPay(GameState s, int playerId, int sourceId) =>
		ManaAbilities.CanPayTapCost(s, s.GetCard(sourceId));

	public override bool Taps => true;

	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[sourceId];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		s.UpdateObject(sourceId, s.GetCard(sourceId) with { IsTapped = true });

	public override string Describe() => "T";
}

/// <summary>
/// Tenured Tethermage: "Tap two untapped artifacts you control:" — tapping for a cost isn't {T}, so
/// summoning sickness doesn't matter (CR 302.6). ponytail: picked for the player, those that make no
/// mana first; make it a choice if the pick ever matters more than that.
/// </summary>
public sealed record TapUntappedCost(int Count, Filter Filter) : AdditionalCost
{
	/// <summary>With "{T}" in the same cost the source is already spoken for — set by ActivatedAbilityComponent.Costs.</summary>
	public bool NotTheSource { get; init; }

	public IReadOnlyList<Card> Pick(GameState s, int playerId, int sourceId) =>
		[
			.. s.CardsIn(playerId, ZoneType.Battlefield)
				.Where(c =>
					Filter.Matches(s, c, new(playerId, sourceId))
					&& !c.IsTapped
					&& !(NotTheSource && c.Id == sourceId)
				)
				// What makes no mana first, then what makes least (a Lotus last).
				.OrderBy(c =>
					ManaAbilities.CanTap(s, c) ? ManaAbilities.YieldOf(s, c).Units.Length : 0
				)
				.Take(Count),
		];

	public override bool CanPay(GameState s, int playerId, int sourceId) =>
		Pick(s, playerId, sourceId).Count >= Count;

	public override bool Taps => true;

	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		Pick(s, playerId, sourceId).Select(c => c.Id);

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		Pick(s, playerId, sourceId)
			.Aggregate(
				s,
				(st, c) => st.UpdateObject(c.Id, st.GetCard(c.Id) with { IsTapped = true })
			);

	public override string Describe() => $"Tap {Count} untapped permanents";
}

/// <summary>"Sacrifice this [permanent]:" (Treasure's own is ManaOfOneColorComponent).</summary>
public sealed record SacrificeSelfCost : AdditionalCost
{
	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		s.Sacrifice(sourceId);

	public override string Describe() => "sacrifice it";
}

/// <summary>
/// "Sacrifice another creature or planeswalker:" — one of yours the filter matches (none: any
/// permanent); Silence the Echo's "sacrifice a creature or pay {3}" with OrPay. Emrakul's ward
/// "sacrifice three permanents" is Count = 3, paid by choosing.
/// </summary>
public sealed record SacrificeCost(Filter? Filter = null) : AdditionalCost
{
	public int Count { get; init; } = 1;

	public override CostSlot Slot => CostSlot.Sacrifice;

	public override IEnumerable<int> Candidates(GameState s, int playerId, int sourceId) =>
		Filter.Among(s, playerId, ZoneType.Battlefield, sourceId);

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		chosen == 0 ? s : s.Sacrifice(chosen);

	public override string Describe() =>
		Count == 1 ? "Sacrifice a permanent" : $"Sacrifice {Count} permanents";

	public override GameAction? PaidByChoosing() =>
		Filter is null ? new SacrificePermanents(Count) : null;

	public override bool CanPayByChoosing(GameState s, int payerId) =>
		Filter is null && s.CardsIn(payerId, ZoneType.Battlefield).Count() >= Count;
}

/// <summary>Cycling: "Discard this card:".</summary>
public sealed record DiscardSelfCost : AdditionalCost
{
	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		s.Discard(playerId, [sourceId]);

	public override string Describe() => "Discard this card";
}

/// <summary>
/// Hallway Heckler: "Discard a card:" — a card in your hand (not this one) the filter matches;
/// Solitary Cell: "Discard a legendary card" is Filter = Is.Legendary. Twinned Vision's flashback,
/// and Gideon the Oathless' ward (paid by choosing).
/// </summary>
public sealed record DiscardCost(Filter? Filter = null) : AdditionalCost
{
	public override CostSlot Slot => CostSlot.Discard;

	public override IEnumerable<int> Candidates(GameState s, int playerId, int sourceId) =>
		s.CardsIn(playerId, ZoneType.Hand)
			.Where(c =>
				c.Id != sourceId && (Filter?.Matches(s, c, new(playerId, sourceId)) ?? true)
			)
			.Select(c => c.Id);

	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		chosen == 0 ? s : s.Discard(playerId, [chosen]);

	public override string Describe() => "Discard a card";

	public override GameAction? PaidByChoosing() => Filter is null ? new Discard(Ref.You, 1) : null;

	public override bool CanPayByChoosing(GameState s, int payerId) =>
		Filter is null && s.CardsIn(payerId, ZoneType.Hand).Any();
}

/// <summary>"Exile this card from your graveyard:".</summary>
public sealed record ExileSelfCost : AdditionalCost
{
	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		s.MoveCard(sourceId, s.ZoneId(playerId, ZoneType.Exile));

	public override string Describe() => "Exile this card";
}

/// <summary>Gallia, Tragic Host: "Exile another creature card from your graveyard:" — "another" is !Is.Source.</summary>
public sealed record ExileFromGraveyardCost(Filter Filter) : AdditionalCost
{
	public override CostSlot Slot => CostSlot.Exile;

	public override IEnumerable<int> Candidates(GameState s, int playerId, int sourceId) =>
		Filter.Among(s, playerId, ZoneType.Graveyard, sourceId);

	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		chosen == 0 ? s : s.MoveCard(chosen, s.ZoneId(playerId, ZoneType.Exile));

	public override string Describe() => "Exile a card from your graveyard";
}

/// <summary>
/// Countersculpt: "behold a [Subtype] or pay {N}" — a card you control or in your hand (not this
/// one). Beholding moves nothing, so any one is the same play.
/// </summary>
public sealed record BeholdCost(string Subtype) : AdditionalCost
{
	public override CostSlot Slot => CostSlot.Behold;

	public override bool AnyOneWillDo => true;

	// The battlefield FIRST: AnyOneWillDo offers only the first, and one beheld from the hand is revealed.
	public override IEnumerable<int> Candidates(GameState s, int playerId, int sourceId) =>
		s.CardsIn(playerId, ZoneType.Battlefield)
			.Concat(s.CardsIn(playerId, ZoneType.Hand).Where(c => c.Id != sourceId))
			.Where(c => c.Subtypes.Contains(Subtype))
			.Select(c => c.Id);

	public override IEnumerable<int> Uses(GameState s, int playerId, int sourceId, int chosen) =>
		[];

	/// <summary>Moves nothing; a card beheld from the hand is revealed (story AI-3).</summary>
	public override GameState Pay(GameState s, int playerId, int sourceId, int chosen) =>
		chosen != 0 && s.IsIn(chosen, playerId, ZoneType.Hand)
			? s.MakeKnown(chosen, Knowers.Everyone)
			: s;

	public override string Describe() => $"Behold a {Subtype}";
}

/// <summary>A cost list's choices and payment — the one place casting, activating and their candidates ask.</summary>
public static class CostList
{
	/// <summary>The one cost that picks into this slot, if any — two can't share an action's one id.</summary>
	public static AdditionalCost? In(IEnumerable<AdditionalCost> costs, CostSlot slot)
	{
		var found = costs.Where(c => c.Slot == slot).ToList();
		return found.Count <= 1
			? found.SingleOrDefault()
			: throw new InvalidOperationException(
				$"Two costs pick into {slot}: one id can't name both"
			);
	}

	/// <summary>What a slot may hold: 0 when nothing picks into it (or "or pay" is allowed), else each candidate.</summary>
	public static IEnumerable<int> Options(
		IEnumerable<AdditionalCost> costs,
		CostSlot slot,
		GameState s,
		int playerId,
		int sourceId
	)
	{
		if (In(costs, slot) is not { } cost)
			return [0];
		var candidates = cost.Candidates(s, playerId, sourceId);
		if (cost.AnyOneWillDo)
			candidates = candidates.Take(1);
		return cost.OrPay > 0 ? [0, .. candidates] : candidates;
	}

	/// <summary>The chosen id fits the slot: 0 where nothing picks (or where it may pay instead), else a candidate.</summary>
	public static bool Fits(
		IEnumerable<AdditionalCost> costs,
		CostSlot slot,
		int chosen,
		GameState s,
		int playerId,
		int sourceId
	) =>
		In(costs, slot) is not { } cost ? chosen == 0
		: chosen == 0 ? cost.OrPay > 0
		: cost.Candidates(s, playerId, sourceId).Contains(chosen);

	/// <summary>"… or pay {N}" owed for the slots left empty.</summary>
	public static int OrPayOwed(IEnumerable<AdditionalCost> costs, Func<CostSlot, int> chosen) =>
		costs.Where(c => c.Slot != CostSlot.None && chosen(c.Slot) == 0).Sum(c => c.OrPay);
}
