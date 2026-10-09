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

public record DrawCards(Ref Who, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(All(Who, s).Aggregate(s, (st, p) => st.Draw(p, n)));
	}
}

public record GainLife(Ref Who, int Amount) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(All(Who, s).Aggregate(s, (st, p) => Life.Gain(st, p, Amount)));
}

/// <summary>Life loss that is not damage — lifelink and damage triggers do not see it.</summary>
public record LoseLife(Ref Who, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(
			All(Who, s)
				.Aggregate(
					s,
					(st, p) =>
						st.WithPlayer(st.GetPlayer(p) with { Life = st.GetPlayer(p).Life - n })
				)
		);
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

	public override ActionResult Execute(GameState s)
	{
		var amount = Amount.Value(s, InputContext);
		var source = Source(s);
		var excess = 0;
		foreach (var id in All(To, s).ToList())
		{
			if (!IfExcess.IsEmpty)
				excess += Damage.Excess(s, source, id, amount);
			s = Damage.Deal(s, source, id, amount);
		}
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

	/// <summary>Essence Burn: "if that permanent would die this turn, exile it instead".</summary>
	public bool ExileIfItWouldDie { get; init; }

	/// <summary>Flourishing Grapple: "loses all abilities until end of turn".</summary>
	public bool LosesAllAbilities { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var effect = new UntilEndOfTurn(
			Power.Value(s, InputContext),
			Toughness.Value(s, InputContext),
			Keyword
		)
		{
			SetBase = SetBase,
			Trigger = Trigger,
			ExileIfItWouldDie = ExileIfItWouldDie,
			LosesAllAbilities = LosesAllAbilities,
		};
		return new(
			Permanents(What, s).Aggregate(s, (st, c) => Update(st, (Card)c.WithComponent(effect)))
		);
	}
}

public record AddCounters(Ref What, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return new(
			Permanents(What, s)
				.Where(c => c.Is(CardType.Creature))
				.Aggregate(s, (st, c) => st.AddPlusOneCounters(c.Id, n))
		);
	}
}

/// <summary>Hapatra, the Desert Fang: "put X -1/-1 counters on …" — creatures only.</summary>
public record AddMinusOneCounters(Ref What, Count Amount) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Amount.Value(s, InputContext);
		return n <= 0
			? new(s)
			: new(
				Permanents(What, s)
					.Where(c => c.Is(CardType.Creature))
					.Aggregate(
						s,
						(st, c) => Update(st, c with { MinusOneCounters = c.MinusOneCounters + n })
					)
			);
	}
}

/// <summary>
/// Mabel: "remove up to N counters from …". ponytail: no prompt — from your own permanent it
/// removes the harmful ones (stun, -1/-1, finality), from theirs the helpful ones (+1/+1, loyalty);
/// never the other kind. Make it a choice if a card ever wants something else.
/// </summary>
public record RemoveCounters(Ref What, int UpTo) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var target in Permanents(What, s).ToList())
		{
			var left = UpTo;
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

/// <summary>Proft: "you may pay {2}" — the first step of a May's Then; its Can is <see cref="CanPay"/>.</summary>
public record PayMana(ManaCost Cost, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>Pays, then runs Then — or, unable to pay, does nothing at all ("if you do").</summary>
	public override ActionResult Execute(GameState s) =>
		ManaPayment.Find(s, Controller, Cost, 0) is not { } payment
			? new(s)
			: new(
				ManaPayment
					.Pay(s, Controller, payment)
					.SpawnAction(
						new PipelineAction { Steps = Then, PipelineContext = InputContext }
					)
			);
}

/// <summary>Molten Tide: "Until end of turn, whenever you tap a Mountain for mana, add an additional {R}." — read by ManaAbilities.YieldOf.</summary>
public record MountainsMakeAnExtraRedThisTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.Tally(Controller, t => t with { ExtraRedFromMountains = t.ExtraRedFromMountains + 1 })
		);
}

/// <summary>
/// Way of the Cryomancer's granted "[−3]: When you next cast an instant or sorcery spell this turn,
/// copy that spell. You may choose new targets for the copy." — a delayed trigger, kept on the player.
/// </summary>
public record CopyNextInstantOrSorceryThisTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.Tally(
				Controller,
				t => t with { CopyNextInstantOrSorcery = t.CopyNextInstantOrSorcery + 1 }
			)
		);
}

/// <summary>
/// "Copy that spell. You may choose new targets for the copy." (CR 707.10) The copy goes on the
/// stack above it, controlled by you; it is not cast (no "whenever you cast" sees it) and ceases to
/// exist once it leaves the stack. Its targets are chosen anew from what is legal for it — the
/// original's among them.
/// </summary>
public record CopySpell(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		// Countered or gone in response, it is still copied — as it last was on the stack.
		var originals = All(What, s)
			.Where(id => s.GetParent(id) == stack)
			.Select(s.GetCard)
			.ToList();
		if (originals.Count == 0 && GetInput<Card?>(ContextKeys.EventSnapshot) is { } lastKnown)
			originals.Add(lastKnown);
		foreach (var original in originals.Where(c => c.HasComponent<OnStackComponent>()))
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
							}
						),
				stack
			);
			s = s.MoveObjectToFront(copy.Id, stack); // index 0 is the top
			var onStack = copy.GetComponent<OnStackComponent>()!;
			var specs = StackResolution.TextOf(copy, onStack.Modes).Specs;
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

