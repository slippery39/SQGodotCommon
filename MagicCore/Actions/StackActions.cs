using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>What was decided while casting, carried by the card while it is on the stack.</summary>
public record OnStackComponent(int X, ImmutableArray<int> Targets, ImmutableArray<int> Modes)
	: GameComponent
{
	/// <summary>"If this spell was cast from a graveyard" — and flashback exiles it as it leaves the stack.</summary>
	public ZoneType CastFrom { get; init; } = ZoneType.Hand;
}

/// <summary>A triggered or activated ability on the stack (CR 113.7) — not a card.</summary>
public record StackAbility : GameObject
{
	public int SourceId { get; init; }

	/// <summary>The source as it was when the ability went on the stack — last-known information.</summary>
	public Card Source { get; init; } = null!;
	public int ControllerId { get; init; }
	public ImmutableList<GameAction> Effects { get; init; } = [];
	public ImmutableList<TargetSpec> Specs { get; init; } = [];
	public ImmutableArray<int> Targets { get; init; } = [];
	public int EventCardId { get; init; }
	public Card? EventSnapshot { get; init; }

	/// <summary>Ward's "it": the spell or ability its event was done by, as it was then.</summary>
	public GameObject? EventObject { get; init; }

	/// <summary>X for its effects: the X paid for the permanent, for its own "enters" trigger only (CR 107.3m).</summary>
	public int X { get; init; }
}

/// <summary>
/// CR 601.2, Arena-style: targets chosen, the card goes on the stack, the cost is paid by
/// <see cref="ManaPayment"/>, and the caster keeps priority (CR 117.3c).
/// </summary>
public record CastSpellAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int X { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

	/// <summary>Chosen modes of a modal spell, by index; empty otherwise.</summary>
	public ImmutableArray<int> Modes { get; init; } = [];

	/// <summary>A "sacrifice … or pay {N}" additional cost: what is sacrificed, or 0 to pay.</summary>
	public int SacrificeId { get; init; }

	/// <summary>Twinned Vision's flashback: "Discard a card" — the card discarded; 0 if none is owed.</summary>
	public int DiscardId { get; init; }

	/// <summary>The card beheld for a "behold … or pay" cost; 0 when paying instead.</summary>
	public int BeholdId { get; init; }

	public static bool OwesDiscard(GameState s, Card card) =>
		Costs.Flashback(s, card)?.DiscardACard == true;

