using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Effects;

public abstract record Effect : GameAction
{
	protected int Controller => GetInput<int>(ContextKeys.Controller);

	/// <summary>
	/// The source as it is now, or — once it no longer exists (a token that died) — as it last was
	/// (CR 113.7a): its power, keywords and controller still decide what its ability does.
	/// </summary>
	protected Card Source(GameState s) =>
		s.HasObject(GetInput<int>(ContextKeys.Source))
		&& s.GetObject(GetInput<int>(ContextKeys.Source)) is Card now
			? now
			: GetInput<Card>(ContextKeys.SourceSnapshot);

	protected IEnumerable<int> All(Ref r, GameState s) => r.Resolve(s, InputContext).ToList();

	protected IEnumerable<Card> Permanents(Ref r, GameState s) =>
		All(r, s).Where(s.IsOnBattlefield).Select(s.GetCard);

	protected static GameState Update(GameState s, Card card) => s.UpdateObject(card.Id, card);
}

/// <summary>"[Who] draws N cards" — a draw from an empty library is flagged for the state-based loss.</summary>
public record DrawCards(Ref Who, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(All(Who, s).Aggregate(s, (st, p) => st.Draw(p, n)));
	}
}

/// <summary>"[Who] gains N life" — through Rules.Life.Gain, which raises "whenever you gain life".</summary>
public record GainLife(Ref Who, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext); // Species 8472: "life equal to the number of …", counted as it resolves
		return new(All(Who, s).Aggregate(s, (st, p) => Life.Gain(st, p, n)));
	}
}

/// <summary>Life loss that is not damage — lifelink and damage triggers do not see it.</summary>
public record LoseLife(Ref Who, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(All(Who, s).Aggregate(s, (st, p) => Life.Lose(st, p, n)));
	}
}

/// <summary>"[Source] deals N damage to …" — through <see cref="Damage.Deal"/>, so lifelink and deathtouch apply.</summary>
public record DealDamage(Ref To, Count Amount) : Effect
{
	/// <summary>
	/// Violent Echoes: "if excess damage was dealt to that permanent this way, [IfExcess]" — X is the
	/// excess. For a single target: with several, X would be their total.
	/// </summary>
	public ImmutableList<GameAction> IfExcess { get; init; } = [];

	/// <summary>
	/// Screaming Nemesis: "If a player is dealt damage this way, they can't gain life for the rest of the game." — run
	/// after, with Ref.ThisWay what was actually dealt damage (none prevented, none whose target is gone).
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var source = Source(s);
		var excess = 0;
		var dealt = ImmutableArray.CreateBuilder<int>();
		var before = s;
		foreach (var id in All(To, s).ToList())
		{
			// counted per object, Ref.It that one (Pyrokinesis: each target its share, Count.Share) — and
			// before any of it is dealt: the damage is simultaneous
			var amount = Amount.Value(before, InputContext.SetItem(ContextKeys.It, id));
			if (!IfExcess.IsEmpty)
				excess += Damage.Excess(s, source, id, amount);
			var after = Damage.Deal(s, source, id, amount);
			if (!ReferenceEquals(after, s)) // Damage.Deal hands back the same state when it dealt none
				dealt.Add(id);
			s = after;
		}
		if (!Then.IsEmpty && dealt.Count > 0)
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps = Then,
					PipelineContext = InputContext.SetItem(
						ContextKeys.ThisWay,
						dealt.ToImmutable()
					),
				}
			);
		return excess == 0 || IfExcess.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						Steps = IfExcess,
						PipelineContext = InputContext.SetItem(ContextKeys.X, excess),
					}
				)
			);
	}
}

/// <summary>"Gets +P/+T [and gains Keyword] until end of turn." Amounts are counted as it resolves.</summary>
public record ModifyUntilEndOfTurn(Ref What, Count Power, Count Toughness, string? Keyword = null)
	: Effect
{
	/// <summary>Perfected Theory: "has base power and toughness P/T until end of turn" (CR 613, layer 7b).</summary>
	public (int Power, int Toughness)? SetBase { get; init; }

	/// <summary>Lyra: "until end of turn, whenever …" — a triggered ability it has until cleanup.</summary>
	public TriggeredAbilityComponent? Trigger { get; init; }

	/// <summary>
	/// Essence Burn: "if that permanent would die this turn, exile it instead" ([Graveyard]); From the Catacombs: "If
	/// that creature would leave the battlefield, exile it instead of putting it anywhere else" (every other zone).
	/// </summary>
	public ImmutableArray<ZoneType> ExileInsteadOf { get; init; } = [];

	/// <summary>Flourishing Grapple: "loses all abilities until end of turn".</summary>
	public bool LosesAllAbilities { get; init; }

	/// <summary>Garruk, Veiled Butcher: "until your next turn" instead of end of turn.</summary>
	public bool UntilYourNextTurn { get; init; }

	/// <summary>
	/// I'm a Doctor, Not a . . .: "becomes a Doctor (It loses all other creature types.)"; Nog, Ambitious
	/// Cadet: "he becomes a Ferengi Scout" — every other creature type lost.
	/// </summary>
	public ImmutableArray<string>? BecomesCreatureTypes { get; init; }

	/// <summary>Nog, Ambitious Cadet: "gains 'You may play lands from your graveyard'" — static or activated abilities it gains.</summary>
	public ImmutableList<GameComponent> Gains { get; init; } = [];

	/// <summary>Worf, Intelligence Contractor: "they become Warriors in addition to their other types".</summary>
	public string? AlsoCreatureType { get; init; }

	/// <summary>Seven of Nine, Unassimilated: "Seven of Nine's base power and toughness become 7/9." — no duration: until it leaves the battlefield.</summary>
	public bool ForGood { get; init; }

	/// <summary>
	/// Does Machines: "it becomes a 0/0 Robot creature in addition to its other types" — card types added, STAMPED
	/// (Card.WithTypesUntilItLeaves: every rule reads card.Is), with SetBase and AlsoCreatureType; ForGood, or until end
	/// of turn — crew (Esika's Chariot): "This Vehicle becomes an artifact creature until end of turn", the stamp undone
	/// by the cleanup step (Animated.EndsAtCleanup). ponytail: one stamp a permanent — a type it already has is not
	/// stamped again, so "for good" on a Vehicle crewed this turn ends with the crew; a stamp per effect if a card
	/// needs both.
	/// </summary>
	public Cards.CardType AlsoType { get; init; }

	/// <summary>
	/// The Wondrous Wasp: "It loses all abilities for as long as The Wondrous Wasp remains on the battlefield";
	/// Tishana's Tidebinder: "… for as long as this creature remains on the battlefield". LosesAllAbilities only:
	/// each permanent's EnchantedLosesAllAbilities { Object } stamped on THIS object of the source. With the source
	/// gone as it resolves, nothing (CR 611.2b).
	/// </summary>
	public bool WhileSourceRemains { get; init; }

	/// <summary>
	/// Necromancy: "it becomes an Aura with 'enchant creature put onto the battlefield with Necromancy'"; Animate Dead:
	/// "it loses 'enchant creature card in a graveyard' and gains 'enchant creature …'" — its enchant from now on
	/// (UntilEndOfTurn.Enchant), so ForGood only. ponytail: "put onto the battlefield with this" is read as plain
	/// "creature" — another effect could attach it to any creature (none in the cube does; DesignNotes).
	/// </summary>
	public TargetSpec? Enchant { get; init; }

	/// <summary>
	/// Celestial Colonnade: "becomes a 4/4 white and blue Elemental creature" (CR 205.1b: the colours are SET, not added)
	/// — read by Characteristics.ColorsOf while it lasts.
	/// </summary>
	public ImmutableArray<ManaColor>? Colors { get; init; }

	public override ActionResult Execute(GameState s)
	{
		if (Enchant is not null && !ForGood)
			throw new InvalidOperationException(
				"Enchant is a change no cleanup undoes: ForGood only (MC-4.4h5)"
			);
		if (WhileSourceRemains)
		{
			if (!LosesAllAbilities)
				throw new InvalidOperationException(
					"WhileSourceRemains lasts for LosesAllAbilities only (MC-4.4e2e)"
				);
			if (!Ref.Source.Resolve(s, InputContext).Any() || !s.IsOnBattlefield(Source(s).Id))
				return new(s);
			return new(
				Permanents(What, s)
					.Aggregate(
						s,
						(st, c) =>
							Update(
								st,
								(Card)
									st.GetCard(Source(s).Id)
										.WithComponent(
											new EnchantedLosesAllAbilities
											{
												Object = new Filters.TheObject(c.Id, c.Incarnation),
											}
										)
							)
					)
			);
		}
		var effect = new UntilEndOfTurn(
			Power.Value(s, InputContext),
			Toughness.Value(s, InputContext),
			Keyword
		)
		{
			SetBase = SetBase,
			Trigger = Trigger,
			ExileInsteadOf = ExileInsteadOf,
			LosesAllAbilities = LosesAllAbilities,
			UntilTurnOf =
				ForGood ? UntilEndOfTurn.Never
				: UntilYourNextTurn ? Controller
				: 0,
			BecomesCreatureTypes = BecomesCreatureTypes,
			Gains = Gains,
			AlsoCreatureType = AlsoCreatureType,
			Enchant = Enchant,
			Colors = Colors,
		};
		if (AlsoType != Cards.CardType.None && UntilYourNextTurn)
			throw new InvalidOperationException(
				"AlsoType is a stamp the cleanup step undoes: for good or until end of turn, never until your next turn (MC-4.4k3)"
			);
		return new(
			Permanents(What, s)
				.Aggregate(
					s,
					(st, c) =>
						Update(
							st,
							(Card)
								(
									(c.Types & AlsoType) == AlsoType
										? c
										: c.WithTypesUntilItLeaves(
											c.Types | AlsoType,
											endsAtCleanup: !ForGood
										)
								).WithComponent(effect)
						)
				)
		);
	}
}

