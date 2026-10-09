using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Effects;

// Who and what an effect acts on (Ref), and the numbers in rules text (Count).

/// <summary>Pipeline context keys a resolving spell or ability provides to its effects.</summary>
public static class ContextKeys
{
	public const string Controller = "Controller";
	public const string X = "X";

	/// <summary>ImmutableArray&lt;int&gt;, one per target word; 0 where unchosen or no longer legal.</summary>
	public const string Targets = "Targets";

	/// <summary>Every chosen target, all modes together; Targets is narrowed to one mode's slice.</summary>
	public const string AllTargets = "AllTargets";

	/// <summary>The spell card or the permanent whose ability this is.</summary>
	public const string Source = "Source";

	/// <summary>The card the trigger event was about — the creature that entered, the land that fell.</summary>
	public const string EventCard = "EventCard";

	/// <summary>The event's card as it last existed (a dead creature's counters), for "dies" triggers.</summary>
	public const string EventSnapshot = "EventSnapshot";

	/// <summary>The spell or ability an event was done by, as it was — ward's "it".</summary>
	public const string EventObject = "EventObject";

	/// <summary>An ability's source as it last existed — used once the source itself is gone (CR 113.7a).</summary>
	public const string SourceSnapshot = "SourceSnapshot";
}

public enum RefKind
{
	You,
	EachOpponent,
	EachPlayer,
	Target,
	ControllerOfTarget,
	Source,
	EventCard,
	EventObject, // ward: "counter it" — the spell or ability that targeted, if it is still that object
	YourPermanents, // of Type: "creatures you control", "lands you control" …
	OpponentsPermanents, // of Type: "each creature and planeswalker your opponents control"
	AllPermanents, // of Type: "all creatures"
	YourGraveyard, // the cards in it
	YourHand, // the cards in it
	ControllerOfEventCard, // Tetsuko, Pursuer: "that creature's controller"
	YourPermanentsWithCounters, // of Type, with a +1/+1 counter on it
	AllPermanentsExceptYourTokens, // of Type: Ajani's "each creature except for tokens you control"
	YourTokens, // "all tokens you control"
	YourOtherPermanents, // of Type: "each other creature you control" — not the source
	OwnerOfTarget, // Clash of Elements: "its owner" — wherever the card now is
	TargetPlayersPermanents, // of Type: Twisted Fates' "each creature target player controls"
	SourceHost, // an Aura's "enchanted creature" — what the source is attached to
}

/// <summary>Who or what an effect acts on, resolved against the context at resolution.</summary>
public record Ref(RefKind Kind, int Index = 0, CardType Type = CardType.None)
{
	/// <summary>Command the Stage: "each other Wizard token you control" — only cards with this subtype.</summary>
	public string? Subtype { get; init; }

	public static readonly Ref You = new(RefKind.You);
	public static readonly Ref EachOpponent = new(RefKind.EachOpponent);
	public static readonly Ref EachPlayer = new(RefKind.EachPlayer);
	public static readonly Ref Source = new(RefKind.Source);
	public static readonly Ref EventCard = new(RefKind.EventCard);
	public static readonly Ref EventObject = new(RefKind.EventObject);
	public static readonly Ref CreaturesYouControl =
		new(RefKind.YourPermanents, Type: CardType.Creature);
	public static readonly Ref LandsYouControl = new(RefKind.YourPermanents, Type: CardType.Land);
	public static readonly Ref PlaneswalkersYouControl =
		new(RefKind.YourPermanents, Type: CardType.Planeswalker);
	public static readonly Ref AllCreatures = new(RefKind.AllPermanents, Type: CardType.Creature);
	public static readonly Ref CardsInYourGraveyard = new(RefKind.YourGraveyard);
	public static readonly Ref CardsInYourHand = new(RefKind.YourHand);
	public static readonly Ref ControllerOfEventCard = new(RefKind.ControllerOfEventCard);
	public static readonly Ref YourCreaturesWithCounters =
		new(RefKind.YourPermanentsWithCounters, Type: CardType.Creature);
	public static readonly Ref CreaturesExceptYourTokens =
		new(RefKind.AllPermanentsExceptYourTokens, Type: CardType.Creature);
	public static readonly Ref TokensYouControl = new(RefKind.YourTokens);
	public static readonly Ref EnchantedCreature = new(RefKind.SourceHost);
	public static readonly Ref OtherCreaturesYouControl =
		new(RefKind.YourOtherPermanents, Type: CardType.Creature);

	public static Ref YourGraveyard(CardType type) => new(RefKind.YourGraveyard, Type: type);

