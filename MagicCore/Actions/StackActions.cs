using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
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

	/// <summary>Theorist's Proxy: this cast can't be countered — gone with the stack object, so a recast can be.</summary>
	public bool CantBeCountered { get; init; }
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

	/// <summary>A triggered ability, not an activated one — Consign to Memory counters only these.</summary>
	public bool Triggered { get; init; }
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

	/// <summary>Omnipresence: cast without paying its mana cost — X is 0 (CR 107.3c).</summary>
	public bool WithoutPayingManaCost { get; init; }

	/// <summary>Consign to Memory: how many times its replicate cost is paid — that many copies.</summary>
	public int Replicate { get; init; }

	/// <summary>
	/// Cast by an effect as it resolves (CastNow — Chandra, Uldaros): no priority, no timing, and the
	/// effect is the permission to cast it from where it is (and, free, to skip its mana cost). Every
	/// other rule — targets, modes, X, additional costs, "can't cast unless", payment — still applies.
	/// </summary>
	public bool DuringResolution { get; init; }

	/// <summary>What casting it costs beyond mana: its additional costs, then its flashback's when cast that way.</summary>
	public static ImmutableList<AdditionalCost> CostsOf(GameState s, Card card) =>
		[
			.. card.GetComponent<AdditionalCostsComponent>()?.Costs ?? [],
			.. Costs.Flashback(s, card)?.Costs ?? [],
		];

	int Chosen(CostSlot slot) =>
		slot switch
		{
			CostSlot.None => 0,
			CostSlot.Sacrifice => SacrificeId,
			CostSlot.Discard => DiscardId,
			CostSlot.Behold => BeholdId,
			_ => throw new InvalidOperationException($"A spell's cost can't pick into {slot}"),
		};

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
				+ Replicate * (card.GetComponent<ReplicateComponent>()?.Generic ?? 0)
				+ CostList.OrPayOwed(CostsOf(s, card), Chosen),
			free: WithoutPayingManaCost,
			targets: Targets
		);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (!DuringResolution && Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card candidate
			|| (
				DuringResolution
					? !Playable.CastRestrictionHolds(s, candidate, PlayerId)
					: !Playable.CanCastFromHere(s, candidate, PlayerId)
			)
		)
			return ValidationResult.Invalid("You may not cast that card from where it is");
		var card = s.GetCard(CardId);
		if (Turns.Combat.LockedDown(s))
			return ValidationResult.Invalid("No spells now: combat lockdown or split second");
		var costs = CostsOf(s, card);
		if (!CostList.Fits(costs, CostSlot.Discard, DiscardId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (card.Is(CardType.Land))
			return ValidationResult.Invalid("Lands are played, not cast");
		if (!DuringResolution && !CanCastNow(s, card))
			return ValidationResult.Invalid("Sorcery speed: your main phase with an empty stack");
		if (X < 0 || (X > 0 && (!card.ManaCost.HasX || WithoutPayingManaCost)))
			return ValidationResult.Invalid("Invalid X");
		if (Replicate < 0 || (Replicate > 0 && !card.HasComponent<ReplicateComponent>()))
			return ValidationResult.Invalid("It has no replicate cost");
		if (
			WithoutPayingManaCost
			&& !DuringResolution
			&& !Playable.MayCastWithoutPaying(s, card, PlayerId)
		)
			return ValidationResult.Invalid("You may not cast that without paying its mana cost");
		var text = card.GetComponent<SpellEffectsComponent>();
		if (!Modal.IsLegal(text?.Modes ?? [], text?.Choose ?? 1, Modes, text?.ChooseAtLeast))
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
		if (!CostList.Fits(costs, CostSlot.Sacrifice, SacrificeId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (!CostList.Fits(costs, CostSlot.Behold, BeholdId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can behold for this");
		if (costs.Any(c => !c.CanPay(s, PlayerId, CardId)))
			return ValidationResult.Invalid("Cannot pay the additional cost");
		if (
			ManaPayment.Find(
				s,
				PlayerId,
				CostToPay(s, card),
				0,
				convoke: s.Has(card, "Convoke"),
				forSpell: card,
				alsoExclude: [.. costs.SelectMany(c => c.Uses(s, PlayerId, CardId, Chosen(c.Slot)))]
			)
			is null
		)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	bool CanCastNow(GameState s, Card card)
	{
		return Priority.AtInstantSpeed(s, card) || Priority.AtSorcerySpeed(s, PlayerId);
	}

	public override ActionResult Execute(GameState s) => new(Priority.Retain(Cast(s), PlayerId));

	/// <summary>Pays and puts it on the stack — the priority action keeps priority after; a resolution goes on.</summary>
	public GameState Cast(GameState s)
	{
		var card = s.GetCard(CardId);
		var costs = CostsOf(s, card); // read before paying: flashback's are known only in the graveyard
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		s = ManaPayment.Pay(
			s,
			PlayerId,
			ManaPayment.Find(
				s,
				PlayerId,
				CostToPay(s, card),
				0,
				convoke: s.Has(card, "Convoke"),
				forSpell: card,
				alsoExclude: [.. costs.SelectMany(c => c.Uses(s, PlayerId, CardId, Chosen(c.Slot)))]
			)!
		);
		foreach (var cost in costs)
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		s = PutOnStack(s, PlayerId, CardId, X, Targets, Modes, from);
		if (Replicate == 0)
			return s;
		// Replicate (CR 702.56a): "when you cast this spell, copy it for each time you paid its
		// replicate cost" — a triggered ability above it; CopySpell lets each copy choose new targets.
		// ponytail: pushed here, not through Triggers.Collect — other "whenever you cast" triggers go
		// above it with no ordering choice; route it through Collect if a card makes the order matter.
		var spell = s.GetCard(CardId);
		return StackResolution.Push(
			s,
			new StackAbility
			{
				Name = $"{spell.Name} replicate",
				SourceId = CardId,
				Source = spell,
				ControllerId = PlayerId,
				Effects =
				[
					.. Enumerable.Repeat<GameAction>(new CopySpell(Ref.EventCard), Replicate),
				],
				EventCardId = CardId,
				EventSnapshot = spell,
				Triggered = true,
			}
		);
	}

	/// <summary>
	/// THE way a cast spell goes on the stack (CR 601.2a, 601.2i) once its costs are paid — from a
	/// priority action or in the middle of a resolution (Uldaros, Chandra): controlled by its caster,
	/// what was decided kept on it, counted for "this turn", and "whenever you cast" told.
	/// </summary>
	public static GameState PutOnStack(
		GameState s,
		int playerId,
		int cardId,
		int x,
		ImmutableArray<int> targets,
		ImmutableArray<int> modes,
		ZoneType from
	)
	{
		var card = s.GetCard(cardId);
		if (Prepare.CreatureOf(s, card) is { } prepared)
			s = Prepare.Unprepare(s, prepared.Id); // casting the copy unprepares it
		s = s.MoveCard(cardId, s.GetWellKnownId(MagicKeys.Stack), toTop: true); // index 0 is the top
		var proxies = s.ThisTurnEffects<NextSpellCantBeCountered>(playerId).ToList();
		s = s.UpdateObject(
			cardId,
			// Its caster controls it (CR 601.2a) — Null Summoner casts the opponent's card.
			(s.GetCard(cardId) with { ControllerId = playerId }).WithComponent(
				new OnStackComponent(x, targets, modes)
				{
					CastFrom = from,
					// Theorist's Proxy: "the next spell you cast this turn can't be countered" — this one.
					CantBeCountered = proxies.Count > 0,
				}
			)
		);
		var noncreature = card.Is(CardType.Creature) ? 0 : 1;
		s = s.Tally(
			playerId,
			t =>
				t with
				{
					SpellsCast = t.SpellsCast + 1,
					NoncreatureSpellsCast = t.NoncreatureSpellsCast + noncreature,
				}
		);
		// "When you next cast an instant or sorcery spell this turn": this one takes every waiting copy.
		var copies = card.Is(CardType.Instant | CardType.Sorcery)
			? s.ThisTurnEffects<CopyNextInstantOrSorcery>(playerId).ToList()
			: [];
		foreach (var used in proxies.Concat(copies))
			s = s.RemoveObject(used.Id);
		// Read from the tallied state — on one line with Tally, `s` would still be the old one.
		s = s.Raise(
			new SpellCastEvent(
				cardId,
				playerId,
				noncreature * s.GetPlayer(playerId).ThisTurn.NoncreatureSpellsCast,
				copies.Count
			)
		);
		return Turns.Triggers.RaiseTargeted(s, targets, playerId, cardId);
	}
}

/// <summary>"[Cost]: [effect]" — pays, taps if {T} is in the cost, and puts the ability on the stack.</summary>
public record ActivateAbilityAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int AbilityIndex { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

	/// <summary>The modes chosen as it is activated (a modal ability's); empty otherwise.</summary>
	public ImmutableArray<int> Modes { get; init; } = [];

	/// <summary>The permanent sacrificed as the cost ("Sacrifice another creature:"); 0 if none.</summary>
	public int SacrificeId { get; init; }

	/// <summary>The card discarded as the cost ("Discard a card:"); 0 if none.</summary>
	public int DiscardId { get; init; }

	/// <summary>The graveyard card exiled as the cost ("Exile another creature card from your graveyard:"); 0 if none.</summary>
	public int ExileId { get; init; }

	/// <summary>The creature tapped as the cost (Station: "Tap another creature you control"); 0 if none.</summary>
	public int TapId { get; init; }

	/// <summary>An ability's target words and steps for these modes (the whole text when not modal).</summary>
	public static (ImmutableList<TargetSpec> Specs, ImmutableList<GameAction> Steps) TextOf(
		ActivatedAbilityComponent ability,
		ImmutableArray<int> modes
	) => Modal.Compose(ability.Targets, ability.Effects, ability.Modes, modes);

	int Chosen(CostSlot slot) =>
		slot switch
		{
			CostSlot.None => 0,
			CostSlot.Sacrifice => SacrificeId,
			CostSlot.Discard => DiscardId,
			CostSlot.Exile => ExileId,
			CostSlot.Tap => TapId,
			_ => throw new InvalidOperationException($"An ability's cost can't pick into {slot}"),
		};

	/// <summary>Permanents the non-mana part of the cost uses — never mana for the same activation.</summary>
	List<int> CostPermanents(GameState s, ActivatedAbilityComponent ability) =>
		[.. ability.Costs.SelectMany(c => c.Uses(s, PlayerId, CardId, Chosen(c.Slot)))];

	ActivatedAbilityComponent? Ability(GameState s) =>
		s.ActivatedAbilitiesOf(s.GetCard(CardId)).ElementAtOrDefault(AbilityIndex);

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
		if (Turns.Combat.LockedDown(s))
			return ValidationResult.Invalid("No abilities now: combat lockdown or split second");
		// Judged as the activating player's: a card off the battlefield may still carry an old
		// controller (CR 108.4a — in a graveyard it is its owner's).
		if (
			ability.OnlyIf is { } onlyIf
			&& !onlyIf.Holds(s, Conditions.Of(card with { ControllerId = PlayerId }))
		)
			return ValidationResult.Invalid("Its condition isn't met");
		if (ability.Exhaust && card.ExhaustUsed)
			return ValidationResult.Invalid("Exhaust: activated once already");
		var costs = ability.Costs;
		if (costs.Any(c => c is TapUntappedCost && !c.CanPay(s, PlayerId, CardId)))
			return ValidationResult.Invalid("Not enough untapped permanents to tap");
		if (!CostList.Fits(costs, CostSlot.Tap, TapId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can tap for this");
		if (!CostList.Fits(costs, CostSlot.Discard, DiscardId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (!CostList.Fits(costs, CostSlot.Exile, ExileId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can exile for this");
		if (costs.Any(c => c is TapSelfCost && !c.CanPay(s, PlayerId, CardId)))
			return ValidationResult.Invalid("Cannot pay {T}");
		if (ability.SorcerySpeed && !Priority.AtSorcerySpeed(s, PlayerId))
			return ValidationResult.Invalid("Activate only as a sorcery");
		if (!CostList.Fits(costs, CostSlot.Sacrifice, SacrificeId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (!Modal.IsLegal(ability.Modes, ability.Choose, Modes))
			return ValidationResult.Invalid("Not a legal choice of modes");
		if (!TargetSpec.AreLegal(s, TextOf(ability, Modes).Specs, Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		if (
			ManaPayment.Find(
				s,
				PlayerId,
				Costs.OfAbility(s, ability, Targets, PlayerId, CardId),
				0,
				alsoExclude: CostPermanents(s, ability)
			)
			is null
		)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var ability = Ability(s)!;
		var mana = ManaPayment.Find(
			s,
			PlayerId,
			Costs.OfAbility(s, ability, Targets, PlayerId, CardId),
			0,
			alsoExclude: CostPermanents(s, ability)
		)!;
		// Station's "its power" is read as the ability resolves — from this snapshot once the creature
		// left (last-known information). ponytail: as it was TAPPED, not as it left — a pump between
		// the two is missed (Shayne: rare; full LKI if play shows otherwise, DesignNotes).
		var tapped = TapId == 0 ? null : s.GetCard(TapId);
		// The taps on the state the mana search excluded them from — the same permanents; then the
		// mana, which never uses them.
		foreach (var cost in ability.Costs.Where(c => c.Taps))
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		s = ManaPayment.Pay(s, PlayerId, mana);
		if (ability.Exhaust)
			s = s.UpdateObject(CardId, s.GetCard(CardId) with { ExhaustUsed = true });
		var source = s.GetCard(CardId); // as it was when activated — the cost may sacrifice it
		foreach (var cost in ability.Costs.Where(c => !c.Taps))
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		var (specs, steps) = TextOf(ability, Modes);
		s = StackResolution.Push(
			s,
			new StackAbility
			{
				Name = $"{source.Name} ability",
				SourceId = CardId,
				Source = source,
				ControllerId = PlayerId,
				Effects = steps,
				Specs = specs,
				Targets = Targets,
				EventCardId = TapId, // "its power": the creature tapped for it
				EventSnapshot = tapped,
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

	/// <summary>"This spell can't be countered" — printed, or given to this one cast (Theorist's Proxy).</summary>
	public static bool CanBeCountered(Card spell) =>
		!spell.HasComponent<CantBeCountered>()
		&& spell.GetComponent<OnStackComponent>() is not { CantBeCountered: true };

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
			Card spell when !CanBeCountered(spell) => s,
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
			if (permanent.IsCopy) // CR 707.10f: a copy of a permanent spell becomes a token (Uldaros)
				s = s.UpdateObject(permanent.Id, permanent with { IsCopy = false, IsToken = true });
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
			if (cast?.CastFrom == ZoneType.Graveyard || auraFizzles)
				return Priority.Retain(
					s.MoveCard(permanent.Id, s.ZoneId(permanent.OwnerId, ZoneType.Graveyard)), // flashback: exiled instead (MoveCard)
					s.GetGame().ActivePlayerId
				);
			// Two steps, so a choice it makes as it enters (Flesh Duplicate's copy, Room of Refuge's
			// colour) is answered before anyone gets priority — and before the state check sees a 0/0.
			return s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new EnterFromStackAction(
							permanent.Id,
							permanent.ControllerId,
							cast?.X ?? 0,
							auraHost,
							cast is not null
						),
						new ActivePlayerGetsPriorityAction(),
					],
				}
			);
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
		if (s.GetParent(CardId) == s.GetWellKnownId(MagicKeys.Stack)) // Sphinx's Approach may have exiled itself
			s = s.MoveCard(CardId, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
		return new ActionResult(Priority.Retain(s, s.GetGame().ActivePlayerId));
	}
}

/// <summary>A resolving permanent spell enters the battlefield (CR 608.3) — then ActivePlayerGetsPriorityAction.</summary>
public record EnterFromStackAction(int CardId, int ControllerId, int X, int AttachTo, bool Cast)
	: GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(s.EnterBattlefield(CardId, ControllerId, X, attachTo: AttachTo, cast: Cast));
}

/// <summary>After a resolution, the active player gets priority (CR 117.3b).</summary>
public record ActivePlayerGetsPriorityAction : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(Priority.Retain(s, s.GetGame().ActivePlayerId));
}

public record FinishAbilityAction : GameAction
{
	public int AbilityId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(Priority.Retain(s.RemoveObject(AbilityId), s.GetGame().ActivePlayerId));
}
