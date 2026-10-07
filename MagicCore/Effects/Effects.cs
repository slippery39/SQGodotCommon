using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Targeting;

namespace MagicCore.Effects;

/// <summary>Pipeline context keys a resolving spell or ability provides to its effects.</summary>
public static class ContextKeys
{
	public const string Controller = "Controller";
	public const string X = "X";

	/// <summary>ImmutableArray&lt;int&gt;, one per target word; 0 where unchosen or no longer legal.</summary>
	public const string Targets = "Targets";

	/// <summary>Every chosen target, all modes together; Targets is narrowed to one mode's slice.</summary>
	public const string AllTargets = "AllTargets";

	/// <summary>The spell card or the permanent whose ability this is.</summary>
	public const string Source = "Source";

	/// <summary>The card the trigger event was about — the creature that entered, the land that fell.</summary>
	public const string EventCard = "EventCard";

	/// <summary>The event's card as it last existed (a dead creature's counters), for "dies" triggers.</summary>
	public const string EventSnapshot = "EventSnapshot";

	/// <summary>An ability's source as it last existed — used once the source itself is gone (CR 113.7a).</summary>
	public const string SourceSnapshot = "SourceSnapshot";
}

public enum RefKind
{
	You,
	EachOpponent,
	EachPlayer,
	Target,
	ControllerOfTarget,
	Source,
	EventCard,
	YourPermanents, // of Type: "creatures you control", "lands you control" …
	OpponentsPermanents, // of Type: "each creature and planeswalker your opponents control"
	AllPermanents, // of Type: "all creatures"
	YourGraveyard, // the cards in it
}

/// <summary>Who or what an effect acts on, resolved against the context at resolution.</summary>
public record Ref(RefKind Kind, int Index = 0, CardType Type = CardType.None)
{
	public static readonly Ref You = new(RefKind.You);
	public static readonly Ref EachOpponent = new(RefKind.EachOpponent);
	public static readonly Ref EachPlayer = new(RefKind.EachPlayer);
	public static readonly Ref Source = new(RefKind.Source);
	public static readonly Ref EventCard = new(RefKind.EventCard);
	public static readonly Ref CreaturesYouControl =
		new(RefKind.YourPermanents, Type: CardType.Creature);
	public static readonly Ref LandsYouControl = new(RefKind.YourPermanents, Type: CardType.Land);
	public static readonly Ref PlaneswalkersYouControl =
		new(RefKind.YourPermanents, Type: CardType.Planeswalker);
	public static readonly Ref AllCreatures = new(RefKind.AllPermanents, Type: CardType.Creature);
	public static readonly Ref CardsInYourGraveyard = new(RefKind.YourGraveyard);

	public static Ref YourGraveyard(CardType type) => new(RefKind.YourGraveyard, Type: type);

	public static Ref OpponentsPermanents(CardType type) =>
		new(RefKind.OpponentsPermanents, Type: type);

	public static Ref Target(int index = 0) => new(RefKind.Target, index);

	public static Ref ControllerOfTarget(int index = 0) => new(RefKind.ControllerOfTarget, index);

	/// <summary>Is the source still the object the ability came from? Not if it left and came back (CR 400.7).</summary>
	static bool SameObject(GameState s, ImmutableDictionary<string, object> context) =>
		!context.TryGetValue(ContextKeys.SourceSnapshot, out var snapshot)
		|| snapshot is not Card then
		|| !s.HasObject(then.Id)
		|| s.GetObject(then.Id) is not Card now
		|| now.Incarnation == then.Incarnation;