/// <summary>Jace's Machinations: Jace loyalty abilities at instant speed, on any turn, until end of turn.</summary>
public record JaceLoyaltyAtInstantSpeedThisTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.Tally(Controller, t => t with { JaceLoyaltyAtInstantSpeed = true }));
}

/// <summary>Theorist's Proxy: "The next spell you cast this turn can't be countered." — taken by that cast.</summary>
public record NextSpellCantBeCounteredThisTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.Tally(Controller, t => t with { NextSpellCantBeCountered = true }));
}

/// <summary>Hungering Puppetbeast: "It gains your choice of trample, hexproof, or haste until end of turn."</summary>
public record GainsChosenKeyword(Ref What, ImmutableArray<string> Keywords) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new ChooseKeywordChoice(Controller, Keywords),
						new GrantChosenKeywordAction(What, Keywords),
					],
				}
			)
		);
}

/// <summary>After the choice: the chosen keyword, until end of turn.</summary>
public record GrantChosenKeywordAction(Ref What, ImmutableArray<string> Keywords) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var chosen = InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is int id
			? Keywords[id - 1]
			: null;
		return chosen is null
			? new(s)
			: (
				new ModifyUntilEndOfTurn(What, 0, 0, chosen) with
				{
					InputContext = InputContext,
				}
			).Execute(s);
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

	public override ActionResult Execute(GameState s)
	{
		var decider = Who is null ? Controller : All(Who, s).FirstOrDefault();
		return !Can.Holds(s, InputContext) || decider == 0
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						// Its own key, cleared — as MaySacrifice: a "no" must not read a stale "yes".
						PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
						Steps =
						[
							new YesNoChoice(decider, Prompt),
							new IfChosenAction(Then) { Otherwise = Otherwise },
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

public record Untap(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => st.Untap(c.Id)));
}

/// <summary>Cryotheory Adept: "put a stun counter on it".</summary>
public record AddStunCounters(Ref What, int N) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Aggregate(s, (st, c) => Update(st, c with { StunCounters = c.StunCounters + N }))
		);
}

public record AddMana(ManaColor Color, int Amount = 1) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var player = s.GetPlayer(Controller);
		return new(s.WithPlayer(player with { ManaPool = player.ManaPool.Add(Color, Amount) }));
	}
}

public record Tap(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => Update(st, c with { IsTapped = true })));
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

/// <summary>Graft Surgeon: "put its counters on …" — the counters the dead card had as it last existed.</summary>
public record PutEventCardCounters(Ref To) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var dead = GetInput<Card?>(ContextKeys.EventSnapshot);
		s = (new AddCounters(To, dead?.PlusOneCounters ?? 0) with { InputContext = InputContext })
			.Execute(s)
			.GameState;
		return (
			new AddMinusOneCounters(To, dead?.MinusOneCounters ?? 0) with
			{
				InputContext = InputContext,
			}
		).Execute(s);
	}
}

/// <summary>
/// "Counter [it] unless its controller pays {N}" — Icy Reception, and ward. The payer CHOOSES (CR
/// 118.12): asked only when they can pay. MtgCore auto-pays; real rules don't.
/// </summary>
public record CounterUnlessPays(Ref What, ManaCost Cost) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		foreach (var id in All(What, s).Where(id => s.GetParent(id) == stack).ToList())
		{
			if (
				s.GetObject(id) is Card uncounterable
				&& !StackResolution.CanBeCountered(uncounterable)
			)
				continue; // nothing to pay for: the counter can't touch it
			var payer = s.GetObject(id) switch
			{
				Card spell => spell.ControllerId,
				StackAbility ability => ability.ControllerId,
				_ => 0,
			};
			if (payer == 0 || ManaPayment.Find(s, payer, Cost, 0) is null)
			{
				s = StackResolution.Counter(s, id);
				continue;
			}
			s = s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
					Steps =
					[
						new PayToKeepChoice(payer, $"Pay {Cost} to keep {s.GetObject(id).Name}?"),
						new PayOrCounterAction(id, payer, Cost),
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
	public override ActionResult Execute(GameState s) =>
		new(All(What, s).ToList().Aggregate(s, StackResolution.Counter));
}

/// <summary>After the payer's answer: pay the cost, or the spell or ability is countered.</summary>
public record PayOrCounterAction(int StackObjectId, int PayerId, ManaCost Cost) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(
			ChoosePermanentChoice.Read(InputContext).Any()
			&& ManaPayment.Find(s, PayerId, Cost, 0) is { } payment
				? ManaPayment.Pay(s, PayerId, payment)
				: StackResolution.Counter(s, StackObjectId)
		);
}

/// <summary>
/// "When you do, [effects]" — a reflexive trigger (CR 603.12): it triggers as this resolves, its
/// targets are chosen then, and it goes on the stack above — so a player can respond knowing what
/// was done. Tether Technician: "you may discard a card. When you do, …".
/// </summary>
public record WhenYouDo(ImmutableList<TargetSpec> Targets, ImmutableList<GameAction> Then) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var ability = new TriggeredAbilityComponent(TriggerEvent.Reflexive, Then)
		{
			Targets = Targets,
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
					0
				),
				Targets,
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