/// <summary>
/// A Class's "[Cost]: Level N" (CR 716.2a; Does Machines: "{1}{U}: Level 2") as it resolves — "this Class's level
/// becomes N": ClassLevel on this object of the source, and BecameLevelEvent ("when this Class becomes level 2").
/// Written with Scripting.Level. A source gone (or a new object of it) gains nothing.
/// </summary>
public record GainLevel(int N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var c in Permanents(Ref.Source, s).ToList())
			s = Update(s, (Card)c.WithComponentReplaced(new ClassLevel(N)))
				.Raise(new BecameLevelEvent(c.Id, N));
		return new(s);
	}
}

/// <summary>
/// H.M.S. Bounty: "take an extra turn after this one" (CR 500.7) — queued on MagicGame.ExtraTurns, begun by
/// TurnStructure.FinishCleanup. Adapted from MtgCore's TakeExtraTurnAction (a count: it has one player a turn).
/// </summary>
public record TakeExtraTurn : Effect
{
	/// <summary>Time Warp: "Target player takes an extra turn after this one."</summary>
	public Ref Who { get; init; } = Ref.You;

	public override ActionResult Execute(GameState s) =>
		new(
			s.WithGame(
				s.GetGame() with
				{
					ExtraTurns = s.GetGame().ExtraTurns.AddRange(All(Who, s)),
				}
			)
		);
}

/// <summary>
/// Fear of Missing Out: "After this phase, there is an additional combat phase." (CR 500.8) — counted on
/// MagicGame.AdditionalCombats, taken as the end of combat step ends (TurnStructure.EndStep): right for a combat step,
/// and for the precombat main phase (that combat, then the turn's own: two in a row either way). ponytail: refused
/// anywhere else — after the second main phase it would need a combat begun from there and the end step after it, and
/// "followed by an additional main phase" (Relentless Assault) a main phase owed; no card in any set says either.
/// </summary>
public record AdditionalCombatPhase : Effect
{
	public override ActionResult Execute(GameState s) =>
		s.GetGame().Step is < Step.PreCombatMain or > Step.EndCombat
			? throw new InvalidOperationException(
				$"An additional combat phase after the {s.GetGame().Step} isn't built — only during combat or the first main phase"
			)
			: new(
				s.WithGame(
					s.GetGame() with
					{
						AdditionalCombats = s.GetGame().AdditionalCombats + 1,
					}
				)
			);
}

/// <summary>
/// "Put N [Kind] counters on [What]" (CR 122) — every kind, on any permanent, through MagicState.PutCounters
/// (Yoshimaru's, Brad's and Geordi's replacements apply there). +1/+1 by default; Cryotheory Adept: { Kind =
/// Stun }; Gardenize: { Kind = Charge }; Anthropological Mission: { Kind = Quest }. N is counted for EACH
/// object, with Ref.It that object — Bristly Bill, Spine Sower: "Double the number of +1/+1 counters on each
/// creature you control" is N = Count.CountersOn(Ref.It). Graft Surgeon's "put its counters on …" is one per
/// kind, counted on Ref.EventCard as it last was.
/// </summary>
public record PutCounters(Ref What, Count N) : Effect
{
	/// <summary>
	/// The kind. Null is Dathon, His Heart Open: "Choose a counter on target enchantment you control. Put
	/// another counter of that kind on it." — one kind on it: that kind, unasked; several: chosen by name.
	/// ponytail: on the first object What names only (Dathon's is a target); per object if a card says "each".
	/// </summary>
	public string? Kind { get; init; } = CounterKinds.PlusOne;

	public override ActionResult Execute(GameState s)
	{
		if (Kind is null)
			return Permanents(What, s).FirstOrDefault() is { } first
				? OfAKindOn(
					s,
					first,
					N.Value(s, InputContext.SetItem(ContextKeys.It, first.Id)),
					Controller,
					InputContext
				)
				: new(s);
		// on a permanent, or a player (CR 122.1) — Galvanic Discharge: "You get {E}{E}{E}"; Etali: "they get that many
		// poison counters"
		foreach (
			var id in All(What, s)
				.Where(id => s.IsOnBattlefield(id) || s.GetObject(id) is MagicPlayer)
				.ToList()
		)
			s = s.PutCounters(id, Kind, N.Value(s, InputContext.SetItem(ContextKeys.It, id)));
		return new(s);
	}

	/// <summary>
	/// N counters of ONE kind already on that permanent, put (Dathon) or — N negative — removed (Leatherhead,
	/// RemoveCounters.AnyKind): one kind on it is that kind, unasked; several are chosen by name.
	/// </summary>
	internal static ActionResult OfAKindOn(
		GameState s,
		Card card,
		int n,
		int controller,
		ImmutableDictionary<string, object> context
	)
	{
		ImmutableArray<string> kinds = [.. card.AllCounters().Select(c => c.Kind)];
		return kinds.Length switch
		{
			0 => new(s),
			1 => new(s.PutCounters(card.Id, kinds[0], n)),
			_ => new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = context,
						Steps =
						[
							new ChooseKeywordChoice(controller, kinds)
							{
								Prompt = "Choose a kind of counter",
							},
							new AddChosenCounterAction(card.Id, kinds, n),
						],
					}
				)
			),
		};
	}
}

/// <summary>
/// "Remove N [Kind] counters from [What]" — each object counted for itself, with Ref.It that object:
/// Beckett Mariner's "remove those counters" is N = Count.CountersOfKind(Ref.It, Promotion). Never below 0.
/// </summary>
public record RemoveCounters(Ref What, Count N) : Effect
{
	/// <summary>
	/// The kind. Null is Mabel: "remove up to N counters from …". ponytail: no prompt — from your own
	/// permanent it removes the harmful ones (stun, -1/-1, finality), from theirs the helpful ones (+1/+1,
	/// loyalty); never the other kind. Make it a choice if a card ever wants something else.
	/// </summary>
	public string? Kind { get; init; }

	/// <summary>
	/// Leatherhead, Swamp Stalker: "you may remove a counter from her" — of whichever kind its controller chooses
	/// among those on the first object What names (one kind: unasked), PutCounters' choose-a-kind removing. It is
	/// how a null Kind is resolved — chosen, in place of Mabel's rule — so a Kind beside it is never read.
	/// ponytail: the kinds are Card.AllCounters — a finality counter (a flag) isn't one; and the first object only.
	/// </summary>
	public bool AnyKind { get; init; }

