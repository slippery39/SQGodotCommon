using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore.Cards;

[Flags]
public enum CardType
{
	None = 0,
	Artifact = 1 << 0,
	Battle = 1 << 1,
	Creature = 1 << 2,
	Enchantment = 1 << 3,
	Instant = 1 << 4,
	Kindred = 1 << 5,
	Land = 1 << 6,
	Planeswalker = 1 << 7,
	Sorcery = 1 << 8,
}

[Flags]
public enum Supertype
{
	None = 0,
	Basic = 1 << 0,
	Legendary = 1 << 1,
	Snow = 1 << 2,
	World = 1 << 3,
}

/// <summary>
/// Who knows a card in a hidden zone, relative to its owner — a library or hand card is always in
/// its owner's zone, so "owner" and "opponent" say it for both players (story AI-3).
/// </summary>
[Flags]
public enum Knowers
{
	None = 0,
	Owner = 1 << 0,
	Opponent = 1 << 1,
	Everyone = Owner | Opponent,
}

/// <summary>
/// A card in any zone. Printed characteristics are parsed from <see cref="CardData"/> by
/// <see cref="CardFactory"/> — never typed in by hand. GameObject.Name is the Scryfall name,
/// the key back to the printed data.
/// </summary>
public record Card : GameObject
{
	/// <summary>
	/// The set of the printing it shows ("TRK") — its art, as Arena lets a deck choose a printing.
	/// Never rules: a reprint plays the same in every set (MC-5.5). Null for tokens and test cards.
	/// </summary>
	public string? Set { get; init; }

	public int OwnerId { get; init; }
	public int ControllerId { get; init; }
	public CardType Types { get; init; }
	public Supertype Supertypes { get; init; }
	public ImmutableArray<string> Subtypes { get; init; } = ImmutableArray<string>.Empty;
	public ManaCost ManaCost { get; init; } = ManaCost.None;

	/// <summary>Scryfall's printed keywords ("Flash", "Flying"). Rules read these; nothing retypes them.</summary>
	public ImmutableArray<string> Keywords { get; init; } = ImmutableArray<string>.Empty;
	public ImmutableArray<ManaColor> Colors { get; init; } = ImmutableArray<ManaColor>.Empty;
	public int BasePower { get; init; }
	public int BaseToughness { get; init; }
	public bool IsToken { get; init; }

	/// <summary>A copy of a spell (a prepared copy) — not a card; it ceases to exist off the stack (CR 704.5e).</summary>
	public bool IsCopy { get; init; }

	/// <summary>
	/// Bumped by every zone change: ids survive a zone change but the object does not (CR 400.7).
	/// "Is this still the same permanent?" compares this, never the id alone.
	/// </summary>
	public int Incarnation { get; init; }

	/// <summary>The loyalty printed in the corner; a planeswalker enters with that many counters (CR 306.5b).</summary>
	public int PrintedLoyalty { get; init; }

	/// <summary>
	/// The card's OWN power and toughness: printed (or a base-setting effect) + counters + until-end-
	/// of-turn effects. NOT what rules read — s.Power(card) adds every static on the battlefield.
	/// </summary>
	public int OwnPower =>
		(CurrentBase?.Power ?? BasePower)
		+ PlusOneCounters
		- MinusOneCounters
		+ Pumped(power: true);
	public int OwnToughness =>
		(CurrentBase?.Toughness ?? BaseToughness)
		+ PlusOneCounters
		- MinusOneCounters
		+ Pumped(power: false);

	// Loops, not LINQ, here and in HasOwn: LINQ boxes the ImmutableArray, and these are read at every
	// step the search simulates (story AI-7.3c).
	int Pumped(bool power)
	{
		var sum = 0;
		foreach (var component in Components)
			if (component is UntilEndOfTurn u)
				sum += power ? u.Power : u.Toughness;
		return sum;
	}

	/// <summary>The latest "has base power and toughness P/T" effect, if any (CR 613.4b, timestamp order).</summary>
	(int Power, int Toughness)? CurrentBase
	{
		get
		{
			(int, int)? latest = null;
			foreach (var component in Components)
				if (component is UntilEndOfTurn { SetBase: { } set })
					latest = set;
			return latest;
		}
	}

	/// <summary>
	/// Who knows this card while it is in a library or a hand — in a library, who knows its POSITION;
	/// in a hand, whether the opponent has seen it (the owner always knows its hand). For the search's
	/// "shuffle only what isn't known" (story AI-3). <see cref="MagicState.MoveCard"/> sets it on every
	/// zone change; surveil and "reveal" add to it; a shuffle clears the library's.
	/// </summary>
	public Knowers KnownTo { get; init; }

