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

	/// <summary>The targets as they were chosen, legal now or not — Pure Energy's split was announced then (Count.Share).</summary>
	public const string ChosenTargets = "ChosenTargets";

	/// <summary>Pyrokinesis: what each chosen target takes of what was divided, in the chosen targets' order (OnStackComponent.Shares).</summary>
	public const string Shares = "Shares";

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

	/// <summary>The object an effect is acting on right now, one at a time (Ref.It) — PutCounters, RemoveCounters.</summary>
	public const string It = "It";

	/// <summary>ImmutableArray&lt;int&gt;: what an effect did its thing to, for its Then (Ref.ThisWay) — Destroy.</summary>
	public const string ThisWay = "ThisWay";

	/// <summary>Who a filter asks for in a resolving context: "you", "this", "that creature".</summary>
	public static Filters.FilterContext Asking(ImmutableDictionary<string, object> context) =>
		new(
			(int)context[Controller],
			context.TryGetValue(Source, out var source) ? (int)source : 0,
			context.TryGetValue(EventCard, out var e) ? (int)e : 0,
			context.GetValueOrDefault(SourceSnapshot) is Card was ? was.Incarnation : null
		);
}

public enum RefKind
{
	You,
	EachOpponent,
	EachPlayer,
	Target,
	ControllerOfTarget,
	OwnerOf, // Clash of Elements: "its owner" — of the cards a Ref names (Of), wherever they now are
	Source,
	SourceHost, // an Aura's "enchanted creature" — what the source is attached to
	EventCard,
	EventObject, // ward: "counter it" — the spell or ability that targeted, if it is still that object
	ControllerOfEventCard, // Tetsuko, Pursuer: "that creature's controller"
	All, // "creatures you control", "cards in your graveyard": a zone's objects that match a filter
	EachTarget, // Dominion Infiltration: "for each of them" — every target chosen, those still legal
	ActivePlayer, // Peace in Our Time: "on each player's turn, that player" — whose turn it is
	It, // Bristly Bill: "double the number of +1/+1 counters on EACH creature" — the one being acted on
	ThisWay, // Decree of Pain: "each creature destroyed this way" — what the effect did its thing to
	Chosen, // Temporal Conqueror: "return a land card from your graveyard" — the card(s) just chosen (MoveChosen)
	Id, // explore's revealed card, discover's exiled one — a card the engine named as it built the step
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
	/// Target(i); "your graveyard" is Of = You. None is every player's, yours first. For OwnerOf: the cards.
	/// </summary>
	public Ref? Of { get; init; }

	/// <summary>
	/// For All: Engineered Explosives: "each nonland permanent with mana value equal to the number of
	/// charge counters on this artifact" — counted as it resolves (the sacrificed source as it last was).
	/// </summary>
	public Count? ManaValueIs { get; init; }

	/// <summary>
	/// For All: Shallow Grave: "the top creature card of your graveyard" — only the one nearest the top of each
	/// player's zone that the filter matches: a library's first, a graveyard's last put there (MoveObject appends).
	/// </summary>
	public bool Top { get; init; }

	public static readonly Ref You = new(RefKind.You);
	public static readonly Ref EachTarget = new(RefKind.EachTarget);
	public static readonly Ref EachOpponent = new(RefKind.EachOpponent);
	public static readonly Ref EachPlayer = new(RefKind.EachPlayer);
	public static readonly Ref Source = new(RefKind.Source);

	/// <summary>Peace in Our Time: "At the beginning of combat on each player's turn, that player …" — whose turn it is.</summary>
	public static readonly Ref ActivePlayer = new(RefKind.ActivePlayer);

	/// <summary>
	/// Bristly Bill, Spine Sower: "Double the number of +1/+1 counters on each creature you control" — the
	/// object an effect is acting on, for a Count counted per object: Count.CountersOn(Ref.It).
	/// </summary>
	public static readonly Ref It = new(RefKind.It);

	/// <summary>
	/// Decree of Pain: "Draw a card for each creature destroyed this way" — in an effect's Then, what that
	/// effect did its thing to (Destroy.Then): Count.NumberOf(Ref.ThisWay). The ids, wherever they are now.
	/// </summary>
	public static readonly Ref ThisWay = new(RefKind.ThisWay);