	public override ActionResult Execute(GameState s)
	{
		if (AnyKind)
			return Permanents(What, s).FirstOrDefault() is { } first
				? PutCounters.OfAKindOn(
					s,
					first,
					-N.Value(s, InputContext.SetItem(ContextKeys.It, first.Id)),
					Controller,
					InputContext
				)
				: new(s);
		// a player's (CR 122.1) — Guide of Souls: "you may pay {E}{E}{E}" (PutCounters' reach)
		if (Kind is not null)
			foreach (var id in All(What, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
				s = s.PutCounters(id, Kind, -N.Value(s, InputContext.SetItem(ContextKeys.It, id)));
		foreach (var target in Permanents(What, s).ToList())
		{
			var n = N.Value(s, InputContext.SetItem(ContextKeys.It, target.Id));
			if (Kind is not null)
			{
				s = s.PutCounters(target.Id, Kind, -n);
				continue;
			}
			var left = n;
			int Take(int have)
			{
				var n = Math.Min(have, left);
				left -= n;
				return n;
			}
			var c = s.GetCard(target.Id);
			if (c.ControllerId == Controller)
			{
				var stun = Take(c.StunCounters);
				var minus = Take(c.MinusOneCounters);
				var finality = c.HasFinalityCounter && Take(1) == 1;
				s = Update(
					s,
					c with
					{
						StunCounters = c.StunCounters - stun,
						MinusOneCounters = c.MinusOneCounters - minus,
						HasFinalityCounter = c.HasFinalityCounter && !finality,
					}
				);
			}
			else
			{
				s = Update(
					s,
					c with
					{
						PlusOneCounters = c.PlusOneCounters - Take(c.PlusOneCounters),
					}
				);
				if (c.Is(CardType.Planeswalker))
					s = Actions.Loyalty.Change(s, c.Id, -Take(c.Loyalty));
			}
		}
		return new(s);
	}
}

/// <summary>"Put N loyalty counters on …" — no-op on anything that is not a planeswalker.</summary>
public record AddLoyalty(Ref What, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(
			Permanents(What, s)
				.Where(c => c.Is(CardType.Planeswalker))
				.Aggregate(s, (st, c) => Actions.Loyalty.Change(st, c.Id, n, Controller))
		);
	}
}

/// <summary>
/// Proft: "you may pay {2}" — the first step of a May's Then; its Can is <see cref="CanPay"/> (Scripting.MayPay
/// for "if you don't" too). The cost is Cost, or the mana cost of ManaCostOf, plus Generic, reduced by ReducedBy.
/// </summary>
public record PayMana(ManaCost Cost, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>Flash: "its mana cost" — the printed mana cost of the card this names (X is 0), in place of Cost.</summary>
	public Ref? ManaCostOf { get; init; }

	/// <summary>Transmute Artifact: "{X}, where X is the difference" — generic mana added, counted as it is paid.</summary>
	public Count? Generic { get; init; }

	/// <summary>
	/// Flash: "reduced by {2}"; Transmute: X is the found card's mana value reduced by the sacrificed one's — generic
	/// taken off, never the coloured pips (CR 601.2f), never below none.
	/// </summary>
	public Count? ReducedBy { get; init; }

	/// <summary>What paying it costs now.</summary>
	public ManaCost Owed(GameState s, ImmutableDictionary<string, object> context)
	{
		var cost =
			ManaCostOf is null ? Cost
			: ManaCostOf.Resolve(s, context).FirstOrDefault() is var id
			&& id != 0
			&& s.HasObject(id)
			&& s.GetObject(id) is Card card
				? card.ManaCost
			: ManaCost.None;
		cost = cost.WithGeneric(Math.Max(0, Generic?.Value(s, context) ?? 0));
		// ponytail: a printed {U/P} (Flash putting in Phyrexian Metamorph) is paid with its colour, never 2 life —
		// offer the life as a yes/no when a card makes it matter (DesignNotes).
		return cost.WithGeneric(-Math.Max(0, ReducedBy?.Value(s, context) ?? 0)).PayingLifeFor(0);
	}

	/// <summary>Pays, then runs Then — or, unable to pay, does nothing at all ("if you do").</summary>
	public override ActionResult Execute(GameState s) =>
		ManaPayment.Find(s, Controller, Owed(s, InputContext), 0) is not { } payment
			? new(s)
			: new(
				ManaPayment
					.Pay(s, Controller, payment)
					.SpawnAction(
						new PipelineAction { Steps = Then, PipelineContext = InputContext }
					)
			);
}

/// <summary>
/// "Copy that spell. You may choose new targets for the copy." (CR 707.10) The copy goes on the
/// stack above it, controlled by you; it is not cast (no "whenever you cast" sees it — magecraft's "or copy" does:
/// SpellCopiedEvent) and ceases to exist once it leaves the stack. Its targets are chosen anew from what is legal
/// for it — the original's among them.
/// </summary>
public record CopySpell(Ref What) : Effect
{
	/// <summary>Storm (Brain Freeze): "copy it for each spell cast before it this turn" — how many copies; unset is one.</summary>
	public Count? N { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var n = N?.Value(s, InputContext) ?? 1;
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		// Countered or gone in response, it is still copied — as it last was on the stack.
		var originals = All(What, s)
			.Where(id => s.GetParent(id) == stack)
			.Select(s.GetCard)
			.ToList();
		if (originals.Count == 0 && GetInput<Card?>(ContextKeys.EventSnapshot) is { } lastKnown)
			originals.Add(lastKnown);
		foreach (
			var original in originals
				.Where(c => c.HasComponent<OnStackComponent>())
				.SelectMany(c => Enumerable.Repeat(c, Math.Max(0, n)))
		)
		{
			Card copy;
			(s, copy) = s.AddObject(
				(Card)
					(original with { IsCopy = true, ControllerId = Controller })
						.WithoutComponents<PreparedCopyOf>()
						// Not cast: what this cast got (Theorist's Proxy) is the original's alone.
						.WithComponentReplaced(
							original.GetComponent<OnStackComponent>()! with
							{
								CantBeCountered = false,
								FirstSpellThisGame = false,
								ColorsSpent = 0,
								KeywordsUntilEndOfTurn = [], // Arena of Glory's haste was the cast's mana's
							}
						),
				stack
			);
			s = s.MoveObjectToFront(copy.Id, stack) // index 0 is the top
				.Raise(new SpellCopiedEvent(copy.Id, Controller));
			var onStack = copy.GetComponent<OnStackComponent>()!;
			var specs = StackResolution
				.TextOf(copy, onStack.Modes, onStack.Kicked, onStack.X, onStack.AlternativeCost)
				.Specs;
			if (specs.IsEmpty)
				continue;
			s = s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						.. specs.Select(
							(spec, i) => new ChooseTargetChoice(Controller, copy.Id, spec, i)
						),
						new SetCopyTargetsAction(copy.Id),
					],
				}
			);
		}
		return new(s);
	}
}

/// <summary>
/// The copy's targets, as chosen — a word with no new choice keeps the original's (CR 707.10: so a
/// copy whose target is gone fizzles, as the spell would). Each "becomes the target" of it (ward).
/// </summary>
public record SetCopyTargetsAction(int CopyId) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		if (!s.HasObject(CopyId))
			return new(s);
		var copy = s.GetCard(CopyId);
		var onStack = copy.GetComponent<OnStackComponent>()!;
		var chosen = ChooseTargetChoice.Read(InputContext, onStack.Targets.Length);
		ImmutableArray<int> targets =
		[
			.. chosen.Select((id, i) => id != 0 ? id : onStack.Targets[i]),
		];
		// Replace, never add: GetComponent reads the first.
		s = s.UpdateObject(CopyId, copy.WithComponentReplaced(onStack with { Targets = targets }));
		return new(Turns.Triggers.RaiseTargeted(s, targets, copy.ControllerId, CopyId));
	}
}

/// <summary>Culber and Stamets, Star-Crossed: "Add two mana of any one color." — the colour chosen as it resolves.</summary>
public record AddManaOfAnyOneColor(Count Amount) : Effect
{
	/// <summary>Dal R'El, Augment Orphan: "add {R} or {G}" — the colours to choose from; empty is any.</summary>
	public ImmutableArray<ManaColor> Colors { get; init; } = [];

	/// <summary>
	/// Wizard's Rockets: "Add X mana in any combination of colors."; Coalition Relic: "Add one mana of any color for
	/// each charge counter removed this way." — a colour chosen for EACH unit, counted as it resolves; never one option
	/// per combination (5^X).
	/// </summary>
	public bool AnyCombination { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						.. Enumerable
							.Range(
								0,
								AnyCombination ? Math.Max(0, Amount.Value(s, InputContext)) : 1
							)
							.SelectMany(_ =>
								(GameAction[])
									[
										new ChooseColorChoice(Controller) { Colors = Colors },
										new AddChosenColorManaAction(AnyCombination ? 1 : Amount),
									]
							),
					],
				}
			)
		);
}

/// <summary>Culber and Stamets, after the choice: "Add two mana of [the chosen] color."</summary>
public record AddChosenColorManaAction(Count Amount) : Effect
{
	public override ActionResult Execute(GameState s) =>
		InputContext.GetValueOrDefault(ChooseColorChoice.Chosen) is int id
			? (
				new AddMana(ChooseColorChoice.ColorOf(id), Amount) with
				{
					InputContext = InputContext,
				}
			).Execute(s)
			: new(s);
}

/// <summary>After the choice: N more counters of the chosen kind (PutCounters with no Kind).</summary>
public record AddChosenCounterAction(int CardId, ImmutableArray<string> Kinds, int N = 1) : Effect
{
	public override ActionResult Execute(GameState s) =>
		InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is int id
		&& s.IsOnBattlefield(CardId)
			? new(s.PutCounters(CardId, Kinds[id - 1], N))
			: new(s);
}