	/// <summary>
	/// Vulcan Mind-Meld: a card exiled face down this player hasn't seen — a front end draws its back (CR 406.3). A
	/// face-down permanent (Abhorrent Oculus' manifest dread) is hidden from all but its controller (CR 708.5).
	/// </summary>
	public bool IsHiddenFrom(int playerId) =>
		HasComponent<FaceDown>()
			? playerId != ControllerId
			: HasComponent<Effects.ExiledFaceDown>() && !IsKnownTo(playerId);

	/// <summary>This player has seen the card where it is (V'Ger's look at a hand, a reveal) — the table shows it face up (TBL-6).</summary>
	public bool IsKnownTo(int playerId) =>
		(KnownTo & (playerId == OwnerId ? Knowers.Owner : Knowers.Opponent)) != 0;

	// ===== Permanent-only state — wiped by Fresh() whenever the card changes zones =====

	public bool IsTapped { get; init; }
	public int PlusOneCounters { get; init; }

	/// <summary>Loyalty counters on a planeswalker. Damage removes them (CR 120.3c); at 0 it dies (CR 704.5i).</summary>
	public int Loyalty { get; init; }

	/// <summary>
	/// The spell this permanent resolved from — what was decided as it was cast, carried onto it (EnterBattlefield):
	/// its X (CR 107.3m), Thieving Skydiver's "if it was kicked", evoke's alternative cost (Subtlety), squad's payments
	/// (Sicarian Infiltrator). Null when it wasn't cast; gone as it leaves.
	/// </summary>
	public Actions.OnStackComponent? CastAs { get; init; }

	/// <summary>The X paid to cast this permanent — its "when this enters" abilities use it (CR 107.3m).</summary>
	public int CastX => CastAs?.X ?? 0;

	/// <summary>What was decided as it was cast — THE read: on the stack its OnStackComponent, a permanent the spell it was (CastAs).</summary>
	public Actions.OnStackComponent? AsCast => GetComponent<Actions.OnStackComponent>() ?? CastAs;

	/// <summary>Null Summoner: "if you cast it" — it entered by resolving as a spell.</summary>
	public bool WasCast { get; init; }

	/// <summary>Gardenize's charge counters — a resource; nothing but the card that holds them reads them.</summary>
	public int ChargeCounters { get; init; }

	/// <summary>Vanishing's time counters (Flesh Duplicate) — one removed each upkeep; the last sacrifices it.</summary>
	public int TimeCounters { get; init; }

	/// <summary>The TurnNumber a loyalty ability of this permanent was last activated — once per turn (CR 606.3).</summary>
	public int LoyaltyActivatedOnTurn { get; init; }
	public int Damage { get; init; }
	public bool DamagedByDeathtouch { get; init; }

	/// <summary>TurnNumber it came under its controller's control — summoning sickness reads it.</summary>
	public int ControlledSinceTurn { get; init; }

	/// <summary>TurnNumber it entered the battlefield — "target creature that entered this turn".</summary>
	public int EnteredOnTurn { get; init; }

	/// <summary>TurnNumber it was last declared as an attacker — Hexhaven's "creature that attacked this turn".</summary>
	public int AttackedOnTurn { get; init; }

	/// <summary>
	/// How many times it was declared as an attacker on turn AttackedOnTurn — an additional combat phase (Fear of
	/// Missing Out) makes it more than one: "attacks for the first time each turn" (AttackedEvent.Number).
	/// </summary>
	public int TimesAttacked { get; init; }

	/// <summary>-1/-1 counters — annihilated with +1/+1 counters by the state-based check (CR 704.5q).</summary>
	public int MinusOneCounters { get; init; }

	/// <summary>TurnNumber its "triggers only once each turn" ability last triggered.</summary>
	public int OncePerTurnTriggeredOn { get; init; }

	/// <summary>Kobayashi Maru Scenario: "choose one that hasn't been chosen" — bit i is mode i, chosen by this object (ModeChoice.NotChosenBefore).</summary>
	public int ModesChosen { get; init; }

	/// <summary>Captain Michael Burnham: how many times its ability resolved on turn ResolvedOnTurn (IfNthResolutionThisTurn).</summary>
	public int ResolvedTimes { get; init; }

	/// <summary>The turn ResolvedTimes counts.</summary>
	public int ResolvedOnTurn { get; init; }

	/// <summary>Prepare: its prepare spell's copy waits in exile (Rules/Prepare.cs).</summary>
	public bool IsPrepared { get; init; }

	/// <summary>An Aura or Equipment: the permanent it is attached to; 0 if none (Rules/Attachments.cs).</summary>
	public int AttachedToId { get; init; }

	/// <summary>Which object of that id — a host that left and came back is a new one (CR 400.7).</summary>
	public int AttachedToIncarnation { get; init; }