	/// <summary>
	/// MC-6.12: Temporal Conqueror's "a land card from your graveyard", chosen as it resolves — in the steps after a
	/// choice, what it picked (ChooseCardsFromHandChoice.Chosen; MoveChosen moves it with MoveTo).
	/// </summary>
	public static readonly Ref Chosen = new(RefKind.Chosen);

	/// <summary>
	/// MC-6.12: explore's "put the card back or put it into your graveyard" — one card the engine already knows by
	/// id as it builds the step (the revealed card), never from a script.
	/// </summary>
	public static Ref Id(int cardId) => new(RefKind.Id, cardId);

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

	public static Ref OwnerOfTarget(int index = 0) => OwnerOf(Target(index));

	/// <summary>
	/// Skyclave Apparition: "the exiled card's owner creates …" — the owners of the cards a Ref names, each once:
	/// OwnerOf(All(Exile, Is.ExiledBySource)). Clash of Elements' "its owner" is OwnerOf(Target()).
	/// </summary>
	public static Ref OwnerOf(Ref cards) => new(RefKind.OwnerOf) { Of = cards };

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
			// Ertai Resurrected: "Counter target spell, activated ability, or triggered ability. Its controller draws."
			RefKind.ControllerOfTarget => !s.HasObject(TargetAt(Index))
				? []
				: s.GetObject(TargetAt(Index)) switch
				{
					Card c => [c.ControllerId],
					Actions.StackAbility a => [a.ControllerId],
					MagicPlayer p => [p.Id], // Chain Lightning: "that player or that permanent's controller"
					_ => [],
				},
			// The Aura as it was (last-known, CR 608.2h) — so removing it in response changes nothing,
			// and an Aura that left and came back on something else doesn't redirect it.
			RefKind.SourceHost => (
				context.GetValueOrDefault(ContextKeys.SourceSnapshot) as Card
				?? s.GetObject((int)context[ContextKeys.Source]) as Card
			)
				is { } aura
			&& Rules.Attachments.HostOf(s, aura, orGraveyard: true) is { } host // Animate Dead's: a card in a graveyard
				? [host.Id]
				: [],
			RefKind.OwnerOf => Of!
				.Resolve(s, context)
				.Select(s.GetObject)
				.OfType<Card>()
				.Select(c => c.OwnerId)
				.Distinct(),
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
			RefKind.EachTarget => targets,
			RefKind.ActivePlayer => [s.GetGame().ActivePlayerId],
			RefKind.It => context.TryGetValue(ContextKeys.It, out var it) ? [(int)it] : [],
			RefKind.ThisWay => context.TryGetValue(ContextKeys.ThisWay, out var done)
				? (ImmutableArray<int>)done
				: [],
			RefKind.Chosen => ChooseCardsFromHandChoice.Read(context),
			RefKind.Id => [Index],
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
		if (
			Kind == RefKind.Source
			&& objects.Count == 0
			&& context.GetValueOrDefault(ContextKeys.SourceSnapshot) is Card then
		)
			return [then];
		return objects;
	}

	/// <summary>
	/// What a Count counts: <see cref="ObjectsAsTheyWere"/>, and "its power" once that creature changed
	/// zones (CR 400.7: gone, or another object now) as it was — Station's tapped creature, as it was
	/// tapped. Not Matches: "if it's in your graveyard" asks where it is NOW.
	/// </summary>
	internal IEnumerable<GameObject> CountedAsTheyWere(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		var objects = ObjectsAsTheyWere(s, context).ToList();
		return
			Kind == RefKind.EventCard
			&& context.GetValueOrDefault(ContextKeys.EventSnapshot) is Card was
			&& !(objects is [Card now] && now.Incarnation == was.Incarnation)
			? [was]
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
		if (Top && zone is not (ZoneType.Library or ZoneType.Graveyard))
			throw new InvalidOperationException(
				$"Ref.All's Top means a library or a graveyard, not {zone}"
			);
		var asking = ContextKeys.Asking(context);
		var manaValue = ManaValueIs?.Value(s, context);
		return Whose(s, context, controller)
			.SelectMany(p =>
			{
				var matching = s.CardsIn(p, zone)
					.Where(c =>
						(Filter?.Matches(s, c, asking) ?? true)
						&& (manaValue is null || c.ManaValueNow == manaValue)
					);
				return !Top ? matching
					: zone == ZoneType.Library ? matching.Take(1)
					: matching.TakeLast(1);
			})
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
	GreatestToughnessOf, // Ghalta, the Immovable: "the greatest toughness among creatures you control"
	GreatestManaValueOf, // Hapatra: "the greatest mana value among cards in your graveyard"
	DistinctPowersOf, // Loot, the Nexus: "each different power among [Ref]"
	ColorsAmong, // Karn, Gilded Guardian: "each color among [Ref]"
	PowerOf, // "equal to its power"
	LoyaltyOf, // "equal to its loyalty"
	X, // the X paid
	Share, // Pyrokinesis: what this target takes of what was divided
	CountersOn, // "+1/+1 counters on [Ref]"
	LifeYouGainedThisTurn,
	CardsYouDrewThisTurn,
	CardsYouDiscardedThisTurn,
	LoyaltyAbilitiesYouActivatedThisTurn,
	BasicLandTypesAmongLandsYouControl, // domain
	CardTypesAmong, // Tarmogoyf: "card types among cards in all graveyards"
	YourSurveilsThisTurn,
	SpellsYouCastThisTurn,
	NoncreatureSpellsYouCastThisTurn, // Discovery of the Doomsayer's "if you've cast a noncreature spell this turn"
	NoncombatDamageToOpponentsLastTurn,
	NoncombatDamageToOpponentsThisTurn,
	CreaturesDiedThisTurn, // anyone's
	ChargeCountersOn, // Gardenize: "for each charge counter on [Ref]"
	ClassLevelOf, // Does Machines: a Class's level — 1 until it gains one (ClassLevel)
	CardsFromLibraryToGraveyardThisTurn, // Cruel Calculations: of the player [Ref]
	PermanentsReturnedToHandThisTurn, // Miles O'Brien: "a permanent was returned to your hand from the battlefield this turn" — of the player [Ref]
	NonlandCardsChosen, // Seasoned Cryomancer: "nonland cards [discarded] this way" — the last hand choice
	PermanentsChosen, // Genesis Device: "up to that many" — the last permanent choice (the lands sacrificed)
	TotalLoyaltyOf, // Jace, Reality Sculptor: "loyalty counters among Jaces you control"
	SubtypesAmong, // Tam: "planeswalker types among planeswalkers you control"
	DistinctNamesOf, // Strange new worlds: "the number of differently named lands you control"
	TotalPowerOf, // Power 8: "creatures you control have total power 8 or greater"
	CountersOfKind, // Anthropological Mission: "three or more quest counters on [Ref]" — Count.CounterKind; a player's too (Guide of Souls' {E})
	ColorsSpent, // Prismatic Ending: "the number of colors of mana spent to cast this spell" — [Ref]'s spell (Card.AsCast)
	CreatureTypesAmong, // Federation: "creature types among non-Borg creatures you control"
	AllCountersOn, // Jadzia Dax: "the number of counters on her" — every kind
	LifeTotalOf, // Live Long and Prosper: "Double your life total" — the players [Ref] names
	LifeLostThisTurnOf, // Gul Dukat: "the life that player lost this turn" — the players [Ref] names
	DilemmasFacedThisTurnBy, // Sisko: "each dilemma you've faced this turn" — the players [Ref] names
	ModesChosen, // Riku of Many Paths: "the number of times you chose a mode for that spell" — the spell on the stack [Ref]
	TimesReplicated, // Sicarian Infiltrator's squad: "create that many tokens" — the times [Ref]'s replicate cost was paid, on the stack or the permanent it became
	SpellsCastBefore, // Storm (Brain Freeze): "for each spell cast before it this turn" — every player's, counted as [Ref] was cast
	DevotionTo, // Mogis: "your devotion to black and red" — Count.Colors
	BeyondTheFewest, // Balance: "equal to the number … controlled by the player who controls the fewest, then sacrifices the rest" — the rest, for the player Ref.It
	PermanentsLeftTheBattlefieldThisTurn, // Revolt (Fatal Push): "if a permanent left the battlefield under your control this turn" — of the player [Ref]
	AmountPaid, // PayAnyAmount: "that many" / "that much" — the number paid
	LandsYouPlayedThisTurn, // Fastbond: "if it wasn't the first land you played this turn"
}

