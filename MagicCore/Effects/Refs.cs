using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Filters;
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

	/// <summary>Who a filter asks for in a resolving context: "you", "this", "that creature".</summary>
	public static Filters.FilterContext Asking(ImmutableDictionary<string, object> context) =>
		new(
			(int)context[Controller],
			context.TryGetValue(Source, out var source) ? (int)source : 0,
			context.TryGetValue(EventCard, out var e) ? (int)e : 0
		);
}

public enum RefKind
{
	You,
	EachOpponent,
	EachPlayer,
	Target,
	ControllerOfTarget,
	OwnerOfTarget, // Clash of Elements: "its owner" — wherever the card now is
	Source,
	SourceHost, // an Aura's "enchanted creature" — what the source is attached to
	EventCard,
	EventObject, // ward: "counter it" — the spell or ability that targeted, if it is still that object
	ControllerOfEventCard, // Tetsuko, Pursuer: "that creature's controller"
	All, // "creatures you control", "cards in your graveyard": a zone's objects that match a filter
}

/// <summary>
/// Who or what an effect acts on, resolved against the context at resolution: a referent ("you",
/// "target creature", "this"), or <see cref="All"/> — every object in a zone that a filter matches.
/// </summary>
public record Ref(RefKind Kind, int Index = 0)
{
	/// <summary>For All: the zone — Eye of Jace: "cards in your GRAVEYARD"; Ajani: "each creature" (the battlefield).</summary>
	public ZoneType? Zone { get; init; }

	/// <summary>For All: which objects there — Ajani: "each creature except for tokens you control". None is every one.</summary>
	public Filter? Filter { get; init; }

	/// <summary>
	/// For All: whose zone — Twisted Fates: "each creature target player controls" is Of =
	/// Target(i); "your graveyard" is Of = You. None is every player's, yours first.
	/// </summary>
	public Ref? Of { get; init; }

	public static readonly Ref You = new(RefKind.You);
	public static readonly Ref EachOpponent = new(RefKind.EachOpponent);
	public static readonly Ref EachPlayer = new(RefKind.EachPlayer);
	public static readonly Ref Source = new(RefKind.Source);
	public static readonly Ref EventCard = new(RefKind.EventCard);
	public static readonly Ref EventObject = new(RefKind.EventObject);
	public static readonly Ref ControllerOfEventCard = new(RefKind.ControllerOfEventCard);
	public static readonly Ref EnchantedCreature = new(RefKind.SourceHost);

	/// <summary>
	/// Eye of Jace: "the cards in your graveyard" is All(Graveyard, of: You) — the objects in a zone a
	/// filter matches, from every player's zone (yours first) unless Of names whose, in Of's order.
	/// </summary>
	public static Ref All(ZoneType zone, Filter? filter = null, Ref? of = null) =>
		new(RefKind.All)
		{
			Zone = zone,
			Filter = filter,
			Of = of,
		};

	/// <summary>"Each Angel you control": Permanents(Is.Creature &amp; Is.Yours &amp; Is.Subtype("Angel")).</summary>
	public static Ref Permanents(Filter filter) => All(ZoneType.Battlefield, filter);

	public static readonly Ref CreaturesYouControl = Permanents(Is.Creature & Is.Yours);
	public static readonly Ref LandsYouControl = Permanents(Is.Land & Is.Yours);
	public static readonly Ref PlaneswalkersYouControl = Permanents(Is.Planeswalker & Is.Yours);
	public static readonly Ref AllCreatures = Permanents(Is.Creature);
	public static readonly Ref CardsInYourGraveyard = All(ZoneType.Graveyard, of: You);
	public static readonly Ref CardsInYourHand = All(ZoneType.Hand, of: You);
	public static readonly Ref YourCreaturesWithCounters = Permanents(
		Is.Creature & Is.Yours & Is.WithPlusOneCounter
	);

	/// <summary>Ajani: "each creature except for tokens you control".</summary>
	public static readonly Ref CreaturesExceptYourTokens = Permanents(
		Is.Creature & !(Is.Token & Is.Yours)
	);
	public static readonly Ref TokensYouControl = Permanents(Is.Token & Is.Yours);
	public static readonly Ref OtherCreaturesYouControl = Permanents(
		Is.Creature & Is.Yours & !Is.Source
	);

	public static Ref YourGraveyard(CardType type) => All(ZoneType.Graveyard, Is.Type(type), You);

	public static Ref OpponentsPermanents(CardType type) =>
		Permanents(Is.Type(type) & Is.Opponents);

	public static Ref Target(int index = 0) => new(RefKind.Target, index);

