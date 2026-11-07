using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;

namespace MagicCore.Ai;

/// <summary>
/// A planeswalker priced by its value over its lifetime (story AI-9.3, Shayne, 2026-11-07: "value over time
/// engines … value created over their lifetime (which could be anywhere from 1-X turns depending on if the
/// opponent can pressure it or not)"). AI-8.2's price — half of ONE activation — read The Theorist, Jace
/// Beleren as the cheapest card in a hand of commons and sent a 2/2 at Chandra, Torch of Defiance instead
/// of the 4/4 that kills her.
///
/// The price: what its abilities make each turn it lives (<see cref="Makes"/>, read from the effects, never
/// text), each turn worth <see cref="LifetimeDecay"/> of the one before, up to <see cref="LifetimeTurns"/>.
/// It lives while its loyalty outlasts the opponent's attacks: the power that gets past our blockers, the
/// share of it they'd send at a walker rather than at us (<see cref="AtTheWalker"/>). The damage it soaks is
/// life we didn't lose, credited at the life price (Shayne: "if an opponent attacks our walker instead of
/// us, it is effectively gaining us life"). The best SEQUENCE of abilities counts, one a turn or none —
/// ticking up, cashing a minus now, an ultimate once reached, a Jace token spending one every turn — found
/// by a small search over turns and loyalty left.
///
/// MONOTONIC by construction: every sequence open at a loyalty is open at one more, with as much left each
/// turn — it lives as long, soaks as much — so the best is worth as much or more (the "as soon as
/// affordable" plans this replaced were not: the Theorist fired his −6 a turn sooner at 3 loyalty than at
/// 2, and died a turn sooner); <see cref="PerCounter"/> makes a counter past the horizon count too.
/// Built so another permanent can plug in later (Shayne: Dark Confidant, Sun Titan) — <see cref="Makes"/>
/// prices any effect list, and the lifetime asks only its loyalty, its abilities and its triggers.
///
/// ponytail: every turn priced on the board as it stands; a tap without a stun counter, scry, a trigger
/// on anything but a step or a loyalty activation (Garruk, Curse Breaker's draw), a −X past today's
/// loyalty and "activate only if" are unpriced; a mass pump on one target counts on every attacker.
/// </summary>
public static partial class Evaluator
{
	/// <summary>The most activations a walker's lifetime counts — beyond, the game is a guess.</summary>
	public const int LifetimeTurns = 5;

	/// <summary>
	/// Each turn's activation worth this of the one before (the first too: it is next turn's). 0.6, not the 0.7 first
	/// tried: at 0.7 a Jace token's second −3, two turns away, outweighed the card drawn now and it passed (Shayne:
	/// draw "in 99.9% of cases" — `AJaceToken_DrawsWithItsMinusThree_RatherThanBankingTheLoyalty`).
	/// </summary>
	public const double LifetimeDecay = 0.6;

	/// <summary>A loyalty counter on its own, so one past the horizon still counts (monotonic, not capped).</summary>
	public const double PerCounter = 0.1;

	/// <summary>Surveil or scry, a card each (Shayne: a useless card binned is "effectively drawn you a card").</summary>
	public const double SurveilShare = 0.33;

	/// <summary>"Exile the top card, you may cast it" (Shayne: close to a draw, "technically still worse").</summary>
	public const double ExileCastShare = 0.8;

	/// <summary>
	/// A point of damage the walker soaks, as a share of a point of life (Shayne, 2026-11-07: "Loyalty = Life
	/// should not be a 1-1 thing") — at 1 a Jace token kept its loyalty as a sponge and passed where it drew.
	/// </summary>
	public const double SoakShare = 0.5;

	/// <summary>A draw that happens only "if" — half a card.</summary>
	public const double IfShare = 0.5;

