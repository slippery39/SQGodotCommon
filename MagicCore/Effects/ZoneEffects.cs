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
	public override ActionResult Execute(GameState s) => new(All(s, Permanents(What, s)));

	/// <summary>CR 701.7: each to its owner's graveyard — but not one that is indestructible.</summary>
	public static GameState All(GameState s, IEnumerable<Card> permanents) =>
		permanents
			.Where(c => !s.IsIndestructible(c))
			.ToList()
			.Aggregate(s, (st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Graveyard)));
}

/// <summary>
/// Kindred Judgment: "Choose a creature type. Destroy all creatures that aren't of the chosen type."
/// The options are the types among creatures on the battlefield, and one for a type none of them
/// has — which destroys them all.
/// </summary>
public record DestroyCreaturesNotOfChosenType : Effect
{
	public const string NoneOfThem = "A type no creature has";

	public override ActionResult Execute(GameState s)
	{
		ImmutableArray<string> types =
		[
			.. Ref
				.AllCreatures.Resolve(s, InputContext)
				.SelectMany(id => s.GetCard(id).Subtypes)
				.Distinct()
				.Order(),
			NoneOfThem,
		];
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new ChooseKeywordChoice(Controller, types)
						{
							Prompt = "Choose a creature type",
						},
						new DestroyCreaturesNotOfTypeAction(types),
					],
				}
			)
		);
	}
}

/// <summary>After the choice: every creature without the chosen type is destroyed.</summary>
public record DestroyCreaturesNotOfTypeAction(ImmutableArray<string> Types) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var chosen = InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is int id
			? Types[id - 1]
			: DestroyCreaturesNotOfChosenType.NoneOfThem;
		return new(
			Destroy.All(s, Permanents(Ref.AllCreatures, s).Where(c => !c.Subtypes.Contains(chosen)))
		);
	}
}

/// <summary>
/// Flickering Hound: "exile [it], then return that card to the battlefield under its owner's
/// control" — a new object (CR 400.7). A token exiled this way ceases to exist (CR 111.7).
/// </summary>
public record Flicker(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var card in Permanents(What, s).ToList())
		{
			s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Exile));
			if (!card.IsToken && s.IsIn(card.Id, card.OwnerId, ZoneType.Exile))
				s = s.EnterBattlefield(card.Id, card.OwnerId);
		}
		return new(s);
	}
}

/// <summary>Curse-Marred Demon: "discard a card at random" — MtgCore's DiscardRandomCardAction, through the one discard.</summary>
public record DiscardAtRandom(Ref Who, int N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		{
			var hand = s.CardsIn(player, ZoneType.Hand).Select(c => c.Id).ToArray();
			(var rng, s) = s.TakeRng();
			rng.Shuffle(hand);
			s = s.Discard(player, [.. hand.Take(N)]);
		}
		return new(s);
	}
}

/// <summary>
/// Identity Echo: "Reveal cards from the top of your library until you reveal a [Types] card. Put
/// that card onto the battlefield and the rest on the bottom of your library in a random order."
/// </summary>
public record RevealUntilOntoBattlefield(CardType Types) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var library = s.CardsIn(Controller, ZoneType.Library).ToList();
		var hit = library.FindIndex(c => c.Is(Types));
		var rest = (hit < 0 ? library : library.Take(hit)).Select(c => c.Id).ToArray();
		if (hit >= 0)
			s = s.EnterBattlefield(library[hit].Id, Controller);
		(var rng, s) = s.TakeRng();
		rng.Shuffle(rest);
		var libraryId = s.ZoneId(Controller, ZoneType.Library);
		foreach (var id in rest) // out and back in: MoveObject appends, and the end is the bottom
			s = s.MoveObject(id, 0).MoveObject(id, libraryId);
		return new(s);
	}
}

/// <summary>
/// Sphinx's Approach: "exile this spell and four cards named Sphinx's Approach from your graveyard"
/// — the first of a May's Then; asked only when <see cref="GraveyardHasCopiesOfSource"/>.
/// </summary>
public record ExileSourceAndCopiesFromGraveyard(int N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var source = Source(s);
		var stack = s.GetWellKnownId(MagicKeys.Stack);
		if (s.HasObject(source.Id) && s.GetParent(source.Id) == stack)
			s = s.MoveCard(source.Id, s.ZoneId(source.OwnerId, ZoneType.Exile));
		foreach (
			var copy in GraveyardHasCopiesOfSource.Copies(s, Controller, source).Take(N).ToList()
		)
			s = s.MoveCard(copy.Id, s.ZoneId(copy.OwnerId, ZoneType.Exile));
		return new(s);
	}
}

