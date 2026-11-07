using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;

namespace MagicCore.Turns;

/// <summary>
/// A trigger event: its kind, the objects it is about — a permanent on the battlefield now, a dead
/// creature as it last was, a spell wherever it is — and the player who did it. A trigger's
/// Subject filter asks about the subjects; its By about the player (audit §10.5).
/// </summary>
public abstract record MagicEvent : GameEvent
{
	public abstract EventKind Kind();

	/// <summary>"Whenever a creature you control enters": the creature. None for "whenever you gain life".</summary>
	public virtual IEnumerable<GameObject> Subjects(GameState s) => [];

	/// <summary>"Whenever YOU cast", "at the beginning of YOUR upkeep" (the active player); 0: nobody.</summary>
	public virtual int By(GameState s) => 0;
}

/// <param name="Types">What it was as it entered — Karn, Argent Defender asks, whatever it is by the next priority.</param>
/// <summary>A permanent entered the battlefield — raised by MagicState.EnterBattlefield, the one way in.</summary>
public record EnteredBattlefieldEvent(int CardId, CardType Types = CardType.None) : MagicEvent
{
	public override EventKind Kind() => EventKind.Entered;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>A creature was declared as an attacker — one event per attacker.</summary>
public record AttackedEvent(int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Attacked;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>Attackers were declared — one event however many ("whenever you attack").</summary>
public record AttackersDeclaredEvent(int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.AttackersDeclared;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.CardsIn(PlayerId, ZoneType.Battlefield).Where(c => c.AttackingId != 0);

	public override int By(GameState s) => PlayerId;
}

/// <summary>Combat damage was dealt to these players in one damage step ("one or more").</summary>
public record CombatDamageToPlayersEvent(ImmutableList<int> PlayerIds) : MagicEvent
{
	public override EventKind Kind() => EventKind.CombatDamageToPlayers;

	public override IEnumerable<GameObject> Subjects(GameState s) => PlayerIds.Select(s.GetObject);

	public override int By(GameState s) => s.GetGame().ActivePlayerId;
}

/// <param name="NoncreatureSpellNumber">Which noncreature spell of the caster's turn this is — 0 for a creature.</param>
/// <param name="SpellNumber">Stella Lee: "your second spell each turn" — which spell its caster cast this turn, any kind.</param>
/// <param name="Copies">Way of the Cryomancer: delayed "copy that spell" triggers this cast set off.</param>
/// <summary>A spell was cast — raised by CastSpellAction.PutOnStack (copies are not cast, and raise none).</summary>
public record SpellCastEvent(
	int CardId,
	int PlayerId,
	int NoncreatureSpellNumber = 0,
	int Copies = 0,
	int SpellNumber = 0
) : MagicEvent
{
	public override EventKind Kind() => EventKind.SpellCast;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>A creature or planeswalker went from the battlefield to a graveyard — as it last existed.</summary>
/// <param name="Attached">What was attached to it as it died — "when enchanted creature dies" looks back (CR 603.10a).</param>
public record DiedEvent(Card LastKnown) : MagicEvent
{
	public override EventKind Kind() => EventKind.Died;

	public override IEnumerable<GameObject> Subjects(GameState s) => [LastKnown];

	public ImmutableList<Card> Attached { get; init; } = [];
}

/// <summary>
/// A player was dealt damage — by Rules.Damage.Deal, combat or not. The Inner Light's "you draw that
/// many cards" is Amount — the trigger's X.
/// </summary>
public record PlayerDealtDamageEvent(int PlayerId, bool Combat, int SourceId, int Amount)
	: MagicEvent
{
	public override EventKind Kind() => EventKind.PlayerDealtDamage;

	public override IEnumerable<GameObject> Subjects(GameState s) => [s.GetObject(PlayerId)];
}

/// <summary>A creature was dealt damage — Hexhaven Invigorator's "that many" is Amount.</summary>
public record CreatureDealtDamageEvent(int CardId, int Amount) : MagicEvent
{
	public override EventKind Kind() => EventKind.CreatureDealtDamage;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];
}

/// <summary>A step began — "at the beginning of …". Raised for the steps scripted cards trigger on.</summary>
public record StepBeganEvent(Step Step) : MagicEvent
{
	public override EventKind Kind() => EventKind.StepBegan;

	public override int By(GameState s) => s.GetGame().ActivePlayerId;
}

/// <summary>A creature was declared as a blocker.</summary>
public record BlocksEvent(int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Blocks;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>A player surveilled — raised only by Surveil (MagicCore has no Scry yet; "whenever you scry" would need it to raise this too).</summary>
public record SurveilledEvent(int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Surveilled;

	public override int By(GameState s) => PlayerId;
}

/// <summary>A player discarded these cards — one event per discard, however many cards.</summary>
public record DiscardedEvent(int PlayerId, ImmutableList<int> CardIds) : MagicEvent
{
	public override EventKind Kind() => EventKind.Discarded;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		CardIds.Where(s.HasObject).Select(s.GetObject);

	public override int By(GameState s) => PlayerId;
}

/// <summary>Data, New Life: "Whenever you draw a card" — a player drew one card; raised by MagicState.Draw, once per card.</summary>
public record DrewCardEvent(int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Drew;

	public override int By(GameState s) => PlayerId;
}

/// <summary>A card left its owner's graveyard, to any zone — raised by MagicState.MoveCard, every path (Vash).</summary>
public record LeftGraveyardEvent(int PlayerId, int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.LeftGraveyard;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>A player put loyalty counters on a planeswalker — by a cost, an effect, or its entering with them (CR 122.6).</summary>
public record LoyaltyCountersPutEvent(int PutterId) : MagicEvent
{
	public override EventKind Kind() => EventKind.LoyaltyCountersPut;

	public override int By(GameState s) => PutterId;
}

/// <summary>A spell or ability put on the stack with this card as a target (CR 115).</summary>
public record BecameTargetEvent(int CardId, int ByPlayerId, int ByStackObjectId) : MagicEvent
{
	public override EventKind Kind() => EventKind.BecameTarget;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => ByPlayerId;
}

/// <summary>
/// Kai Winn: "Whenever an opponent … activates an ability of a source with the chosen name" — every
/// activation: ActivateAbilityAction, a loyalty ability, and a mana ability (TapForManaAction, ManaPayment.Pay —
/// Shayne: "If you name Llanowar Elves and they tap it for mana it would trigger her"). The subject is the
/// source as it was activated: a cost may have sacrificed it. Keyword: the ability's (Decree of Pain's
/// "when you cycle", Trigger.Ability).
/// </summary>
public record AbilityActivatedEvent(int PlayerId, Cards.Card Source, string? Keyword = null)
	: MagicEvent
{
	public override EventKind Kind() => EventKind.AbilityActivated;

	public override IEnumerable<GameObject> Subjects(GameState s) => [Source];

	public override int By(GameState s) => PlayerId;
}

/// <summary>"Whenever you activate a loyalty ability" — Cost is +N/−N, so "removed two or more" reads it.</summary>
public record LoyaltyActivatedEvent(int PlayerId, int Cost) : MagicEvent
{
	public override EventKind Kind() => EventKind.LoyaltyActivated;

	public override int By(GameState s) => PlayerId;
}

/// <summary>Counters of a named kind were put on a permanent (MagicState.PutCounters) — From and To are how many it had and has.</summary>
public record CountersPutEvent(int CardId, string CounterKind, int From, int To) : MagicEvent
{
	public override EventKind Kind() => EventKind.CountersPut;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>A permanent went from untapped to tapped — raised by MagicState.Tap, every tap path (Hemmer).</summary>
public record BecameTappedEvent(int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.BecameTapped;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>
/// Face a dilemma: "You face a dilemma as you choose one or more modes for a spell or ability" — one
/// event however many, whether or not it resolves. Raised where modes are chosen: a cast
/// (CastSpellAction.PutOnStack), an activation, a trigger's ChooseModesChoice.
/// </summary>
public record FacedDilemmaEvent(int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.FacedDilemma;

	public override int By(GameState s) => PlayerId;

	/// <summary>The one place a player faces a dilemma: counted for "this turn" (Sisko), then raised.</summary>
	public static GameState Face(GameState s, int playerId) =>
		s.Tally(playerId, t => t with { DilemmasFaced = t.DilemmasFaced + 1 })
			.Raise(new FacedDilemmaEvent(playerId));
}

/// <summary>Raised by Rules.Life.Gain — one per life-gain event, however much (CR 119.9).</summary>
public record LifeGainedEvent(int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.LifeGained;

	public override int By(GameState s) => PlayerId;
}

/// <summary>
/// Triggered abilities (CR 603). Events are staged on GameState.PendingGameEvents where they
/// happen; whenever a player would receive priority, every ability that saw one goes on the stack.
/// </summary>
public static class Triggers
{
	public static GameState Raise(this GameState s, GameEvent e) =>
		s with
		{
			PendingGameEvents = s.PendingGameEvents.Add(e),
		};

	/// <summary>Each card a spell or ability now on the stack targets "becomes the target" of it.</summary>
	/// <param name="byStackObjectId">The spell or ability that targets — "it" to ward.</param>
	public static GameState RaiseTargeted(
		GameState s,
		IEnumerable<int> targets,
		int byPlayerId,
		int byStackObjectId
	) =>
		targets
			.Distinct() // targeted by two words of one spell, it still becomes its target once
			.Where(id => id != 0 && s.HasObject(id) && s.GetObject(id) is Card)
			.Aggregate(
				s,
				(st, id) => st.Raise(new BecameTargetEvent(id, byPlayerId, byStackObjectId))
			);

	/// <summary>A triggered ability waiting to go on the stack. EventSnapshot is the card the event was about, as it last was (a dead creature's counters).</summary>
	/// <param name="EventObject">Ward's "it": the spell or ability that targeted, as it was — so a recast with the same id is not it.</param>
	/// <param name="X">"That many": how much the event was — the damage Hexhaven Invigorator was dealt.</param>
	public record Pending(
		Card Source,
		TriggeredAbilityComponent Ability,
		int EventCardId,
		Card? EventSnapshot = null,
		GameObject? EventObject = null,
		int X = 0
	);

	/// <summary>
	/// Every ability that triggered on the staged events, the active player's first (CR 101.4 APNAP:
	/// they go on the stack first, so the other player's resolve first).
	///
	/// A dying card's OWN "when this dies" ability is read from the card as it last existed (CR
	/// 603.10a). ponytail: other leaves-the-battlefield abilities are read from permanents still on
	/// the battlefield, so Edgar dying alongside another creature misses that one's death. Look back
	/// for every leaves trigger if a card ever needs it.
	/// </summary>
	public static List<Pending> Collect(GameState s)
	{
		// Every trigger below is read from a staged event, so with none nothing can trigger — and
		// most priority passes stage none (62% in the search; story AI-7.3d).
		if (s.PendingGameEvents.IsEmpty)
			return [];
		var active = s.GetGame().ActivePlayerId;
		// Permanents, and emblems and effects in the command zone (CR 113.6b).
		var permanents = new[] { active, s.OpponentOf(active) }
			.SelectMany(p =>
				s.CardsIn(p, ZoneType.Battlefield).Concat(s.CardsIn(p, ZoneType.Command))
			)
			.ToList(); // walked several times below
		// A card in a graveyard is controlled by its owner (CR 108.4a).
		var inGraveyards = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Graveyard)
				.Where(c => AbilitiesOf(s, c).Any(a => a.FromGraveyard))
				.Select(c => c with { ControllerId = c.OwnerId })
		);
		// An exiled or dead card's delayed trigger only — never its own abilities (Ghost of Illyria's
		// "return that card at the beginning of the next end step"; Dr. McCoy's, on the card that died).
		// A permanent's delayed trigger whose controller is named (The Wrath of Khan's, on the creature
		// it will steal) is read here too; the rest stay with the permanent's own (AbilitiesOf).
		var delayedInExile = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Exile)
				.Concat(s.CardsIn(p, ZoneType.Graveyard))
				.Where(c => c.HasComponent<DelayedTrigger>())
				.Concat(
					s.CardsIn(p, ZoneType.Battlefield)
						.Where(c => c.GetComponents<DelayedTrigger>().Any(d => d.ControllerId != 0))
				)
				.SelectMany(c =>
					c.GetComponents<DelayedTrigger>()
						.Where(d => d.ControllerId != 0 || !s.IsOnBattlefield(c.Id))
						.Select(d =>
							(
								source: d.ControllerId == 0
									? c
									: c with
									{
										ControllerId = d.ControllerId,
									},
								a: d.Ability
							)
						)
				)
		);
		// Hive Mind Coprocessor: "Artifact creatures you control have '[trigger]'" — found once here.
		var grants = permanents
			.Where(p => p.HasComponent<GrantsTriggeredAbility>() && s.IsOnBattlefield(p.Id))
			.SelectMany(p => s.AbilitiesOf<GrantsTriggeredAbility>(p).Select(g => (granter: p, g)))
			.ToList();
		// Dr. Crusher, Lwaxana Troi, Isshin: "that ability triggers an additional time" — found once here.
		var doublers = permanents
			.Where(p => p.HasComponent<TriggersAdditionalTime>() && s.IsOnBattlefield(p.Id))
			.ToList();
		var events = WithoutSilencedEntries(s, permanents);
		var fromTheBattlefield = permanents
			.SelectMany(source =>
				AbilitiesOf(s, source)
					.Where(a => !a.FromGraveyard)
					.Concat(Granted(s, grants, source))
					.Select(a => (source, a))
			)
			.Concat(
				inGraveyards.SelectMany(source =>
					AbilitiesOf(s, source).Where(a => a.FromGraveyard).Select(a => (source, a))
				)
			)
			.Concat(delayedInExile)
			.SelectMany(found =>
				events
					.Where(e => Matches(s, found.source, found.a.On, e))
					.Take(found.a.On.OneOrMore ? 1 : int.MaxValue) // "one or more": one per batch
					.SelectMany(e =>
						Enumerable.Repeat(
							new Pending(
								found.source,
								found.a,
								EventCard(e),
								Snapshot(s, e),
								EventObject(s, e),
								e switch
								{
									CreatureDealtDamageEvent damaged => damaged.Amount,
									PlayerDealtDamageEvent dealt => dealt.Amount,
									_ => 0,
								}
							),
							1 + AdditionalTimes(s, doublers, found.source, found.a, e)
						)
					)
			);
		var fromTheDead = s
			.PendingGameEvents.OfType<DiedEvent>()
			.SelectMany(died =>
				// As it last existed (CR 603.10a): under Infinite Coursework it had no abilities — the
				// Aura is gone from the battlefield by now, so ask what was attached as it died.
				(
					died.Attached.Any(a => a.HasComponent<EnchantedLosesAllAbilities>())
						? []
						: AbilitiesOf(s, died.LastKnown)
				)
					// "When this dies", and its own "whenever [this or another] creature … dies" seeing
					// its own death — leaves-the-battlefield abilities look back (CR 603.10a; Excalbian
					// Gamemaster). Another's death is matched on the battlefield.
					// ponytail: so a creature dying ALONGSIDE others misses their deaths (a wrath: Gamemaster
					// triggers once, not three times). Match its Died abilities against every DiedEvent in
					// the batch if a card needs it (DesignNotes).
					.Where(a =>
						(
							a.On.ReadFrom == ReadFrom.TheDyingCard
							|| a.On.On == EventKind.Died && a.On.ReadFrom == ReadFrom.Battlefield
						)
						&& !a.FromGraveyard
						&& Concerns(s, a.On, died.LastKnown, died.LastKnown)
					)
					// Lwaxana's linked creature dying: its "when this dies" looks back (CR 603.10a), doubled too.
					.SelectMany(a =>
						Enumerable.Repeat(
							new Pending(died.LastKnown, a, died.LastKnown.Id, died.LastKnown),
							1
								+ AdditionalTimes(
									s,
									doublers,
									died.LastKnown,
									a,
									died,
									lookBack: true
								)
						)
					)
					// The Aura is in the graveyard by the time triggers are collected — the state-based
					// check takes it in the same pass — so its trigger is read from it as it was.
					.Concat(
						died.Attached.SelectMany(attachment =>
							AbilitiesOf(s, attachment)
								.Where(a => a.On.ReadFrom == ReadFrom.ItsAttachment)
								.Select(a => new Pending(
									attachment,
									a,
									died.LastKnown.Id,
									died.LastKnown
								))
						)
					)
			);
		// "When you cast this spell" (Emrakul): the spell's own, read from it on the stack.
		var fromTheSpell = s
			.PendingGameEvents.OfType<SpellCastEvent>()
			.Where(cast => s.HasObject(cast.CardId))
			.SelectMany(cast =>
				AbilitiesOf(s, s.GetCard(cast.CardId))
					.Where(a =>
						a.On.ReadFrom == ReadFrom.TheSpellCast
						&& Concerns(s, a.On, s.GetCard(cast.CardId), s.GetCard(cast.CardId))
					)
					.Select(a => new Pending(s.GetCard(cast.CardId), a, cast.CardId))
			);
		// Delayed triggers set off by a cast (Way of the Cryomancer): "copy that spell" — one per copy.
		var fromACast = s
			.PendingGameEvents.OfType<SpellCastEvent>()
			.Where(cast => cast.Copies > 0 && s.HasObject(cast.CardId))
			.SelectMany(cast =>
				Enumerable.Repeat(
					new Pending(
						s.GetCard(cast.CardId) with
						{
							ControllerId = cast.PlayerId,
						},
						CopyThatSpell,
						cast.CardId,
						s.GetCard(cast.CardId) // copied from this if it is gone by resolution
					),
					cast.Copies
				)
			);
		return fromTheBattlefield
			.Concat(fromTheDead)
			.Concat(fromACast)
			.Concat(fromTheSpell)
			.Where(p => InterveningIfHolds(s, p))
			.OrderBy(p => p.Source.ControllerId == active ? 0 : 1) // APNAP; stable within a player
			.ToList();
	}

	/// <summary>
	/// "That ability triggers an additional time" (CR 603.2d) — how many more times a permanent's
	/// ability triggers on this event: one per TriggersAdditionalTime whose Cause matches the event and
	/// whose WhoseAbilities matches the source. A delayed trigger on a permanent isn't one of its
	/// abilities (CR 603.7c: it triggers once). <paramref name="lookBack"/>: the source left as this happened
	/// (a dying creature's own "when this dies"), so it is matched as it last existed.
	/// </summary>
	static int AdditionalTimes(
		GameState s,
		List<Card> doublers,
		Card source,
		TriggeredAbilityComponent ability,
		GameEvent e,
		bool lookBack = false
	)
	{
		if (
			doublers.Count == 0
			|| !lookBack && !s.IsOnBattlefield(source.Id)
			|| source.GetComponents<DelayedTrigger>().Any(d => d.Ability == ability)
		)
			return 0;
		var n = 0;
		foreach (var d in doublers)
		{
			// Printed ones are its abilities ("loses all abilities" ends them); Lwaxana's link is an
			// effect's result, kept while she stays tapped.
			var rows = s.AbilitiesOf<TriggersAdditionalTime>(d)
				.Where(t => !t.UntilUntapped)
				.Concat(
					d.IsTapped
						? d.GetComponents<TriggersAdditionalTime>().Where(t => t.UntilUntapped)
						: []
				);
			foreach (var t in rows)
				if (
					(t.Cause is null || Matches(s, d, t.Cause, e))
					&& t.WhoseAbilities.Matches(s, source, new FilterContext(d.ControllerId, d.Id))
				)
					n++;
		}
		return n;
	}

	/// <summary>
	/// Karn, Argent Defender: "Artifacts and creatures entering the battlefield don't cause abilities
	/// to trigger" — their entering events are dropped, for every ability (CR 603.2g). Read at the
	/// next priority, so Karn entering alongside a creature silences it too, as the rulings say.
	/// </summary>
	static IReadOnlyList<GameEvent> WithoutSilencedEntries(
		GameState s,
		IEnumerable<Card> permanents
	)
	{
		var events = s.PendingGameEvents;
		if (
			!events.Any(e => e is EnteredBattlefieldEvent)
			|| !permanents.Any(p => s.HasAbility<EnteringArtifactsAndCreaturesDontTrigger>(p))
		)
			return events;
		return
		[
			.. events.Where(e =>
				e is not EnteredBattlefieldEvent entered
				|| (entered.Types & (CardType.Artifact | CardType.Creature)) == 0
			),
		];
	}

	/// <summary>
	/// CR 603.4: "When/At …, if [condition], …" — checked as it triggers here, and again as it
	/// resolves (<see cref="QueueTargets"/> wraps the effects in it).
	/// </summary>
	static bool InterveningIfHolds(GameState s, Pending t)
	{
		if (t.Ability.If is not { } condition)
			return true;
		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, t.Source.ControllerId)
			.Add(ContextKeys.Source, t.Source.Id)
			.Add(ContextKeys.SourceSnapshot, t.Source) // a dying card as it was: "if it isn't a token"
			.Add(ContextKeys.EventCard, t.EventCardId);
		if (t.EventSnapshot is { } snapshot)
			context = context.Add(ContextKeys.EventSnapshot, snapshot);
		return condition.Holds(s, context);
	}

	/// <summary>The event's card as it is now — so "that creature's controller" survives it leaving (CR 608.2h).</summary>
	static Card? Snapshot(GameState s, GameEvent e) =>
		e switch
		{
			DiedEvent died => died.LastKnown,
			BlocksEvent blocks when s.IsOnBattlefield(blocks.CardId) => s.GetCard(blocks.CardId),
			AttackedEvent attacked when s.IsOnBattlefield(attacked.CardId) => s.GetCard(
				attacked.CardId
			),
			_ => null,
		};

	/// <summary>The delayed trigger Way of the Cryomancer's ability sets up: "copy that spell".</summary>
	static readonly TriggeredAbilityComponent CopyThatSpell =
		new(TriggerEvent.Reflexive, [new CopySpell(Ref.EventCard)]);

	/// <summary>Its own triggered abilities, and any it gained until end of turn (Lyra) — none once it lost them all.</summary>
	static IEnumerable<TriggeredAbilityComponent> AbilitiesOf(GameState s, Card card) =>
		(
			s.HasLostAllAbilities(card)
				? []
				: card.GetComponents<TriggeredAbilityComponent>()
					.Concat(
						card.GetComponents<UntilEndOfTurn>()
							.Select(u => u.Trigger)
							.OfType<TriggeredAbilityComponent>()
					)
		).Concat(
			card.GetComponents<DelayedTrigger>()
				.Where(d => d.ControllerId == 0) // one with its own controller: delayedInExile
				.Select(d => d.Ability)
		); // not its own: kept

	/// <summary>The triggered abilities a static grants this permanent — none once it lost them all.</summary>
	static IEnumerable<TriggeredAbilityComponent> Granted(
		GameState s,
		List<(Card granter, GrantsTriggeredAbility g)> grants,
		Card permanent
	) =>
		grants.Count == 0 || !s.IsOnBattlefield(permanent.Id) || s.HasLostAllAbilities(permanent)
			? []
			: grants
				.Where(x =>
					x.g.Affects.Matches(s, permanent, new(x.granter.ControllerId, x.granter.Id))
				)
				.Select(x => x.g.Ability);

	/// <summary>A look-back trigger's Subject, asked of the card it reads from.</summary>
	static bool Concerns(GameState s, Trigger on, GameObject subject, Card source) =>
		on.Subject?.Matches(s, subject, new FilterContext(source.ControllerId, source.Id)) ?? true;

	static bool Matches(GameState s, Card source, Trigger on, GameEvent e)
	{
		// "When this dies / when you cast this" are read from the card itself (Collect), never here.
		if (
			on.ReadFrom != ReadFrom.Battlefield
			|| e is not MagicEvent happened
			|| happened.Kind() != on.On
		)
			return false;
		if (on.Step is { } step && (e is not StepBeganEvent began || began.Step != step))
			return false;
		if (on.By != Whose.Any && !on.By.Includes(happened.By(s), source.ControllerId))
			return false;
		var asking = new FilterContext(source.ControllerId, source.Id);
		if (
			on.Subject is { } subject
			&& !happened.Subjects(s).Any(o => subject.Matches(s, o, asking))
		)
			return false;
		if (on.While is { } holding && !holding.Holds(s, Conditions.Of(source)))
			return false;
		if (
			on.Chapter > 0
			&& (
				e is not CountersPutEvent { CounterKind: CounterKinds.Lore } lore
				|| lore.From >= on.Chapter
				|| lore.To < on.Chapter
			)
		)
			return false;
		if (on.Counters is { } kind && (e is not CountersPutEvent put || put.CounterKind != kind))
			return false;
		if (
			on.Ability is { } keyword
			&& (e is not AbilityActivatedEvent act || act.Keyword != keyword)
		)
			return false;
		if (
			on.SpellNumber > 0
			&& (e is not SpellCastEvent numbered || numbered.SpellNumber != on.SpellNumber)
		)
			return false;
		if (
			on.CombatDamageBy is { } by
			&& (
				e is not PlayerDealtDamageEvent { Combat: true } damage
				|| !s.HasObject(damage.SourceId)
				|| !by.Matches(s, s.GetObject(damage.SourceId), asking)
			)
		)
			return false;
		// A spell's target that is a player, or a permanent on the battlefield now.
		bool Targets(SpellCastEvent cast, Filter f) =>
			SpellTargets(s, cast)
				.Any(t =>
					(s.GetObject(t) is MagicPlayer || s.IsOnBattlefield(t))
					&& f.Matches(s, s.GetObject(t), asking)
				);
		return on.Also switch
		{
			EventTest.RemovingTwoOrMore => e is LoyaltyActivatedEvent { Cost: <= -2 },
			// The only creature its controller has attacking (Subject says it attacks a player).
			EventTest.Alone => s.CardsIn(source.ControllerId, ZoneType.Battlefield)
				.Count(c => c.AttackingId != 0) == 1,
			EventTest.EquipmentOrTargetsYourCreature => e is SpellCastEvent cast
				&& s.HasObject(cast.CardId)
				&& (
					s.GetCard(cast.CardId).Subtypes.Contains("Equipment")
					|| Targets(cast, Is.Creature & Is.Yours)
				),
			EventTest.TargetsAnOpponentOrTheirCreature => e is SpellCastEvent cast
				&& Targets(cast, Is.Opponents & (Is.Player | Is.Creature)),
			// Counted as it is cast, so a second spell cast in response can't take the first's place.
			EventTest.FirstNoncreatureSpell => e is SpellCastEvent { NoncreatureSpellNumber: 1 },
			EventTest.CombatDamageFromThis => e is PlayerDealtDamageEvent damaged
				&& damaged.Combat
				&& damaged.SourceId == source.Id,
			EventTest.Noncombat => e is PlayerDealtDamageEvent { Combat: false },
			_ => true,
		};
	}

	/// <summary>What a spell targeted as it was cast — every mode's targets (CR 115.1).</summary>
	static IEnumerable<int> SpellTargets(GameState s, SpellCastEvent cast) =>
		s.HasObject(cast.CardId)
		&& s.GetCard(cast.CardId).GetComponent<OnStackComponent>() is { } onStack
			? onStack.Targets.Where(id => id != 0 && s.HasObject(id))
			: [];

	/// <summary>The spell or ability an event was done BY, as it is now — ward's "it".</summary>
	static GameObject? EventObject(GameState s, GameEvent e) =>
		e is BecameTargetEvent targeted && s.HasObject(targeted.ByStackObjectId)
			? s.GetObject(targeted.ByStackObjectId)
			: null;

	/// <summary>The card the event is about — what "that creature" and "that land" mean; 0 if none.</summary>
	static int EventCard(GameEvent e) =>
		e switch
		{
			EnteredBattlefieldEvent entered => entered.CardId,
			AttackedEvent attacked => attacked.CardId,
			SpellCastEvent cast => cast.CardId,
			DiedEvent died => died.LastKnown.Id,
			BlocksEvent blocks => blocks.CardId,
			BecameTargetEvent targeted => targeted.CardId,
			CreatureDealtDamageEvent damaged => damaged.CardId,
			_ => 0,
		};

	/// <summary>
	/// Each trigger's controller picks its targets, then it goes on the stack; then priority goes
	/// to <paramref name="priorityPlayerId"/>. A trigger with a required target and no legal one is
	/// removed (CR 603.3d).
	/// </summary>
	public static GameState PutOnStack(GameState s, List<Pending> triggers, int priorityPlayerId)
	{
		var firedOnce = new HashSet<(int Id, int Incarnation)>();
		foreach (var t in triggers)
		{
			s = WithoutFiredDelayedTrigger(s, t);
			if (t.Ability.OncePerTurn)
			{
				// "Only once each turn": the first goes on the stack and stamps the card; the rest
				// don't. A source that already left still resolves (CR 603.3) — there is nothing left
				// to stamp, and a new object may trigger again anyway (CR 400.7).
				// ponytail: one stamp per card, so two once-per-turn abilities on one permanent would
				// block each other. Key it by ability if a card ever has two.
				var turn = s.GetGame().TurnNumber;
				if (!firedOnce.Add((t.Source.Id, t.Source.Incarnation)))
					continue;
				if (s.IsOnBattlefield(t.Source.Id) && s.GetCard(t.Source.Id) is var now)
				{
					if (now.OncePerTurnTriggeredOn == turn)
						continue;
					s = s.UpdateObject(now.Id, now with { OncePerTurnTriggeredOn = turn });
				}
			}
			if (t.Ability.Modes.IsEmpty)
			{
				s = QueueTargets(s, t, t.Ability.Targets, t.Ability.Effects);
				continue;
			}
			var choosable = ChoosableModes(s, t);
			var (least, most) = t.Ability.Choose.Range(s, t.Source, choosable.Count);
			if (choosable.Count < least || most == 0)
				continue; // not enough modes can be chosen (CR 700.2a) — or "up to one" with none: it would do nothing
			var choice = new ChooseModesChoice(
				t.Source.ControllerId,
				t.Source.Id,
				t.Ability,
				choosable,
				least,
				most
			);
			s = s.SpawnAction(
				new PipelineAction { Steps = [choice, new QueueModalTriggerAction { Trigger = t }] }
			);
		}
		return s.SpawnAction(new GivePriorityAction { PlayerId = priorityPlayerId });
	}

	/// <summary>
	/// A delayed trigger triggers only once (CR 603.7c): taken off its host as it goes on the stack, so
	/// one countered doesn't fire again at the next end step; an AtNextEndStep object left empty goes too.
	/// </summary>
	static GameState WithoutFiredDelayedTrigger(GameState s, Pending t)
	{
		if (
			!s.HasObject(t.Source.Id)
			|| s.GetObject(t.Source.Id) is not Card host
			|| host.Incarnation != t.Source.Incarnation
			|| host.GetComponents<DelayedTrigger>().FirstOrDefault(d => d.Ability == t.Ability)
				is not { } fired
		)
			return s;
		var rest = host.GetComponents<DelayedTrigger>().Where(d => d != fired).ToList();
		var without = rest.Aggregate(
			host.WithoutComponents<DelayedTrigger>(),
			(c, d) => c.WithComponent(d)
		);
		return without.Components.Length == 0 && s.IsIn(host.Id, host.OwnerId, ZoneType.Command)
			? s.RemoveObject(host.Id)
			: s.UpdateObject(host.Id, without);
	}

	/// <summary>Modes whose required targets all have a legal choice (CR 700.2a).</summary>
	public static ImmutableList<int> ChoosableModes(GameState s, Pending t) =>
		t
			.Ability.Modes.Select((mode, i) => (mode, i))
			.Where(m =>
				m.mode.Targets.All(spec =>
					spec.Optional || spec.Candidates(s, t.Source.ControllerId, t.Source.Id).Any()
				)
			)
			.Where(m =>
				!t.Ability.Choose.NotChosenBefore || (ModesChosenBy(s, t) & (1 << m.i)) == 0
			)
			.Select(m => m.i)
			.ToImmutableList();

	/// <summary>Kobayashi Maru Scenario: the modes this object of the source chose before — the live card's, while it is still that object.</summary>
	public static int ModesChosenBy(GameState s, Pending t) =>
		s.HasObject(t.Source.Id)
		&& s.GetObject(t.Source.Id) is Card now
		&& now.Incarnation == t.Source.Incarnation
			? now.ModesChosen
			: t.Source.ModesChosen;

	/// <summary>The controller chooses each target, then the ability goes on the stack (CR 603.3d).</summary>
	public static GameState QueueTargets(
		GameState s,
		Pending t,
		ImmutableList<Targeting.TargetSpec> specs,
		ImmutableList<GameAction> steps
	)
	{
		var controller = t.Source.ControllerId;
		if (specs.Any(spec => !spec.Optional && !spec.Candidates(s, controller, t.Source.Id).Any()))
			return s;
		if (t.Ability.If is { } condition)
			steps = [new If(condition, steps)]; // CR 603.4: checked again on resolution
		return s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					.. specs.Select(
						(spec, i) => new ChooseTargetChoice(controller, t.Source.Id, spec, i)
					),
					new PutTriggerOnStackAction
					{
						Source = t.Source,
						ControllerId = controller,
						Specs = specs,
						Effects = steps,
						EventCardId = t.EventCardId,
						EventSnapshot = t.EventSnapshot,
						EventObject = t.EventObject,
						// "When this enters": the X it was cast for.
						X =
							t.Ability.On.On == EventKind.Entered
							&& t.Ability.On.Subject == Is.Source
								? t.Source.CastX
								: t.X,
					},
				],
			}
		);
	}
}