	public static Ref OpponentsPermanents(CardType type) =>
		new(RefKind.OpponentsPermanents, Type: type);

	public static Ref Target(int index = 0) => new(RefKind.Target, index);

	public static Ref ControllerOfTarget(int index = 0) => new(RefKind.ControllerOfTarget, index);

	public static Ref OwnerOfTarget(int index = 0) => new(RefKind.OwnerOfTarget, index);

	public static Ref PermanentsOfTargetPlayer(CardType type, int index) =>
		new(RefKind.TargetPlayersPermanents, index, type);

	/// <summary>Is the source still the object the ability came from? Not if it left and came back (CR 400.7).</summary>
	static bool SameObject(GameState s, ImmutableDictionary<string, object> context) =>
		!context.TryGetValue(ContextKeys.SourceSnapshot, out var snapshot)
		|| snapshot is not Card then
		|| !s.HasObject(then.Id)
		|| s.GetObject(then.Id) is not Card now
		|| now.Incarnation == then.Incarnation;

	public IEnumerable<int> Resolve(GameState s, ImmutableDictionary<string, object> context)
	{
		var controller = (int)context[ContextKeys.Controller];
		var targets = context.TryGetValue(ContextKeys.Targets, out var t)
			? (ImmutableArray<int>)t
			: [];
		int TargetAt(int i) => i < targets.Length ? targets[i] : 0;
		var eventCard = context.TryGetValue(ContextKeys.EventCard, out var e) ? (int)e : 0;
		IEnumerable<Card> Yours() => s.CardsIn(controller, ZoneType.Battlefield);
		IEnumerable<Card> All() =>
			new[] { controller, s.OpponentOf(controller) }.SelectMany(p =>
				s.CardsIn(p, ZoneType.Battlefield)
			);

		IEnumerable<int> ids = Kind switch
		{
			RefKind.You => [controller],
			RefKind.EachOpponent => [s.OpponentOf(controller)],
			RefKind.EachPlayer => [s.Player1Id(), s.Player2Id()],
			RefKind.Target => [TargetAt(Index)],
			RefKind.ControllerOfTarget => s.HasObject(TargetAt(Index))
			&& s.GetObject(TargetAt(Index)) is Card c
				? [c.ControllerId]
				: [],
			// The Aura as it was (last-known, CR 608.2h) — so removing it in response changes nothing,
			// and an Aura that left and came back on something else doesn't redirect it.
			RefKind.SourceHost => (
				context.GetValueOrDefault(ContextKeys.SourceSnapshot) as Card
				?? s.GetObject((int)context[ContextKeys.Source]) as Card
			)
				is { AttachedToId: not 0 } aura
			&& s.IsOnBattlefield(aura.AttachedToId)
			&& Rules.Attachments.IsOn(aura, s.GetCard(aura.AttachedToId))
				? [aura.AttachedToId]
				: [],
			RefKind.OwnerOfTarget => s.HasObject(TargetAt(Index))
			&& s.GetObject(TargetAt(Index)) is Card owned
				? [owned.OwnerId]
				: [],
			RefKind.TargetPlayersPermanents => s.HasObject(TargetAt(Index))
			&& s.GetObject(TargetAt(Index)) is MagicPlayer player
				? s.CardsIn(player.Id, ZoneType.Battlefield)
					.Where(c => c.Is(Type))
					.Select(c => c.Id)
				: [],
			RefKind.Source => SameObject(s, context) ? [(int)context[ContextKeys.Source]] : [],
			RefKind.EventCard => [eventCard],
			RefKind.EventObject => context.GetValueOrDefault(ContextKeys.EventObject) switch
			{
				// Same id is not the same object (CR 400.7): a spell recast from the graveyard isn't it.
				Card then
					when s.HasObject(then.Id)
						&& s.GetObject(then.Id) is Card now
						&& now.Incarnation == then.Incarnation => [then.Id],
				Card => [],
				GameObject ability => [ability.Id],
				_ => [],
			},
			// Its controller now if it is still there, else as it last was (CR 608.2h).
			RefKind.ControllerOfEventCard => s.IsOnBattlefield(eventCard)
				? [s.GetCard(eventCard).ControllerId]
			: context.TryGetValue(ContextKeys.EventSnapshot, out var snapshot)
				? [((Card)snapshot).ControllerId]
			: [],
			RefKind.OpponentsPermanents => s.CardsIn(s.OpponentOf(controller), ZoneType.Battlefield)
				.Where(c => c.Is(Type))
				.Select(c => c.Id),
			RefKind.AllPermanents => All().Where(c => c.Is(Type)).Select(c => c.Id),
			RefKind.AllPermanentsExceptYourTokens => All()
				.Where(c => c.Is(Type) && !(c.IsToken && c.ControllerId == controller))
				.Select(c => c.Id),
			RefKind.YourPermanentsWithCounters => Yours()
				.Where(c => c.Is(Type) && c.PlusOneCounters > 0)
				.Select(c => c.Id),
			RefKind.YourTokens => Yours().Where(c => c.IsToken).Select(c => c.Id),
			RefKind.YourOtherPermanents => Yours()
				.Where(c => c.Is(Type) && c.Id != (int)context[ContextKeys.Source])
				.Select(c => c.Id),
			RefKind.YourHand => s.CardsIn(controller, ZoneType.Hand).Select(c => c.Id),
			RefKind.YourGraveyard => s.CardsIn(controller, ZoneType.Graveyard)
				.Where(c => Type == CardType.None || c.Is(Type))
				.Select(c => c.Id),
			_ => Yours().Where(c => c.Is(Type)).Select(c => c.Id),
		};
		return ids.Where(id =>
			id != 0
			&& s.HasObject(id)
			&& (Subtype is null || (s.GetObject(id) is Card c && c.Subtypes.Contains(Subtype)))
		);
	}
}