	public IEnumerable<int> Resolve(GameState s, ImmutableDictionary<string, object> context)
	{
		var controller = (int)context[ContextKeys.Controller];
		var targets = context.TryGetValue(ContextKeys.Targets, out var t)
			? (ImmutableArray<int>)t
			: [];
		int TargetAt(int i) => i < targets.Length ? targets[i] : 0;

		IEnumerable<int> ids = Kind switch
		{
			RefKind.You => [controller],
			RefKind.EachOpponent => [s.OpponentOf(controller)],
			RefKind.EachPlayer => [s.Player1Id(), s.Player2Id()],
			RefKind.Target => [TargetAt(Index)],
			RefKind.ControllerOfTarget => s.HasObject(TargetAt(Index))
			&& s.GetObject(TargetAt(Index)) is Card c
				? [c.ControllerId]
				: [],
			RefKind.Source => SameObject(s, context) ? [(int)context[ContextKeys.Source]] : [],
			RefKind.EventCard =>
			[
				context.TryGetValue(ContextKeys.EventCard, out var e) ? (int)e : 0,
			],
			RefKind.OpponentsPermanents => s.CardsIn(s.OpponentOf(controller), ZoneType.Battlefield)
				.Where(c => c.Is(Type))
				.Select(c => c.Id),
			RefKind.AllPermanents => new[] { controller, s.OpponentOf(controller) }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(c => c.Is(Type))
				.Select(c => c.Id),
			RefKind.YourGraveyard => s.CardsIn(controller, ZoneType.Graveyard)
				.Where(c => Type == CardType.None || c.Is(Type))
				.Select(c => c.Id),
			_ => s.CardsIn(controller, ZoneType.Battlefield)
				.Where(c => c.Is(Type))
				.Select(c => c.Id),
		};
		return ids.Where(id => id != 0 && s.HasObject(id));
	}
}

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

public record Destroy(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Aggregate(
					s,
					(st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Graveyard))
				)
		);
}

public record Exile(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Aggregate(s, (st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Exile)))
		);
}

/// <summary>"Gets +P/+T [and gains Keyword] until end of turn." Amounts are counted as it resolves.</summary>
public record ModifyUntilEndOfTurn(Ref What, Count Power, Count Toughness, string? Keyword = null)
	: Effect
{
	/// <summary>Perfected Theory: "has base power and toughness P/T until end of turn" (CR 613, layer 7b).</summary>
	public (int Power, int Toughness)? SetBase { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var effect = new UntilEndOfTurn(
			Power.Value(s, InputContext),
			Toughness.Value(s, InputContext),
			Keyword
		)
		{
			SetBase = SetBase,
		};
		return new(
			Permanents(What, s).Aggregate(s, (st, c) => Update(st, (Card)c.WithComponent(effect)))
		);
	}
}

public enum CountOf
{
	Fixed,
	NumberOf, // "the number of [Ref]"
	GreatestPowerOf, // "the greatest power among [Ref]"
	PowerOf, // "equal to its power"
	LoyaltyOf, // "equal to its loyalty"
	X, // the X paid
}

/// <summary>
/// A number in rules text, counted at resolution. Counting goes through Ref, so "creatures you
/// control" means one thing everywhere. A negative Sign is "-X".
/// </summary>
public record Count(int Fixed, Ref? Of = null, CountOf Kind = CountOf.Fixed, int Sign = 1)
{
	public static implicit operator Count(int n) => new(n);

	public static Count NumberOf(Ref r) => new(0, r, CountOf.NumberOf);

	public static Count GreatestPowerOf(Ref r) => new(0, r, CountOf.GreatestPowerOf);

	public static Count PowerOf(Ref r) => new(0, r, CountOf.PowerOf);

	public static Count LoyaltyOf(Ref r) => new(0, r, CountOf.LoyaltyOf);

	public static readonly Count X = new(0, null, CountOf.X);

	public static readonly Count CreaturesYouControl = NumberOf(Ref.CreaturesYouControl);
	public static readonly Count LandsYouControl = NumberOf(Ref.LandsYouControl);

	public Count Negated => this with { Sign = -Sign };

	/// <summary>"Is this a fixed nothing?" — a static with Power 0 and Toughness 0 changes no P/T.</summary>
	public static bool operator ==(Count c, int n) =>
		c.Kind == CountOf.Fixed && c.Sign * c.Fixed == n;

	public static bool operator !=(Count c, int n) => !(c == n);

