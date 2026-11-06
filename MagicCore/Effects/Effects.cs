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
			UntilTurnOf =
				ForGood ? UntilEndOfTurn.Never
				: UntilYourNextTurn ? Controller
				: 0,
			BecomesCreatureTypes = BecomesCreatureTypes,
			Gains = Gains,
			AlsoCreatureType = AlsoCreatureType,
		};
		return new(
			Permanents(What, s).Aggregate(s, (st, c) => Update(st, (Card)c.WithComponent(effect)))
		);
	}
}

/// <summary>
/// H.M.S. Bounty: "take an extra turn after this one" (CR 500.7) — queued on MagicGame.ExtraTurns, begun by
/// TurnStructure.FinishCleanup. Adapted from MtgCore's TakeExtraTurnAction (a count: it has one player a turn).
/// </summary>
public record TakeExtraTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.WithGame(s.GetGame() with { ExtraTurns = s.GetGame().ExtraTurns.Add(Controller) }));
}

/// <summary>"Put N +1/+1 counters on [What]" — creatures only, through MagicState.AddPlusOneCounters (Yoshimaru's replacement applies). Negative removes.</summary>
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
					.Aggregate(s, (st, c) => st.PutCounters(c.Id, CounterKinds.MinusOne, n))
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
								FirstSpellThisGame = false,
								ColorsSpent = 0,
							}
						),
				stack
			);
			s = s.MoveObjectToFront(copy.Id, stack); // index 0 is the top
			var onStack = copy.GetComponent<OnStackComponent>()!;
			var specs = StackResolution
				.TextOf(copy, onStack.Modes, onStack.Kicked, onStack.X)
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

	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new ChooseColorChoice(Controller) { Colors = Colors },
						new AddChosenColorManaAction(Amount),
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

/// <summary>
/// Dathon, His Heart Open: "Choose a counter on target enchantment you control. Put another counter of
/// that kind on it." — one kind on it: that kind, unasked; several: the kind is chosen (by name).
/// </summary>
public record AddAnotherCounterOfAKind(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		if (Permanents(What, s).FirstOrDefault() is not { } card)
			return new(s);
		ImmutableArray<string> kinds = [.. card.AllCounters().Select(c => c.Kind)];
		return kinds.Length switch
		{
			0 => new(s),
			1 => new(s.PutCounters(card.Id, kinds[0], 1)),
			_ => new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext,
						Steps =
						[
							new ChooseKeywordChoice(Controller, kinds)
							{
								Prompt = "Choose a kind of counter",
							},
							new AddChosenCounterAction(card.Id, kinds),
						],
					}
				)
			),
		};
	}
}

/// <summary>After the choice: one more counter of the chosen kind.</summary>
public record AddChosenCounterAction(int CardId, ImmutableArray<string> Kinds) : Effect
{
	public override ActionResult Execute(GameState s) =>
		InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is int id
		&& s.IsOnBattlefield(CardId)
			? new(s.PutCounters(CardId, Kinds[id - 1], 1))
			: new(s);
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
/// Necrodominance: "you may pay any amount of life. If you do, draw that many cards." — a number
/// from 0 (not paying) to your life total.
/// </summary>
public record PayAnyLifeThenDraw : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						// ponytail: never your whole life or more cards than your library — either loses
						// the game, and each number is a look-ahead the AI settles.
						new ChooseNumberChoice(
							Controller,
							Math.Max(
								0,
								Math.Min(
									s.GetPlayer(Controller).Life - 1,
									s.CardsIn(Controller, ZoneType.Library).Count()
								)
							)
						)
						{
							Prompt = "Pay how much life? You draw that many cards.",
						},
						new PayChosenLifeThenDraw(),
					],
				}
			)
		);
}

/// <summary>After the choice: that much life paid, that many cards drawn.</summary>
public record PayChosenLifeThenDraw : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = InputContext.GetValueOrDefault(ChooseNumberChoice.Chosen) is int id ? id - 1 : 0;
		if (n <= 0)
			return new(s);
		s = new LoseLife(Ref.You, n) { InputContext = InputContext }
			.Execute(s)
			.GameState;
		return new DrawCards(Ref.You, n) { InputContext = InputContext }.Execute(s);
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

/// <summary>"Untap [What]" — through MagicState.Untap, so a stun counter is removed instead.</summary>
public record Untap(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => st.Untap(c.Id)));
}

/// <summary>Cryotheory Adept: "put a stun counter on it".</summary>
public record AddStunCounters(Ref What, Count N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = N.Value(s, InputContext);
		return new(
			Permanents(What, s).Aggregate(s, (st, c) => st.PutCounters(c.Id, CounterKinds.Stun, n))
		);
	}
}