/// <summary>
/// A number in rules text, counted at resolution. Counting goes through Ref, so "creatures you
/// control" means one thing everywhere. A negative Sign is "-X".
/// </summary>
public record Count(int Fixed, Ref? Of = null, CountOf Kind = CountOf.Fixed, int Sign = 1)
{
	/// <summary>"For every three cards in your graveyard": the count divided by this, rounded down.</summary>
	public int Per { get; init; } = 1;

	/// <summary>Tamiyo, Seasoned Scholar's −7: "half the number of cards in your library, rounded up" — Per rounds up.</summary>
	public bool RoundUp { get; init; }

	/// <summary>Dark Matter Manipulator: "+2/+0 for every seven" — each Per counts this many.</summary>
	public int Times { get; init; } = 1;

	/// <summary>Sickbay: "where X is one plus the amount of life you gained this turn" — added after the rest.</summary>
	public int Plus { get; init; }

	/// <summary>CountersOfKind: which kind — "quest", "lore", "promotion".</summary>
	public string? CounterKind { get; init; }

	public static Count CountersOfKind(Ref r, string kind) =>
		new(0, r, CountOf.CountersOfKind) { CounterKind = kind };

	/// <summary>
	/// Converge (Prismatic Ending): "the number of colors of mana spent to cast this spell" — the spell's (or the
	/// permanent's: what it was cast as) OnStackComponent.ColorsSpent; the cast pays with the most colours it can,
	/// as sunburst's, because the card carries Sunburst.
	/// </summary>
	public static Count ColorsSpent(Ref r) => new(0, r, CountOf.ColorsSpent);