/// <summary>Hungering Puppetbeast: "It gains your choice of trample, hexproof, or haste until end of turn."</summary>
public record GainsChosenKeyword(Ref What, ImmutableArray<string> Keywords) : Effect
{
	/// <summary>
	/// Mother of Runes: "gains protection from the color of your choice until end of turn" — what each choice grants
	/// beyond its word, by the same index (`Protection(Is.Color(c))`), through ModifyUntilEndOfTurn.Gains; the word
	/// is only for the table, as Fear's. Empty: the keyword alone.
	/// </summary>
	public ImmutableArray<GameComponent> Grants { get; init; } = [];

	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new ChooseKeywordChoice(Controller, Keywords),
						new GrantChosenKeywordAction(What, Keywords) { Grants = Grants },
					],
				}
			)
		);
}

/// <summary>After the choice: the chosen keyword, until end of turn.</summary>
public record GrantChosenKeywordAction(Ref What, ImmutableArray<string> Keywords) : Effect
{
	/// <summary>GainsChosenKeyword.Grants: the component each choice grants, by its index.</summary>
	public ImmutableArray<GameComponent> Grants { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		if (InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is not int id)
			return new(s);
		return (
			new ModifyUntilEndOfTurn(What, 0, 0, Keywords[id - 1]) with
			{
				InputContext = InputContext,
				Gains = Grants.IsDefaultOrEmpty ? [] : [Grants[id - 1]],
			}
		).Execute(s);
	}
}

/// <summary>
/// Vanishing: "remove a time counter from it. When the last is removed, sacrifice it." ponytail: one
/// ability, where the rules have two — the same game unless a player acts between them.
/// </summary>
public record RemoveTimeCounter(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var card in Permanents(What, s).ToList())
		{
			if (card.TimeCounters <= 0)
				continue;
			s = s.UpdateObject(card.Id, card with { TimeCounters = card.TimeCounters - 1 });
			if (card.TimeCounters == 1)
				s = s.Sacrifice(card.Id);
		}
		return new(s);
	}
}

/// <summary>
/// "You may pay any amount of [life / {E}]" and what that many does — Necrodominance: "you may pay any amount of life.
/// If you do, draw that many cards."; Galvanic Discharge: "then you may pay any amount of {E}. Galvanic Discharge deals
/// that much damage to that permanent." A number from 0 (not paying) to what you have, then <see cref="Then"/> with
/// Count.AmountPaid that number — only when it is more than 0.
/// </summary>
public record PayAnyAmount : Effect
{
	/// <summary>The kind of counter paid — Galvanic Discharge's CounterKinds.Energy (MagicPlayer.Counters). Null is life.</summary>
	public string? Kind { get; init; }

	/// <summary>Necrodominance: never more cards than your library — the most that may be paid.</summary>
	public Count? AtMost { get; init; }

	public string Prompt { get; init; } = "Pay how much?";

	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var player = s.GetPlayer(Controller);
		// ponytail: never your whole life — it loses the game, and each number is a look-ahead the AI settles.
		var max =
			Kind is { } kind ? player.CountersOf(kind)
			: Rules.Life.CantChange(s, Controller) ? 0 // CR 119.8: no life can be paid
			: player.Life - 1;
		if (AtMost is { } most)
			max = Math.Min(max, most.Value(s, InputContext));
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new ChooseNumberChoice(Controller, Math.Max(0, max)) { Prompt = Prompt },
						new PayChosenAmount(this),
					],
				}
			)
		);
	}
}

/// <summary>
/// After PayAnyAmount's choice: that much paid, then its Then — Galvanic Discharge's {E} removed, then "deals that much
/// damage"; Necrodominance's life lost, then "draw that many cards".
/// </summary>
public record PayChosenAmount(PayAnyAmount Pay) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Count.AmountPaid.Value(s, InputContext);
		if (n <= 0)
			return new(s);
		s = Pay.Kind is { } kind
			? s.PutCounters(Controller, kind, -n)
			: new LoseLife(Ref.You, n) { InputContext = InputContext }
				.Execute(s)
				.GameState;
		return Pay.Then.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction { PipelineContext = InputContext, Steps = Pay.Then }
				)
			);
	}
}

/// <summary>
/// Etali, Primal Conqueror: "{9}{G/P}: Transform Etali." (CR 701.28) — the same object with its other face up
/// (Card.Transform): no zone change, its damage and counters stay; a card with no back face doesn't. It raises
/// TransformedEvent (Revealing Eye: "When this creature transforms into Revealing Eye"). An ability of a permanent
/// transforms it only if it hasn't transformed since the ability was put on the stack (CR 701.28f): Cecil, Dark
/// Knight's second "deals damage" trigger, or Etali activated twice, doesn't turn it back.
/// ponytail: "hasn't transformed" is read as "the same face up as then" — transformed twice since reads as never.
/// </summary>
public record Transform(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var card in Permanents(What, s).ToList())
		{
			if (
				card.Id == GetInput<int>(ContextKeys.Source)
				&& Conditions.SourceAsItWas(s, InputContext) is { } was
				&& was.HasComponent<Transformed>() != card.HasComponent<Transformed>()
			)
				continue;
			var turned = card.Transform();
			if (!ReferenceEquals(turned, card))
				s = s.UpdateObject(card.Id, turned).Raise(new TransformedEvent(card.Id));
		}
		return new(s);
	}
}

/// <summary>Way of the Paradox: "You may play an additional land this turn."</summary>
public record AdditionalLandDrop : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.Tally(Controller, t => t with { ExtraLandDrops = t.ExtraLandDrops + 1 }));
}

/// <summary>
/// "You may [do something]. If you do, [Then]." Asked only when it can be done (<see cref="Can"/>);
/// the doing is the first of Then. Guiding Hydra: "you may remove a +1/+1 counter …".
/// </summary>
public record May(string Prompt, Condition Can, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>Clash of Elements: "its owner may …" — who answers. Unset is you; the effects stay yours.</summary>
	public Ref? Who { get; init; }

	/// <summary>"If they didn't, [Otherwise]" — run on a "no". Not run when it can't be asked.</summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	/// <summary>Peace in Our Time: "that player may draw a card. If they do, they can't attack this turn" — Then runs as the one who answers ("you" is them).</summary>
	public bool ThenAsThem { get; init; }

	/// <summary>
	/// Lim-Dûl's Vault: "As many times as you choose, you may pay 1 life, …" — after Then, the same question again,
	/// while it can be done; a "no" ends it.
	/// </summary>
	public bool Again { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var decider = Who is null ? Controller : All(Who, s).FirstOrDefault();
		var context = InputContext.Remove(ChoosePermanentChoice.Chosen);
		if (ThenAsThem && decider != 0)
			context = context.SetItem(ContextKeys.Controller, decider);
		return !Can.Holds(s, ThenAsThem ? context : InputContext) || decider == 0
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						// Its own key, cleared — as MaySacrifice: a "no" must not read a stale "yes".
						PipelineContext = context,
						Steps =
						[
							new YesNoChoice(decider, Prompt),
							// Again: Then as a child pipeline, so its steps each finish — a look after a look sees
							// the cards the first moved — before the question is asked again
							new IfChosenAction(Again ? [new If(new Always(), Then), this] : Then)
							{
								Otherwise = Otherwise,
							},
						],
					}
				)
			);
	}
}

/// <summary>CR 701.14: each deals damage equal to its power to the other. Nothing if either is gone.</summary>
public record Fight(Ref A, Ref B) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var a = Permanents(A, s).FirstOrDefault(c => c.Is(CardType.Creature));
		var b = Permanents(B, s).FirstOrDefault(c => c.Is(CardType.Creature));
		if (a is null || b is null)
			return new(s);
		s = Damage.Deal(s, a, b.Id, s.Power(a));
		return new(Damage.Deal(s, b, a.Id, s.Power(b)));
	}
}

/// <summary>"Untap [What]" — through MagicState.Untap, so a stun counter is removed instead.</summary>
public record Untap(Ref What) : Effect
{
	/// <summary>
	/// Frantic Search: "Untap up to three lands" (Time Spiral's six) — a choice among what What names, then those
	/// untapped. ponytail: only the TAPPED ones you control are offered, and as many as can be untapped are asked
	/// for (unasked when that is all of them): an untapped land does nothing untapped, an opponent's is never yours
	/// to want, and the AI answers a choice of several with its minimum — asked as "up to", it would never untap one.
	/// </summary>
	public Count? UpTo { get; init; }