	static double WalkerLifetime(GameState s, Card c, HeldValues? held)
	{
		var me = c.ControllerId;
		var game = s.GetGame();
		var options = Options(s, c, held, Loyalty.AbilitiesOf(s, c), c.Loyalty);
		var (perTurn, perActivation) = Triggered(s, c, held);
		// Its controller's next activation comes after the opponent's next attack — unless it is still
		// unused in its controller's turn, or the opponent's combat is already over.
		var attackFirst =
			game.ActivePlayerId == me
				? c.LoyaltyActivatedOnTurn == game.TurnNumber || game.Step >= Step.End
				: game.Step < Step.EndCombat;
		return Priced(
			Lifetime(s, me, options, perTurn, perActivation, c.Loyalty, attackFirst),
			c.Loyalty
		);
	}

	/// <summary>A walker's price from its lifetime: never below AI-8.2's flat body of 2, and a counter more always more.</summary>
	static double Priced(double lifetime, int loyalty) =>
		Math.Max(2, lifetime) + PerCounter * loyalty;

	/// <summary>
	/// The best sequence's worth over the walker's life: <paramref name="options"/> its abilities (cost, what
	/// one use makes), <paramref name="perTurn"/> what its triggers make a turn, <paramref name="perActivation"/>
	/// what they make on a turn it activates.
	/// </summary>
	static double Lifetime(
		GameState s,
		int me,
		IReadOnlyList<(int Cost, double Yield)> options,
		double perTurn,
		double perActivation,
		int loyalty,
		bool attackFirst
	)
	{
		var hits = AtTheWalker(s, me);
		var lifePoint = LifePoint(s.GetPlayer(me).Life);
		List<(int Cost, double Yield)> choices =
		[
			.. options.Select(o => (o.Cost, o.Yield + perActivation)),
			(0, 0), // idling is always open, and activates nothing
		];
		var memo = new Dictionary<(int, double), double>();
		return attackFirst ? Soak(1, loyalty) + Best(1, loyalty - hits) : Best(1, loyalty);

		// The damage soaked by a walker at `left`: life we didn't lose.
		double Soak(double weight, double left) =>
			weight * Math.Min(Math.Max(0, left), hits) * lifePoint * SoakShare;

		// The most turns k on can make from `left` loyalty: one ability a turn (or none), then their attack.
		double Best(int k, double left)
		{
			if (k > LifetimeTurns || left <= 0)
				return 0;
			if (memo.TryGetValue((k, left), out var known))
				return known;
			var weight = Math.Pow(LifetimeDecay, k);
			return memo[(k, left)] = choices
				.Where(o => left + o.Cost >= 0)
				.Max(o =>
					weight * (o.Yield + perTurn)
					+ Soak(weight, left + o.Cost)
					+ Best(k + 1, left + o.Cost - hits)
				);
		}
	}

	/// <summary>
	/// The damage the opponent sends at <paramref name="me"/>'s walker a turn: what gets past our blockers,
	/// times the share they'd aim at it — 1 − 1 ÷ the turns their attack needs to kill us. One turn from
	/// lethal they go face (Shayne: "at lower the opponent might just kill us instead").
	/// </summary>
	static double AtTheWalker(GameState s, int me)
	{
		var them = s.OpponentOf(me);
		var kill = TurnsToKill(s, them, me);
		return kill <= 1 ? 0 : (1 - 1.0 / kill) * Through(s, them, me);
	}

	/// <summary>The next point of life lost at <paramref name="life"/>, as the life term prices it (<see cref="LifeValue"/>).</summary>
	static double LifePoint(int life) => life <= 0 ? 0 : LifeValue(life) - LifeValue(life - 1);

	/// <summary><paramref name="n"/> life lost (negative: gained) at <paramref name="life"/>, as the life term prices it.</summary>
	static double LifeLost(int life, int n) => LifeValue(life) - LifeValue(life - n);

