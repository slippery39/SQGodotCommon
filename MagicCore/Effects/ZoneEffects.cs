using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Effects;

// Effects that move cards between zones: destroy, exile, sacrifice, return, discard, surveil, tokens.

/// <summary>"Destroy [What]" — to its owner's graveyard, unless indestructible (CR 701.7).</summary>
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
public record DiscardAtRandom(Ref Who, Count N) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		{
			var hand = s.CardsIn(player, ZoneType.Hand).Select(c => c.Id).ToArray();
			(var rng, s) = s.TakeRng();
			rng.Shuffle(hand);
			s = s.Discard(player, [.. hand.Take(N.Value(s, InputContext))]);
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
	/// <summary>U.S.S. Voyager: "until you reveal two land cards. Put those land cards …" — what it found, if the library ran out first.</summary>
	public int HowMany { get; init; } = 1;

	/// <summary>U.S.S. Voyager: "onto the battlefield tapped".</summary>
	public bool Tapped { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var library = s.CardsIn(Controller, ZoneType.Library).ToList();
		var hits = library.Where(c => c.Is(Types)).Take(HowMany).Select(c => c.Id).ToList();
		var revealed =
			hits.Count < HowMany ? library.Count : library.FindIndex(c => c.Id == hits[^1]) + 1;
		var rest = library.Take(revealed).Select(c => c.Id).Except(hits).ToArray();
		foreach (var hit in hits)
			s = s.EnterBattlefield(hit, Controller, tapped: Tapped);
		(var rng, s) = s.TakeRng();
		rng.Shuffle(rest);
		var libraryId = s.ZoneId(Controller, ZoneType.Library);
		// Out and back in: MoveObject appends, and the end is the bottom. Not marked known (story AI-3):
		// their ORDER is random, and a miss sends the whole library down — known, its real order would
		// survive "shuffle what isn't known". ponytail: so they are shuffled with the rest, losing "they
		// are at the bottom"; keep them at the bottom, shuffled among themselves, if that ever matters.
		foreach (var id in rest)
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

/// <summary>"Exile [What]" — permanents, to their owners' exile.</summary>
public record Exile(Ref What) : Effect
{
	/// <summary>Overwrite the Multiverse: "… where X is the number of creatures exiled this way" — run after, with X.</summary>
	public ImmutableList<GameAction> ThenWithXExiled { get; init; } = [];

	/// <summary>
	/// The Measure of a Man: "I — Exile up to one target …" whose III returns "the exiled card" — linked
	/// to this object of the source (ExiledBy, CR 607.2a); ReturnLinkedExile brings it back.
	/// </summary>
	public bool Linked { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var exiled = Permanents(What, s).ToList();
		s = exiled.Aggregate(s, (st, c) => st.MoveCard(c.Id, st.ZoneId(c.OwnerId, ZoneType.Exile)));
		// Linked to the object the ability came from — one that left and came back never finds it (CR 607.2a).
		if (Linked && Ref.Source.Resolve(s, InputContext).Any() && s.IsOnBattlefield(Source(s).Id))
			s = exiled.Aggregate(
				s,
				(st, c) =>
					Update(
						st,
						(Card)
							st.GetCard(c.Id)
								.WithComponent(new ExiledBy(Source(st).Id, Source(st).Incarnation))
					)
			);
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
		// "Look at the top N": the ones kept stay where their owner now knows they are (story AI-3).
		foreach (var card in s.CardsIn(Controller, ZoneType.Library).Take(n).ToList())
			s = s.MakeKnown(card.Id, Knowers.Owner);
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
	/// <summary>Chandra, Chill of Compliance: "a noncreature, nonland card" — none of these types.</summary>
	public CardType ExceptTypes { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var max = Max.Value(s, InputContext);
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			if (
				s.IsIn(id, Controller, ZoneType.Graveyard)
				&& s.GetCard(id).ManaCost.ManaValue <= max
				&& !s.GetCard(id).Is(ExceptTypes)
			)
				s = s.MoveCard(id, s.ZoneId(Controller, ZoneType.Hand));
		return new(s);
	}
}

/// <summary>"Create N [Token] tokens" — THE way a token is made: entering through EnterBattlefield (its "enters" fires), Draconic Visitor's replacement applied.</summary>
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

	/// <summary>L'Rell, Fierce Visionary: "Sacrifice it at the beginning of the next end step." — set once it has entered.</summary>
	public bool SacrificeAtNextEndStep { get; init; }

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
			if (SacrificeAtNextEndStep)
				s = ReturnFromExileUnderYourControl.Delay(
					s,
					token.Id,
					ReturnFromExileUnderYourControl.SacrificeItAtTheEndStep
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

	/// <summary>Splinter Twin: "Exile that token at the beginning of the next end step." — set once it has entered.</summary>
	public bool ExileAtNextEndStep { get; init; }

	/// <summary>Gray and Adira: "Sacrifice it at the beginning of the next end step."</summary>
	public bool SacrificeAtNextEndStep { get; init; }

	/// <summary>Gray and Adira: "except it isn't legendary".</summary>
	public bool NotLegendary { get; init; }

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
					Supertypes = NotLegendary
						? copy.Supertypes & ~Supertype.Legendary
						: copy.Supertypes,
				}
			) with
			{
				IsToken = true,
			};
			(s, copy) = s.AddObject(copy);
			s = s.EnterBattlefield(copy.Id, Controller);
			// after entering: a zone change strips a delayed trigger, entering included
			if (ExileAtNextEndStep)
				s = ReturnFromExileUnderYourControl.Delay(
					s,
					copy.Id,
					ReturnFromExileUnderYourControl.ExileItAtTheEndStep
				);
			if (SacrificeAtNextEndStep)
				s = ReturnFromExileUnderYourControl.Delay(
					s,
					copy.Id,
					ReturnFromExileUnderYourControl.SacrificeItAtTheEndStep
				);
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

	/// <summary>"Exile it at the beginning of the next end step" — CreateTokenCopy's too.</summary>
	internal static readonly DelayedTrigger ExileItAtTheEndStep =
		new(new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new Exile(Ref.Source)]));

	/// <summary>"Sacrifice it at the beginning of the next end step" — L'Rell's token, Gray and Adira's copy.</summary>
	internal static readonly DelayedTrigger SacrificeItAtTheEndStep =
		new(new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new Sacrifice(Ref.Source)]));

	/// <summary>A delayed trigger on a permanent that has just entered — after it, as entering strips one given before.</summary>
	internal static GameState Delay(GameState s, int id, DelayedTrigger trigger) =>
		Update(s, (Card)s.GetCard(id).WithComponent(trigger));

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
				s = Delay(s, id, ExileItAtTheEndStep);
		}
		return new(s);
	}
}

