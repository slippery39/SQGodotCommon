using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Turns;

public record EnteredBattlefieldEvent(int CardId) : GameEvent;

public record AttackedEvent(int CardId) : GameEvent;

public record SpellCastEvent(int CardId, int PlayerId) : GameEvent;

/// <summary>A creature or planeswalker went from the battlefield to a graveyard — as it last existed.</summary>
public record DiedEvent(Card LastKnown) : GameEvent;

public record PlayerDealtDamageEvent(int PlayerId, bool Combat, int SourceId) : GameEvent;

/// <summary>A step began — "at the beginning of …". Raised for the steps scripted cards trigger on.</summary>
public record StepBeganEvent(Step Step) : GameEvent;

public record BlocksEvent(int CardId) : GameEvent;

public record SurveilledEvent(int PlayerId) : GameEvent;

/// <summary>A player put loyalty counters on a planeswalker — by a cost, an effect, or its entering with them (CR 122.6).</summary>
public record LoyaltyCountersPutEvent(int PutterId) : GameEvent;

/// <summary>A spell or ability put on the stack with this card as a target (CR 115).</summary>
public record BecameTargetEvent(int CardId, int ByPlayerId) : GameEvent;

/// <summary>"Whenever you activate a loyalty ability" — Cost is +N/−N, so "removed two or more" reads it.</summary>
public record LoyaltyActivatedEvent(int PlayerId, int Cost) : GameEvent;