	public static Ref ControllerOfTarget(int index = 0) => new(RefKind.ControllerOfTarget, index);

	public static Ref OwnerOfTarget(int index = 0) => new(RefKind.OwnerOfTarget, index);

	/// <summary>Twisted Fates: "each creature target player controls".</summary>
	public static Ref PermanentsOfTargetPlayer(CardType type, int index) =>
		All(ZoneType.Battlefield, Is.Type(type), Target(index));

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
			RefKind.All => AllIn(s, context, controller, eventCard),
			_ => throw new ArgumentOutOfRangeException(
				nameof(Kind),
				Kind,
				"a Ref kind Resolve doesn't know"
			),
		};
		return ids.Where(id => id != 0 && s.HasObject(id));
	}

	/// <summary>
	/// The objects it names — and "this" once it is gone, as it last existed (CR 113.7a): Gardenize
	/// destroyed in response still counts its charge counters. Count and Matches both read this.
	/// </summary>
	public IEnumerable<GameObject> ObjectsAsTheyWere(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		var objects = Resolve(s, context).Select(s.GetObject).ToList();
		return
			Kind == RefKind.Source
			&& objects.Count == 0
			&& context.GetValueOrDefault(ContextKeys.SourceSnapshot) is Card then
			? [then]
			: objects;
	}

	IEnumerable<int> AllIn(
		GameState s,
		ImmutableDictionary<string, object> context,
		int controller,
		int eventCard
	)
	{
		// ZoneType's default is Hand: a Ref.All built without a zone must not quietly mean "hands".
		var zone = Zone ?? throw new InvalidOperationException("Ref.All needs a Zone");
		var asking = ContextKeys.Asking(context);
		return Whose(s, context, controller)
			.SelectMany(p => s.CardsIn(p, zone))
			.Where(c => Filter?.Matches(s, c, asking) ?? true)
			.Select(c => c.Id);
	}

	/// <summary>All's players: Of's (only the players it names), or every player, yours first.</summary>
	IEnumerable<int> Whose(
		GameState s,
		ImmutableDictionary<string, object> context,
		int controller
	) =>
		Of is null
			? [controller, s.OpponentOf(controller)]
			: Of.Resolve(s, context).Where(id => s.GetObject(id) is MagicPlayer);
}

public enum CountOf
{
	Fixed,
	NumberOf, // "the number of [Ref]"
	GreatestPowerOf, // "the greatest power among [Ref]"
	GreatestManaValueOf, // Hapatra: "the greatest mana value among cards in your graveyard"
	DistinctPowersOf, // Loot, the Nexus: "each different power among [Ref]"
	ColorsAmong, // Karn, Gilded Guardian: "each color among [Ref]"
	PowerOf, // "equal to its power"
	LoyaltyOf, // "equal to its loyalty"
	X, // the X paid
	CountersOn, // "+1/+1 counters on [Ref]"
	LifeYouGainedThisTurn,
	CardsYouDrewThisTurn,
	CardsYouDiscardedThisTurn,
	LoyaltyAbilitiesYouActivatedThisTurn,
	BasicLandTypesAmongLandsYouControl, // domain
	CardTypesInAllGraveyards, // Tarmogoyf
	YourSurveilsThisTurn,
	SpellsYouCastThisTurn,
	NoncombatDamageToOpponentsLastTurn,
	NoncombatDamageToOpponentsThisTurn,
	CreaturesDiedThisTurn, // anyone's
	ChargeCountersOn, // Gardenize: "for each charge counter on [Ref]"
	CardsFromLibraryToGraveyardThisTurn, // Cruel Calculations: of the player [Ref]
	NonlandCardsChosen, // Seasoned Cryomancer: "nonland cards [discarded] this way" — the last hand choice
	TotalLoyaltyOf, // Jace, Reality Sculptor: "loyalty counters among Jaces you control"
	SubtypesAmong, // Tam: "planeswalker types among planeswalkers you control"
}

/// <summary>
/// A number in rules text, counted at resolution. Counting goes through Ref, so "creatures you
/// control" means one thing everywhere. A negative Sign is "-X".
/// </summary>
public record Count(int Fixed, Ref? Of = null, CountOf Kind = CountOf.Fixed, int Sign = 1)
{
	/// <summary>"For every three cards in your graveyard": the count divided by this, rounded down.</summary>
	public int Per { get; init; } = 1;

	/// <summary>Dark Matter Manipulator: "+2/+0 for every seven" — each Per counts this many.</summary>
	public int Times { get; init; } = 1;

	public static implicit operator Count(int n) => new(n);