/// <summary>"[N] cards named [this card] in your graveyard" — not this one.</summary>
public record GraveyardHasCopiesOfSource(int N) : Condition
{
	public static IEnumerable<Card> Copies(GameState s, int playerId, Card source) =>
		s.CardsIn(playerId, ZoneType.Graveyard)
			.Where(c => c.Name == source.Name && c.Id != source.Id);

	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.HasObject((int)context[ContextKeys.Source])
		&& s.GetObject((int)context[ContextKeys.Source]) is Card source
		&& Copies(s, (int)context[ContextKeys.Controller], source).Count() >= N;
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

	/// <summary>Vraska, the Cutting Glare: "They create a Treasure token" — whose; unset is you.</summary>
	public Ref? ControlledBy { get; init; }

	/// <summary>Tenured Tethermage: "create two tapped Heartwood tokens".</summary>
	public bool Tapped { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var count = Count.Value(s, InputContext);
		var counters = Counters.Value(s, InputContext);
		var controller = ControlledBy is null ? Controller : All(ControlledBy, s).FirstOrDefault();
		if (controller == 0)
			return new(s);
		var template = Instead(
			s,
			controller,
			CardFactory.Create(new(Token, Abilities), controller)
		);
		for (var i = 0; i < count; i++)
		{
			var token = template with { IsToken = true };
			(s, token) = s.AddObject(token);
			s = s.EnterBattlefield(token.Id, controller, extraCounters: counters, tapped: Tapped);
			if (GainsUntilEndOfTurn is { } keyword)
				s = Update(
					s,
					(Card)s.GetCard(token.Id).WithComponent(new UntilEndOfTurn(Keyword: keyword))
				);
		}
		return new(s);
	}

	/// <summary>
	/// The token actually created — Draconic Visitor: an artifact token under its controller's control
	/// is a Dragon instead (CR 614.1a). Every token goes through here, copies included.
	/// </summary>
	public static Card Instead(GameState s, int controller, Card token) =>
		token.Is(CardType.Artifact)
		&& s.CardsIn(controller, ZoneType.Battlefield)
			.SelectMany(s.AbilitiesOf<ArtifactTokensBecome>)
			.FirstOrDefault()
			is { } instead
			? CardFactory.Create(new(instead.Token, instead.Abilities), controller)
			: token;
}

/// <summary>
/// "Create a token that's a copy of [it]" (CR 707.2, 111.10): its copiable values on a new token
/// under your control — never its counters, damage or what it was given — entering through
/// EnterBattlefield, so its own "enters" abilities fire. A source that just died is copied as it
/// last existed (Sphinx of False Conclusions). Adding: Face Yourself's "except it has …" (CR 707.9b).
/// </summary>
public record CreateTokenCopy(Ref What) : Effect
{
	/// <summary>"Except it has haste" — keywords the copies also have.</summary>
	public ImmutableArray<string> AddingKeywords { get; init; } = [];

	/// <summary>"… and '[ability]'" — abilities the copies also have.</summary>
	public ImmutableArray<GameComponent> Adding { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var originals = Permanents(What, s).ToList();
		if (
			originals.Count == 0
			&& What.Kind == RefKind.Source
			&& GetInput<Card?>(ContextKeys.SourceSnapshot) is { } lastKnown
		)
			originals.Add(lastKnown);
		foreach (var original in originals)
		{
			var copy = new Card
			{
				OwnerId = Controller,
				ControllerId = Controller,
				IsToken = true,
			}.WithCopiableValuesOf(original);
			copy = CreateTokens.Instead(
				s,
				Controller,
				copy with
				{
					Keywords = [.. copy.Keywords, .. AddingKeywords],
					Components = [.. copy.Components, .. Adding],
				}
			) with
			{
				IsToken = true,
			};
			(s, copy) = s.AddObject(copy);
			s = s.EnterBattlefield(copy.Id, Controller);
		}
		return new(s);
	}
}

/// <summary>
/// Vindictive Triumph: "return it to the battlefield tapped under your control. Exile it at the
/// beginning of the next end step." — a card exiled earlier in this resolution; a token that left
/// can't come back (CR 111.8).
/// </summary>
public record ReturnFromExileUnderYourControl(Ref What) : Effect
{
	public bool Tapped { get; init; }

