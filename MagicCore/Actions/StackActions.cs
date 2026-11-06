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
	/// <summary>Sunburst (Engineered Explosives): the colours of mana spent to cast it — a copy spent none (CR 702.44c).</summary>
	public int ColorsSpent { get; init; }

	/// <summary>"If this spell was cast from a graveyard" — and flashback exiles it as it leaves the stack.</summary>
	public ZoneType CastFrom { get; init; } = ZoneType.Hand;

	/// <summary>Overflow Submatrix, The Holodeck: cast from a graveyard by a permanent's permission — not flashback, so not exiled leaving the stack; it resolves onto the battlefield.</summary>
	public bool ByPermission { get; init; }

	/// <summary>Overflow Submatrix: "that artifact creature enters with a finality counter on it".</summary>
	public bool Finality { get; init; }

	/// <summary>Theorist's Proxy: this cast can't be countered — gone with the stack object, so a recast can be.</summary>
	public bool CantBeCountered { get; init; }

	/// <summary>Kicker: "if this spell was kicked" — a copy of it is kicked too (CR 707.10).</summary>
	public bool Kicked { get; init; }

	/// <summary>First Contact: it is the first spell its caster cast this game — a copy wasn't cast (CopySpell clears it).</summary>
	public bool FirstSpellThisGame { get; init; }
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

	/// <summary>Beam me up: the creature returned to its owner's hand; 0 if none is owed.</summary>
	public int ReturnId { get; init; }

	/// <summary>Omnipresence: cast without paying its mana cost — X is 0 (CR 107.3c).</summary>
	public bool WithoutPayingManaCost { get; init; }

	/// <summary>Consign to Memory: how many times its replicate cost is paid — that many copies.</summary>
	public int Replicate { get; init; }

	/// <summary>Kicker: its kicker cost is paid too (KickerComponent).</summary>
	public bool Kicked { get; init; }

	/// <summary>
	/// Cast by an effect as it resolves (CastNow — Chandra, Uldaros): no priority, no timing, and the
	/// effect is the permission to cast it from where it is (and, free, to skip its mana cost). Every
	/// other rule — targets, modes, X, additional costs, "can't cast unless", payment — still applies.
	/// </summary>
	public bool DuringResolution { get; init; }

	/// <summary>
	/// Dominion Infiltration: "Choose X target creatures your opponents control" — the X targets are a
	/// choice made as it is cast (ChooseXTargetsChoice), before it is paid (CR 601.2c), not one legal cast
	/// per set: 2^n casts hung the fuzz probe (Shayne, 2026-11-05). One cast per X; Targets empty until chosen.
	/// </summary>
	public bool ChoosesTargets { get; init; }

	/// <summary>What casting it costs beyond mana: its additional costs, then its flashback's when cast that way, then its kicker's when kicked.</summary>
	public static ImmutableList<AdditionalCost> CostsOf(
		GameState s,
		Card card,
		bool kicked = false
	) =>
		[
			.. card.GetComponent<AdditionalCostsComponent>()?.Costs ?? [],
			.. Costs.Flashback(s, card)?.Costs ?? [],
			.. kicked ? card.GetComponent<KickerComponent>()?.Costs ?? [] : [],
		];

	int Chosen(CostSlot slot) =>
		slot switch
		{
			CostSlot.None => 0,
			CostSlot.Sacrifice => SacrificeId,
			CostSlot.Discard => DiscardId,
			CostSlot.Behold => BeholdId,
			CostSlot.Return => ReturnId,
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
				+ CostList.OrPayOwed(CostsOf(s, card, Kicked), Chosen),
			free: WithoutPayingManaCost,
			targets: Targets,
			added: Kicked ? card.GetComponent<KickerComponent>()?.Mana : null
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
		if (Kicked && !card.HasComponent<KickerComponent>())
			return ValidationResult.Invalid("It has no kicker");
		var costs = CostsOf(s, card, Kicked);
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
		var specs = StackResolution.TextOf(card, Modes, Kicked, X).Specs;
		if (
			ChoosesTargets
				? !(Targets.IsEmpty && ChooseXTargetsChoice.CanChoose(s, PlayerId, CardId, specs))
				: !TargetSpec.AreLegal(s, specs, Targets, PlayerId, CardId)
		)
			return ValidationResult.Invalid("Illegal targets");
		if (!CostList.Fits(costs, CostSlot.Sacrifice, SacrificeId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (!CostList.Fits(costs, CostSlot.Behold, BeholdId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can behold for this");
		if (!CostList.Fits(costs, CostSlot.Return, ReturnId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a creature you can return for this");
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

	public override ActionResult Execute(GameState s) =>
		new(ChoosesTargets ? ChooseTargets(s, retain: true) : Priority.Retain(Cast(s), PlayerId));

	/// <summary>Dominion Infiltration: the X targets chosen, then this cast with them.</summary>
	GameState ChooseTargets(GameState s, bool retain) =>
		s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					new ChooseXTargetsChoice(PlayerId, CardId, X, Modes, Kicked),
					new CastWithChosenTargetsAction(this with { ChoosesTargets = false }, retain),
				],
			}
		);

	/// <summary>Pays and puts it on the stack — the priority action keeps priority after; a resolution goes on.</summary>
	public GameState Cast(GameState s)
	{
		if (ChoosesTargets)
			return ChooseTargets(s, retain: false);
		var card = s.GetCard(CardId);
		var costs = CostsOf(s, card, Kicked); // read before paying: flashback's are known only in the graveyard
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		// Zefram Cochrane, Overflow Submatrix, The Holodeck: "once during each of your turns" — this one is used.
		var permission = from switch
		{
			ZoneType.Library => Playable.LibraryTopPermission(s, card, PlayerId),
			ZoneType.Graveyard or ZoneType.Sideboard => Playable.GraveyardPermission(
				s,
				card,
				PlayerId
			),
			_ => null,
		};
		if (permission is not null)
			s = s.UpdateObject(
				permission.Id,
				permission with
				{
					PermissionUsedOn = s.GetGame().TurnNumber,
				}
			);
		var sunburst = card.HasComponent<Sunburst>();
		var payment = ManaPayment.Find(
			s,
			PlayerId,
			CostToPay(s, card),
			0,
			convoke: s.Has(card, "Convoke"),
			forSpell: card,
			alsoExclude: [.. costs.SelectMany(c => c.Uses(s, PlayerId, CardId, Chosen(c.Slot)))],
			mostColors: sunburst
		)!;
		s = ManaPayment.Pay(s, PlayerId, payment);
		foreach (var cost in costs)
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		s = PutOnStack(s, PlayerId, CardId, X, Targets, Modes, from, Kicked);
		if (permission is not null && from != ZoneType.Library)
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponentReplaced(
						s.GetCard(CardId).GetComponent<OnStackComponent>()! with
						{
							ByPermission = true,
							Finality = s.AbilitiesOf<MayPlayFromYourGraveyard>(permission)
								.Any(m => m.Finality),
						}
					)
			);
		if (sunburst)
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponentReplaced(
						s.GetCard(CardId).GetComponent<OnStackComponent>()! with
						{
							ColorsSpent = ManaPayment.ColorsSpent(payment),
						}
					)
			);
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
		ZoneType from,
		bool kicked = false
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
					Kicked = kicked,
					FirstSpellThisGame = s.GetPlayer(playerId).SpellsCastThisGame == 0,
				}
			)
		);
		s = s.WithPlayer(
			s.GetPlayer(playerId) with
			{
				SpellsCastThisGame = s.GetPlayer(playerId).SpellsCastThisGame + 1,
			}
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
		if (!modes.IsEmpty)
			s = Turns.FacedDilemmaEvent.Face(s, playerId);
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

	/// <summary>Worf: "Tap two untapped artifacts and/or creatures you control" — the second one; 0 if none.</summary>
	public int SecondTapId { get; init; }

	/// <summary>Collation Trimatrix: "Tap three untapped artifact creatures you control" — the whole set, in no order; empty if none.</summary>
	public ImmutableArray<int> TapIds { get; init; } = [];

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
			CostSlot.SecondTap => SecondTapId,
			_ => throw new InvalidOperationException($"An ability's cost can't pick into {slot}"),
		};

	/// <summary>What a cost picked: its one id, or the whole set of TapIds.</summary>
	IEnumerable<int> Picks(AdditionalCost cost) =>
		cost.Slot == CostSlot.TapSet ? TapIds : [Chosen(cost.Slot)];

	/// <summary>Permanents the non-mana part of the cost uses — never mana for the same activation.</summary>
	List<int> CostPermanents(GameState s, ActivatedAbilityComponent ability) =>
		[
			.. ability.Costs.SelectMany(c =>
				Picks(c).SelectMany(id => c.Uses(s, PlayerId, CardId, id))
			),
		];

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
		if (ability.OncePerTurn && card.ActivatedOncePerTurnOn == s.GetGame().TurnNumber)
			return ValidationResult.Invalid("Activated once this turn already");
		var costs = ability.Costs;
		if (costs.Any(c => c is TapUntappedCost && !c.CanPay(s, PlayerId, CardId)))
			return ValidationResult.Invalid("Not enough untapped permanents to tap");
		if (!CostList.Fits(costs, CostSlot.Tap, TapId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can tap for this");
		if (
			!CostList.Fits(costs, CostSlot.SecondTap, SecondTapId, s, PlayerId, CardId)
			|| (SecondTapId != 0 && SecondTapId == TapId)
		)
			return ValidationResult.Invalid("Not a second permanent you can tap for this");
		if (
			CostList.In(costs, CostSlot.TapSet) is TapUntappedCost set
				? TapIds.Length != set.Count
					|| TapIds.Distinct().Count() != TapIds.Length
					|| TapIds.Except(set.Candidates(s, PlayerId, CardId)).Any()
				: !TapIds.IsDefaultOrEmpty
		)
			return ValidationResult.Invalid("Not permanents you can tap for this");
		if (!CostList.Fits(costs, CostSlot.Discard, DiscardId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (!CostList.Fits(costs, CostSlot.Exile, ExileId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can exile for this");
		// {T}, and Lieutenant Commander Data's "remove three counters from among" (the picking is after).
		if (costs.Any(c => c is not TapUntappedCost && !c.CanPay(s, PlayerId, CardId)))
			return ValidationResult.Invalid("Cannot pay its cost");
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
				alsoExclude: CostPermanents(s, ability),
				forAbilityOf: s.GetCard(CardId)
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
			alsoExclude: CostPermanents(s, ability),
			forAbilityOf: s.GetCard(CardId)
		)!;
		// Station's "its power" is read as the ability resolves — from this snapshot once the creature
		// left (last-known information). ponytail: as it was TAPPED, not as it left — a pump between
		// the two is missed (Shayne: rare; full LKI if play shows otherwise, DesignNotes). With nothing
		// tapped the event card is the creature sacrificed — Gowron: "If you sacrificed a Klingon this way".
		var eventCardId = TapId != 0 ? TapId : SacrificeId;
		var tapped = eventCardId == 0 ? null : s.GetCard(eventCardId);
		// The taps on the state the mana search excluded them from — the same permanents; then the
		// mana, which never uses them.
		foreach (var cost in ability.Costs.Where(c => c.Taps))
		foreach (var picked in Picks(cost))
			s = cost.Pay(s, PlayerId, CardId, picked);
		s = ManaPayment.Pay(s, PlayerId, mana);
		if (ability.Exhaust)
			s = s.UpdateObject(CardId, s.GetCard(CardId) with { ExhaustUsed = true });
		if (ability.OncePerTurn)
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId) with
				{
					ActivatedOncePerTurnOn = s.GetGame().TurnNumber,
				}
			);
		var source = s.GetCard(CardId); // as it was when activated — the cost may sacrifice it
		foreach (var cost in ability.Costs.Where(c => !c.Taps))
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		var (specs, steps) = TextOf(ability, Modes);
		if (!Modes.IsEmpty)
			s = Turns.FacedDilemmaEvent.Face(s, PlayerId);
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
				EventCardId = eventCardId, // "its power": the creature tapped for it, or sacrificed
				EventSnapshot = tapped,
			}
		);
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

public static class StackResolution
{
	/// <summary>A spell's target words and steps for the chosen modes (the whole text when not modal).</summary>
	/// <param name="kicked">Highly Illogical: a target word with an IfKicked reads as that instead.</param>
	/// <param name="x">The X cast with — "X target creatures" (TargetSpec.PerX) is X words.</param>
	public static (ImmutableList<TargetSpec> Specs, ImmutableList<GameAction> Steps) TextOf(
		Card card,
		ImmutableArray<int> modes,
		bool kicked = false,
		int x = 0
	)
	{
		var (specs, steps) =
			card.GetComponent<SpellEffectsComponent>() is { } text
				? Modal.Compose(text.Targets, text.Effects, text.Modes, modes)
			: card.GetComponent<AuraComponent>() is { } aura ? ([aura.Enchant], [])
			: ([], []);
		if (kicked)
			specs = [.. specs.Select(t => t.IfKicked ?? t)];
		return (
			[
				.. specs.SelectMany(t =>
					t.PerX
						? Enumerable
							.Range(0, x)
							.Select(i => t with { PerX = false, NotThePrevious = i > 0 })
						: [t]
				),
			],
			steps
		);
	}

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
			var fromGraveyard = cast?.CastFrom == ZoneType.Graveyard;
			var mayCastThere =
				permanent.HasComponent<MayCastFromYourGraveyard>() // Storied Veteran
				|| cast?.ByPermission == true; // Overflow Submatrix, The Holodeck
			if ((fromGraveyard && !mayCastThere) || auraFizzles)
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
						)
						{
							Finality =
								fromGraveyard
								&& (
									permanent.HasComponent<MayCastFromYourGraveyard>()
									|| cast?.Finality == true
								), // "it enters with a finality counter on it"
							Sunburst = permanent.HasComponent<Sunburst>()
								? cast?.ColorsSpent ?? 0
								: 0,
						},
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
						&& TextOf(spell, cast.Modes, cast.Kicked, cast.X) is var text => (
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
			.Add(ContextKeys.ChosenTargets, chosen.ToImmutableArray())
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

/// <summary>
/// Dominion Infiltration: "Choose X target creatures your opponents control" — exactly X different ones,
/// the candidates of its X target words (all the same word). ponytail: a text whose only target words are
/// the X ones, outside any mode, as Dominion Infiltration's; another word or a mode waits for a card that has one.
/// </summary>
public record ChooseXTargetsChoice : ChoiceAction
{
	public const string Chosen = "xTargets";

	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int X { get; init; }
	public ImmutableArray<int> Modes { get; init; }
	public bool Kicked { get; init; }

	public ChooseXTargetsChoice(
		int playerId,
		int cardId,
		int x,
		ImmutableArray<int> modes,
		bool kicked
	)
	{
		PlayerId = playerId;
		CardId = cardId;
		X = x;
		Modes = modes;
		Kicked = kicked;
		MinChoices = x;
		MaxChoices = x;
		OutputKey = Chosen;
		Prompt = $"Choose {x} target{(x == 1 ? "" : "s")}";
	}

	public static bool CanChoose(
		GameState s,
		int playerId,
		int cardId,
		IReadOnlyList<TargetSpec> specs
	) => specs.Count > 0 && specs[0].Candidates(s, playerId, cardId).Count() >= specs.Count;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		[
			.. StackResolution
				.TextOf(s.GetCard(CardId), Modes, Kicked, X)
				.Specs[0]
				.Candidates(s, PlayerId, CardId)
				.Select(id => new ChoiceOption { Id = id, DisplayText = s.GetObject(id).Name }),
		];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	public static ImmutableArray<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Chosen) switch
		{
			int id => [id],
			ImmutableList<int> ids => [.. ids.Order()],
			_ => [],
		};
}

/// <summary>After ChooseXTargetsChoice: the cast with those targets, in increasing order (one set, one cast).</summary>
public record CastWithChosenTargetsAction(CastSpellAction Cast, bool Retain) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var cast = Cast with { Targets = ChooseXTargetsChoice.Read(InputContext) };
		return Retain ? cast.Execute(s) : new(cast.Cast(s));
	}
}

/// <summary>A resolving permanent spell enters the battlefield (CR 608.3) — then ActivePlayerGetsPriorityAction.</summary>
public record EnterFromStackAction(int CardId, int ControllerId, int X, int AttachTo, bool Cast)
	: GameAction
{
	/// <summary>Storied Veteran: "If you do, it enters with a finality counter on it."</summary>
	public bool Finality { get; init; }

	/// <summary>Sunburst: "This artifact enters with a charge counter on it for each color of mana spent to cast it."</summary>
	public int Sunburst { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(
			s.EnterBattlefield(
				CardId,
				ControllerId,
				X,
				finality: Finality,
				attachTo: AttachTo,
				cast: Cast,
				sunburst: Sunburst
			)
		);
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