public record PutTriggerOnStackAction : GameAction
{
	/// <summary>The source as it was when it triggered — it may be gone by now (a token that died).</summary>
	public Card Source { get; init; } = null!;
	public int ControllerId { get; init; }

	/// <summary>The target words and steps to run — the whole text, or the chosen modes composed.</summary>
	public ImmutableList<Targeting.TargetSpec> Specs { get; init; } = [];
	public ImmutableList<GameAction> Effects { get; init; } = [];
	public int EventCardId { get; init; }
	public Card? EventSnapshot { get; init; }
	public GameObject? EventObject { get; init; }
	public int X { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var targets = ChooseTargetChoice.Read(InputContext, Specs.Count);
		if (Specs.Where((spec, i) => !spec.Optional && targets[i] == 0).Any())
			return new(s);
		return new(
			StackResolution.Push(
				s,
				new StackAbility
				{
					Name = $"{Source.Name} trigger",
					SourceId = Source.Id,
					Source = Source,
					ControllerId = ControllerId,
					Effects = Effects,
					Specs = Specs,
					Targets = targets,
					EventCardId = EventCardId,
					EventSnapshot = EventSnapshot,
					EventObject = EventObject,
					X = X,
					Triggered = true,
				}
			)
		);
	}
}

/// <summary>After the modes are chosen: compose them and ask for their targets.</summary>
public record QueueModalTriggerAction : GameAction
{
	public Triggers.Pending Trigger { get; init; } = null!;

	public override ActionResult Execute(GameState s)
	{
		var chosen = ChooseModesChoice.Read(InputContext);
		var (specs, steps) = Modal.Compose(
			Trigger.Ability.Targets,
			Trigger.Ability.Effects,
			Trigger.Ability.Modes,
			chosen
		);
		if (!chosen.IsEmpty) // Ertai's "up to one", none chosen: no dilemma faced (as a cast or activation)
			s = FacedDilemmaEvent.Face(s, Trigger.Source.ControllerId);
		// Kobayashi Maru Scenario: "choose one that hasn't been chosen" — stamped on this object as chosen.
		if (
			Trigger.Ability.Choose.NotChosenBefore
			&& s.HasObject(Trigger.Source.Id)
			&& s.GetObject(Trigger.Source.Id) is Card now
			&& now.Incarnation == Trigger.Source.Incarnation
		)
			s = s.UpdateObject(
				now.Id,
				now with
				{
					ModesChosen = chosen.Aggregate(now.ModesChosen, (m, i) => m | (1 << i)),
				}
			);
		return new(Triggers.QueueTargets(s, Trigger, specs, steps));
	}
}

public record GivePriorityAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(TurnStructure.GivePriority(s, PlayerId));
}
