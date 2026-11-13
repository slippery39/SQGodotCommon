using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore;

/// <summary>
/// The ONE place a permanent's power, toughness and keywords are known (CR 613): its own —
/// printed, counters, until-end-of-turn effects (Card.OwnPower / HasOwn) — plus every static
/// ability on the battlefield that applies to it ("creatures you control have haste"). Every rule
/// reads these; reading OwnPower in a rule silently ignores every anthem on the board.
///
/// P/T statics and keyword statics are separate passes, so a keyword static may filter on real
/// power (Tetsuko: "power or toughness 1 or less") without power ever asking about keywords.
///
/// ponytail: no layer dependency ordering (CR 613.8) — statics add up — and a card that has left
/// keeps only its OWN characteristics in last-known information, not what statics gave it then.
/// </summary>
public static class Characteristics
{
	public static int Power(this GameState s, Card card) =>
		card.OwnPower + StaticPT(s, card, power: true);

	public static int Toughness(this GameState s, Card card) =>
		card.OwnToughness + StaticPT(s, card, power: false);

	public static bool Has(this GameState s, Card card, string keyword)
	{
		var board = BoardOf(s);
		return (card.HasOwn(keyword) || Grants(s, board, card, keyword)) && !Lost(board, card); // asked last: it's rarely true and this sits under the AI
	}

	/// <summary>
	/// THE read of a permanent's own abilities of one kind — triggered, activated, static, mana,
	/// replacement — empty once it has lost them all. Every rule asks here, never GetComponents, or
	/// "loses all abilities" misses that rule.
	/// </summary>
	public static IEnumerable<T> AbilitiesOf<T>(this GameState s, Card card)
		where T : GameComponent
	{
		// The card's own components first: most permanents have none of a kind, and then nothing to lose.
		var gains = HasGains(card);
		if (!gains && !card.HasComponent<T>() || s.HasLostAllAbilities(card))
			return [];
		return gains
			? card.GetComponents<T>()
				.Concat(card.GetComponents<UntilEndOfTurn>().SelectMany(u => u.Gains).OfType<T>())
			: card.GetComponents<T>();
	}

	/// <summary>Nog, Q: has it gained abilities (UntilEndOfTurn.Gains)? Rare — a plain loop, it sits under the AI.</summary>
	static bool HasGains(Card card)
	{
		foreach (var c in card.Components)
			if (c is UntilEndOfTurn { Gains.Count: > 0 })
				return true;
		return false;
	}

	public static bool HasAbility<T>(this GameState s, Card card)
		where T : GameComponent => s.AbilitiesOf<T>(card).Any();

	/// <summary>
	/// THE list of a card's activated abilities — activation indexes into it: its own, then those an
	/// Aura on it grants (Splinter Twin). Empty once it has lost all abilities, granted ones too.
	/// </summary>
	public static IEnumerable<ActivatedAbilityComponent> ActivatedAbilitiesOf(
		this GameState s,
		Card card
	)
	{
		var board = BoardOf(s);
		if (board.Granted is null)
			return s.AbilitiesOf<ActivatedAbilityComponent>(card); // nearly always: no Aura grants one
		if (Lost(board, card))
			return [];
		var own = s.AbilitiesOf<ActivatedAbilityComponent>(card);
		return board.Granted.TryGetValue((card.Id, card.Incarnation), out var more)
			? own.Concat(more)
			: own;
	}