	/// <summary>"If a creature with a finality counter on it would die, exile it instead" — MagicState.MoveCard.</summary>
	public bool HasFinalityCounter { get; init; }

	/// <summary>"If a permanent with a stun counter would become untapped, remove one from it instead" — MagicState.Untap.</summary>
	public int StunCounters { get; init; }

	/// <summary>Ruric Thar, Magecrusher: "as long as they haven't dealt combat damage yet" — set by Rules.Damage.</summary>
	public bool HasDealtCombatDamage { get; init; }

	/// <summary>Its exhaust ability has been activated — never again for this object.</summary>
	public bool ExhaustUsed { get; init; }

	/// <summary>DOT-7 Repair Squad: "Activate only once each turn" — the TurnNumber it was last activated.</summary>
	public int ActivatedOncePerTurnOn { get; init; }

	/// <summary>
	/// Lightning Greaves' "Equip {0}": the TurnNumber a FREE ability of this object was last activated
	/// (ActivatedAbilityComponent.IsFree) — no rule reads it; the search is offered one such activation a turn
	/// (Evaluator.SearchMoves).
	/// </summary>
	public int FreeAbilityActivatedOn { get; init; }

	/// <summary>Zefram Cochrane: "Once during each of your turns, you may cast an artifact spell from the top of your library" — the TurnNumber this object let one be cast.</summary>
	public int PermissionUsedOn { get; init; }

	/// <summary>
	/// The counter kinds TRK added, by name — quest (Anthropological Mission), lore (Sagas), promotion
	/// (Beckett Mariner). The older kinds keep their own fields; MagicState.PutCounters is the one way in.
	/// </summary>
	public ImmutableDictionary<string, int> Counters { get; init; } =
		ImmutableDictionary<string, int>.Empty;

	public int CountersOf(string kind) =>
		kind switch
		{
			Effects.CounterKinds.PlusOne => PlusOneCounters,
			Effects.CounterKinds.MinusOne => MinusOneCounters,
			Effects.CounterKinds.Charge => ChargeCounters,
			Effects.CounterKinds.Stun => StunCounters,
			Effects.CounterKinds.Time => TimeCounters,
			Effects.CounterKinds.Loyalty => Loyalty,
			_ => Counters.GetValueOrDefault(kind),
		};

	/// <summary>Dominion Saboteur: "the same number and kinds of counters the copied permanent has" — every kind on it.</summary>
	public IEnumerable<(string Kind, int N)> AllCounters() =>
		Effects
			.CounterKinds.Fielded.Concat(Counters.Keys)
			.Select(kind => (kind, CountersOf(kind)))
			.Where(k => k.Item2 > 0);

	/// <summary>This card with exactly <paramref name="n"/> counters of a kind — written only by MagicState.PutCounters.</summary>
	public Card WithCounters(string kind, int n) =>
		kind switch
		{
			Effects.CounterKinds.PlusOne => this with { PlusOneCounters = n },
			Effects.CounterKinds.MinusOne => this with { MinusOneCounters = n },
			Effects.CounterKinds.Charge => this with { ChargeCounters = n },
			Effects.CounterKinds.Stun => this with { StunCounters = n },
			Effects.CounterKinds.Time => this with { TimeCounters = n },
			Effects.CounterKinds.Loyalty => this with { Loyalty = n },
			_ => this with
			{
				Counters = n == 0 ? Counters.Remove(kind) : Counters.SetItem(kind, n),
			},
		};

	/// <summary>Dominion Supervisor: "creatures you control with counters on them" — a counter of any kind.</summary>
	public bool HasAnyCounter =>
		PlusOneCounters > 0
		|| MinusOneCounters > 0
		|| ChargeCounters > 0
		|| TimeCounters > 0
		|| StunCounters > 0
		|| Loyalty > 0
		|| HasFinalityCounter
		|| Counters.Values.Any(n => n > 0);

	/// <summary>The player OR planeswalker it is attacking; 0 when not attacking. Never assume a player.</summary>
	public int AttackingId { get; init; }

	/// <summary>The attacker it blocks; 0 when not blocking.</summary>
	public int BlockingId { get; init; }

	/// <summary>Stays true after its blockers leave — a blocked creature deals no damage without trample (CR 509.1h).</summary>
	public bool IsBlocked { get; init; }

	/// <summary>
	/// CR 400.7: a card that changes zones is a new object with no memory of its previous one.
	/// The ONLY reset — every zone change goes through MagicState.MoveCard, which calls this.
	/// </summary>
	public Card Fresh() =>
		WithoutAdventure()
			.WithoutAnimation()
			.WithoutCopyEffect()
			.TurnedFaceUp()
			.FrontFaceUp()
			.Reset();

