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
		(
			context.GetValueOrDefault(ContextKeys.SourceSnapshot) as Card
			?? s.GetObject((int)context[ContextKeys.Source]) as Card
		)?.WasCast == true;
}

/// <summary>"If this creature isn't prepared" (with Not) — the source, as it is now.</summary>
public record SourceIsPrepared : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.HasObject((int)context[ContextKeys.Source])
		&& s.GetObject((int)context[ContextKeys.Source]) is Card { IsPrepared: true };
}

/// <summary>Ruric Thar, Magecrusher: "as long as they haven't dealt combat damage yet" (with Not).</summary>
public record SourceHasDealtCombatDamage : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.HasObject((int)context[ContextKeys.Source])
		&& s.GetObject((int)context[ContextKeys.Source]) is Card { HasDealtCombatDamage: true };
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
