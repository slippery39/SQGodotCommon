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
	public override ActionResult Execute(GameState s)
	{
		var amount = Amount.Value(s, InputContext);
		return new(All(To, s).Aggregate(s, (st, id) => Damage.Deal(st, Source(s), id, amount)));
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
				.Aggregate(
					s,
					(st, c) =>
						Update(st, c with { PlusOneCounters = Math.Max(0, c.PlusOneCounters + n) })
				)
		);
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
	public override ActionResult Execute(GameState s) =>
		!Can.Holds(s, InputContext)
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						// Its own key, cleared — as MaySacrifice: a "no" must not read a stale "yes".
						PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
						Steps = [new YesNoChoice(Controller, Prompt), new IfChosenAction(Then)],
					}
				)
			);
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
		var from = Permanents(From, s).FirstOrDefault();
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
		var counters = GetInput<Card?>(ContextKeys.EventSnapshot)?.PlusOneCounters ?? 0;
		return counters == 0
			? new(s)
			: (new AddCounters(To, counters) with { InputContext = InputContext }).Execute(s);
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
				s.GetObject(id) is Card { } uncounterable
				&& uncounterable.HasComponent<CantBeCountered>()
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