	/// <summary>
	/// THE subtypes of a permanent (CR 205.1b) — every rule asks here, never Card.Subtypes, or Federation
	/// counts a Doctor's old types. I'm a Doctor, Not a . . .: "becomes a Doctor (It loses all other
	/// creature types.)" until end of turn; De-evolve: "is a Salamander" while the Aura is on it — that one
	/// creature type, then the subtypes that aren't creature types. ponytail: no timestamps — an
	/// until-end-of-turn type wins over an Aura's whichever came first.
	/// </summary>
	public static ImmutableArray<string> Subtypes(this GameState s, Card card)
	{
		ImmutableArray<string>? set = null;
		List<string>? also = null;
		foreach (var component in card.Components) // a loop, not LINQ: every subtype filter asks
			if (component is UntilEndOfTurn u)
			{
				if (u.BecomesCreatureTypes is { } types)
					set = types;
				if (u.AlsoCreatureType is { } more)
					(also ??= []).Add(more); // Worf's "Warriors in addition to their other types"
			}
		if (
			set is null
			&& BoardOf(s).TypeSet is { } typed
			&& s.IsOnBattlefield(card.Id)
			&& typed.TryGetValue((card.Id, card.Incarnation), out var one)
		)
			set = ImmutableArray.Create(one);
		var subtypes =
			set is not { } setTo || !card.Is(CardType.Creature)
				? card.Subtypes
				: [.. setTo, .. card.Subtypes.Where(t => !setTo.Contains(t) && !IsCreatureType(t))];
		return also is null || !card.Is(CardType.Creature)
			? subtypes
			: [.. subtypes, .. also.Where(t => !subtypes.Contains(t)).Distinct()];
	}

	/// <summary>
	/// Has it this subtype? Changeling (Dominion Infiltration's tokens): "It's every creature type." — asked
	/// one type at a time, since no list holds every creature type. Read from the card's own keywords, not
	/// s.Has: a subtype filter scopes statics, and s.Has reads the statics (it would loop) — and changeling
	/// sets the types in layer 4, before "loses all abilities" (layer 6) could take it away. Behold keeps
	/// Subtypes: it names planeswalker types, which IsCreatureType doesn't know aren't creature types.
	/// </summary>
	public static bool HasSubtype(this GameState s, Card card, string subtype) =>
		s.Subtypes(card).Contains(subtype)
		|| (card.Keywords.Contains("Changeling") && IsCreatureType(subtype));

	/// <summary>
	/// Whether a subtype is a creature type: anything but the land, artifact and enchantment types (CR
	/// 205.3g–h, j). ponytail: the ones printed on cards like these sets'; add one when a set brings it.
	/// </summary>
	public static bool IsCreatureType(string subtype) => !NotCreatureTypes.Contains(subtype);

	static readonly HashSet<string> NotCreatureTypes =
	[
		"Plains",
		"Island",
		"Swamp",
		"Mountain",
		"Forest",
		"Desert",
		"Gate",
		"Planet",
		"Cave",
		"Town",
		"Sphere",
		"Locus",
		"Lair",
		"Mine",
		"Power-Plant",
		"Tower",
		"Urza's",
		"Equipment",
		"Vehicle",
		"Spacecraft",
		"Clue",
		"Food",
		"Treasure",
		"Blood",
		"Gold",
		"Powerstone",
		"Fortification",
		"Incubator",
		"Map",
		"Junk",
		"Aura",
		"Saga",
		"Class",
		"Room",
		"Case",
		"Shrine",
		"Curse",
		"Cartouche",
		"Rune",
		"Background",
		"Role",
		"Shard",
	];

	/// <summary>
	/// "Loses all abilities" — until end of turn (Flourishing Grapple) or while an Aura says so
	/// (Infinite Coursework). ponytail: no timestamps (CR 613.7) — it loses every ability, even one
	/// granted later, and a P/T it sets for itself (layer 7a) with them. Order them if a card cares.
	/// </summary>
	public static bool HasLostAllAbilities(this GameState s, Card card) => Lost(BoardOf(s), card);

	static bool Lost(Board board, Card card)
	{
		if (board.Stripped.Count > 0 && board.Stripped.Contains((card.Id, card.Incarnation)))
			return true;
		foreach (var component in card.Components) // a loop, not LINQ: every ability read asks
			if (component is UntilEndOfTurn { LosesAllAbilities: true })
				return true;
		return false;
	}