	/// <summary>
	/// The total cost: X and whatever is paid instead of a sacrifice are in it, so ManaPayment is
	/// asked for X = 0 — and a reduction applies to the total (CR 601.2f).
	/// </summary>
	ManaCost CostToPay(GameState s, Card card) =>
		Costs.Of(
			s,
			PlayerId,
			card,
			X
				+ (
					card.GetComponent<SacrificeOrPayAdditionalCost>() is { } extra
					&& SacrificeId == 0
						? extra.Generic
						: 0
				)
				+ (
					card.GetComponent<BeholdOrPayAdditionalCost>() is { } behold && BeholdId == 0
						? behold.Generic
						: 0
				)
		);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card candidate
			|| !Playable.CanCastFromHere(s, candidate, PlayerId)
		)
			return ValidationResult.Invalid("You may not cast that card from where it is");
		var card = s.GetCard(CardId);
		if (OwesDiscard(s, card) ? !s.IsIn(DiscardId, PlayerId, ZoneType.Hand) : DiscardId != 0)
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (card.Is(CardType.Land))
			return ValidationResult.Invalid("Lands are played, not cast");
		if (!CanCastNow(s, card))
			return ValidationResult.Invalid("Sorcery speed: your main phase with an empty stack");
		if (X < 0 || (X > 0 && !card.ManaCost.HasX))
			return ValidationResult.Invalid("Invalid X");
		var text = card.GetComponent<SpellEffectsComponent>();
		if (!Modal.IsLegal(text?.Modes ?? [], text?.Choose ?? 1, Modes))
			return ValidationResult.Invalid("Illegal choice of modes");
		if (
			!TargetSpec.AreLegal(
				s,
				StackResolution.TextOf(card, Modes).Specs,
				Targets,
				PlayerId,
				CardId
			)
		)
			return ValidationResult.Invalid("Illegal targets");
		var extra = card.GetComponent<SacrificeOrPayAdditionalCost>();
		if (
			SacrificeId != 0
			&& (extra is null || !extra.Sacrifice.IsLegal(s, PlayerId, CardId, SacrificeId))
		)
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (
			BeholdId != 0
			&& card.GetComponent<BeholdOrPayAdditionalCost>()
				?.Candidates(s, PlayerId, CardId)
				.Contains(BeholdId) != true
		)
			return ValidationResult.Invalid("Not a card you can behold for this");
		if (ManaPayment.Find(s, PlayerId, CostToPay(s, card), 0) is null)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	bool CanCastNow(GameState s, Card card)
	{
		return card.Is(CardType.Instant)
			|| s.Has(card, "Flash")
			|| Priority.AtSorcerySpeed(s, PlayerId);
	}

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(CardId);
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		s = ManaPayment.Pay(s, PlayerId, ManaPayment.Find(s, PlayerId, CostToPay(s, card), 0)!);
		if (SacrificeId != 0)
			s = s.Sacrifice(SacrificeId);
		if (DiscardId != 0)
			s = s.Discard(PlayerId, [DiscardId]);
		if (Prepare.CreatureOf(s, card) is { } prepared)
			s = Prepare.Unprepare(s, prepared.Id); // casting the copy unprepares it
		s = s.MoveCard(CardId, s.GetWellKnownId(MagicKeys.Stack), toTop: true); // index 0 is the top
		s = s.UpdateObject(
			CardId,
			s.GetCard(CardId)
				.WithComponent(new OnStackComponent(X, Targets, Modes) { CastFrom = from })
		);
		var noncreature = card.Is(CardType.Creature) ? 0 : 1;
		s = s.Tally(
			PlayerId,
			t =>
				t with
				{
					SpellsCast = t.SpellsCast + 1,
					NoncreatureSpellsCast = t.NoncreatureSpellsCast + noncreature,
				}
		);
		// Read from the tallied state — on one line with Tally, `s` would still be the old one.
		s = s.Raise(
			new SpellCastEvent(
				CardId,
				PlayerId,
				noncreature * s.GetPlayer(PlayerId).ThisTurn.NoncreatureSpellsCast
			)
		);
		s = Turns.Triggers.RaiseTargeted(s, Targets, PlayerId, CardId);
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

/// <summary>"[Cost]: [effect]" — pays, taps if {T} is in the cost, and puts the ability on the stack.</summary>
public record ActivateAbilityAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int AbilityIndex { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

	/// <summary>The permanent sacrificed as the cost ("Sacrifice another creature:"); 0 if none.</summary>
	public int SacrificeId { get; init; }

	/// <summary>The card discarded as the cost ("Discard a card:"); 0 if none.</summary>
	public int DiscardId { get; init; }

	/// <summary>The graveyard card exiled as the cost ("Exile another creature card from your graveyard:"); 0 if none.</summary>
	public int ExileId { get; init; }