	/// <summary>Each loyalty ability it can use: its cost and what one use makes. A −X once per X up to its loyalty.</summary>
	static List<(int Cost, double Yield)> Options(
		GameState s,
		Card c,
		HeldValues? held,
		IEnumerable<LoyaltyAbilityComponent> abilities,
		int loyalty
	)
	{
		var options = new List<(int, double)>();
		foreach (var a in abilities.Where(a => a.OnlyIf is null))
			if (a.MinusX)
				for (var x = 1; x <= loyalty; x++)
					Add(a, -x, x);
			else
				Add(a, a.Cost, 0);
		return options;

		void Add(LoyaltyAbilityComponent a, int cost, int x)
		{
			if (Makes(s, c.ControllerId, c.Id, held, a.Effects, a.Targets, x) is { } made)
				options.Add((cost, made));
		}
	}

	/// <summary>
	/// What its triggered abilities make: a turn — one at a step ("at the beginning of each opponent's draw
	/// step") once a turn, twice for every player's step — and on each loyalty activation (Ajani Unrelenting's
	/// Cadet), counted only on a turn it activates. ponytail: a condition on the activation ("removing two or
	/// more", Way of the Mind Sculptor) leaves the trigger unpriced; price it if such a walker is misjudged.
	/// </summary>
	static (double PerTurn, double PerActivation) Triggered(GameState s, Card c, HeldValues? held)
	{
		var (turn, activation) = (0.0, 0.0);
		foreach (var t in s.AbilitiesOf<TriggeredAbilityComponent>(c))
		{
			double Made() => Makes(s, c.ControllerId, c.Id, held, t.Effects, t.Targets, 0) ?? 0;
			if (t.On.On == EventKind.StepBegan)
				turn += (t.On.By == Whose.Any ? 2 : 1) * Made();
			else if (
				t.On.On == EventKind.LoyaltyActivated
				&& t.On.By == Whose.You
				&& t.On.Also == EventTest.None
			)
				activation += Made();
		}
		return (turn, activation);
	}