	public static Count NumberOf(Ref r) => new(0, r, CountOf.NumberOf);

	public static Count GreatestPowerOf(Ref r) => new(0, r, CountOf.GreatestPowerOf);

	public static Count GreatestManaValueOf(Ref r) => new(0, r, CountOf.GreatestManaValueOf);

	public static Count DistinctPowersOf(Ref r) => new(0, r, CountOf.DistinctPowersOf);

	public static Count ColorsAmong(Ref r) => new(0, r, CountOf.ColorsAmong);

	public static Count PowerOf(Ref r) => new(0, r, CountOf.PowerOf);

	public static Count LoyaltyOf(Ref r) => new(0, r, CountOf.LoyaltyOf);

	public static readonly Count X = new(0, null, CountOf.X);

	public static Count CountersOn(Ref r) => new(0, r, CountOf.CountersOn);

	public static Count ChargeCountersOn(Ref r) => new(0, r, CountOf.ChargeCountersOn);

	public static Count TotalLoyaltyOf(Ref r) => new(0, r, CountOf.TotalLoyaltyOf);

	public static Count SubtypesAmong(Ref r) => new(0, r, CountOf.SubtypesAmong);

	public static Count CardsFromLibraryToGraveyardThisTurn(Ref player) =>
		new(0, player, CountOf.CardsFromLibraryToGraveyardThisTurn);

	public static readonly Count NonlandCardsChosen = new(0, null, CountOf.NonlandCardsChosen);

	public static readonly Count LifeYouGainedThisTurn =
		new(0, null, CountOf.LifeYouGainedThisTurn);
	public static readonly Count CardsYouDrewThisTurn = new(0, null, CountOf.CardsYouDrewThisTurn);
	public static readonly Count CardsYouDiscardedThisTurn =
		new(0, null, CountOf.CardsYouDiscardedThisTurn);
	public static readonly Count LoyaltyAbilitiesYouActivatedThisTurn =
		new(0, null, CountOf.LoyaltyAbilitiesYouActivatedThisTurn);

	/// <summary>Tarmogoyf: "the number of card types among cards in all graveyards".</summary>
	public static readonly Count CardTypesInAllGraveyards =
		new(0, null, CountOf.CardTypesInAllGraveyards);

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
		Sign * Times * (Counted(s, context) / Per);

	int Counted(GameState s, ImmutableDictionary<string, object> context)
	{
		if (Kind == CountOf.Fixed)
			return Fixed;
		if (Kind == CountOf.X)
			return context.TryGetValue(ContextKeys.X, out var x) ? (int)x : 0;
		if (Kind == CountOf.CreaturesDiedThisTurn)
			return s.GetGame().CreaturesDiedThisTurn;
		if (Kind == CountOf.NonlandCardsChosen)
			return ChooseCardsFromHandChoice
				.Read(context)
				.Count(id => s.HasObject(id) && !s.GetCard(id).Is(CardType.Land));
		if (Kind == CountOf.CardsFromLibraryToGraveyardThisTurn)
			return Of!
				.Resolve(s, context)
				.Select(s.GetObject)
				.OfType<MagicPlayer>()
				.Sum(p => p.ThisTurn.CardsFromLibraryToGraveyard);
		if (Kind == CountOf.CardTypesInAllGraveyards) // one pass: it sits under Tarmogoyf's P/T
			return System.Numerics.BitOperations.PopCount(
				(uint)
					new[] { s.Player1Id(), s.Player2Id() }
						.SelectMany(p => s.CardsIn(p, ZoneType.Graveyard))
						.Aggregate(CardType.None, (types, c) => types | c.Types)
			);
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
		IEnumerable<Card> Cards() => Of.ObjectsAsTheyWere(s, context).OfType<Card>();
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
			CountOf.ColorsAmong => Cards()
				.SelectMany(c => c.Colors)
				.Where(c => c != ManaColor.Colorless)
				.Distinct()
				.Count(),
			CountOf.CountersOn => Cards().Sum(c => c.PlusOneCounters),
			CountOf.ChargeCountersOn => Cards().Sum(c => c.ChargeCounters),
			CountOf.TotalLoyaltyOf => Cards().Sum(c => c.Loyalty),
			CountOf.SubtypesAmong => Cards().SelectMany(c => c.Subtypes).Distinct().Count(),
			_ => Cards().Select(c => c.Loyalty).FirstOrDefault(),
		};
		// CR 107.1b: a number that would be negative is 0 — a -4 power must not make "lose X life"
		// a life gain. The sign ("-X") applies after.
		return Math.Max(0, counted);
	}
}