	/// <summary>"Exile it at the beginning of the next end step" — a delayed trigger on it.</summary>
	public bool ExileAtNextEndStep { get; init; }

	static readonly DelayedTrigger ExileItAtTheEndStep =
		new(new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new Exile(Ref.Source)]));

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in All(What, s).ToList())
		{
			if (
				s.GetObject(id) is not Card { IsToken: false } card
				|| !s.IsIn(id, card.OwnerId, ZoneType.Exile)
			)
				continue;
			s = s.EnterBattlefield(id, Controller, tapped: Tapped);
			if (ExileAtNextEndStep)
				s = Update(s, (Card)s.GetCard(id).WithComponent(ExileItAtTheEndStep));
		}
		return new(s);
	}
}

/// <summary>
/// Hall of Echoes: "This land becomes a copy of target creature you control until end of turn" —
/// what it was is kept on it (CopyUntilEndOfTurn) and comes back in cleanup or as it leaves.
/// </summary>
public record BecomeCopyUntilEndOfTurn(Ref What, Ref Of) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		if (Permanents(Of, s).FirstOrDefault() is not { } model)
			return new(s);
		foreach (var card in Permanents(What, s).ToList())
		{
			var original = card.GetComponent<CopyUntilEndOfTurn>()?.Original ?? card; // a second copy restores to the first original
			s = Update(
				s,
				(Card)
					card.WithCopiableValuesOf(model)
						.WithComponentReplaced(new CopyUntilEndOfTurn(original))
			);
		}
		return new(s);
	}
}

/// <summary>Hall of Echoes: "The 'legend rule' doesn't apply to permanents you control this turn."</summary>
public record NoLegendRuleThisTurn : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(s.Tally(Controller, t => t with { NoLegendRule = true }));
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

	/// <summary>Sphinx's Approach: "a Sphinx creature card".</summary>
	public string? Subtype { get; init; }

	/// <summary>Fblthp: "with different names". Unset, two Forests may both be taken.</summary>
	public bool DifferentNames { get; init; }

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
							Subtype = Subtype,
							DifferentNames = DifferentNames,
							MaxChoices = Math.Max(0, UpTo.Value(s, InputContext)),
							Prompt =
								$"Search for {(UpTo == 1 ? "a" : $"up to {UpTo.Value(s, InputContext)}")} {(Supertype == Supertype.None ? "" : Supertype.ToString().ToLowerInvariant() + " ")}{(Subtype is null ? "" : Subtype + " ")}{(Type == CardType.None ? "" : Type.ToString().ToLowerInvariant() + " ")}card{(UpTo == 1 ? "" : "s")}{(DifferentNames ? " with different names" : "")}",
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
	/// <summary>Null Summoner: "Exile that card" instead — linked to this permanent (ExiledBy).</summary>
	public bool ExileInstead { get; init; }

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
				Prompt = ExileInstead
					? "Choose a card to exile"
					: "Choose a card for them to discard",
			};
			if (!choice.GetOptions(s, InputContext).IsEmpty)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps =
						[
							choice,
							ExileInstead
								? new ExileChosenLinkedAction()
								: new MoveChosenToGraveyardAction { PlayerId = player },
						],
					}
				);
		}
		return new(s);
	}
}

/// <summary>The chosen cards to exile, each linked to this ability's source as it is now (CR 607.2a).</summary>
public record ExileChosenLinkedAction : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var sourceId = GetInput<int>(ContextKeys.Source);
		// The same object the ability came from, or never castable — left and came back is a new one (CR 400.7).
		var incarnation =
			Ref.Source.Resolve(s, InputContext).Any() && s.IsOnBattlefield(sourceId)
				? s.GetCard(sourceId).Incarnation
				: -1;
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext).ToList())
		{
			var card = s.GetCard(id);
			s = s.MoveCard(id, s.ZoneId(card.OwnerId, ZoneType.Exile));
			s = Update(s, (Card)s.GetCard(id).WithComponent(new ExiledBy(sourceId, incarnation)));
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
	/// <summary>Seasoned Cryomancer: "When you discard one or more nonland cards this way" — run after, reading the discarded.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s))
		{
			var n = Math.Min(Count, s.CardsIn(player, ZoneType.Hand).Count());
			if (n > 0)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
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
							.. Then,
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