	Card Reset() =>
		this with
		{
			Incarnation = Incarnation + 1,
			ControllerId = OwnerId, // off the battlefield and stack it is its owner's (CR 108.4a); entering or casting sets it
			IsTapped = false,
			PlusOneCounters = 0,
			MinusOneCounters = 0,
			Loyalty = 0,
			CastAs = null,
			WasCast = false,
			ChargeCounters = 0,
			TimeCounters = 0,
			LoyaltyActivatedOnTurn = 0,
			EnteredOnTurn = 0,
			AttackedOnTurn = 0,
			TimesAttacked = 0,
			OncePerTurnTriggeredOn = 0,
			ModesChosen = 0,
			ResolvedTimes = 0,
			ResolvedOnTurn = 0,
			IsPrepared = false,
			AttachedToId = 0,
			AttachedToIncarnation = 0,
			HasFinalityCounter = false,
			StunCounters = 0,
			HasDealtCombatDamage = false,
			ExhaustUsed = false,
			ActivatedOncePerTurnOn = 0,
			FreeAbilityActivatedOn = 0,
			PermissionUsedOn = 0,
			Counters = ImmutableDictionary<string, int>.Empty,
			Damage = 0,
			DamagedByDeathtouch = false,
			ControlledSinceTurn = 0,
			AttackingId = 0,
			BlockingId = 0,
			IsBlocked = false,
			Components = Components.RemoveAll(IsStatus),
		};

	/// <summary>
	/// What this object was given or decided here, not what it is: gone with a zone change, and never
	/// copied (CR 707.2) — a copy of an exiling permanent doesn't own its exile, nor a pumped one its pump.
	/// </summary>
	public static bool IsStatus(GameComponent c) =>
		c
			is Actions.OnStackComponent
				or UntilEndOfTurn
				or Effects.ExiledUntilLeaves
				or Effects.GrantedFlashback
				or Effects.PlayableFromExile
				or Effects.ExiledFaceDown
				or Effects.ExiledBy
				or Effects.ChosenColor
				or Effects.ChosenType
				or Effects.Exerted
				or Effects.ChosenNumber
				or Effects.ClassLevel
				or Effects.ChosenName
				or Effects.ChosenPlayer
				or CopyUntilEndOfTurn
				or EnteredAsCopy
				or FaceDown
				or CastAsAdventure
				or Transformed
				or Actions.Spliced
				or Effects.DelayedTrigger
				or Animated
				or Effects.TriggersAdditionalTime { UntilUntapped: true }
				or Effects.EnchantedLosesAllAbilities { Object: not null }
				or Effects.TapsForTwoColorlessWhileExiled;

	/// <summary>
	/// CR 707.2: this object with <paramref name="source"/>'s copiable values — name, cost, types,
	/// colours, P/T, keywords and abilities (themselves already copied, if it is a copy: CR 707.3).
	/// Its own status stays: counters, damage, tapped, what it was given. The ONE place a copy is made.
	/// </summary>
	public Card WithCopiableValuesOf(Card source) => WithValuesOf(source.WithoutAnimation()); // an Aura's type change isn't copiable

	Card WithValuesOf(Card source) =>
		this with
		{
			Name = source.Name,
			Set = source.Set, // a copy shows the printing it copies
			Types = source.Types,
			Supertypes = source.Supertypes,
			Subtypes = source.Subtypes,
			ManaCost = source.ManaCost,
			Keywords = source.Keywords,
			Colors = source.Colors,
			BasePower = source.BasePower,
			BaseToughness = source.BaseToughness,
			PrintedLoyalty = source.PrintedLoyalty,
			Components =
			[
				.. source.Components.Where(c => !IsStatus(c)),
				.. Components.Where(IsStatus),
			],
		};

	/// <summary>
	/// Its types changed by a <see cref="Effects.BecomesType"/> (Puppet Crafting, Mogis): types added and
	/// removed, a subtype, a base P/T — and what it was, recorded to put back.
	/// </summary>
	public Card AnimatedBy(Card source, Effects.BecomesType becomes)
	{
		var added = becomes.Subtype is { } sub && !Subtypes.Contains(sub) ? sub : null;
		return (Card)
			(
				this with
				{
					Types = (Types | becomes.Add) & ~becomes.Remove,
					Subtypes = added is null ? Subtypes : Subtypes.Add(added),
					BasePower = becomes.Power ?? BasePower,
					BaseToughness = becomes.Toughness ?? BaseToughness,
				}
			).WithComponent(
				new Animated(source.Id, source.Incarnation, Types, added, BasePower, BaseToughness)
			);
	}