/// <summary>
/// The Measure of a Man: "III — Return the exiled card to the battlefield under its owner's control." —
/// every card this object of the source exiled (Exile.Linked); nothing once the source left (CR 607.2a).
/// </summary>
public record ReturnLinkedExile : Effect
{
	public override ActionResult Execute(GameState s)
	{
		if (!Ref.Source.Resolve(s, InputContext).Any() || !s.IsOnBattlefield(Source(s).Id))
			return new(s);
		var source = Source(s);
		foreach (
			var card in new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Exile))
				.Where(c =>
					c.GetComponent<ExiledBy>() is { } by
					&& by.SourceId == source.Id
					&& by.SourceIncarnation == source.Incarnation
				)
				.ToList()
		)
			s = s.EnterBattlefield(card.Id, card.OwnerId);
		return new(s);
	}
}

/// <summary>
/// Temporal Conqueror: "Then return a land card from your graveyard to your hand. If you can't, create a
/// Treasure token." — chosen, not targeted; Otherwise runs only when there is none.
/// </summary>
public record ReturnFromYourGraveyard(Filters.Filter Filter) : Effect
{
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var choice = new ChooseAmongChoice
		{
			PlayerId = Controller,
			Ids = [.. s.CardsIn(Controller, ZoneType.Graveyard).Select(c => c.Id)],
			Filter = Filter,
			MinChoices = 1,
			Prompt = "Put one into your hand",
		};
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps = choice.GetOptions(s, InputContext).IsEmpty
						? Otherwise
						: [choice, new MoveChosenToAction(Controller, ZoneType.Hand)],
				}
			)
		);
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