	/// <summary>
	/// "Destroy" and lethal damage don't destroy it (CR 702.12b) — 0 toughness still does. The ONE
	/// answer: the Destroy effect and the state-based check both ask here, so they can't disagree
	/// (MtgCore keeps its two destroy actions in step by the same rule).
	/// </summary>
	public static bool IsIndestructible(this GameState s, Card card) =>
		s.Has(card, "Indestructible");

	/// <summary>
	/// What the battlefield says, found once per (immutable) state — this sits under the AI's
	/// simulations: every static that applies (from a permanent that still has its abilities), and
	/// the permanents an Aura strips of theirs. Never mutated once built.
	/// </summary>
	sealed record Board(
		(Card Source, StaticEffectComponent Effect)[] Statics,
		HashSet<(int, int)> Stripped
	)
	{
		/// <summary>Activated abilities Auras grant, by host (id, incarnation); null when none do.</summary>
		public Dictionary<(int, int), List<ActivatedAbilityComponent>>? Granted { get; init; }

		/// <summary>De-evolve's creature type, by host (id, incarnation); null when no Aura sets one.</summary>
		public Dictionary<(int, int), string>? TypeSet { get; init; }

		/// <summary>An Aura, or anything attached, on the battlefield — what <c>Attachments.Detach</c> can act on (AI-22).</summary>
		public bool Attachments { get; init; }

		/// <summary>A type change on the battlefield, stamped or that can stamp — what <c>Attachments.Animations</c> can act on (AI-22).</summary>
		public bool TypeChanges { get; init; }
	}

	static readonly HashSet<(int, int)> NoneStripped = [];

	// The last board each thread built: the questions about one state come together (the state-based
	// check asks every creature's toughness), so one entry finds nearly all of them. A global
	// ConditionalWeakTable did this before, and cost more than it saved: its lock contended across
	// the search's threads, and every collection scanned a weak handle per state (story AI-7).
	// ponytail: one entry — a caller asking about two states in turn rebuilds the board every time
	// (keep two entries if one ever must), and each thread keeps its last state alive until the next.
	[ThreadStatic]
	static (GameState? State, Board? Board) lastBoard;

	/// <summary>
	/// Whether the state-based check's attachment passes can find anything (AI-22): an Aura or an attached
	/// card; a type change stamped (<see cref="Animated"/>) or one that can stamp. Read from the board already built.
	/// </summary>
	public static (bool Attachments, bool TypeChanges) AttachmentWork(GameState s) =>
		BoardOf(s) is var board ? (board.Attachments, board.TypeChanges) : default;

	static Board BoardOf(GameState s)
	{
		if (!ReferenceEquals(lastBoard.State, s))
			lastBoard = (s, BuildBoard(s));
		return lastBoard.Board!;
	}