	/// <summary>
	/// Its types changed by an effect with no duration, so until it leaves — Assimilate: "It's a Borg artifact
	/// creature"; Enduring Innocence returned: "It's an enchantment. (It's not a creature.)" What it was is kept on
	/// an Animated with no source: the state check leaves it, WithoutAnimation puts it back (leaving, a copy).
	/// </summary>
	/// <param name="endsAtCleanup">Crew (Esika's Chariot): "becomes an artifact creature until end of turn" — the
	/// cleanup step puts it back (Combat.CleanupPermanents), as leaving does.</param>
	public Card WithTypesUntilItLeaves(CardType types, bool endsAtCleanup = false) =>
		(Card)
			(this with { Types = types }).WithComponent(
				new Animated(0, 0, Types, null, BasePower, BaseToughness)
				{
					EndsAtCleanup = endsAtCleanup,
				}
			);

	/// <summary>Back to what it was before a type change — its source left or stopped, or it leaves (CR 400.7).</summary>
	public Card WithoutAnimation() =>
		GetComponent<Animated>() is { } a
			? (Card)
				(
					this with
					{
						Types = a.Types,
						Subtypes = a.AddedSubtype is null
							? Subtypes
							: Subtypes.Remove(a.AddedSubtype),
						BasePower = a.BasePower,
						BaseToughness = a.BaseToughness,
					}
				).WithoutComponents<Animated>()
			: this;

	/// <summary>Hall of Echoes back to itself — at cleanup, or as it leaves (CR 400.7).</summary>
	/// <summary>Leaving the battlefield: every copy effect undone — until end of turn first, then entered-as.</summary>
	public Card WithoutCopyEffect() =>
		WithoutCopyUntilEndOfTurn() is var card && card.GetComponent<EnteredAsCopy>() is { } entered
			? (Card)card.WithCopiableValuesOf(entered.Original).WithoutComponents<EnteredAsCopy>()
			: card;

	/// <summary>
	/// Abhorrent Oculus' manifest dread: "Put one onto the battlefield face down as a 2/2 creature" — no name, types
	/// but creature, cost, colour or abilities (CR 708.2): a blank 2/2's copiable values, what it was kept on
	/// <see cref="FaceDown"/>. Its status (tapped, counters, damage) stays its own, as a copy's.
	/// </summary>
	public Card TurnedFaceDown() =>
		(Card)WithCopiableValuesOf(FaceDownModel).WithComponent(new FaceDown(this));

	static readonly Card FaceDownModel =
		new()
		{
			Name = "",
			Types = CardType.Creature,
			BasePower = 2,
			BaseToughness = 2,
		};

	/// <summary>
	/// "Turn it face up" (TurnFaceUpAction) — or it leaves the battlefield, revealed (CR 708.9, Fresh): what it was, the
	/// same object (CR 708.8), its status kept. A type change on the 2/2 is undone first: it remembers the 2/2, and the
	/// state check stamps the card afresh (Attachments.Check).
	/// </summary>
	public Card TurnedFaceUp() =>
		GetComponent<FaceDown>() is { } down
			? (Card)
				WithoutAnimation().WithCopiableValuesOf(down.Original).WithoutComponents<FaceDown>()
			: this;

	/// <summary>
	/// Brazen Borrower // Petty Theft: cast as its Adventure (CR 715.3) — while it is cast and on the stack it has only
	/// the Adventure's characteristics (Petty Theft, {1}{U}, Instant — Adventure, its text): the face's values as a
	/// copy's, the card it is kept on <see cref="CastAsAdventure"/>, as a face-down card's (TurnedFaceDown). Anywhere
	/// else it is the creature: leaving the stack undoes it (Fresh, WithoutAdventure).
	/// </summary>
	public Card AsAdventure() =>
		GetComponent<Effects.AdventureComponent>() is { } adventure
			? (Card)
				(
					WithCopiableValuesOf(
						CardFactory.Create(
							new CardDefinition(CardData.OfFace(adventure.Face), [adventure.Text]),
							OwnerId
						)
					) with
					{
						Set = Set,
					}
				).WithComponent(new CastAsAdventure(this))
			: this;

	/// <summary>
	/// Fable of the Mirror-Breaker, chapter III: "return it to the battlefield transformed"; Etali, Primal Conqueror:
	/// "{9}{G/P}: Transform Etali." (CR 701.28, 712) — the same object with its back face's characteristics: the face's
	/// values as a copy's (its keywords and abilities from <see cref="Effects.TransformsInto"/>), the front kept on
	/// <see cref="Transformed"/>, as a face-down card's (TurnedFaceDown). Transformed again, it is the front. Only a
	/// double-faced card transforms, or a token copy of one (a transforming token) — never a permanent that is a copy of
	/// one by a copy effect (Phyrexian Metamorph, Hall of Echoes: CR 701.28c), nor one under a copy effect taken after it
	/// transformed. Its colours are the face's (Scryfall's colour indicator: Etali, Primal Sickness is red and green).
	/// ponytail: the back face has mana value 0, where CR 712.8e reads the front's — Card.ManaValue reading
	/// Transformed.Original's when a card asks.
	/// </summary>
	public Card Transform() =>
		HasComponent<EnteredAsCopy>()
		|| HasComponent<CopyUntilEndOfTurn>()
		// a modal DFC never transforms (CR 701.28d) — either face up: the back keeps the front on Transformed
		|| (GetComponent<Transformed>()?.Original ?? this).GetComponent<Effects.TransformsInto>()
			is { Modal: true }
			? this
		: GetComponent<Transformed>() is { } ? FrontFaceUp()
		: GetComponent<Effects.TransformsInto>() is { } ? BackFaceUp()
		: this;