/// <summary>
/// Chandra, Torch of Defiance: "Exile the top card of your library. You may cast that card. If you
/// don't, [Otherwise]." Cast right then (CastNow), paying its cost. Asked only when it could be cast —
/// a land, or one you can't pay for or target with, is "don't".
/// </summary>
public record ExileTopMayCast(ImmutableList<GameAction> Otherwise) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var top = s.CardsIn(Controller, ZoneType.Library).FirstOrDefault();
		if (top is not null)
			s = s.MoveCard(top.Id, s.ZoneId(Controller, ZoneType.Exile));
		var castable =
			top is not null && CastNow.CanCast(s, Controller, s.GetCard(top.Id), free: false);
		if (!castable)
			return new(
				s.SpawnAction(
					new PipelineAction { Steps = Otherwise, PipelineContext = InputContext }
				)
			);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
					Steps =
					[
						new YesNoChoice(Controller, $"Cast {top!.Name}?"),
						new IfChosenAction([new CastNow(top.Id, Free: false)])
						{
							Otherwise = Otherwise,
						},
					],
				}
			)
		);
	}
}

/// <summary>
/// "You may cast that card" in the middle of a resolution (CR 608.2g) — Chandra, Torch of Defiance;
/// Uldaros' free copies. A real CastSpellAction (DuringResolution): the engine's own cast candidates,
/// so modes, targets, X, convoke and additional costs are all chosen and checked by ValidateAdd, the
/// one legality — then the player picks one, and it goes on the stack as cast.
/// </summary>
public record CastNow(int CardId, bool Free) : Effect
{
	/// <summary>Every legal way to cast it now — free means X is 0 and no mana cost.</summary>
	public static ImmutableList<CastSpellAction> Options(
		GameState s,
		int playerId,
		int cardId,
		bool free
	) =>
		!s.HasObject(cardId) || s.GetObject(cardId) is not Card card || card.Is(CardType.Land)
			? []
			:
			[
				.. s.DistinctMoves(
						MagicRules
							.CastCandidates(s, playerId, card)
							.OfType<CastSpellAction>()
							.Where(c => !free || c.X == 0)
							.Select(c =>
								c with
								{
									DuringResolution = true,
									WithoutPayingManaCost = free,
								}
							)
					)
					.OfType<CastSpellAction>()
					.Where(c => c.ValidateAdd(s).IsValid),
			];

	public static bool CanCast(GameState s, int playerId, Card card, bool free) =>
		!Options(s, playerId, card.Id, free).IsEmpty;

	public override ActionResult Execute(GameState s)
	{
		var options = Options(s, Controller, CardId, Free);
		return options.Count switch
		{
			0 => new(s),
			1 => new(options[0].Cast(s)),
			_ => new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCastChoice.Chosen),
						Steps =
						[
							new ChooseCastChoice(Controller, options),
							new CastChosenOption(options),
						],
					}
				)
			),
		};
	}
}

/// <summary>After the choice: that way of casting it, if it is still legal.</summary>
public record CastChosenOption(ImmutableList<CastSpellAction> Options) : Effect
{
	public override ActionResult Execute(GameState s) =>
		InputContext.GetValueOrDefault(ChooseCastChoice.Chosen) is int id
		&& Options[id - 1] is var cast
		&& cast.ValidateAdd(s).IsValid
			? new(cast.Cast(s))
			: new(s);
}

