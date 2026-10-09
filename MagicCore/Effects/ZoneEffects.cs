using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Effects;

// Effects that move cards between zones: destroy, exile, sacrifice, return, discard, surveil, tokens.

public record Destroy(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			Permanents(What, s)
				.Where(c => !s.IsIndestructible(c))
				.Aggregate(
					s,
					(st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Graveyard))
				)
		);
}

public record Exile(Ref What) : Effect
{
	/// <summary>Overwrite the Multiverse: "… where X is the number of creatures exiled this way" — run after, with X.</summary>
	public ImmutableList<GameAction> ThenWithXExiled { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var exiled = Permanents(What, s).ToList();
		s = exiled.Aggregate(s, (st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Exile)));
		return ThenWithXExiled.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						Steps = ThenWithXExiled,
						PipelineContext = InputContext.SetItem(ContextKeys.X, exiled.Count),
					}
				)
			);
	}
}

/// <summary>
/// CR 701.25: look at the top N, put any of them into the graveyard, the rest back on top.
/// ponytail: the kept cards keep their order; reordering them is a second choice nobody needs at N=1.
/// </summary>
public record Surveil(int N) : Effect
{
	/// <summary>Riders on the cards put into the graveyard "this way" — they read the surveil's choice.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		s = s.Tally(Controller, t => t with { Surveils = t.Surveils + 1 }) // "if you've surveilled"
			.Raise(new SurveilledEvent(Controller));
		var n = Math.Min(N, s.CardsIn(Controller, ZoneType.Library).Count());
		if (n == 0)
			return new(s);
		ImmutableList<GameAction> steps =
		[
			new ChooseFromLibraryTopChoice
			{
				PlayerId = Controller,
				Count = n,
				MinChoices = 0,
				MaxChoices = n,
			},
			new MoveChosenToGraveyardAction { PlayerId = Controller },
			.. Then,
		];
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					// The ability's context for Then; its own key cleared, so no earlier pick leaks in.
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps = steps,
				}
			)
		);
	}
}

/// <summary>
/// Enlightened Confidant: "If you put a card with mana value N or less into your graveyard this way,
/// put that card into your hand." A surveil's Then.
/// </summary>
public record ReturnChosenToHandIfManaValueAtMost(Count Max) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var max = Max.Value(s, InputContext);
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			if (
				s.IsIn(id, Controller, ZoneType.Graveyard)
				&& s.GetCard(id).ManaCost.ManaValue <= max
			)
				s = s.MoveCard(id, s.ZoneId(Controller, ZoneType.Hand));
		return new(s);
	}
}

public record CreateTokens(CardData Token, Count Count) : Effect
{
	/// <summary>The token's own abilities — the Heartwood's "{T}: Add {R} or {G}."</summary>
	public ImmutableArray<GameComponent> Abilities { get; init; } = [];

	/// <summary>Stingerquill Charm: "Create a ... Cadet. It gains haste until end of turn."</summary>
	public string? GainsUntilEndOfTurn { get; init; }

	/// <summary>Recursive Recruitment: "put N +1/+1 counters on each of them" — counted once, as it resolves.</summary>
	public Count Counters { get; init; } = 0;

	public override ActionResult Execute(GameState s)
	{
		var count = Count.Value(s, InputContext);
		var counters = Counters.Value(s, InputContext);
		for (var i = 0; i < count; i++)
		{
			var token = CardFactory.Create(new CardDefinition(Token, Abilities), Controller) with
			{
				IsToken = true,
			};
			(s, token) = s.AddObject(token);
			s = s.EnterBattlefield(token.Id, Controller, extraCounters: counters);
			if (GainsUntilEndOfTurn is { } keyword)
				s = Update(
					s,
					(Card)s.GetCard(token.Id).WithComponent(new UntilEndOfTurn(Keyword: keyword))
				);
		}
		return new(s);
	}
}

/// <summary>Infinite Coursework: "It becomes unprepared."</summary>
public record BecomeUnprepared(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => Prepare.Unprepare(st, c.Id)));
}

/// <summary>"[It] becomes prepared" — through Rules.Prepare, which ignores one already prepared.</summary>
public record BecomePrepared(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => Prepare.Become(st, c.Id)));
}