	public override ActionResult Execute(GameState s)
	{
		if (UpTo is null)
			return new(Permanents(What, s).Aggregate(s, (st, c) => st.Untap(c.Id)));
		ImmutableList<int> mine =
		[
			.. Permanents(What, s)
				.Where(c => c.IsTapped && c.ControllerId == Controller)
				.Select(c => c.Id),
		];
		var n = Math.Min(UpTo.Value(s, InputContext), mine.Count);
		if (n == mine.Count)
			return new(mine.Aggregate(s, (st, id) => st.Untap(id)));
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps =
					[
						new ChooseAmongChoice
						{
							PlayerId = Controller,
							Ids = mine,
							Zone = ZoneType.Battlefield,
							MinChoices = n,
							MaxChoices = n,
							Prompt = $"Untap {n}",
						},
						new Untap(Ref.Chosen),
					],
				}
			)
		);
	}
}

/// <summary>"You get an emblem with [Abilities]" (CR 114) — a card in your command zone, for good.</summary>
public record GetEmblem(ImmutableArray<GameComponent> Abilities) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			ToCommandZone(
				s,
				Controller,
				$"Emblem — {Source(s).Name}",
				// Wrenn and Six's retrace is a play-from-a-zone rule, read where "this turn" ones are: an emblem's
				// lasts the game (CR 114.4), never removed with the turn
				Abilities.Any(a => a is ThisTurnEffect)
					? [.. Abilities, new RestOfTheGame()]
					: Abilities
			)
		);

	/// <summary>The ONE way an object is made in the command zone — emblems and effects alike.</summary>
	public static GameState ToCommandZone(
		GameState s,
		int playerId,
		string name,
		ImmutableArray<GameComponent> components
	) =>
		s.AddObject(
			new Card
			{
				Name = name,
				OwnerId = playerId,
				ControllerId = playerId,
				Components = components,
			},
			s.ZoneId(playerId, ZoneType.Command)
		).GameState;
}

/// <summary>
/// Jace, Reality Sculptor's −3, Garruk, Curse Breaker's −4: "Until your next turn, whenever …" —
/// an effect object in your command zone, so it outlives the planeswalker; it ends as your next
/// turn begins (TurnStructure.EndUntilYourNextTurn).
/// </summary>
public record UntilYourNextTurnWhenever(TriggeredAbilityComponent Ability) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			GetEmblem.ToCommandZone(
				s,
				Controller,
				$"{Source(s).Name} (until your next turn)",
				[new UntilEndOfTurn { UntilTurnOf = Controller, Trigger = Ability }]
			)
		);
}

/// <summary>
/// Deep Space Scan, Live Long and Prosper: "You have no maximum hand size until your next turn." — an
/// object in your command zone, gone as your next turn begins (TurnStructure.EndUntilYourNextTurn).
/// </summary>
public record NoMaximumHandSizeUntilYourNextTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			GetEmblem.ToCommandZone(
				s,
				Controller,
				$"{Source(s).Name} (until your next turn)",
				[new UntilEndOfTurn { UntilTurnOf = Controller, NoMaximumHandSize = true }]
			)
		);
}

/// <summary>
/// "This turn" / "until end of turn" effects that belong to no permanent: each is an object in its
/// controller's command zone, so the table shows it, and it ends as the next turn begins
/// (TurnStructure, with the TurnTally reset). Read by <see cref="MagicState.ThisTurnEffects{T}"/>;
/// one that is used up ("the next spell") is removed by what uses it.
/// </summary>
public abstract record ThisTurnEffect : GameComponent;

/// <summary>
/// The Naked Time, chapter II: "Whenever a creature you control enters this turn, …" — the ability on an
/// object in your command zone, gone with the turn's other "this turn" effects.
/// </summary>
public record WheneverThisTurn(TriggeredAbilityComponent Ability) : Effect
{
	/// <summary>
	/// A Good Day to Die: "When that creature dies this turn, you draw a card." — the trigger's subject
	/// is that one permanent, as it is now; the ability stays yours, whoever controls the creature.
	/// The Inner Light: "Whenever it deals combat damage to a player this turn" — a combat-damage
	/// trigger narrows its damage source instead (the subject there is the player).
	/// </summary>
	public Ref? About { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var ability = Ability;
		if (About is { } about)
		{
			if (Permanents(about, s).FirstOrDefault() is not { } that)
				return new(s);
			Filters.Filter only = new Filters.TheObject(that.Id, that.Incarnation);
			ability = ability with
			{
				On = ability.On.CombatDamageBy is { } by
					? ability.On with
					{
						CombatDamageBy = by & only,
					}
					: ability.On with
					{
						Subject = ability.On.Subject is { } f ? f & only : only,
					},
			};
		}
		return new(
			GetEmblem.ToCommandZone(
				s,
				Controller,
				$"{Source(s).Name} (this turn)",
				[ability, new TriggersThisTurn()]
			)
		);
	}
}

/// <summary>
/// Erica Ortegas: "return a land you control to its owner's hand at the beginning of the next end step"
/// — a delayed trigger (CR 603.7) on an object in your command zone, removed as it triggers
/// (Triggers.PutOnStack); set up during the end step, it waits for the next turn's.
/// </summary>
public record AtNextEndStep(ImmutableList<GameAction> Effects) : Effect
{
	/// <summary>
	/// Dr. Leonard "Bones" McCoy: "When it dies this turn, return that card to the battlefield … at the
	/// beginning of the next end step" — the delayed trigger on that card where it now is (its
	/// graveyard), as Flicker's is on its exiled card: gone if the card leaves (a new object, CR 400.7);
	/// a token never gets one. Its "this" (Ref.Source) is that card. Sneak Attack: "Sacrifice the creature at the
	/// beginning of the next end step" — on a permanent on the battlefield the same way (a token too).
	/// </summary>
	public Ref? On { get; init; }

	/// <summary>
	/// The step it waits for — the end step unless said. Mana Drain: "At the beginning of your next main phase"
	/// (Step.PreCombatMain) — your precombat main, or this turn's postcombat main when set up during your turn before it
	/// (CR 505.1: both are main phases; the card's rulings). Mishra's Bauble: "at the beginning of the next turn's
	/// upkeep" (Step.Upkeep) — whoever's turn that is. Any other step: the next one of anyone's.
	/// </summary>
	public Step At { get; init; } = Step.End;

	/// <summary>
	/// Memory Jar: "At the beginning of the next end step, each player … returns to their hand each card they exiled
	/// this way." — the cards a Ref names AS IT IS SET UP (Ref.ThisWay, in the exile's Then), the delayed trigger's own
	/// Ref.ThisWay when it resolves: its "this" is a command-zone object, which no link (ExiledBy) names.
	/// </summary>
	public Ref? Those { get; init; }

	/// <summary>Mana Drain: "equal to that spell's mana value" — counted as it is set up, the delayed trigger's X.</summary>
	public Count? X { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var game = s.GetGame();
		var trigger = new DelayedTrigger(
			new TriggeredAbilityComponent(
				At switch
				{
					Step.End => TriggerEvent.EachEndStep,
					Step.PreCombatMain => game.ActivePlayerId == Controller
					&& game.Step is >= Step.PreCombatMain and < Step.PostCombatMain
						? TriggerEvent.Beginning(Step.PostCombatMain, Whose.You)
						: TriggerEvent.YourFirstMainPhase,
					_ => TriggerEvent.Beginning(At, Whose.Any),
				},
				Those is null ? Effects : [new WithThoseAction([.. All(Those, s)], Effects)]
			)
			{
				X = X?.Value(s, InputContext) ?? 0,
			}
		);
		if (On is { } on)
			return new(
				All(on, s)
					// on the battlefield (Sneak Attack), or in its owner's graveyard, where it died to —
					// ponytail: by id, so a card that left and came back there inside the same response
					// would still return; stamp the incarnation it died as if that ever matters
					.Where(id =>
						s.HasObject(id)
						&& s.GetObject(id) is Card card
						&& (
							s.IsOnBattlefield(id)
							|| (!card.IsToken && s.IsIn(id, card.OwnerId, ZoneType.Graveyard))
						)
					)
					.Aggregate(
						s,
						(st, id) =>
							MoveTo.Delay(
								st,
								id,
								trigger with
								{
									ControllerId = Controller, // CR 603.7d: yours
								}
							)
					)
			);
		return new(
			GetEmblem.ToCommandZone(
				s,
				Controller,
				$"{Source(s).Name} ({At switch
				{
					Step.End => "next end step",
					Step.PreCombatMain => "next main phase",
					_ => $"next {At.ToString().ToLowerInvariant()}",
				}})",
				[trigger]
			)
		);
	}
}