/// <summary>
/// Uldaros Theorix: "exile up to one target nonland card of each card type from your graveyard. Copy
/// those cards. You may cast any number of spells with total mana value [Budget] or less from among
/// the copies without paying their mana costs." ponytail: the cards are chosen as it resolves, not
/// targeted as it triggers — one per type with no card twice is a word the targeting can't say.
/// </summary>
public record ExileOneOfEachTypeAndCastCopies(int Budget) : Effect
{
	static readonly CardType[] CardTypes =
	[
		CardType.Artifact,
		CardType.Creature,
		CardType.Enchantment,
		CardType.Instant,
		CardType.Sorcery,
		CardType.Planeswalker,
		CardType.Kindred,
		CardType.Battle,
	];

	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						.. CardTypes.Select(t => new ExileOneOfTypeFromGraveyard(t)),
						new CastCopiesWithinBudget(Budget, []),
					],
				}
			)
		);
}

/// <summary>Up to one nonland card of this type from your graveyard, exiled and linked to the source.</summary>
public record ExileOneOfTypeFromGraveyard(CardType Type) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		ImmutableList<int> ids =
		[
			.. s.CardsIn(Controller, ZoneType.Graveyard)
				.Where(c => c.Is(Type) && !c.Is(CardType.Land))
				.Select(c => c.Id),
		];
		return ids.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps =
						[
							new ChooseAmongChoice
							{
								PlayerId = Controller,
								Ids = ids,
								Types = Type,
								Prompt =
									$"Exile up to one {Type.ToString().ToLowerInvariant()} card",
							},
							new ExileChosenLinkedAction(),
						],
					}
				)
			);
	}
}

/// <summary>
/// One copy at a time, while the mana value left allows: choose one of the cards this exiled, cast a
/// copy of it free, then choose again. Declining — or nothing left that fits — ends it, and the
/// link is cleared so a later Uldaros can't offer these again.
/// </summary>
public record CastCopiesWithinBudget(int Budget, ImmutableList<int> Done) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var sourceId = GetInput<int>(ContextKeys.Source);
		var linked = s.CardsIn(Controller, ZoneType.Exile)
			.Where(c => c.GetComponent<ExiledBy>()?.SourceId == sourceId)
			.ToList();
		ImmutableList<int> fits =
		[
			.. linked
				.Where(c =>
					!Done.Contains(c.Id)
					&& c.ManaCost.ManaValue <= Budget
					&& CastNow.CanCast(s, Controller, c, free: true)
				)
				.Select(c => c.Id),
		];
		if (fits.IsEmpty)
			return new(Unlink(s, linked));
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
							Ids = fits,
							Zone = ZoneType.Exile,
							Prompt = $"Cast a copy free ({Budget} mana value left)",
						},
						new CastChosenCopy(Budget, Done),
					],
				}
			)
		);
	}

	public static GameState Unlink(GameState s, IEnumerable<Card> cards) =>
		cards.Aggregate(
			s,
			(st, c) => st.UpdateObject(c.Id, st.GetCard(c.Id).WithoutComponents<ExiledBy>())
		);
}

/// <summary>The chosen card's copy, cast free — then the next choice, with what is left.</summary>
public record CastChosenCopy(int Budget, ImmutableList<int> Done) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var sourceId = GetInput<int>(ContextKeys.Source);
		if (
			ChooseCardsFromHandChoice.Read(InputContext).FirstOrDefault()
			is not (var chosen and not 0)
		)
			return new(
				CastCopiesWithinBudget.Unlink(
					s,
					s.CardsIn(Controller, ZoneType.Exile)
						.Where(c => c.GetComponent<ExiledBy>()?.SourceId == sourceId)
				)
			);
		var original = s.GetCard(chosen);
		Card copy;
		(s, copy) = s.AddObject(
			new Card
			{
				OwnerId = Controller,
				ControllerId = Controller,
				IsCopy = true,
			}.WithCopiableValuesOf(original),
			s.ZoneId(Controller, ZoneType.Exile)
		);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps =
					[
						new CastNow(copy.Id, Free: true),
						new CastCopiesWithinBudget(
							Budget - original.ManaCost.ManaValue,
							Done.Add(chosen)
						),
					],
				}
			)
		);
	}
}

/// <summary>Emrakul: "Sacrifice three permanents" — the payer chooses which.</summary>
public record SacrificePermanents(int N) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
					Steps =
					[
						new ChoosePermanentChoice(
							Controller,
							GetInput<int>(ContextKeys.Source),
							null,
							required: true
						)
						{
							MinChoices = N,
							MaxChoices = N,
							Prompt = $"Sacrifice {N} permanents",
						},
						new SacrificeChosenAction(),
					],
				}
			)
		);
}

