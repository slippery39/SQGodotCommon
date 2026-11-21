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
/// <param name="From">"Name Sticker" Goblin: "enters from anywhere other than a graveyard or exile" — the zone it left (Trigger.From, NotFrom); null for a token just made.</param>
/// <summary>A permanent entered the battlefield — raised by MagicState.EnterBattlefield, the one way in.</summary>
public record EnteredBattlefieldEvent(
	int CardId,
	CardType Types = CardType.None,
	ZoneType? From = null
) : MagicEvent
{
	public override EventKind Kind() => EventKind.Entered;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>A creature was declared as an attacker — one event per attacker.</summary>
public record AttackedEvent(int CardId) : MagicEvent
{
	/// <summary>
	/// Fear of Missing Out: "Whenever this creature attacks for the first time each turn" — which of this object's
	/// attacks this turn it is (Card.TimesAttacked), read by Trigger.Nth.
	/// </summary>
	public int Number { get; init; } = 1;

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
/// <param name="From">Prosper, Tome-Bound: "play a card from exile" — the zone it was cast from (Trigger.From).</param>
public record SpellCastEvent(
	int CardId,
	int PlayerId,
	int NoncreatureSpellNumber = 0,
	int Copies = 0,
	int SpellNumber = 0,
	ZoneType From = ZoneType.Hand
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

/// <summary>
/// A player discarded this card — one event per card (Ivora, Insatiable Heir: "Whenever you discard a card"), as
/// DrewCardEvent; "one or more cards" (Tinybones, Inti) is Trigger.OneOrMore.
/// </summary>
public record DiscardedEvent(int PlayerId, int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Discarded;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>
/// A player played a land — raised by PlayLandAction alone, so a land put onto the battlefield isn't one.
/// Prosper, Tome-Bound: "Whenever you play a card from exile" — From is where it was played from.
/// </summary>
public record LandPlayedEvent(int PlayerId, int CardId, ZoneType From) : MagicEvent
{
	public override EventKind Kind() => EventKind.LandPlayed;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>Data, New Life: "Whenever you draw a card" — a player drew one card; raised by MagicState.Draw, once per card.</summary>
/// <param name="Number">Faerie Mastermind: "their second card each turn" — which card of that player's turn this is, numbered as it is drawn.</param>
/// <param name="TurnDraw">Orcish Bowmasters: "except the first one they draw in each of their draw steps" — the draw step's own draw.</param>
/// <param name="CardId">Miracle (Metamorphosis Fanatic): "when you draw it" — the card drawn, the event's subject while it
/// stays in the hand; never its event card (an opponent's draw is a hidden card).</param>
public record DrewCardEvent(int PlayerId, int Number = 0, bool TurnDraw = false, int CardId = 0)
	: MagicEvent
{
	public override EventKind Kind() => EventKind.Drew;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		CardId != 0 && s.IsIn(CardId, PlayerId, ZoneType.Hand) ? [s.GetCard(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>
/// Sedgemoor Witch: "Whenever you cast or copy an instant or sorcery spell" — a copy of a spell put on the stack, raised by
/// CopySpell (CR 707.10); By is the copy's controller. A copy is never cast: SpellCastEvent never sees it.
/// </summary>
public record SpellCopiedEvent(int CardId, int PlayerId) : MagicEvent
{
	public override EventKind Kind() => EventKind.SpellCopied;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.HasObject(CardId) ? [s.GetObject(CardId)] : [];

	public override int By(GameState s) => PlayerId;
}

/// <summary>
/// A card changed zones — raised by MagicState.MoveCard, every path, for the moves a card reads: out of its owner's
/// graveyard to any zone (Vash: "whenever a card leaves your graveyard") and from their library into exile (Laelia,
/// the Blade Reforged: "put into exile from your library and/or your graveyard") — a token isn't a card — and off
/// the battlefield to any zone, a token too, for a permanent that reads its OWN leaving (Skyclave Apparition: "When
/// this creature leaves the battlefield"; Serra Paragon: "put into a graveyard from the battlefield"). ponytail: not
/// every zone change — MoveCard is the hottest path; widen the cases there when a card reads another move
/// ("whenever ANOTHER permanent leaves the battlefield": raise it for every permanent, MagicState.ReadsItsLeaving).
/// </summary>
public record ChangedZoneEvent(int PlayerId, int CardId, ZoneType From, ZoneType To) : MagicEvent
{
	/// <summary>Off the battlefield: the permanent as it last was — its own leaving abilities look back (CR 603.10a).</summary>
	public Card? LastKnown { get; init; }

	public override EventKind Kind() => EventKind.ChangedZone;

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

/// <summary>
/// A permanent transformed (CR 701.28; Revealing Eye: "When this creature transforms into Revealing Eye") — raised by the
/// Transform effect alone: entering transformed (MoveTo.Transformed) or a modal DFC played as its back face isn't one.
/// </summary>
public record TransformedEvent(int CardId) : MagicEvent
{
	public override EventKind Kind() => EventKind.Transformed;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(CardId) ? [s.GetCard(CardId)] : [];
}

/// <summary>
/// Cecil, Dark Knight: "Whenever Cecil deals damage, you lose that much life" — a source dealt damage to a player, a
/// creature or a planeswalker (Rules.Damage.Deal, every path); its subject is the source while it is on the battlefield.
/// ponytail: one event per recipient, so damage to two blockers at once triggers twice (CR 120: one damage event) —
/// lifelink's ceiling too; a combat damage step's total per source when a card reads it differently.
/// </summary>
public record DealtDamageEvent(int SourceId, int Amount) : MagicEvent
{
	public override EventKind Kind() => EventKind.DealtDamage;

	public override IEnumerable<GameObject> Subjects(GameState s) =>
		s.IsOnBattlefield(SourceId) ? [s.GetCard(SourceId)] : [];
}

/// <summary>A Class became level N (Does Machines: "When this Class becomes level 2") — raised by GainLevel.</summary>
public record BecameLevelEvent(int CardId, int Level) : MagicEvent
{
	public override EventKind Kind() => EventKind.BecameLevel;

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
	)
	{
		/// <summary>A delayed trigger (CR 603.7) — set by PutOnStack, carried to StackAbility.Delayed.</summary>
		public bool Delayed { get; init; }
	}

	/// <summary>
	/// Every ability that triggered on the staged events, the active player's first (CR 101.4 APNAP:
	/// they go on the stack first, so the other player's resolve first).
	///
	/// A dying card's OWN "when this dies" ability, and a permanent's own "when this leaves the battlefield",
	/// are read from the card as it last existed (CR 603.10a). ponytail: other leaves-the-battlefield
	/// abilities are read from permanents still on the battlefield, so Edgar dying alongside another
	/// creature misses that one's death. Look back for every leaves trigger if a card ever needs it.
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
		// A card in a graveyard or a hand is controlled by its owner (CR 108.4a): its abilities that trigger from there
		// (TriggeredAbilityComponent.From) — Darklight Phoenix's graveyard, miracle's hand.
		var offTheBattlefield = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			new[] { ZoneType.Graveyard, ZoneType.Hand }.SelectMany(zone =>
				s.CardsIn(p, zone)
					.Where(c => AbilitiesOf(s, c).Any(a => a.From == zone)) // most cards have none: copy only those
					.Select(c => c with { ControllerId = c.OwnerId })
					.SelectMany(c =>
						AbilitiesOf(s, c).Where(a => a.From == zone).Select(a => (source: c, a))
					)
			)
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
		// A card that left the battlefield: its own abilities, as it last existed (CR 603.10a) — and a doubler's
		// "additional time" for them (Lwaxana's linked creature).
		IEnumerable<Pending> LookingBack(
			Card was,
			GameEvent left,
			IEnumerable<TriggeredAbilityComponent> abilities
		) =>
			abilities
				.Where(a => a.From == ZoneType.Battlefield && Concerns(s, a.On, was, was))
				.SelectMany(a =>
					Enumerable.Repeat(
						new Pending(was, a, was.Id, was),
						1 + AdditionalTimes(s, doublers, was, a, left, lookBack: true)
					)
				);
		var fromTheBattlefield = permanents
			.SelectMany(source =>
				AbilitiesOf(s, source)
					.Where(a => a.From == ZoneType.Battlefield)
					.Concat(Granted(s, grants, source))
					.Select(a => (source, a))
			)
			.Concat(offTheBattlefield)
			.Concat(delayedInExile)
			.SelectMany(found =>
				OnePerBatch(
						s,
						events.Where(e => Matches(s, found.source, found.a.On, e)),
						found.a.On.OneOrMore
					)
					.SelectMany(e =>
						Enumerable.Repeat(
							new Pending(
								found.source,
								found.a,
								EventCard(e),
								Snapshot(s, e),
								// Phelia: a delayed trigger's "Phelia" is the object that set it up
								found
									.source.GetComponents<DelayedTrigger>()
									.FirstOrDefault(d => d.Ability == found.a)
									?.MadeBy ?? EventObject(s, e),
								found.a.X != 0
									? found.a.X
									: e switch
									{
										CreatureDealtDamageEvent damaged => damaged.Amount,
										PlayerDealtDamageEvent dealt => dealt.Amount,
										DealtDamageEvent dealt => dealt.Amount,
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
				LookingBack(
						died.LastKnown,
						died,
						// As it last existed (CR 603.10a): under Infinite Coursework it had no abilities — the
						// Aura is gone from the battlefield by now, so ask what was attached as it died.
						// ponytail: The Wondrous Wasp's strip is stamped on the Wasp, so a creature dying WITH
						// the Wasp (a wrath) reads as having its abilities. Carry the stripping objects on
						// DiedEvent beside Attached if a cube game shows it (DesignNotes).
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
							a.On.On == EventKind.Died
							&& a.On.ReadFrom is ReadFrom.TheCardThatLeft or ReadFrom.Battlefield
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
		// "When this leaves the battlefield" (Skyclave Apparition), "when this permanent is put into a graveyard
		// from the battlefield" (Serra Paragon's gift): the card's own, whatever zone it went to — a death too.
		// ponytail: "had it lost all abilities" is asked of the state NOW (AbilitiesOf) — an Aura's
		// EnchantedLosesAllAbilities that the state check has taken by then is missed, as DiedEvent.Attached
		// isn't here. Carry what was attached on this event if a pool has both (DesignNotes).
		var fromThoseThatLeft = s
			.PendingGameEvents.OfType<ChangedZoneEvent>()
			.Where(left => left.LastKnown is not null)
			.SelectMany(left =>
				LookingBack(
					left.LastKnown!,
					left,
					AbilitiesOf(s, left.LastKnown!)
						.Where(a =>
							a.On.On == EventKind.ChangedZone
							&& a.On.ReadFrom == ReadFrom.TheCardThatLeft
							&& ZonesFit(a.On, left)
						)
				)
			);
		// "When you cast this spell" (Emrakul): the spell's own, read from it on the stack — and as it was cast once
		// it is gone (storm's copies of a countered spell, Brain Freeze).
		var fromTheSpell = s
			.PendingGameEvents.OfType<SpellCastEvent>()
			.Where(cast => s.HasObject(cast.CardId))
			.SelectMany(cast =>
				AbilitiesOf(s, s.GetCard(cast.CardId))
					.Where(a =>
						a.On.ReadFrom == ReadFrom.TheSpellCast
						&& Concerns(s, a.On, s.GetCard(cast.CardId), s.GetCard(cast.CardId))
					)
					.Select(a => new Pending(
						s.GetCard(cast.CardId),
						a,
						cast.CardId,
						s.GetCard(cast.CardId)
					))
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
			.Concat(fromThoseThatLeft)
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
		return condition.Holds(s, ContextOf(t));
	}

	/// <summary>What a pending trigger's conditions and counts read as it triggers — its source and its event's card.</summary>
	static ImmutableDictionary<string, object> ContextOf(Pending t)
	{
		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, t.Source.ControllerId)
			.Add(ContextKeys.Source, t.Source.Id)
			.Add(ContextKeys.SourceSnapshot, t.Source) // a dying card as it was: "if it isn't a token"
			.Add(ContextKeys.EventCard, t.EventCardId)
			.Add(ContextKeys.X, XOf(t));
		return t.EventSnapshot is { } snapshot
			? context.Add(ContextKeys.EventSnapshot, snapshot)
			: context;
	}

	/// <summary>
	/// The X a trigger's intervening "if" and its effects both read: for "when this enters", the X it was cast
	/// for (Jacked Rabbit's ravenous: "If X is 5 or more, draw a card when it enters"); else the event's number.
	/// </summary>
	static int XOf(Pending t) =>
		t.Ability.On.On == EventKind.Entered && t.Ability.On.Subject == Is.Source
			? t.Source.CastX
			: t.X;

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
			&& !(
				e switch
				{
					CountersPutEvent { CounterKind: CounterKinds.Lore } lore => lore.From
						< on.Chapter
						&& lore.To >= on.Chapter,
					BecameLevelEvent level => level.Level == on.Chapter,
					_ => false,
				}
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
			on.Nth > 0
			&& on.Nth
				!= e switch
				{
					SpellCastEvent cast => cast.SpellNumber,
					DrewCardEvent drew => drew.Number,
					AttackedEvent attacked => attacked.Number,
					_ => 0,
				}
		)
			return false;
		if (!ZonesFit(on, e))
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
			EventTest.NotTheTurnsDraw => e is DrewCardEvent { TurnDraw: false },
			_ => true,
		};
	}

	/// <summary>
	/// Trigger.From, NotFrom and To against the zones an event names — the one test, for a trigger matched on the
	/// battlefield and for one read from the card that left.
	/// </summary>
	static bool ZonesFit(Trigger on, GameEvent e)
	{
		if (on.From is not null || !on.NotFrom.IsEmpty)
		{
			var from = e switch
			{
				SpellCastEvent cast => cast.From,
				LandPlayedEvent land => land.From,
				ChangedZoneEvent moved => moved.From,
				EnteredBattlefieldEvent entered => entered.From,
				_ => (ZoneType?)null,
			};
			if (on.From is { } zone && from != zone)
				return false;
			if (from is { } left && on.NotFrom.Contains(left))
				return false;
		}
		return on.To is not { } to || e is ChangedZoneEvent arrived && arrived.To == to;
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

	/// <summary>
	/// "One or more" (Trigger.OneOrMore): one event per player per batch — each player's own (CR 603.2c: Wheel of
	/// Fortune's two discards trigger Tinybones twice; Laelia's and Vash's are By you, so one).
	/// </summary>
	static IEnumerable<GameEvent> OnePerBatch(
		GameState s,
		IEnumerable<GameEvent> matched,
		bool oneOrMore
	) => oneOrMore ? matched.DistinctBy(e => (e as MagicEvent)?.By(s) ?? 0) : matched;

	/// <summary>The card the event is about — what "that creature" and "that land" mean; 0 if none.</summary>
	static int EventCard(GameEvent e) =>
		e switch
		{
			EnteredBattlefieldEvent entered => entered.CardId,
			AttackedEvent attacked => attacked.CardId,
			SpellCastEvent cast => cast.CardId,
			SpellCopiedEvent copied => copied.CardId,
			LandPlayedEvent land => land.CardId,
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
		foreach (var pending in triggers)
		{
			var t = pending with { Delayed = FiredDelayedTrigger(s, pending) is not null };
			s = WithoutFiredDelayedTrigger(s, t);
			// Miracle: "you may reveal it" as it is drawn — the trigger is the reveal (CR 702.94a), so the card is known
			// (and never swapped for a guess by the AI's hidden-hand sampling while its trigger waits).
			if (t.Ability.From == ZoneType.Hand && s.HasObject(t.Source.Id))
				s = s.MakeKnown(t.Source.Id, Knowers.Everyone);
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
			var (least, most) = t.Ability.Choose.Range(
				s,
				t.Source,
				choosable.Count,
				ContextOf(t) // Riku of Many Paths: "X is the number of times you chose a mode for that spell"
			);
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
		if (FiredDelayedTrigger(s, t) is not { } fired)
			return s;
		var host = s.GetCard(t.Source.Id);
		var rest = host.GetComponents<DelayedTrigger>().Where(d => d != fired).ToList();
		var without = rest.Aggregate(
			host.WithoutComponents<DelayedTrigger>(),
			(c, d) => c.WithComponent(d)
		);
		return without.Components.Length == 0 && s.IsIn(host.Id, host.OwnerId, ZoneType.Command)
			? s.RemoveObject(host.Id)
			: s.UpdateObject(host.Id, without);
	}

	/// <summary>The delayed trigger this pending trigger is, still on its host (that object of it); null for any other.</summary>
	static DelayedTrigger? FiredDelayedTrigger(GameState s, Pending t) =>
		s.HasObject(t.Source.Id)
		&& s.GetObject(t.Source.Id) is Card host
		&& host.Incarnation == t.Source.Incarnation
			? host.GetComponents<DelayedTrigger>().FirstOrDefault(d => d.Ability == t.Ability)
			: null;

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
		// Fury: "4 damage divided as you choose among any number of target creatures and/or planeswalkers" — N words
		// chosen one at a time, as a spell's are (Division.Words), the division after them (CR 603.3d). Batroc the
		// Leaper's "each of up to X targets": the word N times, N counted as it triggers (TargetSpec.PerX).
		var divided = specs.Sum(spec => spec.Divide);
		var tooFew = false;
		specs =
		[
			.. specs.SelectMany(spec =>
			{
				if (spec.PerX is not { } n)
					return Actions.Division.Words(spec);
				var count = n.Value(s, ContextOf(t));
				// CR 603.3d: a required "X targets" needs X different objects, or the trigger is removed — never a
				// pick with nothing left to choose.
				tooFew |=
					!spec.Optional && count > spec.Candidates(s, controller, t.Source.Id).Count();
				return Enumerable
					.Range(0, count)
					.Select(i => spec with { PerX = null, NotThePrevious = i > 0 });
			}),
		];
		if (tooFew)
			return s;
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
						X = XOf(t),
						Delayed = t.Delayed,
						Divided = divided,
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

	/// <summary>A delayed trigger (CR 603.7) — StackAbility.Delayed.</summary>
	public bool Delayed { get; init; }

	/// <summary>Fury: the total its targets divide (TargetSpec.Divide); 0 when it divides nothing.</summary>
	public int Divided { get; init; }

	/// <summary>The targets read before the division was asked, and its ways — set (Splits not empty) when it runs again after.</summary>
	public ImmutableArray<int> Chosen { get; init; } = [];
	public ImmutableList<ImmutableArray<int>> Splits { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var targets = Splits.IsEmpty ? ChooseTargetChoice.Read(InputContext, Specs.Count) : Chosen;
		if (Specs.Where((spec, i) => !spec.Optional && targets[i] == 0).Any())
			return new(s);
		ImmutableArray<int> shares = [];
		if (Divided > 0 && !Splits.IsEmpty)
			shares = Splits[(int)InputContext[ChooseKeywordChoice.Chosen] - 1];
		else if (Divided > 0)
		{
			var splits = Actions.Division.Splits(Divided, targets.Count(id => id != 0));
			if (splits.Count > 1)
				return new(
					s.SpawnAction(
						new PipelineAction
						{
							Steps =
							[
								Actions.Division.Ask(s, ControllerId, splits, targets),
								this with
								{
									Chosen = targets,
									Splits = splits,
								},
							],
						}
					)
				);
			shares = splits.Single();
		}
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
					Delayed = Delayed,
					Shares = shares,
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