	ActivatedAbilityComponent? Ability(GameState s) =>
		s.GetCard(CardId)
			.GetComponents<ActivatedAbilityComponent>()
			.ElementAtOrDefault(AbilityIndex);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card
			|| Ability(s) is not { } ability
			|| !s.IsIn(CardId, PlayerId, ability.From)
		)
			return ValidationResult.Invalid("No such ability where you can activate it");
		var card = s.GetCard(CardId);
		// Judged as the activating player's: a card off the battlefield may still carry an old
		// controller (CR 108.4a — in a graveyard it is its owner's).
		if (
			ability.OnlyIf is { } onlyIf
			&& !onlyIf.Holds(s, Conditions.Of(card with { ControllerId = PlayerId }))
		)
			return ValidationResult.Invalid("Its condition isn't met");
		if (ability.Exhaust && card.ExhaustUsed)
			return ValidationResult.Invalid("Exhaust: activated once already");
		if (
			ability.DiscardACard
				? DiscardId == CardId || !s.IsIn(DiscardId, PlayerId, ZoneType.Hand)
				: DiscardId != 0
		)
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (
			ability.ExileFromGraveyard is { } exile
				? !exile.IsLegal(s, PlayerId, CardId, ExileId)
				: ExileId != 0
		)
			return ValidationResult.Invalid("Not a card you can exile for this");
		if (ability.TapCost && !ManaAbilities.CanPayTapCost(s, card))
			return ValidationResult.Invalid("Cannot pay {T}");
		if (ability.SorcerySpeed && !Priority.AtSorcerySpeed(s, PlayerId))
			return ValidationResult.Invalid("Activate only as a sorcery");
		if (
			ability.Sacrifice is { } sacrifice
				? !sacrifice.IsLegal(s, PlayerId, CardId, SacrificeId)
				: SacrificeId != 0
		)
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (!TargetSpec.AreLegal(s, ability.Targets, Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		if (
			ManaPayment.Find(
				s,
				PlayerId,
				Costs.OfAbility(s, ability, Targets),
				0,
				excludeId: ability.TapCost ? CardId : 0
			)
			is null
		)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var ability = Ability(s)!;
		s = ManaPayment.Pay(
			s,
			PlayerId,
			ManaPayment.Find(
				s,
				PlayerId,
				Costs.OfAbility(s, ability, Targets),
				0,
				ability.TapCost ? CardId : 0
			)!
		);
		if (ability.TapCost)
			s = s.UpdateObject(CardId, s.GetCard(CardId) with { IsTapped = true });
		if (ability.Exhaust)
			s = s.UpdateObject(CardId, s.GetCard(CardId) with { ExhaustUsed = true });
		var source = s.GetCard(CardId); // as it was when activated — the cost may sacrifice it
		IEnumerable<int> Paid(bool self, int chosen) =>
			new[] { self ? CardId : 0, chosen }.Where(id => id != 0);
		foreach (var id in Paid(ability.SacrificeSelf, SacrificeId))
			s = s.Sacrifice(id);
		s = s.Discard(PlayerId, [.. Paid(ability.DiscardSelf, DiscardId)]);
		foreach (var id in Paid(ability.ExileSelf, ExileId))
			s = s.MoveCard(id, s.ZoneId(PlayerId, ZoneType.Exile));
		s = StackResolution.Push(
			s,
			new StackAbility
			{
				Name = $"{source.Name} ability",
				SourceId = CardId,
				Source = source,
				ControllerId = PlayerId,
				Effects = ability.Effects,
				Specs = ability.Targets,
				Targets = Targets,
			}
		);
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

public static class StackResolution
{
	/// <summary>A spell's target words and steps for the chosen modes (the whole text when not modal).</summary>
	public static (ImmutableList<TargetSpec> Specs, ImmutableList<GameAction> Steps) TextOf(
		Card card,
		ImmutableArray<int> modes
	) =>
		card.GetComponent<SpellEffectsComponent>() is { } text
			? Modal.Compose(text.Targets, text.Effects, text.Modes, modes)
		: card.GetComponent<AuraComponent>() is { } aura ? ([aura.Enchant], [])
		: ([], []);

	public static GameState Push(GameState s, StackAbility ability)
	{
		var stackId = s.GetWellKnownId(MagicKeys.Stack);
		(s, var added) = s.AddObject(ability, stackId);
		return Turns.Triggers.RaiseTargeted(
			s.MoveObjectToFront(added.Id, stackId),
			ability.Targets,
			ability.ControllerId,
			added.Id
		);
	}

	/// <summary>
	/// THE counter (CR 701.6): a spell goes to its owner's graveyard — through MoveCard, so flashback
	/// still exiles it and a prepared copy still ceases to exist — and an ability is removed. A spell
	/// that "can't be countered" stays: the counter does nothing to it, and it was still cast
	/// (MtgCore's counter traps keep the same two rules).
	/// </summary>
	public static GameState Counter(GameState s, int stackObjectId)
	{
		if (
			!s.HasObject(stackObjectId)
			|| s.GetParent(stackObjectId) != s.GetWellKnownId(MagicKeys.Stack)
		)
			return s;
		return s.GetObject(stackObjectId) switch
		{
			Card spell when spell.HasComponent<CantBeCountered>() => s,
			Card spell => s.MoveCard(spell.Id, s.ZoneId(spell.OwnerId, ZoneType.Graveyard)),
			_ => s.RemoveObject(stackObjectId),
		};
	}

	/// <summary>
	/// CR 608: the top object resolves. A permanent spell enters under its controller. An instant,
	/// sorcery or ability runs its effects — unless every target it had is now illegal, when it
	/// does nothing (CR 608.2b). Targets that became illegal read as 0 to the effects. Then the
	/// active player gets priority (CR 117.3b).
	/// </summary>
	public static GameState ResolveTop(GameState s)
	{
		var stackId = s.GetWellKnownId(MagicKeys.Stack);
		var top = s.GetObject(s.GetChildrenIds(stackId).First());
		s = s.WithGame(s.GetGame() with { PriorityPlayerId = 0 });

		if (top is Card permanent && !permanent.Is(CardType.Instant | CardType.Sorcery))
		{
			var cast = permanent.GetComponent<OnStackComponent>();
			// An Aura spell whose target is now illegal doesn't resolve (CR 608.3b); else it enters on it.
			var auraHost = 0;
			var auraFizzles = false;
			if (permanent.GetComponent<AuraComponent>() is { } aura)
			{
				auraHost = cast is { Targets.Length: > 0 } ? cast.Targets[0] : 0;
				auraFizzles =
					auraHost == 0
					|| !aura.Enchant.IsLegal(s, permanent.ControllerId, permanent.Id, auraHost);
			}
			s =
				cast?.CastFrom == ZoneType.Graveyard || auraFizzles
					? s.MoveCard(permanent.Id, s.ZoneId(permanent.OwnerId, ZoneType.Graveyard)) // flashback: exiled instead (MoveCard)
					: s.EnterBattlefield(
						permanent.Id,
						permanent.ControllerId,
						cast?.X ?? 0,
						attachTo: auraHost
					);
			return Priority.Retain(s, s.GetGame().ActivePlayerId);
		}

		var (controller, sourceId, snapshot, effects, specs, chosen, x, eventCard, finish) =
			top switch
			{
				Card spell
					when spell.GetComponent<OnStackComponent>() is { } cast
						&& TextOf(spell, cast.Modes) is var text => (
					spell.ControllerId,
					spell.Id,
					spell,
					text.Steps,
					text.Specs,
					cast.Targets,
					cast.X,
					0,
					(GameAction)new FinishSpellAction { CardId = spell.Id }
				),
				StackAbility ability => (
					ability.ControllerId,
					ability.SourceId,
					ability.Source,
					ability.Effects,
					ability.Specs,
					ability.Targets,
					ability.X,
					ability.EventCardId,
					new FinishAbilityAction { AbilityId = ability.Id }
				),
				_ => throw new InvalidOperationException(
					$"Unknown stack object {top.GetType().Name}"
				),
			};

		var legal = chosen
			.Select((id, i) => id != 0 && specs[i].IsLegal(s, controller, sourceId, id) ? id : 0)
			.ToImmutableArray();
		var fizzles = chosen.Any(id => id != 0) && legal.All(id => id == 0);

		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, controller)
			.Add(ContextKeys.Source, sourceId)
			.Add(ContextKeys.SourceSnapshot, snapshot)
			.Add(ContextKeys.X, x)
			.Add(ContextKeys.Targets, legal)
			.Add(ContextKeys.AllTargets, legal)
			.Add(ContextKeys.EventCard, eventCard);
		if (top is StackAbility { EventSnapshot: { } snapshotOfEvent })
			context = context.Add(ContextKeys.EventSnapshot, snapshotOfEvent);
		if (top is StackAbility { EventObject: { } eventObject })
			context = context.Add(ContextKeys.EventObject, eventObject);
		return s.SpawnAction(
			new PipelineAction
			{
				Steps = fizzles ? [finish] : [.. effects, finish],
				PipelineContext = context,
			}
		);
	}
}

public record FinishSpellAction : GameAction
{
	public int CardId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(CardId);
		s = s.MoveCard(CardId, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
		return new ActionResult(Priority.Retain(s, s.GetGame().ActivePlayerId));
	}
}

public record FinishAbilityAction : GameAction
{
	public int AbilityId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(Priority.Retain(s.RemoveObject(AbilityId), s.GetGame().ActivePlayerId));
}
