using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>
/// Splice (CR 702.47c; Goryo's Vengeance onto We Say Thee Nay!): the texts of the cards spliced onto this spell as it
/// was cast — kept on the spell, so the card leaving its hand changes nothing — and their splice costs. Status: gone as
/// it leaves the stack (Card.Fresh); a copy of the spell has it too (CR 707.10). Read by StackResolution.TextOf.
/// </summary>
public record Spliced(
	ImmutableArray<int> CardIds,
	ImmutableList<SpellEffectsComponent> Texts,
	ManaCost Cost
) : GameComponent;

/// <summary>What was decided while casting, carried by the card while it is on the stack.</summary>
public record OnStackComponent(int X, ImmutableArray<int> Targets, ImmutableArray<int> Modes)
	: GameComponent
{
	/// <summary>Sunburst (Engineered Explosives): the colours of mana spent to cast it — a copy spent none (CR 702.44c).</summary>
	public int ColorsSpent { get; init; }

	/// <summary>"If this spell was cast from a graveyard" — and flashback exiles it as it leaves the stack.</summary>
	public ZoneType CastFrom { get; init; } = ZoneType.Hand;

	/// <summary>
	/// Pyrokinesis: "4 damage divided as you choose" — how many each target chosen takes, in the targets' order,
	/// announced as it was cast (CR 601.2d); a copy keeps the division (CR 707.10). Read by Count.Share.
	/// </summary>
	public ImmutableArray<int> Shares { get; init; } = [];

	/// <summary>Overflow Submatrix, The Holodeck: cast from a graveyard by a permanent's permission — not flashback, so not exiled leaving the stack; it resolves onto the battlefield.</summary>
	public bool ByPermission { get; init; }

	/// <summary>Overflow Submatrix: "that artifact creature enters with a finality counter on it".</summary>
	public bool Finality { get; init; }

	/// <summary>Serra Paragon: "If you do, it gains '[trigger]'" — the permission's gift (MayPlayFromZone.Gains), given as it enters.</summary>
	public TriggeredAbilityComponent? Gains { get; init; }

	/// <summary>Theorist's Proxy: this cast can't be countered — gone with the stack object, so a recast can be.</summary>
	public bool CantBeCountered { get; init; }

	/// <summary>Kicker: "if this spell was kicked" — a copy of it is kicked too (CR 707.10).</summary>
	public bool Kicked { get; init; }

	/// <summary>
	/// Replicate and squad: how many times its replicate cost was paid (CastSpellAction.Replicate) — squad's "create
	/// that many tokens" reads it on the permanent (Card.CastAs, Count.TimesReplicated). A copy has it too (CR 707.10:
	/// additional costs paid are copied), as it is kicked.
	/// </summary>
	public int Replicate { get; init; }

	/// <summary>
	/// Storm (Brain Freeze): "copy it for each spell cast before it this turn" — every player's spells, counted as it is
	/// cast (CR 702.40a), so a spell cast in response to the storm trigger isn't one (Count.SpellsCastBefore).
	/// </summary>
	public int SpellsCastBefore { get; init; }

	/// <summary>First Contact: it is the first spell its caster cast this game — a copy wasn't cast (CopySpell clears it).</summary>
	public bool FirstSpellThisGame { get; init; }

	/// <summary>
	/// Baleful Mastery: "If the {1}{B} cost was paid" — cast for the alternative cost on the card itself
	/// (MayCastFromHandFree(Is.Source)), not for its mana cost or by another permission — a copy
	/// has it too, as it is kicked (CR 707.10).
	/// </summary>
	public bool AlternativeCost { get; init; }

	/// <summary>
	/// Necromancy: "If you cast it any time a sorcery couldn't have been cast" (CR 307.1) — stamped as it is cast, for
	/// a card with CastAsThoughItHadFlash only.
	/// </summary>
	public bool NotAtSorcerySpeed { get; init; }
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

	/// <summary>
	/// A delayed trigger (CR 603.7): its source is the source of the ability that made it, never the card it sits on —
	/// Tishana's Tidebinder countering Sneak Attack's "sacrifice the creature" strips no creature (Counter.Then).
	/// </summary>
	public bool Delayed { get; init; }
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

	/// <summary>Beam me up: the creature returned to its owner's hand (Daze: the Island); 0 if none is owed.</summary>
	public int ReturnId { get; init; }

	/// <summary>Force of Will: the blue card exiled from your hand; 0 if none is owed.</summary>
	public int ExileId { get; init; }

	/// <summary>Omnipresence: cast without paying its mana cost — X is 0 (CR 107.3c).</summary>
	public bool WithoutPayingManaCost { get; init; }

	/// <summary>Consign to Memory, Lose Focus: how many times its replicate cost is paid — that many copies.</summary>
	public int Replicate { get; init; }

	/// <summary>Kicker: its kicker cost is paid too (KickerComponent).</summary>
	public bool Kicked { get; init; }

	/// <summary>
	/// Delve (CR 702.66; Treasure Cruise, Dig Through Time): "Each card you exile from your graveyard while casting
	/// this spell pays for {1}." — how many: one cast per number, at most the generic in its total cost; the cards
	/// themselves are picked after the cast, one at a time (ExileFromGraveyardCost.PickAfter, Drivnod's), never a
	/// cast per set of cards.
	/// </summary>
	public int Delve { get; init; }

	/// <summary>
	/// Brazen Borrower // Petty Theft: cast as its Adventure (CR 715.3) — the card as Petty Theft (Card.AsAdventure),
	/// validated and cast exactly as that card would be. ponytail: never from exile — the Adventure's own exile lets
	/// only the creature be cast (CR 715.3d), and another effect's exile permission would allow the Adventure too
	/// (715.3a); tell the two apart when a card casts from exile that way (DesignNotes).
	/// </summary>
	public bool Adventure { get; init; }

	/// <summary>
	/// Splice onto Arcane (CR 702.47; Goryo's Vengeance): the cards in the caster's hand revealed and spliced onto this
	/// spell — each one's splice cost added, its text after the spell's own (Spliced); the cards stay in the hand. One
	/// cast per set of them (MagicRules.CastCandidates). Validated and cast as the card stamped with Spliced would be.
	/// </summary>
	public ImmutableArray<int> Splice { get; init; } = [];

	/// <summary>"As you cast an Arcane spell, you may reveal this card from your hand": may this card splice onto it.</summary>
	public static bool CanSplice(GameState s, int player, Card spell, int id) =>
		id != spell.Id
		&& s.IsIn(id, player, ZoneType.Hand)
		&& s.GetCard(id).GetComponent<SpliceComponent>() is { } splice
		&& splice.Onto.Matches(s, spell, new FilterContext(player, spell.Id));

	/// <summary>The state with this cast's card carrying what is spliced onto it; null when that can't be spliced.</summary>
	public GameState? SplicedState(GameState s)
	{
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card card
			|| card.HasComponent<Spliced>()
			|| Splice.Distinct().Count() != Splice.Length
			|| !Splice.All(id => CanSplice(s, PlayerId, card, id))
		)
			return null;
		var spliced = Splice.Select(s.GetCard).ToList();
		return s.UpdateObject(
			CardId,
			card.WithComponent(
				new Spliced(
					Splice,
					[.. spliced.Select(c => c.GetComponent<SpellEffectsComponent>() ?? new([]))],
					spliced
						.Select(c => c.GetComponent<SpliceComponent>()!.Cost)
						.Aggregate((a, b) => a.Plus(b))
				)
			)
		);
	}

	/// <summary>The state with this cast's card as its Adventure; null when it has none or is in exile.</summary>
	GameState? AsAdventure(GameState s) =>
		s.HasObject(CardId)
		&& s.GetObject(CardId) is Card card
		&& card.HasComponent<AdventureComponent>()
		&& !s.IsIn(CardId, card.OwnerId, ZoneType.Exile)
			? s.UpdateObject(CardId, card.AsAdventure())
			: null;

	/// <summary>Delve's cards: any in your graveyard but this one (a spell being cast from there).</summary>
	ExileFromGraveyardCost Delving => new(!Filters.Is.Source) { Count = Delve };

	/// <summary>
	/// Gitaxian Probe: "{U/P}" — how many of the Phyrexian symbols are paid with 2 life each (CR 107.4f), one legal
	/// cast per number, as an activation's (ActivateAbilityAction.LifeForPhyrexian); the rest are paid with their colour.
	/// </summary>
	public int LifeForPhyrexian { get; init; }

	/// <summary>
	/// Cast by an effect as it resolves (CastNow — Chandra, Uldaros): no priority, no timing, and the
	/// effect is the permission to cast it from where it is (and, free, to skip its mana cost). Every
	/// other rule — targets, modes, X, additional costs, "can't cast unless", payment — still applies.
	/// </summary>
	public bool DuringResolution { get; init; }

	/// <summary>
	/// Bilbo, Thief in the Night: "If an instant or sorcery spell cast this way would be put into your graveyard, exile
	/// it instead" — a cast during a resolution from a graveyard keeps flashback's exile (MoveCard) rather than being
	/// by the effect's permission (OnStackComponent.ByPermission). ponytail: flashback's exile takes it from the stack
	/// to anywhere — a Bilbo instant bounced to the hand is exiled too (DesignNotes).
	/// </summary>
	public bool ExiledLeavingTheStack { get; init; }

	/// <summary>
	/// Dominion Infiltration: "Choose X target creatures your opponents control" — the X targets are a
	/// choice made as it is cast (ChooseXTargetsChoice), before it is paid (CR 601.2c), not one legal cast
	/// per set: 2^n casts hung the fuzz probe (Shayne, 2026-11-05). One cast per X; Targets empty until chosen.
	/// Mystic Confluence, Fiery Confluence: "Choose three. You may choose the same mode more than once." — one cast
	/// per choice of modes, each target word then chosen in turn (ChooseTargetChoice, as a copy's are), never a
	/// cast per set: "return target creature" three times is 220 casts against 12 creatures (<see cref="EachWord"/>).
	/// </summary>
	public bool ChoosesTargets { get; init; }

	/// <summary>
	/// Mystic Confluence: a text whose modes may repeat chooses its targets word by word as it is cast — and spree's
	/// (Three Steps Ahead, a mode with a cost: Shayne, 2026-11-18): a cast per set of modes, never per pair of targets.
	/// </summary>
	public static bool EachWord(Card card) =>
		card.GetComponent<SpellEffectsComponent>() is { } text
		&& (text.Choose.Repeats || text.Modes.Any(m => m.Cost is not null));

	/// <summary>
	/// Pyrokinesis: how its 4 damage is divided among the targets chosen (TargetSpec.Divide), one number per
	/// target in their order — empty when the number of targets leaves one way (one target, or one each).
	/// </summary>
	public ImmutableArray<int> Shares { get; init; } = [];

	/// <summary>
	/// What casting it costs beyond mana: its additional costs, then its flashback's when cast that way, then
	/// its kicker's when kicked, then what is paid instead of its mana cost (<paramref name="free"/>: Flare of
	/// Fortitude's sacrifice, Playable.FreeCosts).
	/// </summary>
	public static ImmutableList<AdditionalCost> CostsOf(
		GameState s,
		Card card,
		bool kicked = false,
		ImmutableList<AdditionalCost>? free = null,
		int permissionOf = 0,
		bool flashback = true
	) =>
		[
			.. card.GetComponent<AdditionalCostsComponent>()?.Costs ?? [],
			.. flashback ? Costs.Flashback(s, card)?.Costs ?? [] : [],
			// Underworld Breach's escape: what the rule letting this player cast it from where it is asks
			// (Bolas's Citadel's are paid instead of the mana cost: the free cast's, Playable.FreeCast)
			.. permissionOf == 0 || Playable.MarkedInGraveyard(s, card, permissionOf) ? []
			: Playable.ZonePermission(s, card, permissionOf) is { Rule.Free: false } p
				? p.Rule.Costs
			: [],
			// From the Catacombs' escape: the card's own permission from its graveyard, cast by its player — never
			// with flashback (Snapcaster's grant), a second alternative cost (CR 118.9a)
			.. permissionOf == 0
			|| !s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard)
			|| flashback && Costs.Flashback(s, card) is not null
			|| Playable.MarkedInGraveyard(s, card, permissionOf)
				? []
				: s.AbilitiesOf<MayCastThisFrom>(card)
					.FirstOrDefault(m => m.From == ZoneType.Graveyard)
					?.Costs ?? [],
			.. kicked ? card.GetComponent<KickerComponent>()?.Costs ?? [] : [],
			.. free ?? [],
		];

	/// <summary>
	/// This cast's costs beyond mana — a free cast by a permission pays the permission's (never one an effect casts) —
	/// and escalate's, one per mode chosen beyond the first (Collective Brutality), picked after the cast. A life
	/// payment counted is fixed here for this cast (Toxic Deluge's X, Bolas's Citadel's mana value: PayLifeCost.For).
	/// </summary>
	ImmutableList<AdditionalCost> CostsOf(GameState s, Card card) =>
		[
			.. CostsOf(
					s,
					card,
					Kicked,
					WithoutPayingManaCost && !DuringResolution
						? Playable.FreeCosts(s, card, PlayerId)
						: null,
					DuringResolution ? 0 : PlayerId,
					flashback: !DuringResolution
				)
				.Select(c => c is PayLifeCost life ? life.For(s, card, PlayerId, X) : c),
			.. Modes.Length > 1
			&& card.GetComponent<SpellEffectsComponent>()?.Escalate is { } escalate
				? [escalate with { Count = Modes.Length - 1 }]
				: (AdditionalCost[])[],
		];

	int Chosen(CostSlot slot) =>
		slot switch
		{
			CostSlot.None => 0,
			CostSlot.Sacrifice => SacrificeId,
			CostSlot.Discard => DiscardId,
			CostSlot.Behold => BeholdId,
			CostSlot.Return => ReturnId,
			CostSlot.Exile => ExileId,
			_ => throw new InvalidOperationException($"A spell's cost can't pick into {slot}"),
		};

	/// <summary>
	/// Bone Shards' casts, before validating them: the slots it picks are exactly the slots of the costs it pays —
	/// one half of each "or", never both, never neither (ValidateAdd refuses the rest anyway; this is cheap).
	/// </summary>
	public bool PicksOnlyWhatIsPaid(ImmutableList<AdditionalCost> costs)
	{
		var paid = CostList.Paid(costs, Chosen).Select(c => c.Slot).ToHashSet();
		return costs
			.SelectMany(c => c.Or is { } or ? [c.Slot, or.Slot] : new[] { c.Slot })
			.Where(slot => slot != CostSlot.None)
			.All(slot => paid.Contains(slot) == (Chosen(slot) != 0));
	}

	/// <summary>The costs this cast pays: each "or" resolved by what it picked (CostList.Paid).</summary>
	ImmutableList<AdditionalCost> Paid(GameState s, Card card) =>
		CostList.Paid(CostsOf(s, card), Chosen);

	/// <summary>
	/// The total cost: X and whatever is paid instead of a sacrifice are in it, so ManaPayment is
	/// asked for X = 0 — and a reduction applies to the total (CR 601.2f). Mana beside the printed cost:
	/// the kicker's, the replicate cost's each time (Lose Focus' {U}), and what is paid instead of the mana
	/// cost (Baleful Mastery's {1}{B}), and each chosen mode's spree cost (Three Steps Ahead).
	/// </summary>
	ManaCost CostToPay(GameState s, Card card) =>
		CostBeforeLife(s, card).PayingLifeFor(LifeForPhyrexian);

	/// <summary>Thieving Skydiver: "Kicker {X}" — X is chosen only by a cast whose cost or kicker has one, or whose
	/// additional cost reads it (Toxic Deluge's "pay X life").</summary>
	public static bool ChoosesX(Card card, bool kicked) => XIsMana(card, kicked) || XInCosts(card);

	/// <summary>X is mana — in the mana cost or the kicker — so it joins the total; Toxic Deluge's is life only.</summary>
	static bool XIsMana(Card card, bool kicked) => card.ManaCost.HasX || XInKicker(card, kicked);

	/// <summary>
	/// Toxic Deluge: "As an additional cost to cast this spell, pay X life." — X chosen with no {X} in its cost (CR
	/// 107.3), one cast per X up to the caster's life (MagicRules.CastCandidates); a free cast still chooses it.
	/// </summary>
	public static bool XInCosts(Card card) =>
		card.GetComponent<AdditionalCostsComponent>()
			?.Costs.Any(c => c is PayLifeCost { Amount.Kind: CountOf.X }) == true;

	/// <summary>The X is the kicker's — an additional cost, so a cast without paying the mana cost still chooses it.</summary>
	public static bool XInKicker(Card card, bool kicked) =>
		kicked && card.GetComponent<KickerComponent>()?.Mana.HasX == true;

	ManaCost CostBeforeLife(GameState s, Card card)
	{
		var added = Kicked ? card.GetComponent<KickerComponent>()?.Mana : null;
		if (card.GetComponent<Spliced>() is { } spliced) // Goryo's Vengeance's {2}{B}: an additional cost, paid on a free cast too
			added = added?.Plus(spliced.Cost) ?? spliced.Cost;
		if (
			WithoutPayingManaCost
			&& !DuringResolution
			&& Playable.FreeCast(s, card, PlayerId)?.Mana is { } instead
		)
			added = added?.Plus(instead) ?? instead;
		if (card.GetComponent<ReplicateComponent>() is { } replicate)
			for (var i = 0; i < Replicate; i++)
				added = added?.Plus(replicate.Cost) ?? replicate.Cost;
		// Spree: each chosen mode's cost — asked before the modes are validated, so an index out of range is nothing
		if (card.GetComponent<SpellEffectsComponent>() is { } text)
			foreach (var i in Modes)
				if (text.Modes.ElementAtOrDefault(i)?.Cost is { } spree)
					added = added?.Plus(spree) ?? spree;
		var total = Costs.Of(
			s,
			PlayerId,
			card,
			(XIsMana(card, Kicked) ? X : 0) + CostList.ManaOwed(Paid(s, card)),
			free: WithoutPayingManaCost,
			targets: Targets,
			added: added,
			flashback: !DuringResolution
		);
		return Delve == 0 ? total : total.WithGeneric(-Delve); // delve pays from the TOTAL (CR 702.66a)
	}

	/// <summary>Delve's bound: the generic mana in this cast's total cost (X, kicker, taxes), before delve pays any.</summary>
	public int GenericBeforeDelve(GameState s, Card card) =>
		(this with { Delve = 0 }).CostBeforeLife(s, card).Generic;

	/// <summary>
	/// What paying for this cast taps and spends, or null if it can't be paid — THE call: validation, the cast
	/// itself and the table's preview of a dragged card (TBL-8.2) all make it here, so what is previewed is
	/// what is paid.
	/// </summary>
	public IReadOnlyList<ManaPayment.Payment>? PaymentPreview(GameState s)
	{
		if (Adventure) // Petty Theft's {1}{U}, not the creature's cost
			return AsAdventure(s) is { } asAdventure
				? (this with { Adventure = false }).PaymentPreview(asAdventure)
				: null;
		if (!Splice.IsDefaultOrEmpty)
			return SplicedState(s) is { } spliced
				? (this with { Splice = [] }).PaymentPreview(spliced)
				: null;
		var card = s.GetCard(CardId);
		return FindPayment(s, card, Paid(s, card), card.HasComponent<Sunburst>());
	}

	IReadOnlyList<ManaPayment.Payment>? FindPayment(
		GameState s,
		Card card,
		ImmutableList<AdditionalCost> costs,
		bool mostColors
	) =>
		ManaPayment.Find(
			s,
			PlayerId,
			CostToPay(s, card),
			0,
			forSpell: card,
			alsoExclude: [.. costs.SelectMany(c => c.Uses(s, PlayerId, CardId, Chosen(c.Slot)))],
			mostColors: mostColors
		);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Adventure)
			return AsAdventure(s) is { } asAdventure
				? (this with { Adventure = false }).ValidateAdd(asAdventure)
				: ValidationResult.Invalid("It can't be cast as an Adventure from here");
		if (!Splice.IsDefaultOrEmpty)
			return SplicedState(s) is { } spliced
				? (this with { Splice = [] }).ValidateAdd(spliced)
				: ValidationResult.Invalid("Those cards can't be spliced onto it");
		if (!DuringResolution && Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card candidate
			|| (
				DuringResolution
					? !Playable.CastRestrictionHolds(s, candidate, PlayerId)
					: !Playable.CanCastFromHere(s, candidate, PlayerId, X)
			)
		)
			return ValidationResult.Invalid("You may not cast that card from where it is");
		var card = s.GetCard(CardId);
		if (Turns.Combat.LockedDown(s, PlayerId, card, spell: true))
			return ValidationResult.Invalid("No spells now: a lock or split second");
		if (Kicked && !card.HasComponent<KickerComponent>())
			return ValidationResult.Invalid("It has no kicker");
		var costs = Paid(s, card);
		if (!CostList.Fits(costs, CostSlot.Discard, DiscardId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Discard a card from your hand for this");
		if (!CostList.Fits(costs, CostSlot.Exile, ExileId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can exile for this");
		if (card.Is(CardType.Land))
			return ValidationResult.Invalid("Lands are played, not cast");
		if (!DuringResolution && !CanCastNow(s, card))
			return ValidationResult.Invalid("Sorcery speed: your main phase with an empty stack");
		if (
			X < 0
			|| (
				X > 0
				&& (
					!ChoosesX(card, Kicked)
					|| WithoutPayingManaCost && !XInKicker(card, Kicked) && !XInCosts(card)
				)
			)
			|| (Kicked && X < (card.GetComponent<KickerComponent>()?.MinX ?? 0)) // "X can't be 0"
		)
			return ValidationResult.Invalid("Invalid X");
		if (
			LifeForPhyrexian != 0
			&& (
				LifeForPhyrexian < 0
				|| LifeForPhyrexian > CostBeforeLife(s, card).PhyrexianCount
				|| !MagicCore.Rules.Life.CanPay(s, PlayerId, 2 * LifeForPhyrexian) // CR 119.4
			)
		)
			return ValidationResult.Invalid("Can't pay that much life for its Phyrexian mana");
		if (Replicate < 0 || (Replicate > 0 && !card.HasComponent<ReplicateComponent>()))
			return ValidationResult.Invalid("It has no replicate cost");
		if (
			Delve != 0
			&& (
				Delve < 0
				|| !s.Has(card, "Delve")
				|| Delve > GenericBeforeDelve(s, card)
				// Underworld Breach's escape exiles from the same graveyard
				|| Delve
					> Delving.Candidates(s, PlayerId, CardId).Count()
						- costs.OfType<ExileFromGraveyardCost>().Sum(c => c.Count)
			)
		)
			return ValidationResult.Invalid("Can't delve that many");
		if (
			WithoutPayingManaCost
			&& !DuringResolution
			&& !Playable.MayCastWithoutPaying(s, card, PlayerId)
		)
			return ValidationResult.Invalid("You may not cast that without paying its mana cost");
		if (
			!WithoutPayingManaCost
			&& !DuringResolution
			&& !s.IsIn(CardId, PlayerId, ZoneType.Hand)
			&& Playable.ZonePermission(s, card, PlayerId) is { Rule.Free: true }
		)
			return ValidationResult.Invalid("Cast this way, it is paid for in life"); // Bolas's Citadel
		var text = card.GetComponent<SpellEffectsComponent>();
		if (!(text?.Choose ?? 1).IsLegal(s, card, text?.Modes ?? [], Modes))
			return ValidationResult.Invalid("Illegal choice of modes");
		var specs = StackResolution
			.TextOf(card, Modes, Kicked, X, PaysItsAlternativeCost(s, card))
			.Specs;
		if (
			ChoosesTargets
				? !(Targets.IsEmpty && ChooseXTargetsChoice.CanChoose(s, PlayerId, CardId, specs))
				: !TargetSpec.AreLegal(s, specs, Targets, PlayerId, CardId)
		)
			return ValidationResult.Invalid("Illegal targets");
		if (!ChoosesTargets && !Division.Fits(Division.Of(card), Targets, Shares))
			return ValidationResult.Invalid("Illegal division");
		if (!CostList.Fits(costs, CostSlot.Sacrifice, SacrificeId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can sacrifice for this");
		if (!CostList.Fits(costs, CostSlot.Behold, BeholdId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a card you can behold for this");
		if (!CostList.Fits(costs, CostSlot.Return, ReturnId, s, PlayerId, CardId))
			return ValidationResult.Invalid("Not a permanent you can return for this");
		if (
			costs.Any(c => !c.CanPay(s, PlayerId, CardId))
			// CR 119.4: every life payment of the cast together — Bolas's Citadel's beside Bitter Triumph's, Phyrexian mana's
			|| !MagicCore.Rules.Life.CanPay(
				s,
				PlayerId,
				costs.OfType<PayLifeCost>().Sum(c => c.Amount.Value(s, Conditions.Of(card)))
					+ 2 * LifeForPhyrexian
			)
		)
			return ValidationResult.Invalid("Cannot pay the additional cost");
		if (FindPayment(s, card, costs, mostColors: false) is null)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	bool CanCastNow(GameState s, Card card)
	{
		return Priority.AtInstantSpeed(s, card) || Priority.AtSorcerySpeed(s, PlayerId);
	}

	public override ActionResult Execute(GameState s) =>
		Adventure ? (this with { Adventure = false }).Execute(AsAdventure(s)!)
		: !Splice.IsDefaultOrEmpty ? (this with { Splice = [] }).Execute(SplicedState(s)!)
		: new(ChoosesTargets ? ChooseTargets(s, retain: true) : Priority.Retain(Cast(s), PlayerId));

	/// <summary>
	/// Dominion Infiltration: the X targets chosen, then this cast with them — Pyrokinesis: one to four; Mystic
	/// Confluence: each word in turn.
	/// </summary>
	GameState ChooseTargets(GameState s, bool retain)
	{
		var card = s.GetCard(CardId);
		var specs = StackResolution
			.TextOf(card, Modes, Kicked, X, PaysItsAlternativeCost(s, card))
			.Specs;
		if (EachWord(card))
			return s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						.. specs.Select(
							(spec, i) => new ChooseTargetChoice(PlayerId, CardId, spec, i)
						),
						new CastWithChosenTargetsAction(
							this with
							{
								ChoosesTargets = false,
							},
							retain
						)
						{
							Words = specs.Count,
						},
					],
				}
			);
		return s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					new ChooseXTargetsChoice(
						PlayerId,
						CardId,
						X,
						Modes,
						Kicked,
						specs.Count(t => !t.Optional),
						specs.Count
					),
					new CastWithChosenTargetsAction(this with { ChoosesTargets = false }, retain),
				],
			}
		);
	}

	/// <summary>Baleful Mastery, Damn's overload: this cast pays the alternative cost on the card itself.</summary>
	bool PaysItsAlternativeCost(GameState s, Card card) =>
		WithoutPayingManaCost
		&& !DuringResolution
		&& Playable.OwnAlternativeCost(s, card, PlayerId);

	/// <summary>Pays and puts it on the stack — the priority action keeps priority after; a resolution goes on.</summary>
	public GameState Cast(GameState s)
	{
		if (Adventure) // a cast during a resolution comes here without Execute
			return (this with { Adventure = false }).Cast(AsAdventure(s)!);
		if (!Splice.IsDefaultOrEmpty)
			return (this with { Splice = [] }).Cast(SplicedState(s)!);
		if (ChoosesTargets)
			return ChooseTargets(s, retain: false);
		var card = s.GetCard(CardId);
		var costs = Paid(s, card); // read before paying: flashback's are known only in the graveyard
		var alternative = PaysItsAlternativeCost(s, card);
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		// Zefram Cochrane, Overflow Submatrix, The Holodeck: "once during each of your turns" — this one is used.
		// A cast during a resolution is by that effect's permission, never a permanent's.
		// Emry: "You may cast that card this turn" — a mark in the graveyard, not flashback, and no permanent's rule
		var marked = !DuringResolution && Playable.MarkedInGraveyard(s, card, PlayerId);
		var permission =
			!DuringResolution
			&& !marked
			&& from is ZoneType.Library or ZoneType.Graveyard or ZoneType.Sideboard
				? Playable.ZonePermission(s, card, PlayerId)
				: null;
		// Malcolm, Bilbo: cast from a graveyard as an effect resolves (CR 608.2g) — not flashback, so it isn't exiled
		// leaving the stack and a permanent spell resolves onto the battlefield, unless the effect exiles it.
		var byEffect =
			DuringResolution && from == ZoneType.Graveyard && !ExiledLeavingTheStack || marked;
		if (permission is { Rule.OncePerTurn: true, Holder: var holder })
			s = s.UpdateObject(
				holder.Id,
				holder with
				{
					PermissionUsedOn = s.GetGame().TurnNumber,
				}
			);
		var sunburst = card.HasComponent<Sunburst>();
		var payment = FindPayment(s, card, costs, mostColors: sunburst)!;
		var uncounterable = ManaPayment.MakesUncounterable(s, PlayerId, payment);
		s = ManaPayment.Pay(s, PlayerId, payment);
		s = MagicCore.Rules.Life.Lose(s, PlayerId, 2 * LifeForPhyrexian);
		foreach (var cost in costs)
			s = cost.Pay(s, PlayerId, CardId, Chosen(cost.Slot));
		if (Delve > 0)
			s = Delving.PickAfter(s, PlayerId, CardId, delve: true);
		foreach (var id in card.GetComponent<Spliced>()?.CardIds ?? []) // "you may reveal this card from your hand"
			s = s.MakeKnown(id, Knowers.Everyone);
		s = PutOnStack(s, PlayerId, CardId, X, Targets, Modes, from, Kicked);
		if (Division.Of(card) is > 0 and var divided) // Pyrokinesis, Pure Energy: the division is announced now
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponentReplaced(
						s.GetCard(CardId).GetComponent<OnStackComponent>()! with
						{
							Shares = Shares.IsEmpty
								? Division.Splits(divided, Targets.Count(id => id != 0)).Single()
								: Shares,
						}
					)
			);
		if (alternative || uncounterable)
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponentReplaced(
						s.GetCard(CardId).GetComponent<OnStackComponent>()! with
						{
							AlternativeCost = alternative,
							// Delighted Halfling's mana: "that spell can't be countered"
							CantBeCountered =
								uncounterable
								|| s.GetCard(CardId)
									.GetComponent<OnStackComponent>()!
									.CantBeCountered,
						}
					)
			);
		if (permission is not null && from != ZoneType.Library || byEffect)
			s = s.UpdateObject(
				CardId,
				s.GetCard(CardId)
					.WithComponentReplaced(
						s.GetCard(CardId).GetComponent<OnStackComponent>()! with
						{
							ByPermission = true,
							Finality = permission?.Rule.Finality ?? false,
							Gains = permission?.Rule.Gains,
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
		s = s.UpdateObject(
			CardId,
			s.GetCard(CardId)
				.WithComponentReplaced(
					s.GetCard(CardId).GetComponent<OnStackComponent>()! with
					{
						Replicate = Replicate,
					}
				)
		);
		// Squad (CR 702.157): the copies are made as it enters, by its own trigger (Scripting.Squad) — none now.
		if (card.GetComponent<ReplicateComponent>() is { AsItEnters: true })
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
				Effects = [new CopySpell(Ref.EventCard) { N = Replicate }],
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
		// Necromancy's "any time a sorcery couldn't have been cast" — judged before it moves onto the stack
		var notAtSorcerySpeed =
			card.HasComponent<CastAsThoughItHadFlash>() && !Priority.AtSorcerySpeed(s, playerId);
		if (Prepare.CreatureOf(s, card) is { } prepared)
			s = Prepare.Unprepare(s, prepared.Id); // casting the copy unprepares it
		s = s.MoveCard(cardId, s.GetWellKnownId(MagicKeys.Stack), toTop: true); // index 0 is the top
		if (card.HasComponent<CastAsAdventure>()) // CR 715.3b: on the stack it is the Adventure (the move made it the card)
			s = s.UpdateObject(cardId, s.GetCard(cardId).AsAdventure());
		if (card.GetComponent<Spliced>() is { } spliced) // CR 702.47c: the spliced text rides the spell (the move's Fresh took it)
			s = s.UpdateObject(cardId, s.GetCard(cardId).WithComponent(spliced));
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
					NotAtSorcerySpeed = notAtSorcerySpeed,
					FirstSpellThisGame = s.GetPlayer(playerId).SpellsCastThisGame == 0,
					SpellsCastBefore =
						s.GetPlayer(s.Player1Id()).ThisTurn.SpellsCast
						+ s.GetPlayer(s.Player2Id()).ThisTurn.SpellsCast,
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
				copies.Count,
				s.GetPlayer(playerId).ThisTurn.SpellsCast,
				from
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

	/// <summary>
	/// Drivnod, Carnage Dominus: "{B/P}{B/P}" — how many of the Phyrexian symbols are paid with 2 life
	/// each (CR 107.4f), one legal action per number; the rest are paid with their colour.
	/// </summary>
	public int LifeForPhyrexian { get; init; }

	/// <summary>The mana this activation pays — its cost changes, less the symbols paid with life.</summary>
	ManaCost Mana(GameState s, ActivatedAbilityComponent ability) =>
		Costs.OfAbility(s, ability, Targets, PlayerId, CardId).PayingLifeFor(LifeForPhyrexian);

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
		if (Turns.Combat.LockedDown(s, PlayerId, card))
			return ValidationResult.Invalid("No abilities now: a lock or split second");
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
		if (!ability.Choose.IsLegal(s, s.GetCard(CardId), ability.Modes, Modes))
			return ValidationResult.Invalid("Not a legal choice of modes");
		if (!TargetSpec.AreLegal(s, TextOf(ability, Modes).Specs, Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		if (
			LifeForPhyrexian < 0
			|| LifeForPhyrexian
				> Costs.OfAbility(s, ability, Targets, PlayerId, CardId).PhyrexianCount
			// CR 119.4 — only when life is paid: under Georgiou a player below 0 still activates the rest.
			|| !MagicCore.Rules.Life.CanPay(s, PlayerId, 2 * LifeForPhyrexian)
		)
			return ValidationResult.Invalid("Can't pay that much life for its Phyrexian mana");
		if (
			ManaPayment.Find(
				s,
				PlayerId,
				Mana(s, ability),
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
			Mana(s, ability),
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
		s = MagicCore.Rules.Life.Lose(s, PlayerId, 2 * LifeForPhyrexian);
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
		s = s.Raise(new Turns.AbilityActivatedEvent(PlayerId, source, ability.Keyword));
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
	/// <param name="alternative">
	/// Cast for the card's own alternative cost: overloaded (Damn, MayCastFromHandFree.Overload), it has no target words.
	/// </param>
	public static (ImmutableList<TargetSpec> Specs, ImmutableList<GameAction> Steps) TextOf(
		Card card,
		ImmutableArray<int> modes,
		bool kicked = false,
		int x = 0,
		bool alternative = false
	)
	{
		var (specs, steps) =
			card.GetComponent<SpellEffectsComponent>() is { } text
				? Modal.Compose(text.Targets, text.Effects, text.Modes, modes)
			: card.GetComponent<AuraComponent>() is { } aura ? ([aura.Enchant], [])
			: ([], []);
		if (kicked)
			specs = [.. specs.Select(t => t.IfKicked ?? t)];
		if (alternative && card.GetComponent<MayCastFromHandFree>() is { Overload: true })
			// CR 702.96b: "target" becomes "each" — the script's If(AlternativeCostWasPaid) says each what.
			// ponytail: the card's first alternative cost, not the one paid — none prints two (DesignNotes).
			specs = [];
		ImmutableList<TargetSpec> words =
		[
			.. specs.SelectMany(t =>
				t.PerX
					? Enumerable
						.Range(0, x)
						.Select(i => t with { PerX = false, NotThePrevious = i > 0 })
				: t.Divide > 0
					? Enumerable
						.Range(0, t.Divide)
						.Select(i =>
							t with
							{
								Divide = 0,
								Optional = i > 0,
								NotThePrevious = i > 0,
							}
						)
				: [t]
			),
		];
		// Goryo's Vengeance spliced on (CR 702.47b): its target words after the spell's own, its effects after — each
		// text's Target(0) its own first, as a mode's is (ScopeTargetsAction). ponytail: a spliced text's X or divided
		// target words aren't expanded — no splice card has one.
		if (card.HasComponent<Spliced>())
			// the spell's own text sees only its own words: "each target" is never Goryo's card too
			steps = steps.Insert(0, new ScopeTargetsAction(0, words.Count));
		foreach (var spliced in card.GetComponent<Spliced>()?.Texts ?? [])
		{
			if (!spliced.Modes.IsEmpty) // never silently inert: compose its chosen modes when such a card comes
				throw new InvalidOperationException(
					"A modal text spliced on — splice reads Targets and Effects only"
				);
			steps = steps
				.Add(new ScopeTargetsAction(words.Count, spliced.Targets.Count))
				.AddRange(spliced.Effects);
			words = words.AddRange(spliced.Targets);
		}
		return (words, steps);
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
	/// (MtgCore's counter traps keep the same two rules). Remand's hand or Memory Lapse's library top
	/// instead (<paramref name="to"/>) — "countered this way" only: one that can't be countered stays.
	/// </summary>
	public static GameState Counter(
		GameState s,
		int stackObjectId,
		ZoneType to = ZoneType.Graveyard
	)
	{
		if (
			!s.HasObject(stackObjectId)
			|| s.GetParent(stackObjectId) != s.GetWellKnownId(MagicKeys.Stack)
		)
			return s;
		return s.GetObject(stackObjectId) switch
		{
			Card spell when !CanBeCountered(spell) => s,
			Card spell => s.MoveCard(
				spell.Id,
				s.ZoneId(spell.OwnerId, to),
				toTop: to == ZoneType.Library
			),
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
				MayCastThisFrom.On(permanent, ZoneType.Graveyard) // Storied Veteran
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
							cast,
							auraHost
						)
						{
							Finality =
								fromGraveyard
								&& (
									permanent
										.Components.OfType<MayCastThisFrom>()
										.Any(m => m is { From: ZoneType.Graveyard, Finality: true })
									|| cast?.Finality == true
								), // "it enters with a finality counter on it"
							Gains = cast?.Gains,
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
			.Select(
				(id, i) =>
					id != 0
					&& specs[i].IsLegal(s, controller, sourceId, id)
					&& TargetSpec.FitsPrevious(s, specs[i], chosen, i) // Goblin Welder: still that player's graveyard
						? id
						: 0
			)
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
		if (top is Card divided && divided.GetComponent<OnStackComponent>() is { } division)
			context = context.Add(ContextKeys.Shares, division.Shares);
		if (top is StackAbility { EventSnapshot: { } snapshotOfEvent })
			context = context.Add(ContextKeys.EventSnapshot, snapshotOfEvent);
		if (top is StackAbility { EventObject: { } eventObject })
			context = context.Add(ContextKeys.EventObject, eventObject);
		return s.SpawnAction(
			new PipelineAction
			{
				// a spell that fizzles doesn't resolve (CR 608.2b): no Adventure's exile (FinishSpellAction.Resolved)
				Steps = fizzles
					?
					[
						finish is FinishSpellAction unresolved
							? unresolved with
							{
								Resolved = false,
							}
							: finish,
					]
					: [.. effects, finish],
				PipelineContext = context,
			}
		);
	}
}

public record FinishSpellAction : GameAction
{
	public int CardId { get; init; }

	/// <summary>
	/// False when it fizzled (CR 608.2b) — it didn't resolve, so an Adventure goes to the graveyard (715.3d): Petty Theft
	/// whose "target nonland permanent an opponent controls" left is Brazen Borrower in the graveyard, not on an adventure.
	/// </summary>
	public bool Resolved { get; init; } = true;

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(CardId);
		if (s.GetParent(CardId) == s.GetWellKnownId(MagicKeys.Stack)) // Sphinx's Approach may have exiled itself
			s =
				Resolved && card.HasComponent<CastAsAdventure>()
					? OnAnAdventure(s, card)
					: s.MoveCard(CardId, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
		return new ActionResult(Priority.Retain(s, s.GetGame().ActivePlayerId));
	}

	/// <summary>
	/// Petty Theft resolved (CR 715.3d): "(Then exile this card. You may cast the creature later from exile.)" — its
	/// caster may cast it for as long as it remains exiled, as the creature only (CastSpellAction.Adventure refuses
	/// exile). Countered or fizzled, it goes to the graveyard as the creature (Fresh).
	/// </summary>
	static GameState OnAnAdventure(GameState s, Card spell)
	{
		s = s.MoveCard(spell.Id, s.ZoneId(spell.OwnerId, ZoneType.Exile));
		var mark = new PlayPermission { Forever = true, CastOnly = true }.For(
			s,
			spell.ControllerId
		);
		return s.UpdateObject(spell.Id, (Card)s.GetCard(spell.Id).WithComponent(mark));
	}
}

/// <summary>
/// Pyrokinesis: "4 damage divided as you choose among any number of target creatures" — the ways to divide a
/// total among the targets chosen, each taking at least one (CR 601.2d), in the targets' order. ponytail: a
/// text with one divided word, outside any mode, as Pyrokinesis' and Pure Energy's.
/// </summary>
public static class Division
{
	/// <summary>The total a card's text divides (TargetSpec.Divide); 0 when it divides nothing.</summary>
	public static int Of(Card card) =>
		card.GetComponent<SpellEffectsComponent>()?.Targets.Sum(t => t.Divide) ?? 0;

	/// <summary>Every way to divide the total among k targets, the first target's biggest share first.</summary>
	public static ImmutableList<ImmutableArray<int>> Splits(int total, int k) =>
		k <= 0 || total < k ? []
		: k == 1
			?
			[
				[total],
			]
		:
		[
			.. Enumerable
				.Range(1, total - k + 1)
				.Reverse()
				.SelectMany(first =>
					Splits(total - first, k - 1).Select(rest => rest.Insert(0, first))
				),
		];

	/// <summary>A cast's shares fit its targets: none without a division or when only one way is left, else one of the ways.</summary>
	public static bool Fits(int total, ImmutableArray<int> targets, ImmutableArray<int> shares)
	{
		var splits = Splits(total, targets.Count(id => id != 0));
		return shares.IsEmpty
			? total == 0 || splits.Count == 1
			: splits.Any(split => split.SequenceEqual(shares));
	}
}

/// <summary>
/// Dominion Infiltration: "Choose X target creatures your opponents control" — exactly X different ones,
/// the candidates of its X target words (all the same word); Pyrokinesis' "any number of target creatures":
/// one to four of them. ponytail: a text whose only target words are those, outside any mode, as Dominion
/// Infiltration's; another word or a mode waits for a card that has one.
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
		bool kicked,
		int min,
		int max
	)
	{
		PlayerId = playerId;
		CardId = cardId;
		X = x;
		Modes = modes;
		Kicked = kicked;
		MinChoices = min;
		MaxChoices = max;
		OutputKey = Chosen;
		Prompt =
			min == max
				? $"Choose {min} target{(min == 1 ? "" : "s")}"
				: $"Choose {min} to {max} targets";
	}

	/// <summary>
	/// Each word and the NotThePrevious repeats after it have as many candidates as they need — Dominion
	/// Infiltration's X words are one run, each of Mystic Confluence's chosen modes its own. ponytail: runs don't
	/// share candidates, as no card's modes overlap (a spell and a creature; artifacts alone); two overlapping modes
	/// would need their words checked together, and could leave a later word nothing after earlier picks.
	/// </summary>
	public static bool CanChoose(
		GameState s,
		int playerId,
		int cardId,
		IReadOnlyList<TargetSpec> specs
	)
	{
		for (var i = 0; i < specs.Count; )
		{
			var run = 1;
			while (i + run < specs.Count && specs[i + run].NotThePrevious)
				run++;
			var need = specs.Skip(i).Take(run).Count(t => !t.Optional);
			if (specs[i].Candidates(s, playerId, cardId).Take(need).Count() < need)
				return false;
			i += run;
		}
		return specs.Count > 0;
	}

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

/// <summary>
/// After ChooseXTargetsChoice: the cast with those targets, in increasing order (one set, one cast). Pyrokinesis: two or three targets can share its 4 damage several ways, so that is asked
/// next (a ChooseKeywordChoice of the ways), before the cast. Mystic Confluence: after a ChooseTargetChoice per word,
/// those, in the words' order.
/// </summary>
public record CastWithChosenTargetsAction(CastSpellAction Cast, bool Retain) : GameAction
{
	/// <summary>Mystic Confluence: how many words were chosen one at a time (ChooseTargetChoice); 0 is one ChooseXTargetsChoice.</summary>
	public int Words { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(Cast.CardId);
		var chosen =
			Words > 0
				? ChooseTargetChoice.Read(InputContext, Words)
				: ChooseXTargetsChoice.Read(InputContext);
		var cast = Cast with { Targets = chosen };
		var splits = Division.Splits(Division.Of(card), chosen.Length);
		if (splits.Count > 1)
			return new(
				s.SpawnAction(
					new PipelineAction
					{
						Steps =
						[
							new ChooseKeywordChoice(
								cast.PlayerId,
								[
									.. splits.Select(split =>
										string.Join(
											", ",
											split.Select(
												(n, i) => $"{n} to {s.GetObject(chosen[i]).Name}"
											)
										)
									),
								]
							)
							{
								Prompt = "Divide among the targets",
							},
							new CastWithChosenDivisionAction(cast, splits, Retain),
						],
					}
				)
			);
		return Retain ? cast.Execute(s) : new(cast.Cast(s));
	}
}

/// <summary>Pyrokinesis: after the division was chosen among the ways offered, the cast with it.</summary>
public record CastWithChosenDivisionAction(
	CastSpellAction Cast,
	ImmutableList<ImmutableArray<int>> Splits,
	bool Retain
) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var cast = Cast with { Shares = Splits[(int)InputContext[ChooseKeywordChoice.Chosen] - 1] };
		return Retain ? cast.Execute(s) : new(cast.Cast(s));
	}
}

/// <summary>
/// A resolving permanent spell enters the battlefield (CR 608.3) — then ActivePlayerGetsPriorityAction. Spell: what it
/// was on the stack (null: a copy-less resolution that wasn't cast), carried onto it as Card.CastAs.
/// </summary>
public record EnterFromStackAction(
	int CardId,
	int ControllerId,
	OnStackComponent? Spell,
	int AttachTo
) : GameAction
{
	/// <summary>Storied Veteran: "If you do, it enters with a finality counter on it."</summary>
	public bool Finality { get; init; }

	/// <summary>Sunburst: "This artifact enters with a charge counter on it for each color of mana spent to cast it."</summary>
	public int Sunburst { get; init; }

	/// <summary>Serra Paragon: the triggered ability it gains as it enters (OnStackComponent.Gains).</summary>
	public TriggeredAbilityComponent? Gains { get; init; }

	public override ActionResult Execute(GameState s)
	{
		s = s.EnterBattlefield(
			CardId,
			ControllerId,
			Spell,
			finality: Finality,
			attachTo: AttachTo,
			cast: Spell is not null,
			sunburst: Sunburst,
			gains: Gains
		);
		// Warp: "if this spell's warp cost was paid, exile the permanent … at the beginning of the next end step" — set
		// up as it resolves, after it enters (entering strips a delayed trigger). ponytail: the card's first alternative
		// cost with one is taken as the one paid — a card with two such costs would need OnStackComponent to name it.
		if (
			Spell is { AlternativeCost: true }
			&& s.IsOnBattlefield(CardId)
			&& s.GetCard(CardId)
				.Components.OfType<MayCastFromHandFree>()
				.FirstOrDefault(m => !m.PaidAtNextEndStep.IsEmpty)
				is { } warp
		)
			s = MoveTo.Delay(
				s,
				CardId,
				new DelayedTrigger(
					new TriggeredAbilityComponent(TriggerEvent.EachEndStep, warp.PaidAtNextEndStep)
				)
				{
					ControllerId = ControllerId, // CR 603.7d: the spell's, even once it is stolen
				}
			);
		// Necromancy: "If you cast it any time a sorcery couldn't have been cast, … sacrifices it at the beginning of the
		// next cleanup step" — set up as warp's is.
		if (
			Spell is { NotAtSorcerySpeed: true }
			&& s.IsOnBattlefield(CardId)
			&& s.GetCard(CardId).GetComponent<CastAsThoughItHadFlash>() is { } flash
		)
			s = MoveTo.Delay(
				s,
				CardId,
				new DelayedTrigger(
					new TriggeredAbilityComponent(
						TriggerEvent.Beginning(Step.Cleanup, Whose.Any),
						flash.AtNextCleanup
					)
				)
				{
					ControllerId = ControllerId,
				}
			);
		return new(s);
	}
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
