using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Effects;

/// <summary>"If …" in rules text — read by <see cref="If"/>, intervening "if"s and statics.</summary>
public abstract record Condition
{
	public abstract bool Holds(GameState s, ImmutableDictionary<string, object> context);
}

/// <summary>Prophesied End: "if it wasn't attacking" — read BEFORE the destroy, while it still knows.</summary>
public record TargetIsNotAttacking(int Index) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.Target(Index).Resolve(s, context).Select(s.GetCard).Any(c => c.AttackingId == 0);
}

/// <summary>Budding Insurgent: "if that permanent was a legendary enchantment" — read before it is destroyed.</summary>
public record TargetIs(int Index, CardType Type, Supertype Supertype = Supertype.None) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.Target(Index)
			.Resolve(s, context)
			.Select(s.GetObject)
			.OfType<Card>()
			.Any(c => c.Is(Type) && (Supertype == Supertype.None || c.Is(Supertype)));
}

public record EventCardHasSubtype(string Subtype) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.EventCard.Resolve(s, context).Select(s.GetCard).Any(c => c.Subtypes.Contains(Subtype));
}

public record Not(Condition Inner) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		!Inner.Holds(s, context);
}

/// <summary>The context a condition is judged in outside a resolving ability: a permanent and its controller.</summary>
public static class Conditions
{
	public static ImmutableDictionary<string, object> Of(Card card) =>
		ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, card.ControllerId)
			.Add(ContextKeys.Source, card.Id);
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
