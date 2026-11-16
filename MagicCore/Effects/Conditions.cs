using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Effects;

/// <summary>"If …" in rules text — read by <see cref="If"/>, intervening "if"s and statics.</summary>
public abstract record Condition
{
	public abstract bool Holds(GameState s, ImmutableDictionary<string, object> context);
}

/// <summary>
/// "If [something] is [filter]" — any object the Ref names matches. Prophesied End: "if it wasn't
/// attacking" is Matches(Ref.Target(0), !Is.Attacking), read BEFORE the destroy while it still knows;
/// Sphinx of False Conclusions: "if it isn't a token" is Not(Matches(Ref.Source, Is.Token)) — once the
/// source is gone, as it last was (Ref.ObjectsAsTheyWere).
/// </summary>
public record Matches(Ref What, Filters.Filter Filter) : Condition
{
	/// <summary>
	/// Gowron: "If you sacrificed a Klingon this way" — the event card as it was before the cost moved
	/// it (its snapshot), so a Klingon token that ceased to exist in the graveyard still counts.
	/// </summary>
	public bool AsItLastWas { get; init; }

	public override bool Holds(GameState s, ImmutableDictionary<string, object> context)
	{
		var asking = ContextKeys.Asking(context);
		return (
			AsItLastWas ? What.CountedAsTheyWere(s, context) : What.ObjectsAsTheyWere(s, context)
		).Any(o => Filter.Matches(s, o, asking));
	}
}

/// <summary>"You can behold a [Subtype]" — Theorist's Sanctum: a Jace you control or in your hand (it costs nothing).</summary>
public record YouCanBehold(string Subtype) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		new BeholdCost(Subtype)
			.Candidates(s, (int)context[ContextKeys.Controller], (int)context[ContextKeys.Source])
			.Any();
}

/// <summary>Rescue Girl's "activate only during your turn"; Skilled Battlecarver's "during your turn" static.</summary>
public record DuringYourTurn : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetGame().ActivePlayerId == (int)context[ContextKeys.Controller];
}

/// <summary>Yuriko, Blade of the Mighty: "During combat" — from the beginning of combat to its end.</summary>
public record DuringCombat : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetGame().Step is >= Step.BeginCombat and <= Step.EndCombat;
}

/// <summary>H.M.S. Bounty: "[if you] haven't begun an extra turn this game" — MagicPlayer.BegunExtraTurn.</summary>
public record HaventBegunAnExtraTurn : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		!s.GetPlayer((int)context[ContextKeys.Controller]).BegunExtraTurn;
}

/// <summary>Fblthp: "If your library has no cards in it".</summary>
public record YourLibraryIsEmpty : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		!s.CardsIn((int)context[ContextKeys.Controller], ZoneType.Library).Any();
}

/// <summary>The opposite of a condition — "isn't", "wasn't", "unless".</summary>
public record Not(Condition Inner) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		!Inner.Holds(s, context);
}

/// <summary>Sylvia Tilly: "you may pay {1} and exile a card from your graveyard" — asked only when both can be done.</summary>
public record Both(Condition A, Condition B) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		A.Holds(s, context) && B.Holds(s, context);
}

/// <summary>The context a condition is judged in outside a resolving ability: a permanent and its controller.</summary>
/// <summary>
/// Sulu, Space Swashbuckler: "if an Equipment or Spacecraft entered the battlefield under your control
/// this turn" — one of these subtypes, even if it has left again (TurnTally.SubtypesEntered).
/// </summary>
public record SubtypeEnteredThisTurn(ImmutableArray<string> Subtypes) : Condition
{
	/// <summary>
	/// Robber of the Rich: "During any turn you attacked with a Rogue" — one of these subtypes ATTACKED under your
	/// control this turn instead (TurnTally.SubtypesAttacked), even if it has left since.
	/// </summary>
	public bool Attacked { get; init; }

	public override bool Holds(GameState s, ImmutableDictionary<string, object> context)
	{
		var tally = s.GetPlayer((int)context[ContextKeys.Controller]).ThisTurn;
		return (Attacked ? tally.SubtypesAttacked : tally.SubtypesEntered)
			.Intersect(Subtypes)
			.Any();
	}
}

