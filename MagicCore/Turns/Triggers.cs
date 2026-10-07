using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Turns;

public record EnteredBattlefieldEvent(int CardId) : GameEvent;

public record AttackedEvent(int CardId) : GameEvent;

public record SpellCastEvent(int CardId, int PlayerId) : GameEvent;

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

	public record Pending(Card Source, TriggeredAbilityComponent Ability, int EventCardId);

	/// <summary>
	/// Every ability that triggered on the staged events, the active player's first (CR 101.4 APNAP:
	/// they go on the stack first, so the other player's resolve first).
	///
	/// ponytail: sources are the permanents on the battlefield NOW. That is right for every trigger
	/// built so far; "dies"/"leaves" triggers need the permanent as it last was — add a look-back here.
	/// </summary>
	public static List<Pending> Collect(GameState s)
	{
		var active = s.GetGame().ActivePlayerId;
		var permanents = new[] { active, s.OpponentOf(active) }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Battlefield)
		);
		return permanents
			.SelectMany(source =>
				source
					.GetComponents<TriggeredAbilityComponent>()
					.SelectMany(ability =>
						s.PendingGameEvents.Where(e => Matches(s, source, ability.On, e))
							.Select(e => new Pending(source, ability, EventCard(e)))
					)
			)
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
			var controller = t.Source.ControllerId;
			var unchoosable = t.Ability.Targets.Any(spec =>
				!spec.Optional && !spec.Candidates(s, controller, t.Source.Id).Any()
			);
			if (unchoosable)
				continue;
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						.. t.Ability.Targets.Select(
							(spec, i) => new ChooseTargetChoice(controller, t.Source.Id, spec, i)
						),
						new PutTriggerOnStackAction
						{
							Source = t.Source,
							ControllerId = controller,
							Ability = t.Ability,
							EventCardId = t.EventCardId,
						},
					],
				}
			);
		}
		return s.SpawnAction(new GivePriorityAction { PlayerId = priorityPlayerId });
	}
}

public record PutTriggerOnStackAction : GameAction
{
	/// <summary>The source as it was when it triggered — it may be gone by now (a token that died).</summary>
	public Card Source { get; init; } = null!;
	public int ControllerId { get; init; }
	public TriggeredAbilityComponent Ability { get; init; } = null!;
	public int EventCardId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var targets = ChooseTargetChoice.Read(InputContext, Ability.Targets.Count);
		if (Ability.Targets.Where((spec, i) => !spec.Optional && targets[i] == 0).Any())
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
					Effects = Ability.Effects,
					Specs = Ability.Targets,
					Targets = targets,
					EventCardId = EventCardId,
				}
			)
		);
	}
}

public record GivePriorityAction : GameAction
{
	public int PlayerId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(TurnStructure.GivePriority(s, PlayerId));
}