	/// <summary>DevotionTo: which colours.</summary>
	public ImmutableArray<ManaColor> Colors { get; init; } = [];

	/// <summary>
	/// Mogis, God of Slaughter: "your devotion to black and red" — the mana symbols of those colours among
	/// the mana costs of permanents you control; a hybrid symbol of both counts once (CR 700.5).
	/// </summary>
	public static Count DevotionTo(params ManaColor[] colors) =>
		new(0, Ref.Permanents(Is.Yours), CountOf.DevotionTo) { Colors = [.. colors] };

	public static implicit operator Count(int n) => new(n);

	public static Count NumberOf(Ref r) => new(0, r, CountOf.NumberOf);

	public static Count GreatestPowerOf(Ref r) => new(0, r, CountOf.GreatestPowerOf);

	public static Count GreatestToughnessOf(Ref r) => new(0, r, CountOf.GreatestToughnessOf);

	public static Count GreatestManaValueOf(Ref r) => new(0, r, CountOf.GreatestManaValueOf);

	public static Count DistinctPowersOf(Ref r) => new(0, r, CountOf.DistinctPowersOf);

	public static Count ColorsAmong(Ref r) => new(0, r, CountOf.ColorsAmong);

	public static Count PowerOf(Ref r) => new(0, r, CountOf.PowerOf);

	public static Count LoyaltyOf(Ref r) => new(0, r, CountOf.LoyaltyOf);

	public static readonly Count X = new(0, null, CountOf.X);

	/// <summary>
	/// Pyrokinesis: "4 damage divided as you choose among any number of target creatures" — what the object being
	/// acted on (Ref.It) takes: its share as the spell was cast (TargetSpec.Divide, CR 601.2d). Read by its
	/// place among the targets as CHOSEN, so a target gone illegal takes its share with it — never handed to
	/// the others (Pure Energy's second creature killed in response).
	/// </summary>
	public static readonly Count Share = new(0, null, CountOf.Share);