	/// <summary>
	/// Sink into Stupor // Soporific Springs: the land a modal double-faced card is played as (PlayLandAction.BackFace) —
	/// its back face up — or null when it has no modal back face.
	/// </summary>
	public Card? ModalBackFace() =>
		GetComponent<Effects.TransformsInto>() is { Modal: true } ? BackFaceUp() : null;

	/// <summary>
	/// Its back face up (CR 712.8) — transformed (Transform), entering transformed (EnterBattlefield), or a modal DFC played
	/// as its back face: the face's values as a copy's, the front kept on <see cref="Transformed"/>.
	/// ponytail: a modal DFC returned "transformed" (MoveTo.Transformed) enters back face up, where CR 712.14a has it front
	/// face up — none is; ask Modal in MoveTo when a card returns one (DesignNotes).
	/// </summary>
	public Card BackFaceUp() =>
		!HasComponent<Transformed>() && GetComponent<Effects.TransformsInto>() is { } back
			? (Card)
				(
					WithCopiableValuesOf(
						CardFactory.Create(
							new CardDefinition(
								CardData.OfFace(back.Face) with
								{
									Keywords = [.. back.Keywords],
								},
								back.Abilities
							),
							OwnerId
						)
					) with
					{
						Set = Set,
					}
				).WithComponent(new Transformed(this))
			: this;

	/// <summary>Its front face up again — transformed back, or as it leaves the battlefield (CR 712.14, Fresh).</summary>
	public Card FrontFaceUp() =>
		GetComponent<Transformed>() is { } front
			? (Card)WithCopiableValuesOf(front.Original).WithoutComponents<Transformed>()
			: this;

	/// <summary>The adventurer card again (CR 715.4) — as it leaves the stack, resolved or not (Fresh).</summary>
	public Card WithoutAdventure() =>
		GetComponent<CastAsAdventure>() is { } cast
			? (Card)WithCopiableValuesOf(cast.Original).WithoutComponents<CastAsAdventure>()
			: this;

	/// <summary>Cleanup: only Hall of Echoes' "until end of turn" copy ends — a Flesh Duplicate stays one.</summary>
	public Card WithoutCopyUntilEndOfTurn() =>
		GetComponent<CopyUntilEndOfTurn>() is { } copy
			? (Card)WithCopiableValuesOf(copy.Original).WithoutComponents<CopyUntilEndOfTurn>()
			: this;

	/// <summary>
	/// Printed keywords plus any granted until end of turn — and a keyword counter (CR 122.1b, Drivnod's
	/// indestructible counter): any counter named as the keyword is, lower case.
	/// </summary>
	public bool HasOwn(string keyword)
	{
		if (Keywords.Contains(keyword))
			return true;
		if (HasKeywordCounter(keyword))
			return true;
		foreach (var component in Components)
			if (component is UntilEndOfTurn u && u.Keyword == keyword)
				return true;
		return false;
	}

	/// <summary>A keyword counter (CR 122.1b, Drivnod's indestructible counter): a counter named as the keyword, any case.</summary>
	public bool HasKeywordCounter(string keyword)
	{
		foreach (var (kind, n) in Counters) // no allocation: this sits under s.Has, under the AI
			if (n > 0 && string.Equals(kind, keyword, StringComparison.OrdinalIgnoreCase))
				return true;
		return false;
	}

	/// <summary>Its types — and a Spacecraft at its last station threshold is a creature (StationComponent).</summary>
	public bool Is(CardType type) =>
		(Types & type) != 0
		|| (
			ChargeCounters > 0
			&& (type & CardType.Creature) != 0
			&& GetComponent<Effects.StationComponent>() is { } station
			&& ChargeCounters >= station.CreatureAt
		);

	/// <summary>Its mana value — on the stack an X in its mana cost counts (CR 202.3e); anywhere else X is 0, and an X
	/// that is no mana (Toxic Deluge's "pay X life") never.</summary>
	public int ManaValueNow =>
		ManaCost.ManaValue
		+ (
			ManaCost.HasX
				? ManaCost.XCount * (GetComponent<Actions.OnStackComponent>()?.X ?? 0) // {X}{X}: twice X (CR 202.3e)
				: 0
		);

