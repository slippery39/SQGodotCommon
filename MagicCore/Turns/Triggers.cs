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

public record PlayerDealtDamageEvent(int PlayerId, bool Combat) : GameEvent;

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
		var fromTheBattlefield = permanents.SelectMany(source =>
			source
				.GetComponents<TriggeredAbilityComponent>()
				.SelectMany(ability =>
					s.PendingGameEvents.Where(e => Matches(s, source, ability.On, e))
						.Select(e => new Pending(
							source,
							ability,
							EventCard(e),
							(e as DiedEvent)?.LastKnown
						))
				)
		);
		var fromTheDead = s
			.PendingGameEvents.OfType<DiedEvent>()
			.SelectMany(died =>
				died.LastKnown.GetComponents<TriggeredAbilityComponent>()
					.Where(a => a.On == TriggerEvent.SelfDies)
					.Select(a => new Pending(died.LastKnown, a, died.LastKnown.Id, died.LastKnown))
			);
		return fromTheBattlefield
			.Concat(fromTheDead)
			.OrderBy(p => p.Source.ControllerId == active ? 0 : 1) // APNAP; stable within a player
			.ToList();
	}

	static bool Matches(GameState s, Card source, TriggerEvent on, GameEvent e)
	{
		bool Yours(int cardId) =>
			s.IsOnBattlefield(cardId) && s.GetCard(cardId).ControllerId == source.ControllerId;

		return (on, e) switch
		{
			(TriggerEvent.SelfEnters, EnteredBattlefieldEvent entered) => entered.CardId
				== source.Id,
			(TriggerEvent.AnotherCreatureYouControlEnters, EnteredBattlefieldEvent entered) =>
				entered.CardId != source.Id
					&& Yours(entered.CardId)
					&& s.GetCard(entered.CardId).Is(CardType.Creature),
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
			_ => 0,
		};

	/// <summary>
	/// Each trigger's controller picks its targets, then it goes on the stack; then priority goes
	/// to <paramref name="priorityPlayerId"/>. A trigger with a required target and no legal one is
	/// removed (CR 603.3d).
	/// </summary>
	public static GameState PutOnStack(GameState s, List<Pending> triggers, int priorityPlayerId)
	{
		foreach (var t in triggers)
		{
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