	public int Value(GameState s, ImmutableDictionary<string, object> context)
	{
		if (Kind == CountOf.Fixed)
			return Sign * Fixed;
		if (Kind == CountOf.X)
			return Sign * (context.TryGetValue(ContextKeys.X, out var x) ? (int)x : 0);
		var ids = Of!.Resolve(s, context);
		IEnumerable<Card> Cards() => ids.Select(s.GetObject).OfType<Card>();
		var counted = Kind switch
		{
			CountOf.NumberOf => ids.Count(),
			CountOf.GreatestPowerOf => Cards().Select(c => s.Power(c)).DefaultIfEmpty(0).Max(),
			CountOf.PowerOf => Cards().Select(c => s.Power(c)).FirstOrDefault(),
			_ => Cards().Select(c => c.Loyalty).FirstOrDefault(),
		};
		// CR 107.1b: a number that would be negative is 0 — a -4 power must not make "lose X life"
		// a life gain. The sign ("-X") applies after.
		return Sign * Math.Max(0, counted);
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
					(st, c) => Update(st, c with { PlusOneCounters = c.PlusOneCounters + n })
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
				.Aggregate(s, (st, c) => Actions.Loyalty.Change(st, c.Id, n))
		);
	}
}

/// <summary>
/// CR 701.25: look at the top N, put any of them into the graveyard, the rest back on top.
/// ponytail: the kept cards keep their order; reordering them is a second choice nobody needs at N=1.
/// </summary>
public record Surveil(int N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var n = Math.Min(N, s.CardsIn(Controller, ZoneType.Library).Count());
		if (n == 0)
			return new(s);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new ChooseFromLibraryTopChoice
						{
							PlayerId = Controller,
							Count = n,
							MinChoices = 0,
							MaxChoices = n,
						},
						new MoveChosenToGraveyardAction { PlayerId = Controller },
					],
				}
			)
		);
	}
}

public record CreateTokens(CardData Token, int Count) : Effect
{
	/// <summary>Stingerquill Charm: "Create a ... Cadet. It gains haste until end of turn."</summary>
	public string? GainsUntilEndOfTurn { get; init; }

	public override ActionResult Execute(GameState s)
	{
		for (var i = 0; i < Count; i++)
		{
			var token = CardFactory.Create(CardDefinition.Vanilla(Token), Controller) with
			{
				IsToken = true,
			};
			(s, token) = s.AddObject(token);
			s = s.EnterBattlefield(token.Id, Controller);
			if (GainsUntilEndOfTurn is { } keyword)
				s = Update(
					s,
					(Card)s.GetCard(token.Id).WithComponent(new UntilEndOfTurn(Keyword: keyword))
				);
		}
		return new(s);
	}
}

/// <summary>Each player named discards, choosing for themselves.</summary>
public record Discard(Ref Who, int Count) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s))
		{
			var n = Math.Min(Count, s.CardsIn(player, ZoneType.Hand).Count());
			if (n > 0)
				s = s.SpawnAction(
					new PipelineAction
					{
						Steps =
						[
							new ChooseCardsFromHandChoice
							{
								PlayerId = player,
								MinChoices = n,
								MaxChoices = n,
								Prompt = $"Discard {n}",
							},
							new MoveChosenToGraveyardAction { PlayerId = player },
						],
					}
				);
		}
		return new(s);
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
		new(Permanents(What, s).Aggregate(s, (st, c) => Update(st, c with { IsTapped = false })));
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

/// <summary>"Return target spell or creature to its owner's hand" - from the battlefield or the stack.</summary>
public record ReturnToHand(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		var cards = All(What, s)
			.Where(id => s.IsOnBattlefield(id) || s.GetParent(id) == stack)
			.Select(s.GetCard)
			.ToList();
		return new(
			cards.Aggregate(s, (st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Hand)))
		);
	}
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

/// <summary>"Its owner shuffles it into their library."</summary>
public record ShuffleIntoLibrary(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var card in Permanents(What, s).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Library)).Shuffle(card.OwnerId);
		return new(s);
	}
}