	public static Count CountersOn(Ref r) => new(0, r, CountOf.CountersOn);

	/// <summary>
	/// Balance: "Each player chooses a number of lands they control equal to the number of lands controlled by the
	/// player who controls the fewest, then sacrifices the rest" — the rest: how many more of [r] the player being
	/// acted on (Ref.It) has than the player with the fewest: BeyondTheFewest(Battlefield, Is.Land), each player's
	/// own counted. It is 0 where no player is being acted on.
	/// </summary>
	public static Count BeyondTheFewest(ZoneType zone, Filter? filter = null) =>
		new(0, Ref.All(zone, filter, Ref.It), CountOf.BeyondTheFewest);

	/// <summary>Riku of Many Paths: "the number of times you chose a mode for that spell" — Count.ModesChosen(Ref.EventCard).</summary>
	public static Count ModesChosen(Ref r) => new(0, r, CountOf.ModesChosen);

	/// <summary>Squad (Sicarian Infiltrator): "create that many tokens" — the times [r]'s squad (replicate) cost was paid, read from its spell (Card.AsCast).</summary>
	public static Count TimesReplicated(Ref r) => new(0, r, CountOf.TimesReplicated);

	/// <summary>Storm (Brain Freeze): "for each spell cast before it this turn" — every player's, counted as [r] was cast (OnStackComponent.SpellsCastBefore).</summary>
	public static Count SpellsCastBefore(Ref r) => new(0, r, CountOf.SpellsCastBefore);

	/// <summary>Jadzia Dax, Living Legacy: "an amount of {G} equal to the number of counters on her" — every kind.</summary>
	public static Count AllCountersOn(Ref r) => new(0, r, CountOf.AllCountersOn);

	/// <summary>Galvanic Discharge: "deals that much damage"; Necrodominance: "draw that many cards" — what PayAnyAmount paid.</summary>
	public static readonly Count AmountPaid = new(0, null, CountOf.AmountPaid);

	/// <summary>Live Long and Prosper: "Double your life total" — gain this much (CR 119.5).</summary>
	public static Count LifeTotalOf(Ref r) => new(0, r, CountOf.LifeTotalOf);

	/// <summary>Gul Dukat, Colonial Prefect: "equal to the life that player lost this turn" (TurnTally.LifeLost).</summary>
	public static Count LifeLostThisTurnOf(Ref r) => new(0, r, CountOf.LifeLostThisTurnOf);

	/// <summary>Captain Benjamin Sisko: "for each dilemma you've faced this turn"; The Caretaker: "if you faced a dilemma this turn" (TurnTally.DilemmasFaced).</summary>
	public static Count DilemmasFacedThisTurnBy(Ref r) =>
		new(0, r, CountOf.DilemmasFacedThisTurnBy);

	public static Count ChargeCountersOn(Ref r) => new(0, r, CountOf.ChargeCountersOn);

	/// <summary>Does Machines: "Level 3" — the Class's level (ClassLevel), 1 as it enters.</summary>
	public static Count ClassLevelOf(Ref r) => new(0, r, CountOf.ClassLevelOf);

	public static Count TotalLoyaltyOf(Ref r) => new(0, r, CountOf.TotalLoyaltyOf);

	public static Count SubtypesAmong(Ref r) => new(0, r, CountOf.SubtypesAmong);

	public static Count DistinctNamesOf(Ref r) => new(0, r, CountOf.DistinctNamesOf);

	public static Count TotalPowerOf(Ref r) => new(0, r, CountOf.TotalPowerOf);

	public static Count CreatureTypesAmong(Ref r) => new(0, r, CountOf.CreatureTypesAmong);

	public static Count CardsFromLibraryToGraveyardThisTurn(Ref player) =>
		new(0, player, CountOf.CardsFromLibraryToGraveyardThisTurn);

	public static Count PermanentsReturnedToHandThisTurn(Ref player) =>
		new(0, player, CountOf.PermanentsReturnedToHandThisTurn);