/// <summary>Raised by Rules.Life.Gain — one per life-gain event, however much (CR 119.9).</summary>
public record LifeGainedEvent(int PlayerId) : GameEvent;

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
	public static GameState RaiseTargeted(GameState s, IEnumerable<int> targets, int byPlayerId) =>
		targets
			.Distinct() // targeted by two words of one spell, it still becomes its target once
			.Where(id => id != 0 && s.HasObject(id) && s.GetObject(id) is Card)
			.Aggregate(s, (st, id) => st.Raise(new BecameTargetEvent(id, byPlayerId)));

	/// <summary>A triggered ability waiting to go on the stack. EventSnapshot is the card the event was about, as it last was (a dead creature's counters).</summary>
	public record Pending(
		Card Source,
		TriggeredAbilityComponent Ability,
		int EventCardId,
		Card? EventSnapshot = null
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
		var active = s.GetGame().ActivePlayerId;
		var permanents = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Battlefield)
		);
		// A card in a graveyard is controlled by its owner (CR 108.4a).
		var inGraveyards = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Graveyard)
				.Where(c => AbilitiesOf(c).Any(a => a.FromGraveyard))
				.Select(c => c with { ControllerId = c.OwnerId })
		);
		var fromTheBattlefield = permanents
			.SelectMany(source =>
				AbilitiesOf(source).Where(a => !a.FromGraveyard).Select(a => (source, a))
			)
			.Concat(
				inGraveyards.SelectMany(source =>
					AbilitiesOf(source).Where(a => a.FromGraveyard).Select(a => (source, a))
				)
			)
			.SelectMany(found =>
				s.PendingGameEvents.Where(e => Matches(s, found.source, found.a.On, e))
					.Select(e => new Pending(found.source, found.a, EventCard(e), Snapshot(s, e)))
			);
		var fromTheDead = s
			.PendingGameEvents.OfType<DiedEvent>()
			.SelectMany(died =>
				AbilitiesOf(died.LastKnown)
					.Where(a => a.On == TriggerEvent.SelfDies && !a.FromGraveyard)
					.Select(a => new Pending(died.LastKnown, a, died.LastKnown.Id, died.LastKnown))
			);
		return fromTheBattlefield
			.Concat(fromTheDead)
			.Where(p => InterveningIfHolds(s, p))
			.OrderBy(p => p.Source.ControllerId == active ? 0 : 1) // APNAP; stable within a player
			.ToList();
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
			_ => null,
		};

	/// <summary>Its own triggered abilities, and any it gained until end of turn (Lyra).</summary>
	static IEnumerable<TriggeredAbilityComponent> AbilitiesOf(Card card) =>
		card.GetComponents<TriggeredAbilityComponent>()
			.Concat(
				card.GetComponents<UntilEndOfTurn>()
					.Select(u => u.Trigger)
					.OfType<TriggeredAbilityComponent>()
			);

	static bool Matches(GameState s, Card source, TriggerEvent on, GameEvent e)
	{
		bool Yours(int cardId) =>
			s.IsOnBattlefield(cardId) && s.GetCard(cardId).ControllerId == source.ControllerId;
		var yourTurn = s.GetGame().ActivePlayerId == source.ControllerId;
		bool AnotherOfYoursEntered(EnteredBattlefieldEvent entered, CardType types) =>
			entered.CardId != source.Id
			&& Yours(entered.CardId)
			&& s.GetCard(entered.CardId).Is(types);

		return (on, e) switch
		{
			(TriggerEvent.SelfEnters, EnteredBattlefieldEvent entered) => entered.CardId
				== source.Id,
			(TriggerEvent.AnotherCreatureYouControlEnters, EnteredBattlefieldEvent entered) =>
				AnotherOfYoursEntered(entered, CardType.Creature),
			(
				TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlEnters,
				EnteredBattlefieldEvent entered
			) => AnotherOfYoursEntered(entered, CardType.Creature | CardType.Planeswalker),
			(TriggerEvent.LandYouControlEnters, EnteredBattlefieldEvent entered) => Yours(
				entered.CardId
			) && s.GetCard(entered.CardId).Is(CardType.Land),
			(TriggerEvent.SelfAttacks, AttackedEvent attacked) => attacked.CardId == source.Id,
			(TriggerEvent.YouCastNoncreatureSpell, SpellCastEvent cast) => cast.PlayerId
				== source.ControllerId
				&& !s.GetCard(cast.CardId).Is(CardType.Creature),
			(TriggerEvent.YouGainLife, LifeGainedEvent gained) => gained.PlayerId
				== source.ControllerId,
			(TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies, DiedEvent died) =>
				died.LastKnown.Id != source.Id
					&& died.LastKnown.ControllerId == source.ControllerId
					&& died.LastKnown.Is(CardType.Creature | CardType.Planeswalker),
			(TriggerEvent.CreatureYouControlDies, DiedEvent died) => died.LastKnown.ControllerId
				== source.ControllerId
				&& died.LastKnown.Is(CardType.Creature),
			(TriggerEvent.OpponentDealtNoncombatDamage, PlayerDealtDamageEvent damaged) =>
				damaged.PlayerId != source.ControllerId && !damaged.Combat,
			(TriggerEvent.SelfDealsCombatDamageToPlayer, PlayerDealtDamageEvent damaged) =>
				damaged.Combat && damaged.SourceId == source.Id,
			(TriggerEvent.YourUpkeep, StepBeganEvent began) => began.Step == Step.Upkeep
				&& yourTurn,
			(TriggerEvent.EachUpkeep, StepBeganEvent began) => began.Step == Step.Upkeep,
			(TriggerEvent.OpponentsDrawStep, StepBeganEvent began) => began.Step == Step.Draw
				&& !yourTurn,
			(TriggerEvent.YourEndStep, StepBeganEvent began) => began.Step == Step.End && yourTurn,
			(TriggerEvent.EachEndStep, StepBeganEvent began) => began.Step == Step.End,
			(TriggerEvent.YouSurveil, SurveilledEvent surveilled) => surveilled.PlayerId
				== source.ControllerId,
			(TriggerEvent.YouPutLoyaltyCounters, LoyaltyCountersPutEvent put) => put.PutterId
				== source.ControllerId,
			(TriggerEvent.CreatureYouControlEnters, EnteredBattlefieldEvent entered) => Yours(
				entered.CardId
			) && s.GetCard(entered.CardId).Is(CardType.Creature),
			(TriggerEvent.BeginningOfCombatOnYourTurn, StepBeganEvent began) => began.Step
				== Step.BeginCombat
				&& yourTurn,
			(TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility, BecameTargetEvent targeted) =>
				targeted.CardId == source.Id && targeted.ByPlayerId != source.ControllerId,
			(TriggerEvent.OpponentsCreatureWithPowerOrToughness1OrLessBlocks, BlocksEvent blocks) =>
				s.IsOnBattlefield(blocks.CardId)
					&& s.GetCard(blocks.CardId) is var blocker
					&& blocker.ControllerId != source.ControllerId
					&& (s.Power(blocker) <= 1 || s.Toughness(blocker) <= 1),
			(TriggerEvent.YouActivateLoyaltyAbility, LoyaltyActivatedEvent activated) =>
				activated.PlayerId == source.ControllerId,
			(
				TriggerEvent.YouActivateLoyaltyAbilityRemovingTwoOrMore,
				LoyaltyActivatedEvent activated
			) => activated.PlayerId == source.ControllerId && activated.Cost <= -2,
			_ => false,
		};
	}

	/// <summary>The card the event is about — what "that creature" and "that land" mean; 0 if none.</summary>
	static int EventCard(GameEvent e) =>
		e switch
		{
			EnteredBattlefieldEvent entered => entered.CardId,
			AttackedEvent attacked => attacked.CardId,
			SpellCastEvent cast => cast.CardId,
			DiedEvent died => died.LastKnown.Id,
			BlocksEvent blocks => blocks.CardId,
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
			if (choosable.Count < t.Ability.Choose)
				continue; // not enough modes can be chosen: the ability is removed (CR 700.2a)
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new ChooseModesChoice(
							t.Source.ControllerId,
							t.Source.Id,
							t.Ability,
							choosable
						),
						new QueueModalTriggerAction { Trigger = t },
					],
				}
			);
		}
		return s.SpawnAction(new GivePriorityAction { PlayerId = priorityPlayerId });
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
			.Select(m => m.i)
			.ToImmutableList();

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
						X = t.Ability.On == TriggerEvent.SelfEnters ? t.Source.CastX : 0,
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
					X = X,
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
		return new(Triggers.QueueTargets(s, Trigger, specs, steps));
	}
}

public record GivePriorityAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(TurnStructure.GivePriority(s, PlayerId));
}