/// <summary>
/// Emrakul, the Exigent Doom: "Target land gains '{T}: Add {C}{C}' until this card is cast from
/// exile. You may cast this card for as long as it remains exiled." — after the cost exiled it.
/// </summary>
public record LandTapsForTwoUntilThisIsCast(Ref Land) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var sourceId = GetInput<int>(ContextKeys.Source);
		if (
			s.GetObject(sourceId) is not Card exiled
			|| !s.IsIn(sourceId, exiled.OwnerId, ZoneType.Exile)
		)
			return new(s);
		s = Update(s, (Card)exiled.WithComponent(new PlayableFromExile(Controller, int.MaxValue)));
		foreach (var land in Permanents(Land, s).ToList())
			s = Update(
				s,
				(Card)
					land.WithComponentReplaced(
						new TapsForTwoColorlessWhileExiled(sourceId, exiled.Incarnation)
					)
			);
		return new(s);
	}
}

/// <summary>Jace, Reality Sculptor: "Exile all but the bottom card of each opponent's library."</summary>
public record ExileAllButBottom(Ref Who) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		foreach (var card in s.CardsIn(player, ZoneType.Library).SkipLast(1).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(player, ZoneType.Exile));
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

	/// <summary>Sphinx's Approach: "a Sphinx creature card".</summary>
	public string? Subtype { get; init; }

	/// <summary>Fblthp: "with different names". Unset, two Forests may both be taken.</summary>
	public bool DifferentNames { get; init; }

	/// <summary>Eye of Ugin: "a colorless creature card" — what Type, Supertype and Subtype don't say.</summary>
	public Filters.Filter? Filter { get; init; }

	/// <summary>The Filter in words, for the prompt — Eye of Ugin: "colorless".</summary>
	public string? FilterText { get; init; }

	/// <summary>"Reveal it" — landcycling, Fblthp, Pia, Loyal Tutor: the found card is known to everyone where it goes.</summary>
	public bool Reveal { get; init; }

	/// <summary>70,000 Light-Years from Home: "Its controller may search their library" — who searches; unset is you.</summary>
	public Ref? Who { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var searcher = Who is null ? Controller : All(Who, s).FirstOrDefault();
		return searcher == 0 ? new(s) : new(Search(s, searcher));
	}

	GameState Search(GameState s, int searcher) =>
		s.SpawnAction(
			new PipelineAction
			{
				PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
				Steps =
				[
					new ChooseFromLibraryChoice
					{
						PlayerId = searcher,
						Type = Type,
						Supertype = Supertype,
						Subtype = Subtype,
						Filter = Filter,
						DifferentNames = DifferentNames,
						MaxChoices = Math.Max(0, UpTo.Value(s, InputContext)),
						Prompt =
							$"Search for {(UpTo == 1 ? "a" : $"up to {UpTo.Value(s, InputContext)}")} {(Supertype == Supertype.None ? "" : Supertype.ToString().ToLowerInvariant() + " ")}{(FilterText is null ? "" : FilterText + " ")}{(Subtype is null ? "" : Subtype + " ")}{(Type == CardType.None ? "" : Type.ToString().ToLowerInvariant() + " ")}card{(UpTo == 1 ? "" : "s")}{(DifferentNames ? " with different names" : "")}",
					},
					// Loyal Tutor: "then shuffle and put that card on top" — the shuffle comes first.
					.. To == ZoneType.Library
						? (GameAction[])
							[
								new ShuffleLibraryAction(searcher),
								new MoveChosenToAction(searcher, ZoneType.Library) { OnTop = true },
							]
						:
						[
							new MoveChosenToAction(searcher, To) { Tapped = Tapped },
							new ShuffleLibraryAction(searcher),
						],
					.. Reveal ? (GameAction[])[new RevealChosenAction()] : [],
				],
			}
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
	/// <summary>Kirk, Commanding Officer: "You may play that card this turn." — not until the end of your next.</summary>
	public bool ThisTurn { get; init; }

	/// <summary>Reckless Impulse: "Exile the top two cards of your library."</summary>
	public int N { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		foreach (var top in s.CardsIn(Controller, ZoneType.Library).Take(N).ToList())
		{
			s = s.MoveCard(top.Id, s.ZoneId(Controller, ZoneType.Exile));
			s = Update(
				s,
				(Card)
					s.GetCard(top.Id)
						.WithComponent(
							new PlayableFromExile(
								Controller,
								ThisTurn
									? s.GetGame().TurnNumber
									: Playable.EndOfNextTurnOf(s, Controller)
							)
						)
			);
		}
		return new(s);
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
/// V'Ger, the Intruder: "Look at target opponent's hand" — every card in it becomes known to its owner's
/// opponent (Card.KnownTo); the table logs what it now knows (TBL-6 will show it in the hand).
/// </summary>
public record LookAtHand(Ref Who) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer).ToList())
		foreach (var card in s.CardsIn(player, ZoneType.Hand).ToList())
			s = s.MakeKnown(card.Id, Knowers.Opponent);
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
			foreach (var card in s.CardsIn(player, ZoneType.Hand).ToList()) // "reveals their hand"
				s = s.MakeKnown(card.Id, Knowers.Everyone);
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