/// <summary>"Mill N" (CR 701.13): the top N cards of each player's library into their graveyard.</summary>
public record Mill(Ref Who, int N) : Effect
{
	/// <summary>Primal Witchstalker: "mill four cards. When you do, …" — run only if a card was milled.</summary>
	public ImmutableList<GameAction> IfMilled { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var any = false;
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		{
			(s, var milled) = Top(s, player, N);
			any |= !milled.IsEmpty;
		}
		return !any || IfMilled.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction { Steps = IfMilled, PipelineContext = InputContext }
				)
			);
	}

	/// <summary>The ONE mill: the top N into the graveyard, stopping early on a short library; the milled ids.</summary>
	public static (GameState State, ImmutableList<int> Milled) Top(GameState s, int playerId, int n)
	{
		var milled = s.CardsIn(playerId, ZoneType.Library)
			.Take(n)
			.Select(c => c.Id)
			.ToImmutableList();
		foreach (var id in milled)
			s = s.MoveCard(id, s.ZoneId(playerId, ZoneType.Graveyard));
		return (s, milled);
	}
}

/// <summary>"Search your library for a [Type] card, put it [To], then shuffle." Enroot; landcycling.</summary>
public record SearchLibrary(CardType Type, ZoneType To) : Effect
{
	/// <summary>Basic landcycling: "a basic land card".</summary>
	public Supertype Supertype { get; init; }

	/// <summary>Fblthp: "up to X … with different names" — the options are already one per name.</summary>
	public Count UpTo { get; init; } = 1;

	/// <summary>Simulacrum Shaper: "put that card onto the battlefield tapped".</summary>
	public bool Tapped { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps =
					[
						new ChooseFromLibraryChoice
						{
							PlayerId = Controller,
							Type = Type,
							Supertype = Supertype,
							MaxChoices = Math.Max(0, UpTo.Value(s, InputContext)),
							Prompt =
								$"Search for {(UpTo == 1 ? "a" : $"up to {UpTo.Value(s, InputContext)}")} {(Supertype == Supertype.None ? "" : Supertype.ToString().ToLowerInvariant() + " ")}{Type.ToString().ToLowerInvariant()} card{(UpTo == 1 ? "" : "s with different names")}",
						},
						// Loyal Tutor: "then shuffle and put that card on top" — the shuffle comes first.
						.. To == ZoneType.Library
							? (GameAction[])
								[
									new ShuffleLibraryAction(Controller),
									new MoveChosenToAction(Controller, ZoneType.Library)
									{
										OnTop = true,
									},
								]
							:
							[
								new MoveChosenToAction(Controller, To) { Tapped = Tapped },
								new ShuffleLibraryAction(Controller),
							],
					],
				}
			)
		);
}

/// <summary>
/// "Put [it] on the bottom (or top) of its owner's library" — Paradox Shaper's graveyard card,
/// Clash of Elements' permanent.
/// </summary>
public record PutIntoLibrary(Ref What, bool OnTop) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in All(What, s).ToList())
			if (s.GetObject(id) is Card card)
				s = s.MoveCard(id, s.ZoneId(card.OwnerId, ZoneType.Library), toTop: OnTop);
		return new(s);
	}
}

/// <summary>Improvised Act: "You may discard a card. If you do, [Then]."</summary>
public record MayDiscard(ImmutableList<GameAction> Then) : Effect
{
	public override ActionResult Execute(GameState s) =>
		s.CardsIn(Controller, ZoneType.Hand).Any()
			? new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps =
						[
							new ChooseCardsFromHandChoice
							{
								PlayerId = Controller,
								MinChoices = 0,
								MaxChoices = 1,
								Prompt = "You may discard a card",
							},
							new MoveChosenToGraveyardAction { PlayerId = Controller },
							new IfChosenAction(Then) { Key = ChooseCardsFromHandChoice.Chosen },
						],
					}
				)
			)
			: new(s);
}

/// <summary>
/// Arc of Fortune: "Each player may [Then]." Each player answers for themselves, active player
/// first, and Then runs as that player's ("you" is them).
/// ponytail: each acts as they answer; the rules have every player choose, then all act at once.
/// The difference shows only if a later chooser could see an earlier one's new hand.
/// </summary>
public record EachPlayerMay(string Prompt, ImmutableList<GameAction> Then) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var active = s.GetGame().ActivePlayerId;
		foreach (var player in new[] { active, s.OpponentOf(active) })
			s = s.SpawnAction(
				new May(Prompt, new Always(), Then)
				{
					InputContext = InputContext.SetItem(ContextKeys.Controller, player),
				}
			);
		return new(s);
	}
}

/// <summary>Stingcaster Mage: "[it] gains flashback until end of turn" — read by Costs.Flashback.</summary>
public record GainFlashbackThisTurn(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			All(What, s)
				.Where(id => s.GetObject(id) is Card)
				.Aggregate(
					s,
					(st, id) =>
						Update(
							st,
							(Card)
								st.GetCard(id)
									.WithoutComponents<GrantedFlashback>()
									.WithComponent(new GrantedFlashback(st.GetGame().TurnNumber))
						)
				)
		);
}