/// <summary>
/// AtNextEndStep.Those, as its delayed trigger resolves — Memory Jar: "each player discards their hand and returns to
/// their hand each card they exiled this way": the effects, with Ref.ThisWay the cards named when it was set up.
/// </summary>
public record WithThoseAction(ImmutableArray<int> Those, ImmutableList<GameAction> Effects) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.SetItem(ContextKeys.ThisWay, Those),
					Steps = Effects,
				}
			)
		);
}

/// <summary>
/// The Wrath of Khan, chapter I: "At the beginning of the next combat this turn, gain control of that
/// creature until end of turn." — a delayed trigger (CR 603.7) on that permanent, yours (CR 603.7d), gone
/// if it leaves. Its "this" (Ref.Source) is that permanent. ponytail: not removed as the turn ends — set
/// up in a precombat main phase (a Saga's chapter I) the next combat is this turn's; stamp the turn if a
/// card sets it up after combat.
/// </summary>
public record AtBeginningOfNextCombat(Ref On, ImmutableList<GameAction> Effects) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var trigger = new DelayedTrigger(
			new TriggeredAbilityComponent(
				TriggerEvent.Beginning(Step.BeginCombat, Whose.Any),
				Effects
			)
		)
		{
			ControllerId = Controller,
		};
		return new(
			Permanents(On, s).ToList().Aggregate(s, (st, c) => MoveTo.Delay(st, c.Id, trigger))
		);
	}
}

/// <summary>
/// Lwaxana Troi, Charming Guest: "Choose another target creature you control. For as long as Lwaxana
/// Troi remains tapped, if a triggered ability of that creature triggers, that ability triggers an
/// additional time." — nothing if she is already untapped as it resolves (CR 611.2b).
/// </summary>
public record TriggersAdditionalTimeWhileSourceTapped(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		if (!Ref.Source.Resolve(s, InputContext).Any() || !s.IsOnBattlefield(Source(s).Id))
			return new(s);
		var source = Source(s);
		if (!source.IsTapped || Permanents(What, s).FirstOrDefault() is not { } creature)
			return new(s);
		var unlinked = source with
		{
			Components = source.Components.RemoveAll(c =>
				c is TriggersAdditionalTime { UntilUntapped: true }
			),
		};
		return new(
			Update(
				s,
				(Card)
					unlinked.WithComponent(
						new TriggersAdditionalTime(
							null,
							new Filters.TheObject(creature.Id, creature.Incarnation)
						)
						{
							UntilUntapped = true,
						}
					)
			)
		);
	}
}

/// <summary>Marks WheneverThisTurn's object as a "this turn" effect, so the turn's end removes it.</summary>
public record TriggersThisTurn : ThisTurnEffect;

/// <summary>Puts a <see cref="ThisTurnEffect"/> in your command zone for the rest of the turn.</summary>
public record ThisTurn(ThisTurnEffect What) : Effect
{
	/// <summary>Sen Triplets: "This turn, that player can't …" — whose command zone (Ref.Target(0)); unset is you.</summary>
	public Ref? On { get; init; }

	/// <summary>
	/// Screaming Nemesis: "they can't gain life for the rest of the game" — the same object, never removed with the
	/// turn (<see cref="Effects.RestOfTheGame"/>); a player who already has that one gets no second.
	/// </summary>
	public bool RestOfTheGame { get; init; }

	/// <summary>
	/// The One Ring: "you gain protection from everything until your next turn" — the same object kept past this
	/// turn's end, gone as that player's next turn begins (UntilEndOfTurn.UntilTurnOf beside the effect,
	/// TurnStructure.EndUntilYourNextTurn).
	/// </summary>
	public bool UntilYourNextTurn { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var who = On is null
			? Controller
			: All(On, s).FirstOrDefault(id => s.HasObject(id) && s.GetObject(id) is MagicPlayer); // "if a PLAYER is dealt damage this way"
		if (
			RestOfTheGame
			&& who != 0
			&& s.CardsIn(who, ZoneType.Command)
				.Any(c => c.HasComponent<RestOfTheGame>() && c.Components.Contains(What))
		)
			return new(s);
		// Sen Triplets' "that player's hand": who it names, kept as it resolves — a target can't be read later.
		var what = What switch
		{
			MayPlayFromZone { Of: { } of } may => may with
			{
				OwnerId = All(of, s).FirstOrDefault(),
			},
			_ => What,
		};
		return who == 0
			? new(s)
			: new(
				GetEmblem.ToCommandZone(
					s,
					who,
					$"{Source(s).Name} ({(RestOfTheGame ? "rest of the game" : UntilYourNextTurn ? "until your next turn" : "this turn")})",
					RestOfTheGame ? [what, new RestOfTheGame()]
						: UntilYourNextTurn
							? [what, new UntilEndOfTurn { UntilTurnOf = Controller }] // YOUR next turn, whoever On names
						: [what]
				)
			);
	}
}

/// <summary>
/// Screaming Nemesis: "for the rest of the game" — marks a ThisTurn object the turn's end leaves (TurnStructure).
/// </summary>
public record RestOfTheGame : GameComponent;

/// <summary>
/// "Can't cast spells or activate abilities" — Yuriko, Blade of the Mighty: "During combat, players can't
/// cast spells or activate abilities that aren't mana abilities." (a permanent's: every player); Sen
/// Triplets: "This turn, that player can't cast spells or activate abilities" (in that player's command
/// zone, mana abilities too). Whose, which of the two, of which sources and when are its options (MC-4.4d2b:
/// Voice of Victory, Archon of Emeria, Clarion Conqueror). Combat.LockedDown.
/// </summary>
public record CantCastOrActivate : ThisTurnEffect
{
	/// <summary>Yuriko: "During combat" (DuringCombat); Voice of Victory: "during your turn" — judged as its holder's.</summary>
	public Condition? If { get; init; }

	/// <summary>Sen Triplets: "activate abilities" — mana abilities too (Kai Winn's ruling); TapForManaAction and ManaPayment ask.</summary>
	public bool ManaAbilitiesToo { get; init; }

	/// <summary>
	/// Voice of Victory: "Your opponents can't cast spells" — whose, from its holder's controller: a PERMANENT's.
	/// A "this turn" lock is put with the player it binds (ThisTurn.On, Sen Triplets) and needs none.
	/// </summary>
	public Whose By { get; init; } = Whose.Any;

	/// <summary>Clarion Conqueror: "Activated abilities … can't be activated" — false: spells are still cast.</summary>
	public bool Spells { get; init; } = true;

	/// <summary>Voice of Victory, Archon of Emeria: "can't cast spells" — false: abilities are still activated.</summary>
	public bool Abilities { get; init; } = true;

	/// <summary>
	/// Archon of Emeria: "Each player can't cast more than one spell each turn." — it binds only a player who
	/// has cast that many this turn (TurnTally.SpellsCast); 0: from the first.
	/// </summary>
	public int SpellsEachTurn { get; init; }

	/// <summary>
	/// Clarion Conqueror: "Activated abilities of artifacts, creatures, and planeswalkers" — the spell cast, or
	/// the PERMANENT whose ability it is (never a card in a hand or graveyard), asked from its holder.
	/// </summary>
	public Filters.Filter? Of { get; init; }

	/// <summary>
	/// Teferi, Time Raveler: "Each opponent can cast spells only any time they could cast a sorcery" — the lock binds
	/// only while the player it binds could NOT cast a sorcery now (Priority.AtSorcerySpeed: theirs, never the holder's).
	/// </summary>
	public bool ExceptAtSorcerySpeed { get; init; }
}

/// <summary>
/// Molten Tide: "Until end of turn, whenever you tap a Mountain for mana, add an additional {R}." — in a ThisTurn;
/// Badgermole Cub: "Whenever you tap a creature for mana, add an additional {G}." — on the permanent. One more mana of
/// that colour for each permanent of YOURS (its holder's controller's) the filter matches, as it is tapped for mana:
/// a mana ability triggering on a mana ability resolves at once (CR 605.1b), so it is part of the yield
/// (ManaAbilities.YieldOf) — payment, tapping by hand and the X bound all see it.
/// </summary>
public record ExtraMana(Filters.Filter What, ManaColor Color) : ThisTurnEffect;

/// <summary>Flare of Fortitude: "Until end of turn, your life total can't change" — Rules.Life (Gain, Lose, CanPay).</summary>
public record LifeTotalCantChange : ThisTurnEffect
{
	/// <summary>Screaming Nemesis: "they can't gain life" — only gaining: life is still lost and paid (Life.Gain).</summary>
	public bool GainOnly { get; init; }
}

/// <summary>Peace in Our Time: "they can't attack this turn" — on that player; Combat.CanAttack.</summary>
public record CantAttackThisTurn : ThisTurnEffect;