	public bool Is(Supertype supertype) => (Supertypes & supertype) != 0;
}

/// <summary>
/// "Gets +P/+T and gains Keyword until end of turn" on a permanent. Removed in cleanup (CR 514.2)
/// and by any zone change.
/// </summary>
public record UntilEndOfTurn(int Power = 0, int Toughness = 0, string? Keyword = null)
	: GameComponent
{
	/// <summary>
	/// "Until your next turn": it lasts until this player's next turn begins (its untap step), not
	/// past this cleanup. 0 is "until end of turn".
	/// </summary>
	public int UntilTurnOf { get; init; }

	/// <summary>UntilTurnOf for an effect with no duration (Seven of Nine's base 7/9): no player's turn ends it — only leaving the battlefield.</summary>
	public const int Never = -1;

	/// <summary>"Has base power and toughness P/T" - replaces the printed numbers, under counters and pumps.</summary>
	public (int Power, int Toughness)? SetBase { get; init; }

	/// <summary>Lyra: "Until end of turn, whenever [this] deals combat damage to a player, …" — Collect reads it.</summary>
	public Effects.TriggeredAbilityComponent? Trigger { get; init; }

	/// <summary>
	/// Essence Burn: "if that permanent would die this turn, exile it instead" — the zones it would go to that exile it
	/// instead (From the Catacombs: anywhere else) — MoveCard reads it.
	/// </summary>
	public ImmutableArray<ZoneType> ExileInsteadOf { get; init; } = [];

	/// <summary>Flourishing Grapple: "loses all abilities until end of turn" — Characteristics.HasLostAllAbilities.</summary>
	public bool LosesAllAbilities { get; init; }

	/// <summary>
	/// I'm a Doctor, Not a . . .: "becomes a Doctor (It loses all other creature types.)"; Nog, Ambitious
	/// Cadet: "becomes a Ferengi Scout" — Characteristics.Subtypes.
	/// </summary>
	public ImmutableArray<string>? BecomesCreatureTypes { get; init; }

	/// <summary>
	/// Nog, Ambitious Cadet: "gains 'You may play lands from your graveyard' and 'You may play an additional
	/// land on each of your turns.'"; Q, Stacking the Deck: an exiled card's gained "Exile a card from your
	/// hand: …" — abilities it has while this lasts, read by Characteristics.AbilitiesOf.
	/// </summary>
	public ImmutableList<GameComponent> Gains { get; init; } = [];

	/// <summary>Deep Space Scan: "You have no maximum hand size until your next turn." — on an object in your command zone; TurnStructure.MaxHandSizeOf.</summary>
	public bool NoMaximumHandSize { get; init; }

	/// <summary>Worf, Intelligence Contractor: "they become Warriors in addition to their other types" — Characteristics.Subtypes.</summary>
	public string? AlsoCreatureType { get; init; }

	/// <summary>Worf, Intelligence Contractor: "gain control of … until end of turn" — the player cleanup gives it back to (Combat.CleanupPermanents).</summary>
	public int ReturnControlTo { get; init; }

	/// <summary>
	/// Animate Dead: "it loses 'enchant creature card in a graveyard' and gains 'enchant creature put onto the
	/// battlefield with this Aura'"; Necromancy: "it becomes an Aura with 'enchant creature …'" — the enchant it has
	/// now, in place of any AuraComponent's (Rules.Attachments.EnchantOf). Set for good only (ModifyUntilEndOfTurn).
	/// </summary>
	public Targeting.TargetSpec? Enchant { get; init; }

	/// <summary>Celestial Colonnade: "becomes a 4/4 white and blue Elemental creature" — its colours SET while this lasts (Characteristics.ColorsOf).</summary>
	public ImmutableArray<ManaColor>? Colors { get; init; }
}

/// <summary>
/// Hall of Echoes: "becomes a copy of [it] until end of turn" — what it was before, restored in
/// cleanup or on leaving the battlefield (Card.WithoutCopyEffect).
/// </summary>
public record CopyUntilEndOfTurn(Card Original) : GameComponent;

/// <summary>
/// Flesh Duplicate: it entered as a copy — what it was, restored as it leaves the battlefield
/// (Card.WithoutCopyEffect), never in cleanup.
/// </summary>
public record EnteredAsCopy(Card Original) : GameComponent;

/// <summary>
/// Abhorrent Oculus' manifest dread: "Put one onto the battlefield face down as a 2/2 creature … Turn it face up any
/// time for its mana cost if it's a creature card." — the card it is, kept while it is a face-down 2/2
/// (Card.TurnedFaceDown). Status: a copy of the 2/2 is no face-down card, and leaving turns it face up.
/// ponytail: the card is in the state, so the search knows an opponent's face-down card — Hidden.Sample would put a
/// guess in its place when the AI should play fair against it (DesignNotes).
/// </summary>
public record FaceDown(Card Original) : GameComponent;