	/// <summary>
	/// What <paramref name="effects"/> make for <paramref name="me"/> — each target its best legal choice,
	/// read from the effects and the board, never text. Null: a required target has no legal choice.
	/// </summary>
	static double? Makes(
		GameState s,
		int me,
		int source,
		HeldValues? held,
		IReadOnlyList<GameAction> effects,
		IReadOnlyList<TargetSpec> targets,
		int x
	)
	{
		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, me)
			.Add(ContextKeys.Source, source)
			.Add(ContextKeys.X, x);
		double Priced(ImmutableArray<int> chosen) =>
			effects.Sum(e => Effect(s, me, held, e, context.SetItem(ContextKeys.Targets, chosen)));
		var none = ImmutableArray.CreateRange(Enumerable.Repeat(0, targets.Count));
		var untargeted = Priced(none); // a target's effects find nothing at id 0
		var total = untargeted;
		for (var i = 0; i < targets.Count; i++)
		{
			var picks = targets[i]
				.Candidates(s, me, source)
				.Select(id => Priced(none.SetItem(i, id)) - untargeted)
				.ToList();
			if (targets[i].Optional)
				picks.Add(0);
			if (picks.Count == 0)
				return null;
			total += picks.Max();
		}
		return total;
	}

	/// <summary>What one effect makes for <paramref name="me"/> — the ONE place each effect type is priced.</summary>
	static double Effect(
		GameState s,
		int me,
		HeldValues? held,
		GameAction effect,
		ImmutableDictionary<string, object> context
	)
	{
		var them = s.OpponentOf(me);
		var card = held?.Unknown ?? FlatHandCard;
		int N(Count n) => n.Value(s, context);
		IEnumerable<int> Ids(Ref r) => r.Resolve(s, context).Where(s.HasObject);
		IEnumerable<Card> Creatures(Ref r) =>
			Ids(r)
				.Select(s.GetObject)
				.OfType<Card>()
				.Where(c => c.Is(CardType.Creature) && s.IsOnBattlefield(c.Id));
		double Sign(Card c) => c.ControllerId == me ? -1 : 1; // removing theirs is ours to gain
		double Mana(int n) => n <= 0 ? 0 : LandsValue(Lands(me) + n) - LandsValue(Lands(me));
		int Lands(int who) => s.CardsIn(who, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		double Sum(IEnumerable<GameAction> steps) =>
			steps.Sum(e => Effect(s, me, held, e, context));

		switch (effect)
		{
			case CreateTokens t
				when t.ControlledBy is null && t.Token.TypeLine.Contains("Creature"):
				return N(t.Count) * TokenValue(t.Token);
			case DrawCards d:
				return Ids(d.Who).Sum(p => (p == me ? 1 : -1) * N(d.Amount) * card);
			case Discard d:
				return Ids(d.Who)
						.Sum(p =>
							(p == me ? -1 : 1)
							* Math.Min(d.Count, s.CardsIn(p, ZoneType.Hand).Count())
							* card
						) + Sum(d.Then);
			case DiscardHand d:
				return Ids(d.Who)
					.Sum(p => (p == me ? -1 : 1) * s.CardsIn(p, ZoneType.Hand).Count() * card);
			case If i:
				return IfShare * Sum(i.Then);
			case GainLife g:
				return Ids(g.Who)
					.Sum(p => (p == me ? 1 : -1) * -LifeLost(s.GetPlayer(p).Life, -N(g.Amount)));
			case DealDamage d:
				return Ids(d.To)
					.Sum(id =>
						s.GetObject(id) switch
						{
							MagicPlayer p => (p.Id == me ? -1 : 1) * LifeLost(p.Life, N(d.Amount)),
							Card c when c.Is(CardType.Creature) && Dies(c, N(d.Amount), 0) => Sign(
								c
							) * ValueOf(s, c),
							_ => 0,
						}
					);
			case ReturnToHand r:
				// A bounce gains what the evaluator loses them: the creature, less what it is worth held —
				// at most its value less the mana to cast it again (KnownHand), all of a token's.
				return Creatures(r.What)
					.Sum(c =>
						Sign(c)
						* (
							ValueOf(s, c)
							- (
								c.IsToken
									? 0
									: Math.Max(
										held?.Of(c.Name) ?? FlatHandCard,
										ValueOf(s, c) - RecastPerMana * c.ManaCost.ManaValue
									)
							)
						)
					);
			case PutCounters p when p.Kind == CounterKinds.Stun:
				// Each stun counter is an attack it misses — its power, at our life price.
				return Creatures(p.What)
					.Sum(c => Sign(c) * s.Power(c) * N(p.N) * LifePoint(s.GetPlayer(me).Life));
			case PutCounters p when p.Kind == CounterKinds.PlusOne:
				return Creatures(p.What)
					.Sum(c => -Sign(c) * N(p.N) * (Body(1, 1, 0) - Body(0, 0, 0)));
			case ModifyUntilEndOfTurn m:
				return Modified(m);
			case AddMana a:
				return Mana(N(a.Amount)) * (a.Restriction == ManaRestriction.None ? 1 : 0.5);
			case AddManaOfAnyOneColor a:
				return Mana(N(a.Amount));
			case Untap u:
				return Mana(
					Ids(u.What)
						.Count(id =>
							s.GetObject(id) is Card l && l.Is(CardType.Land) && l.ControllerId == me
						)
				);
			case Surveil v:
				return v.N * SurveilShare * card + Sum(v.Then);
			case ReturnChosenToHandIf:
				// "If you put a [card] into your graveyard this way, put it into your hand": a draw, only if.
				return IfShare * card;
			case ExileTopMayCast e:
				return Math.Max(ExileCastShare * card, Sum(e.Otherwise));
			case EachSacrifices e:
				return Ids(e.Who)
					.Select(s.GetObject)
					.OfType<MagicPlayer>()
					.Sum(p =>
						s.CardsIn(p.Id, ZoneType.Battlefield)
							.Where(c =>
								e.Filter.Matches(s, c, new(p.Id, (int)context[ContextKeys.Source]))
							)
							.ToList()
							is { Count: > 0 } theirs
							? (p.Id == me ? -1 : 1)
								* theirs.Min(c => c.Is(CardType.Creature) ? ValueOf(s, c) : 2)
								+ (p.Id == me ? Sum(e.IfYouDid) : 0)
							: 0
					);
			case EmpowerJace j:
				return Empowered(N(j.Amount));
			case UntilYourNextTurnWhenever w:
				// "Whenever a creature attacks …, it gets ±N/−0": a −N blunts their next attack on us, a +N
				// sharpens ours — read by the sign, at the defender's life price.
				return w
					.Ability.Effects.OfType<ModifyUntilEndOfTurn>()
					.Where(m => m.What == Ref.EventCard)
					.Sum(m => Sharper(N(m.Power)));
			case GetEmblem g:
				return Emblem(g);
			default:
				return 0;
		}

		bool Dies(Card c, int damage, int shrink) =>
			!s.IsIndestructible(c) && s.Toughness(c) - c.Damage - damage + shrink <= 0;

		double Modified(ModifyUntilEndOfTurn m)
		{
			var (power, toughness) = (N(m.Power), N(m.Toughness));
			var value = 0.0;
			foreach (var c in Creatures(m.What))
				if (toughness < 0 && Dies(c, 0, toughness))
					value += Sign(c) * ValueOf(s, c);
				else if (power < 0 && m.UntilYourNextTurn && c.ControllerId != me)
					value += Math.Min(-power, s.Power(c)) * LifePoint(s.GetPlayer(me).Life);
			// A pump on ours: what more gets past their blockers.
			if (power > 0 && Creatures(m.What).Any(c => c.ControllerId == me))
				value += Sharper(power);
			return value;
		}

		double Sharper(int power) =>
			power >= 0
				? (Through(s, me, them, power) - Through(s, me, them))
					* LifePoint(s.GetPlayer(them).Life)
				: (Through(s, them, me) - Through(s, them, me, power))
					* LifePoint(s.GetPlayer(me).Life);

		// "Put N loyalty on a Jace token you control; if none, create one first" — the token's own lifetime.
		double Empowered(int n)
		{
			var token = s.CardsIn(me, ZoneType.Battlefield).FirstOrDefault(EmpowerJace.IsJaceToken);
			var abilities = token is null
				? EmpowerJace.JaceToken.Components.OfType<LoyaltyAbilityComponent>().ToList()
				: Loyalty.AbilitiesOf(s, token).ToList();
			var had = token?.Loyalty ?? 0;
			// as the token is priced on the battlefield; made in our turn, it can activate before their attack
			double Life(int loyalty) =>
				loyalty <= 0
					? 0
					: Priced(
						Lifetime(
							s,
							me,
							Options(s, token ?? FakeSource(), held, abilities, loyalty),
							0,
							0,
							loyalty,
							attackFirst: false
						),
						loyalty
					);
			return Life(had + n) - Life(had);
		}

		Card FakeSource() => s.GetCard((int)context[ContextKeys.Source]);

		// An emblem lasts: an anthem as counters on each creature we have; a "whenever you cast a spell"
		// trigger as one spell a turn over the horizon.
		double Emblem(GetEmblem g)
		{
			var ours = s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Creature));
			var value = 0.0;
			foreach (var component in g.Abilities)
				if (component is StaticEffectComponent anthem)
					value += ours * (Body(N(anthem.Power), N(anthem.Toughness), 0) - Body(0, 0, 0));
				else if (component is TriggeredAbilityComponent t)
					value +=
						(
							Makes(
								s,
								me,
								(int)context[ContextKeys.Source],
								held,
								t.Effects,
								t.Targets,
								0
							) ?? 0
						) * Enumerable.Range(1, LifetimeTurns).Sum(k => Math.Pow(LifetimeDecay, k));
			return value;
		}
	}
}