	public static Count PermanentsLeftTheBattlefieldThisTurn(Ref player) =>
		new(0, player, CountOf.PermanentsLeftTheBattlefieldThisTurn);

	public static readonly Count NonlandCardsChosen = new(0, null, CountOf.NonlandCardsChosen);

	/// <summary>Genesis Device: "Sacrifice any number of lands …, then search … for up to that many" — the last permanent choice.</summary>
	public static readonly Count PermanentsChosen = new(0, null, CountOf.PermanentsChosen);

	public static readonly Count LifeYouGainedThisTurn =
		new(0, null, CountOf.LifeYouGainedThisTurn);

	/// <summary>
	/// "Cards you've drawn this turn" — and, as each of the "you … this turn" counts, another player's with Of:
	/// Narset, Parter of Veils' "[an opponent] can't draw more than one card each turn" is
	/// CardsYouDrewThisTurn with { Of = Ref.It }, the player drawing.
	/// </summary>
	public static readonly Count CardsYouDrewThisTurn = new(0, null, CountOf.CardsYouDrewThisTurn);
	public static readonly Count CardsYouDiscardedThisTurn =
		new(0, null, CountOf.CardsYouDiscardedThisTurn);
	public static readonly Count LoyaltyAbilitiesYouActivatedThisTurn =
		new(0, null, CountOf.LoyaltyAbilitiesYouActivatedThisTurn);

	/// <summary>
	/// "The number of card types among [Ref]" — Tarmogoyf's "cards in all graveyards" (Ref.All(Graveyard)), Occult
	/// Epiphany's "cards discarded this way" (Ref.Chosen in Discard.Then).
	/// </summary>
	public static Count CardTypesAmong(Ref r) => new(0, r, CountOf.CardTypesAmong);

	/// <summary>Domain: "the number of basic land types among lands you control".</summary>
	public static readonly Count BasicLandTypesAmongLandsYouControl =
		new(0, null, CountOf.BasicLandTypesAmongLandsYouControl);
	public static readonly Count YourSurveilsThisTurn = new(0, null, CountOf.YourSurveilsThisTurn);
	public static readonly Count SpellsYouCastThisTurn =
		new(0, null, CountOf.SpellsYouCastThisTurn);
	public static readonly Count NoncreatureSpellsYouCastThisTurn =
		new(0, null, CountOf.NoncreatureSpellsYouCastThisTurn);

	/// <summary>Fastbond: "if it wasn't the first land you played this turn" — TurnTally.LandsPlayed.</summary>
	public static readonly Count LandsYouPlayedThisTurn =
		new(0, null, CountOf.LandsYouPlayedThisTurn);
	public static readonly Count NoncombatDamageToOpponentsLastTurn =
		new(0, null, CountOf.NoncombatDamageToOpponentsLastTurn);
	public static readonly Count NoncombatDamageToOpponentsThisTurn =
		new(0, null, CountOf.NoncombatDamageToOpponentsThisTurn);
	public static readonly Count CreaturesDiedThisTurn =
		new(0, null, CountOf.CreaturesDiedThisTurn);

	public static readonly Count CreaturesYouControl = NumberOf(Ref.CreaturesYouControl);
	public static readonly Count LandsYouControl = NumberOf(Ref.LandsYouControl);

	public Count Negated => this with { Sign = -Sign, Plus = -Plus };

	// The generated one prints Negated, whose own Negated … forever — a counted filter bound in a
	// refusal's message hung on it (StatBetween, MC-6.4).
	protected virtual bool PrintMembers(System.Text.StringBuilder builder)
	{
		builder.Append(
			$"Fixed = {Fixed}, Of = {Of}, Kind = {Kind}, Sign = {Sign}, Per = {Per}, Times = {Times}, Plus = {Plus}, CounterKind = {CounterKind}"
		);
		return true;
	}

	/// <summary>"Is this a fixed nothing?" — a static with Power 0 and Toughness 0 changes no P/T.</summary>
	public static bool operator ==(Count c, int n) =>
		c.Kind == CountOf.Fixed && c.Sign * c.Fixed + c.Plus == n;

	public static bool operator !=(Count c, int n) => !(c == n);