public static class Conditions
{
	/// <summary>The source as it was when it triggered (CR 603.10a), else as it is now — null if gone.</summary>
	public static Card? SourceAsItWas(GameState s, ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(ContextKeys.SourceSnapshot) as Card
		?? (
			s.HasObject((int)context[ContextKeys.Source])
				? s.GetObject((int)context[ContextKeys.Source]) as Card
				: null
		);

	public static ImmutableDictionary<string, object> Of(Card card) =>
		ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, card.ControllerId)
			.Add(ContextKeys.Source, card.Id);
}

/// <summary>"If this spell was cast from a graveyard" / "wasn't cast from your hand" (with Not).</summary>
public record CastFrom(ZoneType Zone) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetObject((int)context[ContextKeys.Source]) is Card spell
		&& spell.GetComponent<Actions.OnStackComponent>()?.CastFrom == Zone;
}

/// <summary>Eject the Warp Core: "If this spell was kicked" — read from the spell on the stack.</summary>
public record WasKicked : Condition
{
	/// <summary>The spell on the stack, or the permanent it became (Thieving Skydiver: "if it was kicked") — as it
	/// last was too, so one bounced in response was still kicked.</summary>
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetObject((int)context[ContextKeys.Source]) is Card card && Kicked(card)
		|| context.GetValueOrDefault(ContextKeys.SourceSnapshot) is Card snapshot
			&& Kicked(snapshot);

	static bool Kicked(Card card) =>
		card.GetComponent<Actions.OnStackComponent>()?.Kicked == true || card.CastKicked;
}

/// <summary>Baleful Mastery: "If the {1}{B} cost was paid" — cast for the alternative cost on the card (OnStackComponent.AlternativeCost).</summary>
public record AlternativeCostWasPaid : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetObject((int)context[ContextKeys.Source]) is Card spell
		&& spell.GetComponent<Actions.OnStackComponent>()?.AlternativeCost == true;
}

/// <summary>First Contact: "If this spell is the first spell you've cast this game" — stamped as it was cast.</summary>
public record FirstSpellYouCastThisGame : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetObject((int)context[ContextKeys.Source]) is Card spell
		&& spell.GetComponent<Actions.OnStackComponent>()?.FirstSpellThisGame == true;
}

/// <summary>
/// Null Summoner: "if you cast it" — it entered by resolving as a spell. Read from the source as it
/// was when it triggered, so it leaving in response doesn't change the answer.
/// </summary>
public record SourceWasCast : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Conditions.SourceAsItWas(s, context)?.WasCast == true;
}

/// <summary>
/// All Good Things..., chapter IV: "If all cards exiled with this Saga share a card type" — the cards linked
/// to this object of the source (ExiledBy, CR 607.2a). ponytail: none exiled is no (each chapter draws two
/// first, so only a decked player has none).
/// </summary>
public record ExiledWithSourceShareACardType : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context)
	{
		var types = Ref.All(ZoneType.Exile, Filters.Is.ExiledBySource)
			.Resolve(s, context)
			.Select(id => s.GetCard(id).Types)
			.ToList();
		return types.Count > 0 && types.Aggregate((a, b) => a & b) != CardType.None;
	}
}

/// <summary>A "you may" that can always be done.</summary>
public record Always : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) => true;
}

/// <summary>The shocklands: "you may pay 2 life"; Hive Mind Coprocessor: "pay 1 life" — asked only when it can be paid (Rules.Life.CanPay).</summary>
public record CanPayLife(int N) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Rules.Life.CanPay(s, (int)context[ContextKeys.Controller], N);
}

/// <summary>Proft: "you may pay {2}" — asked only when it can be paid.</summary>
public record CanPay(ManaCost Cost) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		ManaPayment.Find(s, (int)context[ContextKeys.Controller], Cost, 0) is not null;
}

/// <summary>Eye of Jace: "if there are seven or more cards in your graveyard".</summary>
public record AtLeast(Count Count, int N) : Condition
{
	/// <summary>
	/// Robber of the Rich: "if defending player has more cards in hand than you" — at least N more than this count
	/// (their hand, 1, MoreThan your hand).
	/// </summary>
	public Count? MoreThan { get; init; }

	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Count.Value(s, context) >= N + (MoreThan?.Value(s, context) ?? 0);
}
