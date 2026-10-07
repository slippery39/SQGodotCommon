using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;

namespace MagicCore.Effects;

/// <summary>Pipeline context keys a resolving spell or ability provides to its effects.</summary>
public static class ContextKeys
{
	public const string Controller = "Controller";
	public const string X = "X";

	/// <summary>ImmutableArray&lt;int&gt;, one per target word; 0 where unchosen or no longer legal.</summary>
	public const string Targets = "Targets";

	/// <summary>The spell card or the permanent whose ability this is.</summary>
	public const string Source = "Source";

	/// <summary>The card the trigger event was about — the creature that entered, the land that fell.</summary>
	public const string EventCard = "EventCard";

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
	CreaturesYouControl,
}

/// <summary>Who or what an effect acts on, resolved against the context at resolution.</summary>
public record Ref(RefKind Kind, int Index = 0)
{
	public static readonly Ref You = new(RefKind.You);
	public static readonly Ref EachOpponent = new(RefKind.EachOpponent);
	public static readonly Ref EachPlayer = new(RefKind.EachPlayer);
	public static readonly Ref Source = new(RefKind.Source);
	public static readonly Ref EventCard = new(RefKind.EventCard);
	public static readonly Ref CreaturesYouControl = new(RefKind.CreaturesYouControl);

	public static Ref Target(int index = 0) => new(RefKind.Target, index);

	public static Ref ControllerOfTarget(int index = 0) => new(RefKind.ControllerOfTarget, index);

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
			RefKind.Source => [(int)context[ContextKeys.Source]],
			RefKind.EventCard =>
			[
				context.TryGetValue(ContextKeys.EventCard, out var e) ? (int)e : 0,
			],
			_ => s.CardsIn(controller, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Creature))
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

public record DrawCards(Ref Who, int Count) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(All(Who, s).Aggregate(s, (st, p) => st.Draw(p, Count)));
}

public record GainLife(Ref Who, int Amount) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			All(Who, s)
				.Aggregate(
					s,
					(st, p) =>
						st.WithPlayer(st.GetPlayer(p) with { Life = st.GetPlayer(p).Life + Amount })
				)
		);
}

/// <summary>Life loss that is not damage — lifelink and damage triggers do not see it.</summary>
public record LoseLife(Ref Who, int Amount) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			All(Who, s)
				.Aggregate(
					s,
					(st, p) =>
						st.WithPlayer(st.GetPlayer(p) with { Life = st.GetPlayer(p).Life - Amount })
				)
		);
}

/// <summary>"[Source] deals N damage to …" — through <see cref="Damage.Deal"/>, so lifelink and deathtouch apply.</summary>
public record DealDamage(Ref To, int Amount) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(All(To, s).Aggregate(s, (st, id) => Damage.Deal(st, Source(s), id, Amount)));
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

/// <summary>"Gets +P/+T [and gains Keyword] until end of turn."</summary>
public record ModifyUntilEndOfTurn(Ref What, int Power, int Toughness, string? Keyword = null)
	: Effect
{
	/// <summary>Arni: "+X/+0 where X is that creature's power" — the entering creature's power, read now.</summary>
	public bool PowerFromEventCard { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var power = PowerFromEventCard
			? Ref
				.EventCard.Resolve(s, InputContext)
				.Select(id => s.GetCard(id).Power)
				.FirstOrDefault()
			: Power;
		return new(
			Permanents(What, s)
				.Aggregate(
					s,
					(st, c) =>
						Update(
							st,
							(Card)c.WithComponent(new UntilEndOfTurn(power, Toughness, Keyword))
						)
				)
		);
	}
}

public record AddCounters(Ref What, int Count) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Where(c => c.Is(CardType.Creature))
				.Aggregate(
					s,
					(st, c) => Update(st, c with { PlusOneCounters = c.PlusOneCounters + Count })
				)
		);
}

public record CreateTokens(CardData Token, int Count) : Effect
{
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
							new DiscardChosenAction { PlayerId = player },
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
		s = Damage.Deal(s, a, b.Id, a.Power);
		return new(Damage.Deal(s, b, a.Id, b.Power));
	}
}

public record Untap(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => Update(st, c with { IsTapped = false })));
}

public record AddMana(ManaColor Color) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var player = s.GetPlayer(Controller);
		return new(s.WithPlayer(player with { ManaPool = player.ManaPool.Add(Color) }));
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