/// <summary>The Needs of the Many: "You gain hexproof until end of turn." — TargetSpec: no opponent's spell or ability targets you.</summary>
public record YouHaveHexproof : ThisTurnEffect;

/// <summary>The Needs of the Many: "Prevent all damage that would be dealt to you this turn." — Damage.Deal: none is dealt, so no lifelink either.</summary>
public record PreventAllDamageToYou : ThisTurnEffect;

/// <summary>
/// A numeric replacement for the rest of the turn — Brad Boimler: "until end of turn, if one or more
/// counters would be put on a permanent you control, that many plus one of each of those kinds of counters
/// are put on that permanent instead." Read by Rules.Replacements beside the permanents' own.
/// </summary>
public record ThisTurnReplacement(NumericReplacement Replacement) : ThisTurnEffect;

/// <summary>
/// Way of the Cryomancer's granted "[−3]: When you next cast an instant or sorcery spell this turn,
/// copy that spell. You may choose new targets for the copy." — a delayed trigger; the next such
/// spell takes them all (CastSpellAction).
/// </summary>
public record CopyNextInstantOrSorcery : ThisTurnEffect;

/// <summary>
/// "You may activate [their] loyalty abilities any time you could cast an instant" — Jace's Machinations: "you may
/// activate loyalty abilities of Jace planeswalkers you control … any time you could cast an instant" this turn
/// (Is.Subtype("Jace"), in a ThisTurn); The Wandering Emperor: "As long as The Wandering Emperor entered this
/// turn, you may activate her loyalty abilities any time you could cast an instant." (Is.EnteredThisTurn, on
/// her — a planeswalker's own is about itself). The filter is asked of the planeswalker; LoyaltyActions.
/// </summary>
public record LoyaltyAtInstantSpeed(Filters.Filter Of) : ThisTurnEffect;

/// <summary>Theorist's Proxy: "The next spell you cast this turn can't be countered." — taken by that cast.</summary>
public record NextSpellCantBeCountered : ThisTurnEffect;

/// <summary>Hall of Echoes: "The 'legend rule' doesn't apply to permanents you control this turn." — the state-based check.</summary>
public record NoLegendRule : ThisTurnEffect;

/// <summary>
/// Tam: "Proliferate X times" (CR 701.27). ponytail: no prompt — it chooses your permanents' good
/// counters (+1/+1, loyalty, charge) and opponents' bad ones (-1/-1, stun), never the other kind; as
/// RemoveCounters does. No FRA player has counters. Make it a choice if a card ever wants otherwise.
/// ponytail: the named counters (Card.Counters — quest, lore, promotion) aren't proliferated; offer
/// them once a card proliferates beside them (DesignNotes).
/// </summary>
public record Proliferate(Count Times) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		for (var i = Times.Value(s, InputContext); i > 0; i--)
			foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
			foreach (var c in s.CardsIn(p, ZoneType.Battlefield).ToList())
			{
				if (p == Controller)
				{
					if (c.PlusOneCounters > 0)
						s = s.AddPlusOneCounters(c.Id, 1);
					if (c.ChargeCounters > 0)
						s = s.PutCounters(c.Id, CounterKinds.Charge, 1);
					if (c.Loyalty > 0)
						s = Actions.Loyalty.Change(s, c.Id, 1, Controller);
				}
				else
				{
					if (c.MinusOneCounters > 0)
						s = s.PutCounters(c.Id, CounterKinds.MinusOne, 1);
					if (c.StunCounters > 0)
						s = s.PutCounters(c.Id, CounterKinds.Stun, 1);
				}
			}
		return new(s);
	}
}

/// <summary>"Add {C}" — or Gardenize's "{G} for each charge counter", counted as it resolves.</summary>
public record AddMana(ManaColor Color, Count Amount) : Effect
{
	public AddMana(ManaColor color)
		: this(color, 1) { }

	/// <summary>Chandra, Chill of Compliance: "Spend this mana only to cast a noncreature spell."</summary>
	public ManaRestriction Restriction { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		var pool = s.GetPlayer(Controller).ManaPool;
		pool =
			Restriction == ManaRestriction.None
				? pool.Add(Color, n)
				: pool with
				{
					Restricted = pool.Restricted.AddRange(
						Enumerable.Repeat(new RestrictedMana(Color, Restriction), n)
					),
				};
		return n <= 0
			? new(s)
			: new(s.WithPlayer(s.GetPlayer(Controller) with { ManaPool = pool }));
	}
}

/// <summary>"Tap [What]" — permanents only.</summary>
public record Tap(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => st.Tap(c.Id)));
}

/// <summary>
/// "[A] deals damage equal to its power (or loyalty) to [B]" - the source is A, not the spell, so
/// A's deathtouch and lifelink apply (Compel Brutality).
/// </summary>
public record DamageFrom(Ref From, Ref To, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		// Ingris: "that creature deals 1 damage" — the attacker as it last was, if it left in response.
		var from =
			Permanents(From, s).FirstOrDefault()
			?? (From.Kind == RefKind.EventCard ? GetInput<Card?>(ContextKeys.EventSnapshot) : null);
		if (from is null)
			return new(s);
		var amount = Amount.Value(s, InputContext);
		return new(All(To, s).Aggregate(s, (st, id) => Damage.Deal(st, from, id, amount)));
	}
}

/// <summary>
/// "Counter [it] unless its controller pays {N}" — Icy Reception, and ward. The payer CHOOSES (CR
/// 118.12): asked only when they can pay. MtgCore auto-pays; real rules don't.
/// </summary>
public record CounterUnlessPays(Ref What, ManaCost Cost) : Effect
{
	/// <summary>
	/// Ward with a cost beyond mana, paid by choosing as it resolves — Gideon the Oathless: [DiscardCost()];
	/// Emrakul: [SacrificeCost { Count = 3 }]. Empty: the mana Cost.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];

	/// <summary>
	/// No More Lies: "If that spell is countered this way, exile it instead of putting it into its owner's graveyard"
	/// (Exile) — Counter.To's option (Remand), only when it was countered.
	/// </summary>
	public ZoneType To { get; init; } = ZoneType.Graveyard;

	public override ActionResult Execute(GameState s)
	{
		if (!Costs.IsEmpty && (!Cost.Symbols.IsEmpty || Costs.Any(c => c.PaidByChoosing() is null)))
			throw new InvalidOperationException(
				"Ward pays a list of costs by choosing, with no mana besides: one can't be asked for"
			);
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		foreach (var id in All(What, s).Where(id => s.GetParent(id) == stack).ToList())
		{
			if (
				s.GetObject(id) is Card uncounterable
				&& !StackResolution.CanBeCountered(uncounterable)
			)
				continue; // nothing to pay for: the counter can't touch it
			var payer = s.StackObjectControllerId(id);
			var canPay =
				payer != 0
				&& (
					Costs.IsEmpty
						? ManaPayment.Find(s, payer, Cost, 0) is not null
						: Costs.All(c => c.CanPayByChoosing(s, payer))
				);
			if (!canPay)
			{
				s = StackResolution.Counter(s, id, To);
				continue;
			}
			var name = s.GetObject(id).Name;
			s = s.SpawnAction(
				new PipelineAction
				{
					// The payer is "you" for a discard; the counter reads the id, not who.
					PipelineContext = InputContext
						.Remove(ChoosePermanentChoice.Chosen)
						.SetItem(ContextKeys.Controller, payer),
					Steps = Costs.IsEmpty
						?
						[
							new PayToKeepChoice(payer, $"Pay {Cost} to keep {name}?"),
							new PayOrCounterAction(id, payer, Cost) { To = To },
						]
						:
						[
							new PayToKeepChoice(
								payer,
								$"{string.Join(" and ", Costs.Select(c => c.Describe()))} to keep {name}?"
							),
							new IfChosenAction([.. Costs.Select(c => c.PaidByChoosing()!)])
							{
								Otherwise = [new Counter(What) { To = To }],
							},
						],
				}
			);
		}
		return new(s);
	}
}

/// <summary>Precise Redaction: "Counter target spell" — through StackResolution.Counter.</summary>
public record Counter(Ref What) : Effect
{
	/// <summary>
	/// Remand: "If that spell is countered this way, put it into its owner's hand instead of into that player's
	/// graveyard" (Hand); Memory Lapse: "on top of its owner's library" (Library — the top).
	/// </summary>
	public ZoneType To { get; init; } = ZoneType.Graveyard;