	public int Value(GameState s, ImmutableDictionary<string, object> context) =>
		Sign * Times * ((Counted(s, context) + (RoundUp ? Per - 1 : 0)) / Per) + Plus;

	int Counted(GameState s, ImmutableDictionary<string, object> context)
	{
		if (Kind == CountOf.Fixed)
			return Fixed;
		if (Kind == CountOf.X)
			return context.TryGetValue(ContextKeys.X, out var x) ? (int)x : 0;
		if (Kind == CountOf.Share)
			return
				context.GetValueOrDefault(ContextKeys.Shares) is ImmutableArray<int> shares
				&& context.GetValueOrDefault(ContextKeys.ChosenTargets)
					is ImmutableArray<int> chosen
				&& context.GetValueOrDefault(ContextKeys.It) is int it
				&& chosen.Where(id => id != 0).ToList().IndexOf(it) is >= 0 and var place // a word left empty has no share
				? shares[place]
				: 0;
		if (Kind == CountOf.BeyondTheFewest)
		{
			// Two players: what one has beyond the other, or nothing — a Ref.It that is no player has none.
			int Has(int player) => Of!.Resolve(s, context.SetItem(ContextKeys.It, player)).Count();
			return context.GetValueOrDefault(ContextKeys.It) is int it
				? Math.Max(0, Has(it) - Has(s.OpponentOf(it)))
				: 0;
		}
		if (Kind == CountOf.CreaturesDiedThisTurn)
			return s.GetGame().CreaturesDiedThisTurn;
		if (Kind == CountOf.PermanentsChosen)
			return ChoosePermanentChoice.Read(context).Count();
		if (Kind == CountOf.AmountPaid)
			return context.GetValueOrDefault(ChooseNumberChoice.Chosen) is int paid ? paid - 1 : 0;
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
		if (Kind == CountOf.PermanentsReturnedToHandThisTurn)
			return Of!
				.Resolve(s, context)
				.Select(s.GetObject)
				.OfType<MagicPlayer>()
				.Sum(p => p.ThisTurn.PermanentsReturnedToHand);
		if (Kind == CountOf.PermanentsLeftTheBattlefieldThisTurn)
			return Of!
				.Resolve(s, context)
				.Select(s.GetObject)
				.OfType<MagicPlayer>()
				.Sum(p => p.ThisTurn.PermanentsLeftTheBattlefield);

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
				or CountOf.NoncreatureSpellsYouCastThisTurn
				or CountOf.LandsYouPlayedThisTurn
		)
		{
			// Whose turn tally: Of (Narset: Ref.It, the player drawing); unset is you.
			var whose = Of is null
				? (int)context[ContextKeys.Controller]
				: Of.Resolve(s, context).FirstOrDefault();
			if (!s.HasObject(whose) || s.GetObject(whose) is not MagicPlayer whoseTally)
				return 0;
			var tally = whoseTally.ThisTurn;
			return Kind switch
			{
				CountOf.LifeYouGainedThisTurn => tally.LifeGained,
				CountOf.CardsYouDrewThisTurn => tally.CardsDrawn,
				CountOf.CardsYouDiscardedThisTurn => tally.CardsDiscarded,
				CountOf.LoyaltyAbilitiesYouActivatedThisTurn => tally.LoyaltyAbilitiesActivated,
				CountOf.SpellsYouCastThisTurn => tally.SpellsCast,
				CountOf.NoncreatureSpellsYouCastThisTurn => tally.NoncreatureSpellsCast,
				CountOf.LandsYouPlayedThisTurn => tally.LandsPlayed,
				_ => tally.Surveils,
			};
		}
		var ids = Of!.Resolve(s, context);
		IEnumerable<Card> Cards() => Of.CountedAsTheyWere(s, context).OfType<Card>();
		var counted = Kind switch
		{
			CountOf.NumberOf => ids.Count(),
			CountOf.GreatestPowerOf => Cards().Select(c => s.Power(c)).DefaultIfEmpty(0).Max(),
			CountOf.GreatestToughnessOf => Cards()
				.Select(c => s.Toughness(c))
				.DefaultIfEmpty(0)
				.Max(),
			CountOf.GreatestManaValueOf => Cards()
				.Select(c => c.ManaValueNow) // a spell on the stack: its X too (Mana Drain, CR 202.3e)
				.DefaultIfEmpty(0)
				.Max(),
			CountOf.PowerOf => Cards().Select(c => s.Power(c)).FirstOrDefault(),
			CountOf.DistinctPowersOf => Cards().Select(c => s.Power(c)).Distinct().Count(),
			// the ids, not Cards()' two lists as they were: it sits under Tarmogoyf's P/T
			CountOf.CardTypesAmong => System.Numerics.BitOperations.PopCount(
				(uint)
					ids.Aggregate(
						CardType.None,
						(types, id) => s.GetObject(id) is Card c ? types | c.Types : types
					)
			),
			CountOf.ColorsAmong => Cards()
				.SelectMany(c => c.Colors)
				.Where(c => c != ManaColor.Colorless)
				.Distinct()
				.Count(),
			CountOf.CountersOn => Cards().Sum(c => c.PlusOneCounters),
			CountOf.AllCountersOn => Cards().Sum(c => c.AllCounters().Sum(k => k.N)),
			CountOf.LifeTotalOf => ids.Sum(id => s.GetObject(id) is MagicPlayer p ? p.Life : 0),
			CountOf.LifeLostThisTurnOf => ids.Sum(id =>
				s.GetObject(id) is MagicPlayer p ? p.ThisTurn.LifeLost : 0
			),
			CountOf.DilemmasFacedThisTurnBy => ids.Sum(id =>
				s.GetObject(id) is MagicPlayer p ? p.ThisTurn.DilemmasFaced : 0
			),
			CountOf.ChargeCountersOn => Cards().Sum(c => c.ChargeCounters),
			CountOf.ClassLevelOf => Cards().Sum(c => c.GetComponent<ClassLevel>()?.N ?? 1),
			CountOf.ModesChosen => Cards()
				.Sum(c => c.GetComponent<Actions.OnStackComponent>()?.Modes.Length ?? 0),
			CountOf.TimesReplicated => Cards().Sum(c => c.AsCast?.Replicate ?? 0),
			CountOf.SpellsCastBefore => Cards().Sum(c => c.AsCast?.SpellsCastBefore ?? 0),
			CountOf.CountersOfKind => Of.CountedAsTheyWere(s, context)
				.Sum(o =>
					o switch
					{
						Card c => c.CountersOf(CounterKind!),
						MagicPlayer p => p.CountersOf(CounterKind!), // a player's (CR 122.1): energy, poison
						_ => 0,
					}
				),
			CountOf.ColorsSpent => Cards().Select(c => c.AsCast?.ColorsSpent ?? 0).FirstOrDefault(),
			CountOf.DevotionTo => Cards()
				.SelectMany(c => c.ManaCost.Symbols)
				.Count(sym =>
					sym.Kind is not (SymbolKind.Generic or SymbolKind.X)
					&& (
						Colors.Contains(sym.Color)
						|| sym.Kind == SymbolKind.Hybrid && Colors.Contains(sym.Color2)
					)
				),
			CountOf.TotalLoyaltyOf => Cards().Sum(c => c.Loyalty),
			CountOf.SubtypesAmong => Cards().SelectMany(c => s.Subtypes(c)).Distinct().Count(),
			CountOf.DistinctNamesOf => Cards().Select(c => c.Name).Distinct().Count(),
			CountOf.TotalPowerOf => Cards().Sum(c => s.Power(c)),
			CountOf.CreatureTypesAmong => Cards() // a stationed Spacecraft's "Spacecraft" isn't one
				.SelectMany(c => s.Subtypes(c))
				.Where(Characteristics.IsCreatureType)
				.Distinct()
				.Count(),
			_ => Cards().Select(c => c.Loyalty).FirstOrDefault(),
		};
		// CR 107.1b: a number that would be negative is 0 — a -4 power must not make "lose X life"
		// a life gain. The sign ("-X") applies after.
		return Math.Max(0, counted);
	}
}