/// <summary>"Return this card from your graveyard to the battlefield" (Darklight Phoenix) or "to your hand" (Command the Stage).</summary>
public record ReturnFromGraveyard(Ref What, ZoneType To) : Effect
{
	/// <summary>Generous Revival: "with an additional +1/+1 counter on it" — it enters with it.</summary>
	public int Counters { get; init; }

	/// <summary>Gallia, Tragic Host: "to the battlefield tapped".</summary>
	public bool Tapped { get; init; }

	/// <summary>Grim Repriser, Proctor: "with a finality counter on it".</summary>
	public bool Finality { get; init; }

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in All(What, s).ToList())
			if (s.GetObject(id) is Card card && s.IsIn(id, card.OwnerId, ZoneType.Graveyard))
				s =
					To == ZoneType.Battlefield
						? s.EnterBattlefield(
							id,
							card.OwnerId,
							extraCounters: Counters,
							tapped: Tapped,
							finality: Finality
						)
						: s.MoveCard(id, s.ZoneId(card.OwnerId, To));
		return new(s);
	}
}

/// <summary>Blazing Crescendo: "Exile the top card of your library. Until the end of your next turn, you may play that card."</summary>
public record ExileTopPlayable : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var top = s.CardsIn(Controller, ZoneType.Library).FirstOrDefault();
		if (top is null)
			return new(s);
		s = s.MoveCard(top.Id, s.ZoneId(Controller, ZoneType.Exile));
		return new(
			Update(
				s,
				(Card)
					s.GetCard(top.Id)
						.WithComponent(
							new PlayableFromExile(
								Controller,
								Playable.EndOfNextTurnOf(s, Controller)
							)
						)
			)
		);
	}
}

/// <summary>Afterthought Sentry: "exile [target card] from a graveyard" — a card, not a permanent.</summary>
public record ExileFromGraveyard(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in All(What, s).ToList())
			if (s.GetObject(id) is Card card && s.IsIn(id, card.OwnerId, ZoneType.Graveyard))
				s = s.MoveCard(id, s.ZoneId(card.OwnerId, ZoneType.Exile));
		return new(s);
	}
}

/// <summary>
/// Solve for Disappointment: "Target opponent reveals their hand. You choose a [Types] card from it.
/// That player discards that card." A real choice — MtgCore stands in "their most expensive card",
/// but the card is costed on the choosing.
/// </summary>
public record YouChooseTheyDiscard(Ref Who, CardType Types) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		{
			var choice = new ChooseCardsFromHandChoice
			{
				PlayerId = player,
				DeciderId = Controller,
				Types = Types,
				MinChoices = 1,
				MaxChoices = 1,
				Prompt = "Choose a card for them to discard",
			};
			if (!choice.GetOptions(s, InputContext).IsEmpty)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps = [choice, new MoveChosenToGraveyardAction { PlayerId = player }],
					}
				);
		}
		return new(s);
	}
}

/// <summary>
/// Something Worth Saving: "Mill N. You may put a [Types] card from among them into your hand." Mill,
/// CHOOSE, move — MtgCore's dig once shipped without its middle step and did nothing at all.
/// </summary>
public record MillAndMayTake(int N, CardType Types) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		(s, var milled) = Mill.Top(s, Controller, N);
		var choice = new ChooseAmongChoice
		{
			PlayerId = Controller,
			Ids = milled,
			Types = Types,
			Prompt = "You may put one into your hand",
		};
		if (choice.GetOptions(s, InputContext).IsEmpty)
			return new(s); // nothing to choose: no prompt with no options
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps = [choice, new MoveChosenToAction(Controller, ZoneType.Hand)],
				}
			)
		);
	}
}

/// <summary>Equip: "Attach to target creature you control." This Equipment, to the target.</summary>
public record AttachSourceTo(Ref Host) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var self = Ref.Source.Resolve(s, InputContext).FirstOrDefault();
		var host = Permanents(Host, s).FirstOrDefault();
		return new(self == 0 || host is null ? s : Attachments.Attach(s, self, host.Id));
	}
}

/// <summary>"Sacrifice this …" — only what is still on the battlefield.</summary>
public record Sacrifice(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Aggregate(s, (st, c) => st.Sacrifice(c.Id)));
}

/// <summary>Ajani Unrelenting: "Discard your hand" — the whole hand, so there is nothing to choose.</summary>
public record DiscardHand(Ref Who) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
			s = s.Discard(player, [.. s.CardsIn(player, ZoneType.Hand).Select(c => c.Id)]);
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