public enum CountOf
{
	Fixed,
	NumberOf, // "the number of [Ref]"
	GreatestPowerOf, // "the greatest power among [Ref]"
	GreatestManaValueOf, // Hapatra: "the greatest mana value among cards in your graveyard"
	DistinctPowersOf, // Loot, the Nexus: "each different power among [Ref]"
	PowerOf, // "equal to its power"
	LoyaltyOf, // "equal to its loyalty"
	X, // the X paid
	CountersOn, // "+1/+1 counters on [Ref]"
	LifeYouGainedThisTurn,
	CardsYouDrewThisTurn,
	CardsYouDiscardedThisTurn,
	LoyaltyAbilitiesYouActivatedThisTurn,
	BasicLandTypesAmongLandsYouControl, // domain
	YourSurveilsThisTurn,
	SpellsYouCastThisTurn,
	NoncombatDamageToOpponentsLastTurn,
	NoncombatDamageToOpponentsThisTurn,
	CreaturesDiedThisTurn, // anyone's
}

/// <summary>
/// A number in rules text, counted at resolution. Counting goes through Ref, so "creatures you
/// control" means one thing everywhere. A negative Sign is "-X".
/// </summary>
public record Count(int Fixed, Ref? Of = null, CountOf Kind = CountOf.Fixed, int Sign = 1)
{
	/// <summary>"For every three cards in your graveyard": the count divided by this, rounded down.</summary>
	public int Per { get; init; } = 1;

	public static implicit operator Count(int n) => new(n);

	public static Count NumberOf(Ref r) => new(0, r, CountOf.NumberOf);

	public static Count GreatestPowerOf(Ref r) => new(0, r, CountOf.GreatestPowerOf);

	public static Count GreatestManaValueOf(Ref r) => new(0, r, CountOf.GreatestManaValueOf);

	public static Count DistinctPowersOf(Ref r) => new(0, r, CountOf.DistinctPowersOf);

	public static Count PowerOf(Ref r) => new(0, r, CountOf.PowerOf);

	public static Count LoyaltyOf(Ref r) => new(0, r, CountOf.LoyaltyOf);

	public static readonly Count X = new(0, null, CountOf.X);

	public static Count CountersOn(Ref r) => new(0, r, CountOf.CountersOn);

	public static readonly Count LifeYouGainedThisTurn =
		new(0, null, CountOf.LifeYouGainedThisTurn);
	public static readonly Count CardsYouDrewThisTurn = new(0, null, CountOf.CardsYouDrewThisTurn);
	public static readonly Count CardsYouDiscardedThisTurn =
		new(0, null, CountOf.CardsYouDiscardedThisTurn);
	public static readonly Count LoyaltyAbilitiesYouActivatedThisTurn =
		new(0, null, CountOf.LoyaltyAbilitiesYouActivatedThisTurn);