	/// <summary>
	/// Tishana's Tidebinder: "If an ability of an artifact, creature, or planeswalker is countered this way, that
	/// permanent …" — run with Ref.ThisWay what was countered: a spell's card, an ability's SOURCE while it is still
	/// that object on the battlefield (StackAbility.Source's incarnation).
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var done = ImmutableArray.CreateBuilder<int>();
		foreach (var id in All(What, s).ToList())
		{
			var countered = s.HasObject(id) ? s.GetObject(id) : null;
			var after = StackResolution.Counter(s, id, To);
			if (!ReferenceEquals(after, s))
				switch (countered)
				{
					case Card spell:
						done.Add(spell.Id);
						break;
					case StackAbility { Delayed: false, Source: var source }
						when after.IsOnBattlefield(source.Id)
							&& after.GetCard(source.Id).Incarnation == source.Incarnation:
						done.Add(source.Id);
						break;
				}
			s = after;
		}
		return Then.IsEmpty || done.Count == 0
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.SetItem(
							ContextKeys.ThisWay,
							done.ToImmutable()
						),
						Steps = Then,
					}
				)
			);
	}
}

/// <summary>After the payer's answer: pay the cost, or the spell or ability is countered.</summary>
public record PayOrCounterAction(int StackObjectId, int PayerId, ManaCost Cost) : GameAction
{
	/// <summary>CounterUnlessPays.To: where a spell countered this way goes (No More Lies: exile).</summary>
	public ZoneType To { get; init; } = ZoneType.Graveyard;

	public override ActionResult Execute(GameState s) =>
		new(
			ChoosePermanentChoice.Read(InputContext).Any()
			&& ManaPayment.Find(s, PayerId, Cost, 0) is { } payment
				? ManaPayment.Pay(s, PayerId, payment)
				: StackResolution.Counter(s, StackObjectId, To)
		);
}

/// <summary>
/// "When you do, [effects]" — a reflexive trigger (CR 603.12): it triggers as this resolves, its
/// targets are chosen then, and it goes on the stack above — so a player can respond knowing what
/// was done. Tether Technician: "you may discard a card. When you do, …".
/// </summary>
public record WhenYouDo(ImmutableList<TargetSpec> Targets, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>
	/// Seasoned Cryomancer: "tap up to that many target creatures" — Targets[0], that many times, each
	/// optional and not the one before; nothing triggers at 0. ponytail: "not the one before" keeps
	/// two apart, all FRA needs; three or more could repeat a target.
	/// </summary>
	public Count? TargetsUpTo { get; init; }

	/// <summary>
	/// Ajani, Nacatl Avenger: "When you do, if you control a red permanent other than Ajani, …" — an intervening if
	/// (CR 603.4): nothing triggers unless it holds, and it is checked again as it resolves (the ability's If).
	/// </summary>
	public Condition? If { get; init; }

	/// <summary>
	/// Minsc &amp; Boo: "Sacrifice a creature. When you do, Minsc &amp; Boo deals X damage to any target, where X is that
	/// creature's power" — a number fixed as it triggers, read by Then as Count.X (Count.PowerOf(Ref.ThisWay): the
	/// sacrificed as it last was).
	/// </summary>
	public Count? X { get; init; }

	public override ActionResult Execute(GameState s)
	{
		if (If is { } condition && !condition.Holds(s, InputContext))
			return new(s);
		var x = X?.Value(s, InputContext) ?? 0;
		var targets = Targets;
		if (TargetsUpTo is { } upTo)
		{
			var n = upTo.Value(s, InputContext);
			if (n <= 0)
				return new(s);
			targets =
			[
				.. Enumerable
					.Range(0, n)
					.Select(i => Targets[0] with { Optional = true, NotThePrevious = i > 0 }),
			];
		}
		var ability = new TriggeredAbilityComponent(TriggerEvent.Reflexive, Then)
		{
			Targets = targets,
			If = If,
		};
		return new(
			Turns.Triggers.QueueTargets(
				s,
				new Turns.Triggers.Pending(
					Source(s) with
					{
						ControllerId = Controller,
					},
					ability,
					0,
					X: x
				),
				targets,
				Then
			)
		);
	}
}

/// <summary>
/// "You win the game" (CR 104.2a): every opponent loses, now — and the game ends, so no state-based
/// check runs after it (a same-moment draw from an empty library can't make it a draw).
/// </summary>
public record WinTheGame : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.WithPlayer(s.GetPlayer(s.OpponentOf(Controller)) with { HasLost = true }));
}

/// <summary>Kobayashi Maru Scenario: "Abandon Ship — You lose the game." (CR 104.3a) — now, as WinTheGame.</summary>
public record LoseTheGame : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.WithPlayer(s.GetPlayer(Controller) with { HasLost = true }));
}

/// <summary>
/// Captain Michael Burnham: "If this is the second time this ability has resolved this turn, put a
/// +1/+1 counter on Captain Burnham." — counted on the source as it resolves, Then run on exactly the
/// Nth. ponytail: one count per card, not per ability (no TRK card has two such abilities), and a
/// source that left the battlefield counts nothing (it runs Otherwise).
/// </summary>
public record IfNthResolutionThisTurn(int N, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>
	/// Scythecat Cub: "put a +1/+1 counter on target creature you control. If this is the second time this ability
	/// has resolved this turn, double the number of +1/+1 counters on that creature instead." — run on every other
	/// resolution.
	/// </summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var steps = Otherwise;
		if (All(Ref.Source, s).Where(s.IsOnBattlefield).FirstOrDefault() is > 0 and var id)
		{
			var card = s.GetCard(id);
			var turn = s.GetGame().TurnNumber;
			var times = card.ResolvedOnTurn == turn ? card.ResolvedTimes + 1 : 1;
			s = Update(s, card with { ResolvedOnTurn = turn, ResolvedTimes = times });
			if (times == N)
				steps = Then;
		}
		return steps.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(new PipelineAction { Steps = steps, PipelineContext = InputContext })
			);
	}
}

/// <summary>
/// Mana Crypt: "flip a coin. If you lose the flip, this artifact deals 3 damage to you." — a fair flip, taken
/// from the game's seeded random (s.TakeRng(), so a recorded game replays); the steps run as If's do.
/// </summary>
public record FlipACoin(ImmutableList<GameAction> IfLost) : Effect
{
	/// <summary>"If you win the flip, …".</summary>
	public ImmutableList<GameAction> IfWon { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		(var rng, s) = s.TakeRng();
		var steps = rng.Next(2) == 0 ? IfWon : IfLost;
		return steps.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(new PipelineAction { Steps = steps, PipelineContext = InputContext })
			);
	}
}

/// <summary>
/// "Name Sticker" Goblin: "roll a 20-sided die. 1-6 | Add {R}{R}{R}{R}. 7-14 | … 15-20 | …" — one fair roll
/// (CR 706) from the game's seeded random (s.TakeRng(), so a recorded game replays), then the steps of the row
/// the result falls in, run as If's do. Not FlipACoin with sides: a flip is won or lost (CR 705), a roll is a
/// number read against a table — and "whenever you roll a die" never sees a flip.
/// </summary>
public record RollDie(int Sides, ImmutableList<DieRow> Rows) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		(var rng, s) = s.TakeRng();
		var rolled = rng.Next(Sides) + 1;
		var steps = Rows.FirstOrDefault(r => r.From <= rolled && rolled <= r.To)?.Then ?? [];
		return steps.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(new PipelineAction { Steps = steps, PipelineContext = InputContext })
			);
	}
}

/// <summary>
/// Comet, Stellar Pup: "you may activate Comet's loyalty ability two more times this turn" — that many more
/// activations of each planeswalker named, this turn (Card.ExtraLoyaltyActivations), beyond the one a turn (CR 606.3).
/// </summary>
public record ActivateLoyaltyAgain(Ref What, int Times) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var turn = s.GetGame().TurnNumber;
		foreach (var walker in Permanents(What, s).ToList())
			s = s.UpdateObject(
				walker.Id,
				walker with
				{
					// one not activated this turn has its own activation besides: the stamp below takes it
					ExtraLoyaltyActivations =
						(walker.LoyaltyActivatedOnTurn == turn ? walker.ExtraLoyaltyActivations : 1)
						+ Times,
					LoyaltyActivatedOnTurn = turn,
				}
			);
		return new(s);
	}
}

/// <summary>"Name Sticker" Goblin: "7-14 | Add {R}{R}{R}{R}{R}." — a row of a die's table: the results From to To, and what happens.</summary>
public record DieRow(int From, int To, ImmutableList<GameAction> Then);

/// <summary>"If [condition], [effects]" — the effects run in order with this ability's context.</summary>
public record If(Condition When, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>Desperate Futurescribe: "… instead" — what happens when the condition does not hold.</summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var steps = When.Holds(s, InputContext) ? Then : Otherwise;
		return steps.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(new PipelineAction { Steps = steps, PipelineContext = InputContext })
			);
	}
}
