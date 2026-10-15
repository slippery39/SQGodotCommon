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
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context)
	{
		var asking = ContextKeys.Asking(context);
		return What.ObjectsAsTheyWere(s, context).Any(o => Filter.Matches(s, o, asking));
	}
}

/// <summary>"You can behold a [Subtype]" — Theorist's Sanctum: a Jace you control or in your hand (it costs nothing).</summary>
public record YouCanBehold(string Subtype) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		new BeholdOrPayAdditionalCost(Subtype, 0)
			.Candidates(s, (int)context[ContextKeys.Controller], (int)context[ContextKeys.Source])
			.Any();
}

/// <summary>Rescue Girl's "activate only during your turn"; Skilled Battlecarver's "during your turn" static.</summary>
public record DuringYourTurn : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetGame().ActivePlayerId == (int)context[ContextKeys.Controller];
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

/// <summary>The context a condition is judged in outside a resolving ability: a permanent and its controller.</summary>
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

/// <summary>
/// Null Summoner: "if you cast it" — it entered by resolving as a spell. Read from the source as it
/// was when it triggered, so it leaving in response doesn't change the answer.
/// </summary>
public record SourceWasCast : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Conditions.SourceAsItWas(s, context)?.WasCast == true;
}

/// <summary>A "you may" that can always be done.</summary>
public record Always : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) => true;
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
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Count.Value(s, context) >= N;
}