	/// <summary>Domain: "the number of basic land types among lands you control".</summary>
	public static readonly Count BasicLandTypesAmongLandsYouControl =
		new(0, null, CountOf.BasicLandTypesAmongLandsYouControl);
	public static readonly Count YourSurveilsThisTurn = new(0, null, CountOf.YourSurveilsThisTurn);
	public static readonly Count SpellsYouCastThisTurn =
		new(0, null, CountOf.SpellsYouCastThisTurn);
	public static readonly Count NoncombatDamageToOpponentsLastTurn =
		new(0, null, CountOf.NoncombatDamageToOpponentsLastTurn);
	public static readonly Count NoncombatDamageToOpponentsThisTurn =
		new(0, null, CountOf.NoncombatDamageToOpponentsThisTurn);
	public static readonly Count CreaturesDiedThisTurn =
		new(0, null, CountOf.CreaturesDiedThisTurn);

	public static readonly Count CreaturesYouControl = NumberOf(Ref.CreaturesYouControl);
	public static readonly Count LandsYouControl = NumberOf(Ref.LandsYouControl);

	public Count Negated => this with { Sign = -Sign };

	/// <summary>"Is this a fixed nothing?" — a static with Power 0 and Toughness 0 changes no P/T.</summary>
	public static bool operator ==(Count c, int n) =>
		c.Kind == CountOf.Fixed && c.Sign * c.Fixed == n;

	public static bool operator !=(Count c, int n) => !(c == n);

	public int Value(GameState s, ImmutableDictionary<string, object> context) =>
		Sign * (Counted(s, context) / Per);

	int Counted(GameState s, ImmutableDictionary<string, object> context)
	{
		if (Kind == CountOf.Fixed)
			return Fixed;
		if (Kind == CountOf.X)
			return context.TryGetValue(ContextKeys.X, out var x) ? (int)x : 0;
		if (Kind == CountOf.CreaturesDiedThisTurn)
			return s.GetGame().CreaturesDiedThisTurn;
		if (Kind == CountOf.BasicLandTypesAmongLandsYouControl)
			return Ref
				.LandsYouControl.Resolve(s, context)
				.SelectMany(id => s.GetCard(id).Subtypes)
				.Where(ManaAbilities.IsBasicLandType)
				.Distinct()
				.Count();
		if (Kind == CountOf.NoncombatDamageToOpponentsLastTurn)
			return s.GetPlayer(
				s.OpponentOf((int)context[ContextKeys.Controller])
			).LastTurn.NoncombatDamageTaken;
		if (Kind == CountOf.NoncombatDamageToOpponentsThisTurn)
			return s.GetPlayer(
				s.OpponentOf((int)context[ContextKeys.Controller])
			).ThisTurn.NoncombatDamageTaken;
		if (
			Kind
			is CountOf.LifeYouGainedThisTurn
				or CountOf.CardsYouDrewThisTurn
				or CountOf.CardsYouDiscardedThisTurn
				or CountOf.LoyaltyAbilitiesYouActivatedThisTurn
				or CountOf.YourSurveilsThisTurn
				or CountOf.SpellsYouCastThisTurn
		)
		{
			var tally = s.GetPlayer((int)context[ContextKeys.Controller]).ThisTurn;
			return Kind switch
			{
				CountOf.LifeYouGainedThisTurn => tally.LifeGained,
				CountOf.CardsYouDrewThisTurn => tally.CardsDrawn,
				CountOf.CardsYouDiscardedThisTurn => tally.CardsDiscarded,
				CountOf.LoyaltyAbilitiesYouActivatedThisTurn => tally.LoyaltyAbilitiesActivated,
				CountOf.SpellsYouCastThisTurn => tally.SpellsCast,
				_ => tally.Surveils,
			};
		}
		var ids = Of!.Resolve(s, context);
		IEnumerable<Card> Cards() => ids.Select(s.GetObject).OfType<Card>();
		var counted = Kind switch
		{
			CountOf.NumberOf => ids.Count(),
			CountOf.GreatestPowerOf => Cards().Select(c => s.Power(c)).DefaultIfEmpty(0).Max(),
			CountOf.GreatestManaValueOf => Cards()
				.Select(c => c.ManaCost.ManaValue)
				.DefaultIfEmpty(0)
				.Max(),
			CountOf.PowerOf => Cards().Select(c => s.Power(c)).FirstOrDefault(),
			CountOf.DistinctPowersOf => Cards().Select(c => s.Power(c)).Distinct().Count(),
			CountOf.CountersOn => Cards().Sum(c => c.PlusOneCounters),
			_ => Cards().Select(c => c.Loyalty).FirstOrDefault(),
		};
		// CR 107.1b: a number that would be negative is 0 — a -4 power must not make "lose X life"
		// a life gain. The sign ("-X") applies after.
		return Math.Max(0, counted);
	}
}