/// <summary>
/// Rutherford, Upgraded Ensign: "look at the top six cards of your library. You may reveal an artifact
/// card from among them and put it into your hand. Put the rest on the bottom of your library in a
/// random order." Hoshi Sato's "put one of them into your hand" is Must; Pattern Buffer Preservation's
/// "… and the other into your graveyard" is RestToGraveyard. A revealed card is known to everyone; the
/// rest go down as Identity Echo's do — a random order is not known (magic-mechanics.md).
/// ponytail: Spock's "in any order" is a random order too (Shayne, 2026-10-31; DesignNotes).
/// </summary>
public record LookAndTake(Count N, Filters.Filter? Filter = null) : Effect
{
	/// <summary>"You may reveal [it] and put it into your hand" — known to everyone there.</summary>
	public bool Reveal { get; init; }

	/// <summary>Hoshi Sato: "Put one of them into your hand" — not "you may".</summary>
	public bool Must { get; init; }

	/// <summary>Pattern Buffer Preservation: "the other into your graveyard", not the bottom.</summary>
	public bool RestToGraveyard { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var top = s.CardsIn(Controller, ZoneType.Library)
			.Take(N.Value(s, InputContext))
			.Select(c => c.Id)
			.ToImmutableList();
		var choice = new ChooseAmongChoice
		{
			PlayerId = Controller,
			Ids = top,
			Zone = ZoneType.Library,
			Filter = Filter,
			Prompt = Must ? "Put one into your hand" : "You may put one into your hand",
			MinChoices = Must ? 1 : 0,
		};
		var put = new PutLookedAtAction(Controller, top, Reveal, RestToGraveyard);
		var context = InputContext.Remove(ChooseCardsFromHandChoice.Chosen);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = context,
					// Nothing to take: no prompt with no options, but the rest still go.
					Steps = choice.GetOptions(s, context).IsEmpty ? [put] : [choice, put],
				}
			)
		);
	}
}