	static Board BuildBoard(GameState state)
	{
		// One pass over the battlefield: it runs for every new state the AI simulates.
		List<(Card, StaticEffectComponent)> statics = [];
		HashSet<(int, int)>? stripped = null;
		List<(Card Aura, GrantsActivatedAbility Grant)>? grants = null;
		Dictionary<(int, int), string>? typeSet = null;
		bool attachments = false,
			typeChanges = false;
		foreach (var p in new[] { state.Player1Id(), state.Player2Id() })
		foreach (
			var c in state
				.CardsIn(p, ZoneType.Battlefield)
				.Concat(state.CardsIn(p, ZoneType.Command))
		) // emblems too
		{
			attachments |= c.AttachedToId != 0;
			foreach (var component in c.Components)
			{
				// AI-22: what the state-based check's attachment passes can act on, noted in the same pass
				attachments |= component is AuraComponent;
				typeChanges |=
					component is Animated or BecomesType
					|| component is UntilEndOfTurn { Gains.Count: > 0 } u
						&& u.Gains.Any(g => g is BecomesType);
				if (component is StaticEffectComponent effect)
					statics.Add((c, effect));
				else if (component is EnchantedLosesAllAbilities)
					(stripped ??= []).Add((c.AttachedToId, c.AttachedToIncarnation));
				else if (component is GrantsActivatedAbility grant && c.AttachedToId != 0)
					(grants ??= []).Add((c, grant));
				else if (component is EnchantedIsCreatureType type && c.AttachedToId != 0)
					(typeSet ??= [])[(c.AttachedToId, c.AttachedToIncarnation)] = type.Subtype;
			}
		}
		var lost = new Board([], stripped ?? NoneStripped);
		// An Aura that lost its abilities sets no type either.
		if (typeSet is not null)
			foreach (
				var c in state
					.CardsIn(state.Player1Id(), ZoneType.Battlefield)
					.Concat(state.CardsIn(state.Player2Id(), ZoneType.Battlefield))
			)
				if (c.HasComponent<EnchantedIsCreatureType>() && Lost(lost, c))
					typeSet.Remove((c.AttachedToId, c.AttachedToIncarnation));
		// An Aura that lost its abilities grants none either.
		var live = grants?.Where(g => !Lost(lost, g.Aura)).ToList();
		return lost with
		{
			Attachments = attachments,
			TypeChanges = typeChanges,
			TypeSet = typeSet,
			Granted = live is null or []
				? null
				: live.GroupBy(g => (g.Aura.AttachedToId, g.Aura.AttachedToIncarnation))
					.ToDictionary(g => g.Key, g => g.Select(x => x.Grant.Ability).ToList()),
			// A permanent that lost its abilities has no statics either.
			Statics = [.. statics.Where(x => !Lost(lost, x.Item1))],
		};
	}

	// The statics below are plain loops, no iterator or LINQ: every rule and every simulated step reads
	// power, toughness and keywords (story AI-7.3c — `IsIndestructible` allocated 736 B a call).

	/// <summary>Statics apply to permanents on the battlefield — not to a card in hand or a dead card's last-known copy.</summary>
	static bool OnTheBattlefieldNow(GameState s, Board board, Card card) =>
		board.Statics.Length > 0
		&& s.IsOnBattlefield(card.Id)
		&& s.GetCard(card.Id).Incarnation == card.Incarnation;

	/// <summary>What the statics that apply add to its power (or toughness).</summary>
	static int StaticPT(GameState s, Card card, bool power)
	{
		var board = BoardOf(s);
		if (!OnTheBattlefieldNow(s, board, card))
			return 0;
		var sum = 0;
		foreach (var (source, effect) in board.Statics)
			if (effect.ChangesPowerOrToughness && AppliesTo(s, source, effect, card))
				sum += (power ? effect.Power : effect.Toughness).Value(s, ContextOf(source));
		return sum;
	}

	/// <summary>Whether a static that applies grants it <paramref name="keyword"/>.</summary>
	static bool Grants(GameState s, Board board, Card card, string keyword)
	{
		if (!OnTheBattlefieldNow(s, board, card))
			return false;
		foreach (var (source, effect) in board.Statics)
			if (effect.Keyword == keyword && AppliesTo(s, source, effect, card))
				return true;
		return false;
	}

	static ImmutableDictionary<string, object> ContextOf(Card source) =>
		ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, source.ControllerId)
			.Add(ContextKeys.Source, source.Id);

	/// <summary>
	/// A keyword static's scope may read real power and toughness (CR 613) — Tetsuko — because the
	/// P/T pass never asks about keywords; StaticEffectComponent refuses the scopes that would loop.
	/// `If` must not read keywords (nor, on a P/T static, P/T) — threshold and "this turn" counts only.
	/// </summary>
	static bool AppliesTo(GameState s, Card source, StaticEffectComponent effect, Card card) =>
		effect.Affects.Matches(s, card, new(source.ControllerId, source.Id))
		&& (effect.If is not { } condition || condition.Holds(s, Conditions.Of(source)));
}