/// <summary>"You get an emblem with [Abilities]" (CR 114) — a card in your command zone, for good.</summary>
public record GetEmblem(ImmutableArray<GameComponent> Abilities) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(ToCommandZone(s, Controller, $"Emblem — {Source(s).Name}", Abilities));

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
	/// a token never gets one. Its "this" (Ref.Source) is that card.
	/// </summary>
	public Ref? On { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var trigger = new DelayedTrigger(
			new TriggeredAbilityComponent(TriggerEvent.EachEndStep, Effects)
		);
		if (On is { } on)
			return new(
				All(on, s)
					// in its owner's graveyard, where it died to — ponytail: by id, so a card that left
					// and came back there inside the same response would still return; stamp the
					// incarnation it died as if that ever matters
					.Where(id =>
						s.HasObject(id)
						&& s.GetObject(id) is Card { IsToken: false } card
						&& s.IsIn(id, card.OwnerId, ZoneType.Graveyard)
					)
					.Aggregate(
						s,
						(st, id) =>
							ReturnFromExileUnderYourControl.Delay(
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
			GetEmblem.ToCommandZone(s, Controller, $"{Source(s).Name} (next end step)", [trigger])
		);
	}
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
			Permanents(On, s)
				.ToList()
				.Aggregate(s, (st, c) => ReturnFromExileUnderYourControl.Delay(st, c.Id, trigger))
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
		return new(
			Update(
				s,
				(Card)
					source
						.WithoutComponents<TriggersAdditionalTimeWhileTapped>()
						.WithComponent(
							new TriggersAdditionalTimeWhileTapped(creature.Id, creature.Incarnation)
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
	public override ActionResult Execute(GameState s) =>
		new(GetEmblem.ToCommandZone(s, Controller, $"{Source(s).Name} (this turn)", [What]));
}

/// <summary>Molten Tide: "Until end of turn, whenever you tap a Mountain for mana, add an additional {R}." — one {R} each; ManaAbilities.YieldOf.</summary>
public record ExtraRedFromMountains : ThisTurnEffect;

/// <summary>Peace in Our Time: "they can't attack this turn" — on that player; Combat.CanAttack.</summary>
public record CantAttackThisTurn : ThisTurnEffect;

/// <summary>The Needs of the Many: "You gain hexproof until end of turn." — TargetSpec: no opponent's spell or ability targets you.</summary>
public record YouHaveHexproof : ThisTurnEffect;

/// <summary>The Needs of the Many: "Prevent all damage that would be dealt to you this turn." — Damage.Deal: none is dealt, so no lifelink either.</summary>
public record PreventAllDamageToYou : ThisTurnEffect;

/// <summary>Brad Boimler's "until end of turn … that many plus one of each of those kinds of counters" — Rules.Replacements.Counters.</summary>
public record CountersPlusOneThisTurn : ThisTurnEffect;

/// <summary>
/// Way of the Cryomancer's granted "[−3]: When you next cast an instant or sorcery spell this turn,
/// copy that spell. You may choose new targets for the copy." — a delayed trigger; the next such
/// spell takes them all (CastSpellAction).
/// </summary>
public record CopyNextInstantOrSorcery : ThisTurnEffect;

/// <summary>Jace's Machinations: "you may activate loyalty abilities of Jace planeswalkers you control … any time you could cast an instant" — LoyaltyActions.</summary>
public record JaceLoyaltyAtInstantSpeed : ThisTurnEffect;

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

/// <summary>
/// Gardenize: "put a charge counter on this enchantment"; Station: "Put charge counters equal to its
/// power" — counted as it resolves; a negative power counts 0 (Count.Value), so none are removed.
/// </summary>
public record AddChargeCounters(Ref What, Count N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = N.Value(s, InputContext);
		return new(
			Permanents(What, s)
				.Aggregate(s, (st, c) => st.PutCounters(c.Id, CounterKinds.Charge, n))
		);
	}
}

/// <summary>
/// "Put a quest counter on this enchantment" (Anthropological Mission), "a promotion counter on her"
/// (Beckett Mariner) — a named kind, on any permanent, through MagicState.PutCounters.
/// </summary>
public record PutCounters(Ref What, string Kind, Count N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = N.Value(s, InputContext);
		return new(Permanents(What, s).Aggregate(s, (st, c) => st.PutCounters(c.Id, Kind, n)));
	}
}

/// <summary>Beckett Mariner: "remove those counters" — every counter of the kind.</summary>
public record RemoveCountersOfKind(Ref What, string Kind) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Aggregate(s, (st, c) => st.PutCounters(c.Id, Kind, -c.CountersOf(Kind)))
		);
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
	/// <summary>
	/// Ward with a cost beyond mana, paid by choosing as it resolves — Gideon the Oathless: [DiscardCost()];
	/// Emrakul: [SacrificeCost { Count = 3 }]. Empty: the mana Cost.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];

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
				s = StackResolution.Counter(s, id);
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
							new PayOrCounterAction(id, payer, Cost),
						]
						:
						[
							new PayToKeepChoice(
								payer,
								$"{string.Join(" and ", Costs.Select(c => c.Describe()))} to keep {name}?"
							),
							new IfChosenAction([.. Costs.Select(c => c.PaidByChoosing()!)])
							{
								Otherwise = [new Counter(What)],
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
	/// <summary>
	/// Seasoned Cryomancer: "tap up to that many target creatures" — Targets[0], that many times, each
	/// optional and not the one before; nothing triggers at 0. ponytail: "not the one before" keeps
	/// two apart, all FRA needs; three or more could repeat a target.
	/// </summary>
	public Count? TargetsUpTo { get; init; }

	public override ActionResult Execute(GameState s)
	{
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
/// source that left the battlefield counts nothing.
/// </summary>
public record IfNthResolutionThisTurn(int N, ImmutableList<GameAction> Then) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		if (All(Ref.Source, s).Where(s.IsOnBattlefield).FirstOrDefault() is not (> 0 and var id))
			return new(s);
		var card = s.GetCard(id);
		var turn = s.GetGame().TurnNumber;
		var times = card.ResolvedOnTurn == turn ? card.ResolvedTimes + 1 : 1;
		s = Update(s, card with { ResolvedOnTurn = turn, ResolvedTimes = times });
		return times == N
			? new(
				s.SpawnAction(new PipelineAction { Steps = Then, PipelineContext = InputContext })
			)
			: new(s);
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