/// <summary>
/// Break Under Pressure: each player named sacrifices one of their [Types] with the greatest mana value.
/// ponytail: on a tie the first is taken; the rules let that player choose among the tied.
/// </summary>
public record SacrificeGreatestManaValue(Ref Who, CardType Types) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer))
		{
			var victim = s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Is(Types))
				.OrderByDescending(c => c.ManaCost.ManaValue)
				.FirstOrDefault();
			if (victim is not null)
				s = s.Sacrifice(victim.Id);
		}
		return new(s);
	}
}

/// <summary>
/// "You may sacrifice a [Filter]. If you do, [Then]." The controller picks one or none; the Then
/// effects run only if something was sacrificed, with this ability's context.
/// </summary>
public record MaySacrifice(TargetSpec Filter, ImmutableList<GameAction> Then) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					// Its own key, cleared: a "none" answer leaves the context untouched, so a stale
					// pick from an earlier choice would otherwise be sacrificed.
					PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
					Steps =
					[
						new ChoosePermanentChoice(
							Controller,
							GetInput<int>(ContextKeys.Source),
							Filter
						)
						{
							Prompt = "You may sacrifice",
						},
						new SacrificeChosenAction(),
						new IfChosenAction(Then),
					],
				}
			)
		);
}

/// <summary>Winter: "each opponent sacrifices a creature of their choice" — each chooses their own.</summary>
public record EachSacrifices(Ref Who, TargetSpec Filter) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer))
		{
			var choice = new ChoosePermanentChoice(
				player,
				GetInput<int>(ContextKeys.Source),
				Filter,
				required: true
			)
			{
				Prompt = $"Sacrifice ({Source(s).Name})",
			};
			if (!choice.GetOptions(s, InputContext).IsEmpty)
				s = s.SpawnAction(
					new PipelineAction { Steps = [choice, new SacrificeChosenAction()] }
				);
		}
		return new(s);
	}
}

/// <summary>"Exile [it] until this leaves the battlefield." Does nothing if this already left (CR 610.3c).</summary>
public record ExileUntilSourceLeaves(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var sourceId = GetInput<int>(ContextKeys.Source);
		var source = Ref.Source.Resolve(s, InputContext).FirstOrDefault(); // 0 if it left, even if it came back
		if (source == 0 || !s.IsOnBattlefield(sourceId))
			return new(s);
		foreach (var card in Permanents(What, s).ToList())
		{
			s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Exile));
			s = Update(
				s,
				(Card)
					s.GetCard(card.Id)
						.WithComponent(
							new ExiledUntilLeaves(sourceId, s.GetCard(sourceId).Incarnation)
						)
			);
		}
		return new(s);
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

public abstract record Condition
{
	public abstract bool Holds(GameState s, ImmutableDictionary<string, object> context);
}

/// <summary>Prophesied End: "if it wasn't attacking" — read BEFORE the destroy, while it still knows.</summary>
public record TargetIsNotAttacking(int Index) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.Target(Index).Resolve(s, context).Select(s.GetCard).Any(c => c.AttackingId == 0);
}

/// <summary>Budding Insurgent: "if that permanent was a legendary enchantment" — read before it is destroyed.</summary>
public record TargetIs(int Index, CardType Type, Supertype Supertype = Supertype.None) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.Target(Index)
			.Resolve(s, context)
			.Select(s.GetObject)
			.OfType<Card>()
			.Any(c => c.Is(Type) && (Supertype == Supertype.None || c.Is(Supertype)));
}

public record EventCardHasSubtype(string Subtype) : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		Ref.EventCard.Resolve(s, context).Select(s.GetCard).Any(c => c.Subtypes.Contains(Subtype));
}

public record If(Condition When, GameAction Then) : Effect
{
	public override ActionResult Execute(GameState s) =>
		When.Holds(s, InputContext)
			? (Then with { InputContext = InputContext }).Execute(s)
			: new(s);
}