/// <summary>After LookAndTake's choice: the chosen card into the hand, the rest to the bottom in a random order (or the graveyard).</summary>
public record PutLookedAtAction(
	int PlayerId,
	ImmutableList<int> Looked,
	bool Reveal,
	bool RestToGraveyard
) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var taken = ChooseCardsFromHandChoice.Read(InputContext).ToHashSet();
		foreach (var id in taken)
		{
			s = s.MoveCard(id, s.ZoneId(PlayerId, ZoneType.Hand));
			if (Reveal)
				s = s.MakeKnown(id, Knowers.Everyone);
		}
		var rest = Looked
			.Where(id => !taken.Contains(id) && s.IsIn(id, PlayerId, ZoneType.Library))
			.ToArray();
		if (RestToGraveyard)
			return new(
				rest.Aggregate(
					s,
					(st, id) => st.MoveCard(id, st.ZoneId(PlayerId, ZoneType.Graveyard))
				)
			);
		(var rng, s) = s.TakeRng();
		rng.Shuffle(rest);
		var libraryId = s.ZoneId(PlayerId, ZoneType.Library);
		// Out and back in, as RevealUntilOntoBattlefield does: MoveObject appends, and the end is the bottom.
		foreach (var id in rest)
			s = s.MoveObject(id, 0).MoveObject(id, libraryId);
		return new(s);
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
			if (n == 0 && !Then.IsEmpty) // Garruk: an empty hand "didn't discard two nonland cards"
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps = Then,
					}
				);
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
	/// <summary>Those Old Scientists: "Choose up to two target cards in graveyards. The owners of those cards shuffle them into their libraries."</summary>
	public bool FromGraveyards { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var cards = FromGraveyards
			? All(What, s)
				.Where(id => s.GetObject(id) is Card c && s.IsIn(id, c.OwnerId, ZoneType.Graveyard))
				.Select(s.GetCard)
			: Permanents(What, s);
		foreach (var card in cards.ToList())
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
public record MaySacrifice(Filters.Filter Filter, ImmutableList<GameAction> Then) : Effect
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
public record EachSacrifices(Ref Who, Filters.Filter Filter) : Effect
{
	/// <summary>Garruk, Veiled Butcher: "If you sacrificed a creature this way, [IfYouDid]."</summary>
	public ImmutableList<GameAction> IfYouDid { get; init; } = [];

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
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
						Steps =
						[
							choice,
							new SacrificeChosenAction(),
							.. player == Controller && !IfYouDid.IsEmpty
								? [new IfChosenAction(IfYouDid)]
								: (GameAction[])[],
						],
					}
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

/// <summary>
/// Extrapolate the Impossible: "You may reveal exactly two cards you own with different names from
/// outside the game. An opponent chooses one of them. You put that card into your hand." Outside the
/// game is the sideboard (MagicRules.WithSideboard); with fewer than two names there, it is not asked.
/// </summary>
public record RevealTwoFromOutsideTheGame : Effect
{
	/// <summary>Set once "you may" was answered yes: now the two cards are chosen.</summary>
	public bool Revealing { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var outside = s.CardsIn(Controller, ZoneType.Sideboard).ToList();
		if (outside.Select(c => c.Name).Distinct().Count() < 2)
			return new(s);
		var context = InputContext
			.Remove(ChoosePermanentChoice.Chosen)
			.Remove(ChooseCardsFromHandChoice.Chosen);
		GameAction[] steps = Revealing
			?
			[
				new ChooseAmongChoice
				{
					PlayerId = Controller,
					Ids = [.. outside.Select(c => c.Id)],
					Zone = ZoneType.Sideboard,
					DifferentNames = true,
					MinChoices = 2,
					MaxChoices = 2,
					Prompt = "Reveal two cards with different names from outside the game",
				},
				new RevealChosenAction(),
				new OpponentChoosesOneAction(Controller),
			]
			:
			[
				new YesNoChoice(Controller, "Reveal two cards from outside the game?"),
				new IfChosenAction([this with { Revealing = true }]),
			];
		return new(
			s.SpawnAction(new PipelineAction { PipelineContext = context, Steps = [.. steps] })
		);
	}
}

/// <summary>Extrapolate the Impossible: "An opponent chooses one of them. You put that card into your hand."</summary>
public record OpponentChoosesOneAction(int PlayerId) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		var revealed = ChooseCardsFromHandChoice.Read(InputContext);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
					Steps =
					[
						new ChooseAmongChoice
						{
							PlayerId = PlayerId,
							DeciderId = s.OpponentOf(PlayerId),
							Ids = [.. revealed],
							Zone = ZoneType.Sideboard,
							MinChoices = 1,
							MaxChoices = 1,
							Prompt = "Choose the card they put into their hand",
						},
						new MoveChosenToAction(PlayerId, ZoneType.Hand),
					],
				}
			)
		);
	}
}