/// <summary>
/// Brazen Borrower // Petty Theft: the adventurer card cast as its Adventure (Card.AsAdventure) — kept while it is
/// Petty Theft, "Return target nonland permanent an opponent controls to its owner's hand." Status: a copy of Petty
/// Theft is no adventurer card, and leaving the stack makes it the creature again.
/// </summary>
public record CastAsAdventure(Card Original) : GameComponent;

/// <summary>
/// Reflection of Kiki-Jiki: a double-faced card with its back face up (Card.Transform) — the card as its front was,
/// kept while it is transformed. Status: leaving the battlefield turns it front face up (CR 712.14).
/// </summary>
public record Transformed(Card Original) : GameComponent;

/// <summary>Its types changed by this source's BecomesType (this object of it); the rest is what it was, to put back. SourceId 0: by an effect with no duration, until it leaves (Assimilate).</summary>
public record Animated(
	int SourceId,
	int SourceIncarnation,
	CardType Types,
	string? AddedSubtype,
	int BasePower,
	int BaseToughness
) : GameComponent
{
	/// <summary>Crew (Esika's Chariot): "becomes an artifact creature until end of turn" — a sourceless stamp the cleanup step undoes.</summary>
	public bool EndsAtCleanup { get; init; }
}

/// <summary>
/// What goes in a deck: the printed data plus the hand-written ability components. A card with no
/// components plays as its printed types and cost only — a vanilla creature, an instant that does
/// nothing. That is correct for a vanilla card and silently wrong for any other. <see cref="Set"/> is
/// the printing it shows (<see cref="Card.Set"/>).
/// </summary>
public sealed record CardDefinition(
	CardData Data,
	ImmutableArray<GameComponent> Components,
	string? Set = null
)
{
	public static CardDefinition Vanilla(CardData data) => new(data, []);
}

public static class CardFactory
{
	public static Card Create(CardDefinition definition, int ownerId)
	{
		var data = definition.Data;
		// A multi-face card (FRA's "prepare") is its front face everywhere but the stack.
		var front = data.CardFaces?[0];
		var (types, supertypes, subtypes) = ParseTypeLine(front?.TypeLine ?? data.TypeLine);
		return new Card
		{
			Name = data.Name,
			Set = definition.Set,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = types,
			Supertypes = supertypes,
			Subtypes = subtypes,
			ManaCost = ManaCost.Parse(front?.ManaCost ?? data.ManaCost),
			BasePower = ParsePowerToughness(front?.Power ?? data.Power),
			BaseToughness = ParsePowerToughness(front?.Toughness ?? data.Toughness),
			PrintedLoyalty = ParsePowerToughness(front?.Loyalty ?? data.Loyalty),
			// a transforming card has no colours of its own in Scryfall's JSON: its front face's
			Colors =
			[
				.. (data.Colors ?? (front is null ? [] : CardData.OfFace(front).Colors!)).Select(
					ManaCost.ColorOf
				),
			],
			// Scryfall merges every face's keywords; keep the ones the front face's text names.
			Keywords = front is null
				? [.. data.Keywords]
				:
				[
					.. data.Keywords.Where(k =>
						front.OracleText?.Contains(k, StringComparison.OrdinalIgnoreCase) == true
					),
				],
			Components = definition.Components,
		};
	}

	/// <summary>
	/// ponytail: "*" and "1+*" read as their number part; the card's own ability must define the rest.
	/// Negative is real — Loot, the Anomaly is -2/4.
	/// </summary>
	static int ParsePowerToughness(string? text) =>
		int.TryParse(
			System.Text.RegularExpressions.Regex.Match(text ?? "", @"^-?\d+").Value,
			out var n
		)
			? n
			: 0;

	/// <summary>
	/// "Legendary Artifact Creature — Human Wizard". Throws on a word it does not know, so a set
	/// that introduces a new type fails at import time instead of producing a typeless card.
	/// </summary>
	public static (CardType, Supertype, ImmutableArray<string>) ParseTypeLine(string typeLine)
	{
		var parts = typeLine.Split(" — ", 2);
		var types = CardType.None;
		var supertypes = Supertype.None;

		foreach (var word in parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (Enum.TryParse<CardType>(word, out var type))
				types |= type;
			else if (Enum.TryParse<Supertype>(word, out var supertype))
				supertypes |= supertype;
			else
				throw new FormatException($"Unknown card type '{word}' in \"{typeLine}\"");
		}

		var subtypes =
			parts.Length > 1
				? parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray()
				: ImmutableArray<string>.Empty;
		return (types, supertypes, subtypes);
	}
}
