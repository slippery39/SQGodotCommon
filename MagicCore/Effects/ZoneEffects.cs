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
	/// <summary>
	/// Decree of Pain: "Destroy all creatures. … Draw a card for each creature destroyed this way." — run
	/// after, with Ref.ThisWay the ones that left (indestructible ones and McCoy's saves stayed).
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var them = Permanents(What, s).ToList();
		s = All(s, them);
		if (Then.IsEmpty)
			return new(s);
		var gone = them.Select(c => c.Id).Where(id => !s.IsOnBattlefield(id)).ToImmutableArray();
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.SetItem(ContextKeys.ThisWay, gone),
					Steps = Then,
				}
			)
		);
	}

	/// <summary>CR 701.7: each through MagicState.Destroy — not one that is indestructible.</summary>
	public static GameState All(GameState s, IEnumerable<Card> permanents) =>
		s.Destroy([.. permanents.Select(c => c.Id)]);
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
				.SelectMany(id => s.Subtypes(s.GetCard(id)))
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
			Destroy.All(s, Permanents(Ref.AllCreatures, s).Where(c => !s.HasSubtype(c, chosen)))
		);
	}
}

/// <summary>
/// Flickering Hound: "exile [it], then return that card to the battlefield under its owner's
/// control" — a new object (CR 400.7). A token exiled this way ceases to exist (CR 111.7).
/// </summary>
public record Flicker(Ref What) : Effect
{
	/// <summary>
	/// Ghost of Illyria: "Return that card to the battlefield under its owner's control at the
	/// beginning of the next end step." — a delayed trigger on the exiled card (Triggers.Collect reads
	/// it there); exiled during the end step, it waits for the next turn's.
	/// </summary>
	public bool AtNextEndStep { get; init; }

	/// <summary>
	/// Phelia, Exuberant Shepherd: "… return that card to the battlefield under its owner's control. If it entered
	/// under your control, put a +1/+1 counter on Phelia." — run by the delayed return (AtNextEndStep), after it:
	/// Ref.ThisWay is the card as it entered, Ref.EventObject the object this ability came from.
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		if (!Then.IsEmpty && !AtNextEndStep)
			throw new NotSupportedException("Flicker.Then is the delayed return's (AtNextEndStep)");
		var returnIt = !AtNextEndStep
			? null
			: new DelayedTrigger(
				new TriggeredAbilityComponent(
					TriggerEvent.EachEndStep,
					[
						new MoveTo(Ref.Source, ZoneType.Battlefield)
						{
							From = [ZoneType.Exile],
							Then = Then,
						},
					]
				)
			)
			{
				ControllerId = Controller, // CR 603.7d: yours
				// the object this ability came from, not whatever has its id now (CR 400.7)
				MadeBy = Then.IsEmpty
					? null
					: GetInput<Card?>(ContextKeys.SourceSnapshot) ?? Source(s),
			};
		foreach (var card in Permanents(What, s).ToList())
		{
			s = s.MoveCard(card.Id, s.ZoneId(card.OwnerId, ZoneType.Exile));
			if (!card.IsToken && s.IsIn(card.Id, card.OwnerId, ZoneType.Exile))
				s = returnIt is not null
					? MoveTo.Delay(s, card.Id, returnIt)
					: s.EnterBattlefield(card.Id, card.OwnerId);
		}
		return new(s);
	}
}

/// <summary>
/// Rules of Acquisition: "Gain control of target nonland permanent an opponent controls" (To: you);
/// "Target opponent gains control of target nonland permanent you control" (To: that player) — for
/// good, through MagicState.GainControl.
/// </summary>
public record GainControl(Ref What, Ref To) : Effect
{
	/// <summary>
	/// Worf, Intelligence Contractor; The Wrath of Khan: "gain control of [it] until end of turn" — its
	/// controller before is kept on it (UntilEndOfTurn.ReturnControlTo) and given it back in cleanup.
	/// </summary>
	public bool UntilEndOfTurn { get; init; }

	public override ActionResult Execute(GameState s)
	{
		if (
			All(To, s).FirstOrDefault(id => s.GetObject(id) is MagicPlayer)
			is not (> 0 and var player)
		)
			return new(s);
		foreach (var c in Permanents(What, s).ToList())
		{
			s = s.GainControl(c.Id, player);
			if (UntilEndOfTurn && c.ControllerId != player)
				s = Update(
					s,
					(Card)
						s.GetCard(c.Id)
							.WithComponent(
								new Cards.UntilEndOfTurn { ReturnControlTo = c.ControllerId }
							)
				);
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

	/// <summary>Branching Timeline: "Reveal the top X cards of your library … Put all permanent cards revealed this way onto the battlefield" — every [Types] card among the top X, not "until".</summary>
	public Count? Top { get; init; }

	/// <summary>Will Riker, First in the Field: "Sacrifice those lands at the beginning of your next end step." — a delayed trigger on each.</summary>
	public bool SacrificeAtNextEndStep { get; init; }

	/// <summary>Deep Space 9: "exile the top four cards of your library. Put all permanent cards exiled this way onto the battlefield." — the rest stay exiled, not to the bottom.</summary>
	public bool Exile { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var library = s.CardsIn(Controller, ZoneType.Library).ToList();
		var top = Top is { } t ? Math.Clamp(t.Value(s, InputContext), 0, library.Count) : -1;
		var hits = (top >= 0 ? library.Take(top) : library)
			.Where(c => c.Is(Types))
			.Take(top >= 0 ? int.MaxValue : HowMany)
			.Select(c => c.Id)
			.ToList();
		var revealed =
			top >= 0 ? top
			: hits.Count < HowMany ? library.Count
			: library.FindIndex(c => c.Id == hits[^1]) + 1;
		var rest = library.Take(revealed).Select(c => c.Id).Except(hits).ToArray();
		foreach (var hit in hits)
		{
			s = s.EnterBattlefield(hit, Controller, tapped: Tapped);
			if (SacrificeAtNextEndStep)
				s = MoveTo.Delay(s, hit, MoveTo.SacrificeItAtTheEndStep);
		}
		// ponytail: Deep Space 9's permanents go library -> battlefield, never through exile; a card that
		// watches cards leaving exile would miss them. Move them via exile if one appears.
		if (Exile)
		{
			foreach (var id in rest)
				s = s.MoveCard(id, s.ZoneId(Controller, ZoneType.Exile));
			return new(s);
		}
		// Not known (story AI-3): their ORDER is random, and a miss sends the whole library down — known,
		// its real order would survive "shuffle what isn't known". ponytail: so they are shuffled with the
		// rest, losing "they are at the bottom"; keep them at the bottom, shuffled among themselves, if that
		// ever matters.
		return new(s.ToBottomAtRandom(Controller, rest));
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
	/// to this object of the source (ExiledBy, CR 607.2a); MoveTo(Ref.All(Exile, Is.ExiledBySource)) brings it back.
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
/// CR 701.25: look at the top N, put any of them into the graveyard, the rest back on top in any order —
/// a second choice when two or more are kept (TBL-9.2, Shayne 2026-11-10: option A, both players).
/// </summary>
public record Surveil(int N) : Effect
{
	/// <summary>Riders on the cards put into the graveyard "this way" — they read the surveil's choice.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	/// <summary>
	/// Where the chosen go. Library is a scry (CR 701.22, Preordain: "look at the top two cards of your library, then put
	/// any number of them on the bottom and the rest on top in any order") — the bottom, and no surveil: no
	/// SurveilledEvent, no tally. ponytail: the bottom ones go in the order chosen, never asked (CR 701.22a: any order);
	/// ask it as the top's order is if a card ever reads the bottom.
	/// </summary>
	public ZoneType To { get; init; } = ZoneType.Graveyard;

	public override ActionResult Execute(GameState s)
	{
		if (To == ZoneType.Graveyard)
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
			To == ZoneType.Graveyard
				? new MoveChosenToGraveyardAction { PlayerId = Controller }
				: new MoveChosenToAction(Controller, To),
			new OrderKeptOnTopAction { PlayerId = Controller, Looked = n },
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
/// put that card into your hand" (Is.ManaValueAtMost(N)); Chandra, Chill of Compliance: "a noncreature,
/// nonland card". A surveil's Then.
/// </summary>
public record ReturnChosenToHandIf(Filters.Filter What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var asking = new Filters.FilterContext(Controller, Actions.SourceOf.Context(InputContext));
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			if (
				s.IsIn(id, Controller, ZoneType.Graveyard) && What.Matches(s, s.GetCard(id), asking)
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

	/// <summary>
	/// Skyclave Apparition: "an X/X blue Illusion creature token, where X is the mana value of the exiled card" —
	/// the tokens' power and toughness, counted once as it resolves.
	/// </summary>
	public Count? Size { get; init; }

	/// <summary>L'Rell, Fierce Visionary: "Sacrifice it at the beginning of the next end step." — set once it has entered.</summary>
	public bool SacrificeAtNextEndStep { get; init; }

	/// <summary>
	/// Voice of Victory's mobilize: "create two tapped and attacking 1/1 red Warrior creature tokens" — attacking
	/// as they enter (EnterBattlefield), never declared, so no "whenever … attacks" (CR 508.4); the opponent, as Kira's.
	/// </summary>
	public bool Attacking { get; init; }

	/// <summary>Gwyndala, the Progeny: "create … Neuroflux Heirloom … Attach it to Gwyndala." — once it has entered, if that is still there.</summary>
	public Ref? AttachTo { get; init; }

	/// <summary>
	/// Cori-Steel Cutter: "create a 1/1 white Monk creature token with prowess. You may attach this Equipment to it." —
	/// run after, with Ref.ThisWay the tokens created (as Destroy.Then, Decree of Pain).
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var made = ImmutableArray.CreateBuilder<int>();
		var counters = Counters.Value(s, InputContext);
		var controller = ControlledBy is null ? Controller : All(ControlledBy, s).FirstOrDefault();
		if (controller == 0)
			return new(s);
		var count = Replacements.Apply( // Elspeth, Storm Slayer: "twice that many of those tokens"
			s,
			ReplacedEvent.Tokens,
			s.GetPlayer(controller),
			Count.Value(s, InputContext)
		);
		var printed = CardFactory.Create(new(Token, Abilities), controller);
		if (Size?.Value(s, InputContext) is { } size)
			printed = printed with { BasePower = size, BaseToughness = size };
		var template = Instead(s, controller, printed);
		for (var i = 0; i < count; i++)
		{
			var token = template with { IsToken = true };
			(s, token) = s.AddObject(token);
			made.Add(token.Id);
			s = s.EnterBattlefield(
				token.Id,
				controller,
				extraCounters: counters,
				tapped: Tapped,
				attacking: Attacking
			);
			if (GainsUntilEndOfTurn is { } keyword)
				s = Update(
					s,
					(Card)s.GetCard(token.Id).WithComponent(new UntilEndOfTurn(Keyword: keyword))
				);
			if (
				AttachTo is not null
				&& s.IsOnBattlefield(token.Id)
				&& Permanents(AttachTo, s).FirstOrDefault() is { } host
			)
				s = Attachments.Attach(s, token.Id, host.Id);
			if (SacrificeAtNextEndStep)
				s = MoveTo.Delay(s, token.Id, MoveTo.SacrificeItAtTheEndStep);
		}
		return Then.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.SetItem(
							ContextKeys.ThisWay,
							made.ToImmutable()
						),
						Steps = Then,
					}
				)
			);
	}

	/// <summary>
	/// The token actually created — Draconic Visitor: an artifact token under its controller's control
	/// is a Dragon instead (CR 614.1a); Holo-Engineer: any token is an Illusion. Every token goes
	/// through here, copies included.
	/// </summary>
	public static Card Instead(GameState s, int controller, Card token) =>
		s.CardsIn(controller, ZoneType.Battlefield)
			.SelectMany(s.AbilitiesOf<ArtifactTokensBecome>)
			// ponytail: the first on the battlefield wins — with Draconic Visitor AND Holo-Engineer the
			// controller should choose which applies (CR 616.1). A choice if a deck ever runs both.
			.FirstOrDefault(r => r.AnyToken || token.Is(CardType.Artifact))
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

	/// <summary>Blastoneuron: "exile target creature card … from your graveyard. Create a token that's a copy of it." — a card in exile, not a permanent.</summary>
	public bool OfAnExiledCard { get; init; }

	/// <summary>Replicator: "Create a tapped token that's a copy of target token you control."</summary>
	public bool Tapped { get; init; }

	/// <summary>Squad (Sicarian Infiltrator): "create that many tokens that are copies of it" — how many of each; unset is one.</summary>
	public Count? N { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var n = N?.Value(s, InputContext) ?? 1;
		if (n <= 0)
			return new(s);
		var originals = OfAnExiledCard
			? All(What, s)
				.Where(id => s.GetObject(id) is Card c && s.IsIn(id, c.OwnerId, ZoneType.Exile))
				.Select(s.GetCard)
				.ToList()
			: Permanents(What, s).ToList();
		if (
			originals.Count == 0
			&& What.Kind == RefKind.Source
			&& GetInput<Card?>(ContextKeys.SourceSnapshot) is { } lastKnown
		)
			originals.Add(lastKnown);
		var each = Replacements.Apply(s, ReplacedEvent.Tokens, s.GetPlayer(Controller), n); // Elspeth: twice that many
		foreach (var original in originals.SelectMany(o => Enumerable.Repeat(o, each)))
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
			s = s.EnterBattlefield(copy.Id, Controller, tapped: Tapped);
			// after entering: a zone change strips a delayed trigger, entering included
			if (ExileAtNextEndStep)
				s = MoveTo.Delay(s, copy.Id, MoveTo.ExileItAtTheEndStep);
			if (SacrificeAtNextEndStep)
				s = MoveTo.Delay(s, copy.Id, MoveTo.SacrificeItAtTheEndStep);
		}
		return new(s);
	}
}

/// <summary>
/// MC-6.12: THE move of the cards a Ref names to a zone of their OWNER's (CR 400.3) — "return this card from your
/// graveyard to the battlefield" (Darklight Phoenix), "return target creature to its owner's hand" (Unsummon), "put
/// target nonland permanent on the bottom of its owner's library" (Paradox Shaper), "its owner shuffles it into their
/// library" (Fblthp), "return it to the battlefield tapped under your control. Exile it at the beginning of the next end
/// step" (Vindictive Triumph), "return the exiled card" (The Measure of a Man: <c>Ref.All(Exile, Is.ExiledBySource)</c>).
/// Onto the battlefield through EnterBattlefield, under its owner unless <see cref="UnderYourControl"/>. A token that
/// has left the battlefield moves nowhere (CR 111.8).
/// </summary>
public record MoveTo(Ref What, ZoneType To) : Effect
{
	/// <summary>
	/// Where it must be now — "from your graveyard" is [Graveyard] (its owner's), Unsummon's "spell or creature"
	/// [Battlefield, Stack] (anyone's), Talon Gates' "from your hand" [Hand]. Empty: wherever it is (Paradox Shaper).
	/// </summary>
	public ImmutableList<ZoneType> From { get; init; } = [];

	/// <summary>Gallia, Tragic Host: "to the battlefield tapped".</summary>
	public bool Tapped { get; init; }

	/// <summary>Generous Revival: "with an additional +1/+1 counter on it" — it enters with it.</summary>
	public int Counters { get; init; }

	/// <summary>Grim Repriser, Proctor: "with a finality counter on it".</summary>
	public bool Finality { get; init; }

	/// <summary>Vindictive Triumph: "under your control" — unset is its owner's (Ghost of Illyria, The Measure of a Man).</summary>
	public bool UnderYourControl { get; init; }

	/// <summary>Vindictive Triumph: "Exile it at the beginning of the next end step" — a delayed trigger on it.</summary>
	public bool ExileAtNextEndStep { get; init; }

	/// <summary>Clash of Elements: "on top of its owner's library" — unset is the bottom.</summary>
	public bool OnTop { get; init; }

	/// <summary>
	/// How far below the top — Oust: "into its owner's library second from the top" (1), Unexpectedly Absent: "just
	/// beneath the top X cards of that library" (Count.X; 0 is the top). A library with fewer cards: the bottom.
	/// </summary>
	public Count? Beneath { get; init; }

	/// <summary>Fblthp, Those Old Scientists: "shuffles it into their library" — its owner shuffles, once per card.</summary>
	public bool Shuffle { get; init; }

	/// <summary>
	/// Enduring Innocence: "return it to the battlefield under its owner's control. It's an enchantment. (It's not a
	/// creature.)" — the types it isn't, for as long as it stays: EnterBattlefield stamps it as it enters, so no
	/// "whenever a creature enters" and no Containment Priest sees a creature.
	/// </summary>
	public CardType Loses { get; init; }

	/// <summary>
	/// Phelia, Exuberant Shepherd: "If it entered under your control, …" — run after, with Ref.ThisWay the cards
	/// that moved (as Destroy.Then).
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	/// <summary>"Exile it at the beginning of the next end step" — CreateTokenCopy's too.</summary>
	internal static readonly DelayedTrigger ExileItAtTheEndStep =
		new(new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new Exile(Ref.Source)]));

	/// <summary>"Sacrifice it at the beginning of the next end step" — L'Rell's token, Gray and Adira's copy.</summary>
	internal static readonly DelayedTrigger SacrificeItAtTheEndStep =
		new(new TriggeredAbilityComponent(TriggerEvent.EachEndStep, [new Sacrifice(Ref.Source)]));

	/// <summary>A delayed trigger on a permanent that has just entered — after it, as entering strips one given before.</summary>
	internal static GameState Delay(GameState s, int id, DelayedTrigger trigger) =>
		Update(s, (Card)s.GetCard(id).WithComponent(trigger));

	bool IsWhereItMustBe(GameState s, Card card) =>
		From.IsEmpty
		|| From.Any(zone =>
			zone switch
			{
				ZoneType.Battlefield => s.IsOnBattlefield(card.Id),
				ZoneType.Stack => s.GetParent(card.Id) == s.GetWellKnownId(MagicKeys.Stack),
				_ => s.IsIn(card.Id, card.OwnerId, zone),
			}
		);

	public override ActionResult Execute(GameState s)
	{
		var moved = ImmutableArray.CreateBuilder<int>();
		foreach (var id in All(What, s).ToList())
		{
			if (
				s.GetObject(id) is not Card card
				|| !IsWhereItMustBe(s, card)
				|| (card.IsToken && !s.IsOnBattlefield(id))
			)
				continue;
			if (To == ZoneType.Battlefield)
			{
				s = s.EnterBattlefield(
					id,
					UnderYourControl ? Controller : card.OwnerId,
					extraCounters: Counters,
					tapped: Tapped,
					finality: Finality,
					loses: Loses
				);
				if (!s.IsOnBattlefield(id))
					continue; // Containment Priest exiled it instead
				moved.Add(id);
				if (ExileAtNextEndStep)
					s = Delay(s, id, ExileItAtTheEndStep);
				continue;
			}
			moved.Add(id);
			var zone = s.ZoneId(card.OwnerId, To);
			var above = s.GetChildrenIds(zone)
				.Where(other => other != id)
				.Take(Beneath?.Value(s, InputContext) ?? 0)
				.Reverse()
				.ToList();
			s = s.MoveCard(id, zone, toTop: OnTop || Beneath is not null);
			foreach (var top in above) // back on top, in their order — a reorder: each as known as it was
				s = s.MoveObjectToFront(top, zone);
			if (Shuffle && To == ZoneType.Library)
				s = s.Shuffle(card.OwnerId);
		}
		return Then.IsEmpty
			? new(s)
			: new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.SetItem(
							ContextKeys.ThisWay,
							moved.ToImmutable()
						),
						Steps = Then,
					}
				)
			);
	}
}

/// <summary>
/// MC-6.12: a CHOSEN move, not targeted — "return a land card from your graveyard to your hand. If you can't, create a
/// Treasure token" (Temporal Conqueror), "exile a card from your graveyard" (Sylvia Tilly), "you may put a land card
/// from your hand onto the battlefield. If you do, …" and "return a land you control to its owner's hand" (Erica
/// Ortegas). The controller picks one of their From zone's cards the filter matches (null: any), then MoveTo. Each zone
/// keeps its own choice: a graveyard's is ChooseAmongChoice, the battlefield's ChoosePermanentChoice, the hand's
/// ChooseCardsFromHandChoice.
/// </summary>
public record MoveChosen(ZoneType From, Filters.Filter? Filter, ZoneType To) : Effect
{
	/// <summary>Erica Ortegas: "you MAY put" — none is an answer, and it is asked even with nothing to pick.</summary>
	public bool May { get; init; }

	/// <summary>Erica Ortegas: "If you do, …" — after a pick.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	/// <summary>Temporal Conqueror: "If you can't, create a Treasure token." — nothing to pick; with May, "if you don't".</summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	/// <summary>The question on the table; unset says where it goes.</summary>
	public string? Prompt { get; init; }

	/// <summary>Brainstorm: "put two cards from your hand on top of your library in any order" — two of these, the second on top.</summary>
	public bool OnTop { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var prompt =
			Prompt
			?? To switch
			{
				ZoneType.Hand when From == ZoneType.Battlefield => "Return to its owner's hand",
				ZoneType.Hand => "Put one into your hand",
				ZoneType.Exile => "Exile one",
				ZoneType.Battlefield => "Put one onto the battlefield",
				_ => $"Put one into your {To.ToString().ToLowerInvariant()}",
			};
		ChoiceAction choice = From switch
		{
			ZoneType.Graveyard => new ChooseAmongChoice
			{
				PlayerId = Controller,
				Ids = [.. s.CardsIn(Controller, ZoneType.Graveyard).Select(c => c.Id)],
				Filter = Filter,
				MinChoices = May ? 0 : 1,
				Prompt = prompt,
			},
			ZoneType.Battlefield => new ChoosePermanentChoice(
				Controller,
				GetInput<int>(ContextKeys.Source),
				Filter,
				required: !May
			)
			{
				Prompt = prompt,
				OutputKey = ChooseCardsFromHandChoice.Chosen, // Ref.Chosen reads one key
			},
			ZoneType.Hand => new ChooseCardsFromHandChoice
			{
				PlayerId = Controller,
				Filter = Filter,
				MinChoices = May ? 0 : 1,
				MaxChoices = 1,
				Prompt = prompt,
			},
			_ => throw new NotSupportedException($"a chosen move from {From}"),
		};
		var context = InputContext.Remove(ChooseCardsFromHandChoice.Chosen);
		if (!May && choice.GetOptions(s, InputContext).IsEmpty)
			return Otherwise.IsEmpty
				? new(s)
				: new(
					s.SpawnAction(
						new PipelineAction { PipelineContext = context, Steps = Otherwise }
					)
				);
		ImmutableList<GameAction> steps =
		[
			choice,
			new MoveTo(Ref.Chosen, To) { From = [From], OnTop = OnTop },
		];
		if (!Then.IsEmpty || (May && !Otherwise.IsEmpty))
			steps = steps.Add(
				new IfChosenAction(Then)
				{
					Key = ChooseCardsFromHandChoice.Chosen,
					Otherwise = May ? Otherwise : [],
				}
			);
		return new(s.SpawnAction(new PipelineAction { PipelineContext = context, Steps = steps }));
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
/// "You may cast one of [these cards]" as it resolves — one chosen among those that can be cast now (CastNow), or
/// none. Brazen Bluff: "you may cast a noncreature spell from your hand without paying its mana cost"
/// (Ref.All(Hand, !Is.Creature, Ref.You)); Bilbo, Thief in the Night: "you may cast an artifact, instant, or sorcery
/// spell from your graveyard" (paid); Malcolm, Alluring Scoundrel: "you may cast the discarded card without paying
/// its mana cost" (Ref.Chosen, in Discard.Then).
/// </summary>
public record CastOneOf(Ref Among) : Effect
{
	/// <summary>"Without paying its mana cost" — X is 0. Unset, the spell is paid for (Bilbo).</summary>
	public bool Free { get; init; } = true;

	/// <summary>
	/// Bilbo: "If an instant or sorcery spell cast this way would be put into your graveyard, exile it instead" — the
	/// spells this filter matches are exiled leaving the stack, as flashback's (CastSpellAction.ExiledLeavingTheStack).
	/// </summary>
	public Filters.Filter? Exiled { get; init; }

	public override ActionResult Execute(GameState s)
	{
		ImmutableList<int> castable =
		[
			.. All(Among, s)
				.Where(id =>
					s.HasObject(id)
					&& s.GetObject(id) is Card c
					&& CastNow.CanCast(s, Controller, c, free: Free)
				),
		];
		return castable.IsEmpty
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
								Ids = castable,
								Zone = (
									(Zone)s.GetObject(s.GetParent(castable[0])!.Value)
								).ZoneType,
								Prompt = Free ? "You may cast one free" : "You may cast one",
							},
							new CastChosen { Free = Free, Exiled = Exiled },
						],
					}
				)
			);
	}
}

/// <summary>
/// After CastOneOf's choice: the chosen card cast (CastNow), if one was chosen — Bilbo, Thief in the Night: "you may
/// cast an artifact, instant, or sorcery spell from your graveyard", paid.
/// </summary>
public record CastChosen : Effect
{
	/// <summary>Malcolm, Alluring Scoundrel: "without paying its mana cost".</summary>
	public bool Free { get; init; }

	/// <summary>Bilbo: "If an instant or sorcery spell cast this way would be put into your graveyard, exile it instead".</summary>
	public Filters.Filter? Exiled { get; init; }

	public override ActionResult Execute(GameState s) =>
		ChooseCardsFromHandChoice.Read(InputContext).FirstOrDefault() is > 0 and var id
			? new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext,
						Steps =
						[
							new CastNow(id, Free)
							{
								ExiledLeavingTheStack =
									Exiled?.Matches(
										s,
										s.GetCard(id),
										new(Controller, GetInput<int>(ContextKeys.Source))
									) ?? false,
							},
						],
					}
				)
			)
			: new(s);
}

/// <summary>
/// Assimilate (Death Is Irrelevant, Borg Cube): "Put it onto the battlefield under your control with a
/// +1/+1 counter. It's a Borg artifact creature and loses all other creature types." — no duration, so
/// until it leaves: the artifact type stamped with an Animated that has no Aura (Card.WithoutAnimation
/// undoes it as it leaves and for a copy, CR 707.2), the creature type an UntilEndOfTurn that never ends.
/// A card, from wherever it is (a graveyard, exile).
/// </summary>
public record Assimilate(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			All(What, s)
				.Where(id => s.GetObject(id) is Card && !s.IsOnBattlefield(id))
				.ToList()
				.Aggregate(s, (st, id) => Into(st, Controller, id))
		);

	public static GameState Into(GameState s, int playerId, int cardId)
	{
		s = s.EnterBattlefield(cardId, playerId);
		if (!s.IsOnBattlefield(cardId))
			return s;
		s = s.AddPlusOneCounters(cardId, 1);
		var card = s.GetCard(cardId);
		return s.UpdateObject(
			cardId,
			(Card)
				card.WithTypesUntilItLeaves(card.Types | CardType.Artifact)
					.WithComponent(
						new Cards.UntilEndOfTurn
						{
							BecomesCreatureTypes = ["Borg"],
							UntilTurnOf = Cards.UntilEndOfTurn.Never,
						}
					)
		);
	}
}

/// <summary>
/// Borg Cube: "assimilate a creature card exiled with it. If you can't, exile up to one target
/// creature." — the cards this object of the source exiled (ExiledBy, CR 607.2a), one chosen if several.
/// </summary>
public record AssimilateExiledWithSource(ImmutableList<GameAction> IfYouCant) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		ImmutableList<int> linked =
		[
			.. Ref.All(ZoneType.Exile, Filters.Is.Creature & Filters.Is.ExiledBySource)
				.Resolve(s, InputContext),
		];
		if (linked.IsEmpty)
			return new(
				s.SpawnAction(
					new PipelineAction { Steps = IfYouCant, PipelineContext = InputContext }
				)
			);
		if (linked.Count == 1)
			return new(Assimilate.Into(s, Controller, linked[0]));
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
							Ids = linked,
							Zone = ZoneType.Exile,
							MinChoices = 1,
							Prompt = "Assimilate a creature card exiled with it",
						},
						new AssimilateChosen(),
					],
				}
			)
		);
	}
}

/// <summary>Borg Cube, after the choice: the chosen card assimilated.</summary>
public record AssimilateChosen : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			ChooseCardsFromHandChoice
				.Read(InputContext)
				.Where(id => s.HasObject(id) && !s.IsOnBattlefield(id))
				.Aggregate(s, (st, id) => Assimilate.Into(st, Controller, id))
		);
}

/// <summary>
/// "You may cast that card" in the middle of a resolution (CR 608.2g) — Chandra, Torch of Defiance;
/// Uldaros' free copies. A real CastSpellAction (DuringResolution): the engine's own cast candidates,
/// so modes, targets, X, convoke and additional costs are all chosen and checked by ValidateAdd, the
/// one legality — then the player picks one, and it goes on the stack as cast.
/// </summary>
public record CastNow(int CardId, bool Free) : Effect
{
	/// <summary>Bilbo's instants and sorceries: exiled leaving the stack (CastSpellAction.ExiledLeavingTheStack).</summary>
	public bool ExiledLeavingTheStack { get; init; }

	/// <summary>Every legal way to cast it now — free means X is 0 and no mana cost.</summary>
	public static ImmutableList<CastSpellAction> Options(
		GameState s,
		int playerId,
		int cardId,
		bool free,
		bool exiled = false
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
									ExiledLeavingTheStack = exiled,
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
		var options = Options(s, Controller, CardId, Free, ExiledLeavingTheStack);
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
		&& id >= 1
		&& id <= Options.Count // past the last is "Don't cast it"
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
	/// <summary>Captain Benjamin Sisko: "You may cast any number of spells … from among those cards" — the cards themselves, not copies.</summary>
	public bool Originals { get; init; }

	/// <summary>Sisko: "spells with mana value less than or equal to [N]" — a cap on each, not a total spent.</summary>
	public bool EachAtMost { get; init; }

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
							Prompt = EachAtMost
								? $"Cast one free (mana value {Budget} or less)"
								: $"Cast a copy free ({Budget} mana value left)",
						},
						new CastChosenCopy(this),
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

/// <summary>The chosen card's copy (Sisko: the card itself), cast free — then the loop's next choice, with what is left.</summary>
public record CastChosenCopy(CastCopiesWithinBudget Loop) : Effect
{
	/// <summary>A copy of a card (CR 707.12), yours, in your exile — to be cast at once; one not cast ceases to exist.</summary>
	public static (GameState, Card) MakeCopy(GameState s, int playerId, Card original) =>
		s.AddObject(
			new Card
			{
				OwnerId = playerId,
				ControllerId = playerId,
				IsCopy = true,
			}.WithCopiableValuesOf(original),
			s.ZoneId(playerId, ZoneType.Exile)
		);

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
		var next = Loop with
		{
			Budget = Loop.EachAtMost ? Loop.Budget : Loop.Budget - original.ManaCost.ManaValue,
			Done = Loop.Done.Add(chosen),
		};
		if (Loop.Originals)
			return new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext,
						Steps = [new CastNow(chosen, Free: true), next],
					}
				)
			);
		Card copy;
		(s, copy) = MakeCopy(s, Controller, original);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext,
					Steps = [new CastNow(copy.Id, Free: true), next],
				}
			)
		);
	}
}

/// <summary>
/// Infinite Diversity: "Copy that card X times … You may cast the copies without paying their mana
/// costs." — one copy at a time, each asked; X is counted once, as it resolves. A copy not cast ceases to
/// exist (CR 704.5e).
/// </summary>
public record CastCopiesOf(Ref What, Count Times) : Effect
{
	/// <summary>Q, Stacking the Deck: "Create a copy of this card. You may cast the copy." — paying its costs.</summary>
	public bool PayCost { get; init; }

	public override ActionResult Execute(GameState s) =>
		All(What, s).FirstOrDefault(id => s.GetObject(id) is Card) is > 0 and var id
			? new(
				s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext,
						Steps =
						[
							new CastCopyOf(id, Times.Value(s, InputContext)) { Free = !PayCost },
						],
					}
				)
			)
			: new(s);
}

/// <summary>One of Infinite Diversity's copies — "cast it free?" — then the next, while any are left.</summary>
public record CastCopyOf(int CardId, int Left) : Effect
{
	/// <summary>Unset: Q's copy, cast paying its costs.</summary>
	public bool Free { get; init; } = true;

	public override ActionResult Execute(GameState s)
	{
		if (Left <= 0 || !s.HasObject(CardId))
			return new(s);
		Card copy;
		(s, copy) = CastChosenCopy.MakeCopy(s, Controller, s.GetCard(CardId));
		ImmutableList<GameAction> next = [this with { Left = Left - 1 }];
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
					Steps = CastNow.CanCast(s, Controller, copy, free: Free)
						?
						[
							new YesNoChoice(
								Controller,
								Free
									? $"Cast a copy of {copy.Name} free? ({Left} left)"
									: $"Cast a copy of {copy.Name}?"
							),
							new IfChosenAction([new CastNow(copy.Id, Free: Free)]),
							.. next,
						]
						: next,
				}
			)
		);
	}
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
		// Bruvac: "If an opponent would mill one or more cards, they mill twice that many" — every mill comes here.
		n = Rules.Replacements.Apply(s, ReplacedEvent.Mill, s.GetPlayer(playerId), n);
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

	/// <summary>
	/// Thada Adel, Acquisitor: "search that player's library for an artifact card and exile it. Then that player
	/// shuffles." — whose library; unset is the searcher's.
	/// </summary>
	public Ref? Of { get; init; }

	/// <summary>Thada Adel: "Until end of turn, you may play that card." — { ThisTurn = true }; To must be Exile.</summary>
	public PlayPermission? Play { get; init; }

	/// <summary>Captain Jean-Luc Picard: "put it onto the battlefield with seven charge counters on it".</summary>
	public int ChargeCounters { get; init; }

	/// <summary>
	/// Spellseeker: "you MAY search your library for … then shuffle" — a search that takes no card was declined, so
	/// nothing is shuffled and a top card its owner knows stays known. Unset (Mystical Tutor, landcycling), the
	/// library is shuffled whatever was found (CR 701.19).
	/// ponytail: a player can't search, take nothing and shuffle anyway on purpose (to be rid of a known top) —
	/// ask "shuffle?" after a "may" search that took nothing if that is ever missed (DesignNotes).
	/// </summary>
	public bool May { get; init; }

	/// <summary>
	/// Transmute Artifact: "search your library for an artifact card. If that card's mana value is less than or equal
	/// to the sacrificed artifact's mana value, put it onto the battlefield. If it's greater, you may pay {X} … If you
	/// don't, put it into its owner's graveyard. Then shuffle." — where the found card goes, decided by these steps:
	/// run with it in Ref.Chosen, still in the library, IN PLACE of putting it To; then the shuffle.
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var searcher = Who is null ? Controller : All(Who, s).FirstOrDefault();
		var owner = Of is null ? searcher : All(Of, s).FirstOrDefault();
		return searcher == 0 || owner == 0 ? new(s) : new(Search(s, searcher, owner));
	}

	GameState Search(GameState s, int searcher, int owner)
	{
		GameAction shuffle = new ShuffleLibrary(Ref.Id(owner));
		if (May)
			shuffle = new IfChosenAction([shuffle]) { Key = ChooseCardsFromHandChoice.Chosen };
		return s.SpawnAction(
			new PipelineAction
			{
				PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
				Steps =
				[
					new ChooseFromLibraryChoice
					{
						PlayerId = owner,
						DeciderId = searcher,
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
					.. !Then.IsEmpty
						?
						[
							new IfChosenAction(Then) { Key = ChooseCardsFromHandChoice.Chosen },
							shuffle,
						]
					: To == ZoneType.Library
						? (GameAction[])
							[
								shuffle,
								new MoveChosenToAction(owner, ZoneType.Library) { OnTop = true },
							]
					:
					[
						// Onto the battlefield under the searcher's control; anywhere else, the owner's zone.
						new MoveChosenToAction(To == ZoneType.Battlefield ? searcher : owner, To)
						{
							Tapped = Tapped,
							ChargeCounters = ChargeCounters,
							Playable = Play is { } play
								? play.For(
									s,
									play.By is { } by ? All(by, s).FirstOrDefault() : Controller
								)
								: null,
						},
						shuffle,
					],
					.. Reveal ? (GameAction[])[new RevealChosenAction()] : [],
				],
			}
		);
	}
}

/// <summary>
/// Ponder: "You may shuffle." Timetwister: "Each player shuffles their hand and graveyard into their library" —
/// after the cards are moved. Each player named shuffles their library, once, with or without anything put into
/// it; every position anyone knew is forgotten (MagicState.Shuffle). A search's "then shuffle" is this too.
/// </summary>
public record ShuffleLibrary(Ref Who) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(
			All(Who, s)
				.Where(id => s.GetObject(id) is MagicPlayer)
				.Aggregate(s, (st, player) => st.Shuffle(player))
		);
}

/// <summary>
/// Explore (CR 701.44) — Captain Kathryn Janeway: "that creature explores. (Reveal the top card of your
/// library. Put that card into your hand if it's a land. Otherwise, put a +1/+1 counter on the creature,
/// then put the card back or put it into your graveyard.)" — "your" is the creature's controller; an empty
/// library reveals nothing and the counter still goes on. ponytail: a creature already gone explores not at
/// all (the rules still reveal, CR 701.44c); copy its controller from last-known if a card needs it. And
/// several exploring in one Explore would read the same top card (each "bin it?" runs after) — TRK explores
/// one creature at a time; chain them one by one if a card explores a group.
/// </summary>
public record Explore(Ref What) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var creature in Permanents(What, s).ToList())
		{
			var player = creature.ControllerId;
			var top = s.CardsIn(player, ZoneType.Library).FirstOrDefault();
			if (top is not null)
				s = s.MakeKnown(top.Id, Knowers.Everyone);
			if (top is not null && top.Is(CardType.Land))
			{
				s = s.MoveCard(top.Id, s.ZoneId(player, ZoneType.Hand));
				continue;
			}
			s = s.PutCounters(creature.Id, CounterKinds.PlusOne, 1);
			if (top is not null)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext
							.Remove(ChoosePermanentChoice.Chosen)
							.SetItem(ContextKeys.Controller, player),
						Steps =
						[
							new YesNoChoice(player, $"Put {top.Name} into your graveyard?"),
							new IfChosenAction(
								[
									new MoveTo(Ref.Id(top.Id), ZoneType.Graveyard)
									{
										From = [ZoneType.Library],
									},
								]
							),
						],
					}
				);
		}
		return new(s);
	}
}

/// <summary>Improvised Act: "You may discard a card. If you do, [Then]."</summary>
public record MayDiscard(ImmutableList<GameAction> Then) : Effect
{
	/// <summary>The Wrath of Khan, chapter II: "You may exile a card from your hand. If you do, draw a card."</summary>
	public bool Exile { get; init; }

	/// <summary>All Good Things..., chapters I–III: "then exile a card from your hand" — not optional.</summary>
	public bool Must { get; init; }

	/// <summary>All Good Things...: "all cards exiled with this Saga" — the exiled card linked to this object of the source (ExiledBy, CR 607.2a).</summary>
	public bool Linked { get; init; }

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
								MinChoices = Must ? 1 : 0,
								MaxChoices = 1,
								Prompt = (Must, Exile) switch
								{
									(true, true) => "Exile a card from your hand",
									(true, false) => "Discard a card",
									(false, true) => "You may exile a card from your hand",
									_ => "You may discard a card",
								},
							},
							!Exile ? new MoveChosenToGraveyardAction { PlayerId = Controller }
							: Linked ? new ExileChosenLinkedAction()
							: new MoveChosenToAction(Controller, ZoneType.Exile),
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

/// <summary>
/// MC-6.9: "You may play [cast] that card" — THE permission an effect gives the cards it exiles: who, how long, free or
/// paid. Unset is you, until the end of your next turn (Blazing Crescendo) — a PlayableFromExile on the card, read by
/// Rules/Playable.cs. <see cref="Now"/> is a cast during the resolution instead (CastNow): Chandra, The Caretaker,
/// discover, Sisko. ExileTopPlayable.Play and SearchLibrary.Play (Thada Adel).
/// </summary>
public record PlayPermission
{
	/// <summary>Kirk, Commanding Officer: "You may play that card this turn."; Thada Adel: "Until end of turn".</summary>
	public bool ThisTurn { get; init; }

	/// <summary>Vulcan Mind-Meld, Chekov: "for as long as it remains exiled".</summary>
	public bool Forever { get; init; }

	/// <summary>Vulcan Mind-Meld: "the player who exiled it may play it" — who may play them; unset is you.</summary>
	public Ref? By { get; init; }

	/// <summary>Vulcan Mind-Meld: "Mana of any type can be spent to cast spells this way."</summary>
	public bool AnyType { get; init; }

	/// <summary>
	/// Chandra, Torch of Defiance: "You may cast that card." — now, as this resolves (a yes/no, then CastNow), asked only
	/// when it could be cast; never a permission left on the card.
	/// </summary>
	public bool Now { get; init; }

	/// <summary>The Caretaker, discover, Sisko: "without paying its mana cost".</summary>
	public bool Free { get; init; }

	/// <summary>
	/// Sisko: "You may cast any number of spells … from among those cards" — chosen one at a time
	/// (CastCopiesWithinBudget on the cards themselves, linked to the source while it runs).
	/// </summary>
	public bool AnyNumber { get; init; }

	/// <summary>
	/// Sisko: "with mana value less than or equal to the number of Officers you control" — a cap on each, counted once
	/// as it resolves (CR 608.2h), so a cast's own cost can't move it.
	/// </summary>
	public Count? ManaValueAtMost { get; init; }

	/// <summary>Chandra: "If you don't, [Otherwise]." — also when there was nothing to cast.</summary>
	public ImmutableList<GameAction> Otherwise { get; init; } = [];

	/// <summary>Discover: "Cast it without paying its mana cost or put it into your hand."</summary>
	public bool OrIntoHand { get; init; }

	/// <summary>
	/// Inti, Seneschal of the Sun: "You may play that card until your next end step." — this turn's when it is yours and
	/// its end step hasn't begun, else your next turn's. ponytail: through that whole turn, as ThisTurn is — so the card
	/// is still playable in that end step and cleanup; end it as the end step begins if a card ever cares.
	/// </summary>
	public bool UntilYourNextEndStep { get; init; }

	/// <summary>Robber of the Rich: "During any turn you attacked with a Rogue, you may cast that card" — while it holds.</summary>
	public Condition? If { get; init; }

	/// <summary>Robber of the Rich: "you may CAST that card" — a land exiled this way is never played.</summary>
	public bool CastOnly { get; init; }

	/// <summary>The marker for the player <paramref name="who"/> (By, resolved by the effect).</summary>
	public PlayableFromExile For(GameState s, int who) =>
		new(
			who,
			Forever ? int.MaxValue
				: ThisTurn ? s.GetGame().TurnNumber
				: UntilYourNextEndStep
				&& s.GetGame().ActivePlayerId == who
				&& s.GetGame().Step < Step.End
					? s.GetGame().TurnNumber
				: Playable.EndOfNextTurnOf(s, who)
		)
		{
			AnyType = AnyType,
			If = If,
			CastOnly = CastOnly,
			Free = Free,
		};
}

/// <summary>
/// MC-6.9: THE "exile these, they may be played" effect. Blazing Crescendo: "Exile the top card of your library. Until
/// the end of your next turn, you may play that card." What is exiled: the top N (or <see cref="Until"/> one matches),
/// of whose library, face down, shuffled first, the rest to the bottom; what may be played, by whom and how long:
/// <see cref="Play"/> — a permission left on the card, or a cast now (Chandra, The Caretaker, discover, Sisko).
/// </summary>
public record ExileTopPlayable : Effect
{
	/// <summary>Who may play them, how long, free or now (PlayPermission) — Kirk, Commanding Officer: { ThisTurn = true }.</summary>
	public PlayPermission Play { get; init; } = new();

	/// <summary>Reckless Impulse: "Exile the top two cards of your library."; Ensign Pavel Chekov: "the top X cards".</summary>
	public Count N { get; init; } = 1;

	/// <summary>
	/// Exploratory Mission, The Caretaker: "exile cards from the top of your library until you exile a nonland card"
	/// (!Is.Land); discover N: "… a nonland card with mana value N or less". Only that card ("that card") may be played
	/// — the rest stay exiled, unplayable; none matching, the whole library is exiled.
	/// </summary>
	public Filters.Filter? Until { get; init; }

	/// <summary>Vulcan Mind-Meld: "the top two cards of their library"; Chekov: "each opponent exiles the top X cards of their library" — whose library; unset is yours.</summary>
	public Ref? Of { get; init; }

	/// <summary>Vulcan Mind-Meld: "looks at the top card …, then exiles it face down" — hidden from all but the player who may play it.</summary>
	public bool FaceDown { get; init; }

	/// <summary>
	/// Chekov: "choose a land card that player owns exiled this way. You may play the chosen cards" —
	/// only the one chosen (by you) becomes playable; the rest stay exiled.
	/// </summary>
	public Filters.Filter? Choose { get; init; }

	/// <summary>Q, Stacking the Deck: "shuffle your library, then exile cards …".</summary>
	public bool ShuffleFirst { get; init; }

	/// <summary>Discover: "Put the rest on the bottom in a random order."</summary>
	public bool RestOnBottom { get; init; }

	/// <summary>
	/// Q, Stacking the Deck: "That nonland card gains '[ability]'" — in place of any permission: an UntilEndOfTurn that
	/// never ends, so the card has it while it stays this exiled object.
	/// </summary>
	public ActivatedAbilityComponent? Gains { get; init; }

	/// <summary>
	/// Tersa Lightshatter: "exile a card at random from your graveyard" — the zone the cards come from (Graveyard); unset
	/// is the library's top.
	/// </summary>
	public ZoneType From { get; init; } = ZoneType.Library;

	/// <summary>Tersa Lightshatter: "a card at random" — the N taken in a random order (s.TakeRng), not from the top.</summary>
	public bool AtRandom { get; init; }

	/// <summary>
	/// Bonehoard Dracosaur: "If you exiled a land card this way, create … If you exiled a nonland card this way, create
	/// a Treasure token." — run after, with Ref.ThisWay the cards exiled.
	/// </summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var who = Play.By is { } by ? All(by, s).FirstOrDefault() : Controller;
		if (who == 0)
			return new(s);
		var sourceId = GetInput<int>(ContextKeys.Source);
		var incarnation = s.HasObject(sourceId) ? s.GetCard(sourceId).Incarnation : -1;
		var cap = Play.ManaValueAtMost?.Value(s, InputContext) ?? int.MaxValue;
		var playable = Play.For(s, who);
		var steps = ImmutableList<GameAction>.Empty;
		var done = ImmutableArray.CreateBuilder<int>();
		foreach (
			var owner in Of is { } of
				? All(of, s).Where(id => s.GetObject(id) is MagicPlayer).ToList()
				: [Controller]
		)
		{
			if (ShuffleFirst)
				s = s.Shuffle(owner);
			var library = s.CardsIn(owner, From).ToList();
			if (AtRandom)
			{
				var order = library.ToArray();
				(var shuffler, s) = s.TakeRng();
				shuffler.Shuffle(order);
				library = [.. order];
			}
			var context = new Filters.FilterContext(Controller, sourceId);
			var hit = Until is { } until
				? library.FirstOrDefault(c => until.Matches(s, c, context))
				: null;
			var exiled =
				Until is null ? library.Take(N.Value(s, InputContext)).ToList()
				: hit is null ? library
				: [.. library.TakeWhile(c => c.Id != hit.Id), hit];
			foreach (var top in exiled)
			{
				s = s.MoveCard(top.Id, s.ZoneId(owner, ZoneType.Exile));
				done.Add(top.Id);
				if (FaceDown) // known only to the one who looked (CR 406.3)
					s = Update(
						s,
						(Card)
							(
								s.GetCard(top.Id) with
								{
									KnownTo = who == owner ? Knowers.Owner : Knowers.Opponent,
								}
							).WithComponent(new ExiledFaceDown())
					);
				if (Play.AnyNumber)
					s = Update(
						s,
						(Card)s.GetCard(top.Id).WithComponent(new ExiledBy(sourceId, incarnation))
					);
				if (
					(Until is not null && top.Id != hit?.Id)
					|| Choose is not null
					|| Play.Now
					|| Play.AnyNumber // a cast now, too
					|| Gains is not null
				)
					continue;
				s = Update(
					s,
					(Card)
						s.GetCard(top.Id)
							.WithComponent(
								playable with
								{
									Blind =
										From == ZoneType.Library // a graveyard card was public (Tersa)
										&& !FaceDown
										&& who == owner
										&& !top.KnownTo.HasFlag(Knowers.Owner),
								}
							)
				);
			}
			if (RestOnBottom)
			{
				var rest = exiled.Where(c => c.Id != hit?.Id).Select(c => c.Id).ToArray();
				(var rng, s) = s.TakeRng();
				rng.Shuffle(rest);
				foreach (var id in rest)
					s = s.MoveCard(id, s.ZoneId(owner, ZoneType.Library)); // appended: the bottom
			}
			if (Gains is not null && hit is not null)
				s = Update(
					s,
					(Card)
						s.GetCard(hit.Id)
							.WithComponent(
								new UntilEndOfTurn
								{
									UntilTurnOf = UntilEndOfTurn.Never,
									Gains = [Gains],
								}
							)
				);
			if (Play.AnyNumber)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext,
						Steps =
						[
							new CastCopiesWithinBudget(cap, [])
							{
								Originals = true,
								EachAtMost = true,
							},
						],
					}
				);
			else if (Play.Now)
				s = CastThatCard(s, Until is null ? exiled.FirstOrDefault() : hit);
			var choice = new ChooseAmongChoice
			{
				PlayerId = owner,
				DeciderId = Controller,
				Zone = ZoneType.Exile,
				Ids = [.. exiled.Select(c => c.Id)],
				Filter = Choose,
				MinChoices = 1,
				Prompt = "Choose one you may play",
			};
			if (Choose is not null && !choice.GetOptions(s, InputContext).IsEmpty) // none of them a land: nothing to choose
				steps = steps.AddRange([choice, new MakeChosenPlayableAction(playable)]);
		}
		if (!Then.IsEmpty)
			s = s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = InputContext.SetItem(ContextKeys.ThisWay, done.ToImmutable()),
					Steps = Then,
				}
			);
		return steps.IsEmpty ? new(s) : new(s.SpawnAction(new PipelineAction { Steps = steps }));
	}

	/// <summary>
	/// "You may cast that card [without paying its mana cost]. If you don't, …" — a yes/no asked only when it could be
	/// cast, then CastNow. Declined, uncastable or nothing exiled: discover's "or put it into your hand", Chandra's damage.
	/// ponytail: "If you don't" runs on a NO to the up-front question only; backing out of the cast itself afterwards (an
	/// empty answer to ChooseCastChoice, TBL-2.16) casts nothing and skips it - make the decline answer the question again
	/// if a table player ever says yes and then backs out for the damage.
	/// </summary>
	GameState CastThatCard(GameState s, Card? that)
	{
		ImmutableList<GameAction> declined =
		[
			.. Play.OrIntoHand && that is not null
				? [new MoveTo(Ref.Id(that.Id), ZoneType.Hand) { From = [ZoneType.Exile] }]
				: Array.Empty<GameAction>(),
			.. Play.Otherwise,
		];
		if (that is null)
			return declined.IsEmpty
				? s
				: s.SpawnAction(
					new PipelineAction { Steps = declined, PipelineContext = InputContext }
				);
		if (!CastNow.CanCast(s, Controller, s.GetCard(that.Id), free: Play.Free))
			return s.SpawnAction(
				new PipelineAction { Steps = declined, PipelineContext = InputContext }
			);
		return s.SpawnAction(
			new PipelineAction
			{
				PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
				Steps =
				[
					new YesNoChoice(
						Controller,
						$"Cast {that.Name}{(Play.Free ? " free" : "")}?"
							+ (Play.OrIntoHand ? " (No: into your hand)" : "")
					),
					new IfChosenAction([new CastNow(that.Id, Free: Play.Free)])
					{
						Otherwise = declined,
					},
				],
			}
		);
	}
}

/// <summary>
/// Emry, Lurker of the Loch: "Choose target artifact card in your graveyard. You may cast that card this turn." — the
/// permission (PlayPermission's mark, PlayableFromExile) on cards that stay where they are: a graveyard or exile. Every
/// other PlayPermission rides an effect that exiles first (ExileTopPlayable, SearchLibrary); the nearest,
/// MakeChosenPlayableAction, is ExileTopPlayable's own step, a mark built there (Blind, Tax) on the cards it just exiled.
/// Cast from a graveyard by it is not flashback (Playable.MarkedInGraveyard) — it isn't exiled, and a permanent spell
/// enters. A card marked again REPLACES its mark: one left from an earlier turn would hide the new one.
/// </summary>
public record MakePlayable(Ref What, PlayPermission Play) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var who = Play.By is null ? Controller : All(Play.By, s).FirstOrDefault(Controller);
		foreach (var id in All(What, s).ToList())
			if (
				s.GetObject(id) is Card card
				&& (
					s.IsIn(id, card.OwnerId, ZoneType.Graveyard)
					|| s.IsIn(id, card.OwnerId, ZoneType.Exile)
				)
			)
				s = Update(s, (Card)card.WithComponentReplaced(Play.For(s, who)));
		return new(s);
	}
}

/// <summary>Chekov, after the choice: "You may play the chosen cards for as long as they remain exiled."</summary>
public record MakeChosenPlayableAction(PlayableFromExile Playable) : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChooseCardsFromHandChoice.Read(InputContext))
			if (s.GetObject(id) is Card card && s.IsIn(id, card.OwnerId, ZoneType.Exile))
				s = s.UpdateObject(id, (Card)card.WithComponent(Playable));
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

	/// <summary>Zero, Incorporeal Sensate: "You may exile a nonland card from it" — choosing none is allowed.</summary>
	public bool Optional { get; init; }

	/// <summary>
	/// Zero: "For as long as that card remains exiled, its owner may play it. A spell cast this way costs
	/// {2} more to cast." — exiled unlinked, playable by its owner with this tax (PlayableFromExile.Tax).
	/// </summary>
	public int? OwnerMayPlayTaxed { get; init; }

	/// <summary>Salt Vampire: "If you do, that player exiles that card, then draws a card." — run only when a card was chosen.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

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
				MinChoices = Optional ? 0 : 1,
				MaxChoices = 1,
				Prompt =
					ExileInstead || OwnerMayPlayTaxed is not null
						? "Choose a card to exile"
						: "Choose a card for them to discard",
			};
			if (!choice.GetOptions(s, InputContext).IsEmpty)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChooseCardsFromHandChoice.Chosen),
						Steps = OwnerMayPlayTaxed is { } tax
							?
							[
								choice,
								new MoveChosenToAction(player, ZoneType.Exile),
								new MakeChosenPlayableAction(
									new PlayableFromExile(player, int.MaxValue) { Tax = tax }
								),
								.. IfChosen(),
							]
							:
							[
								choice,
								ExileInstead
									? new ExileChosenLinkedAction()
									: new MoveChosenToGraveyardAction { PlayerId = player },
								.. IfChosen(),
							],
					}
				);
		}
		return new(s);
	}

	GameAction[] IfChosen() =>
		Then.IsEmpty ? [] : [new IfChosenAction(Then) { Key = ChooseCardsFromHandChoice.Chosen }];
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
/// random order." How many are taken is Take (Stock Up: "Put two of them into your hand"; Ponder: none), where
/// they go is To (Hit It!: "onto the battlefield tapped"; Thassa's Oracle: "up to one of them on top of your
/// library"), where the others go is Rest (TheRest). Hoshi Sato's "put one of them into your hand" is Must. A
/// revealed card is known to everyone; the rest go down as Identity Echo's do — a random order is not known
/// (magic-zones.md).
/// ponytail: Spock's "in any order" is a random order too (Shayne, 2026-10-31; DesignNotes).
/// </summary>
public record LookAndTake(Count N, Filters.Filter? Filter = null) : Effect
{
	/// <summary>"You may reveal [it] and put it into your hand" — known to everyone there.</summary>
	public bool Reveal { get; init; }

	/// <summary>Hoshi Sato: "Put one of them into your hand" — not "you may": exactly Take, not up to it.</summary>
	public bool Must { get; init; }

	/// <summary>
	/// Stock Up: "Put two of them into your hand" — how many are taken (never more than there are to take).
	/// Ponder takes none: no pick is asked.
	/// </summary>
	public Count Take { get; init; } = 1;

	/// <summary>
	/// Where the taken go — your hand unless said. Hit It!: "Put a land card from among them onto the battlefield
	/// tapped"; Loot, Exuberant Explorer: "put it onto the battlefield" (Battlefield). Thassa's Oracle: "Put up
	/// to one of them on top of your library" (Library): it stays above the rest, and you know it is there.
	/// </summary>
	public ZoneType To { get; init; } = ZoneType.Hand;

	/// <summary>
	/// Where the cards not taken go — Rutherford's "the rest on the bottom of your library in a random order"
	/// unless said; Pattern Buffer Preservation: "the other into your graveyard" (Graveyard); Ponder: "then put
	/// them back in any order" (TopInAnyOrder).
	/// </summary>
	public TheRest Rest { get; init; }

	/// <summary>Hit It!: "… tapped".</summary>
	public bool Tapped { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var top = s.CardsIn(Controller, ZoneType.Library)
			.Take(N.Value(s, InputContext))
			.Select(c => c.Id)
			.ToImmutableList();
		var context = InputContext.Remove(ChooseCardsFromHandChoice.Chosen);
		var choice = new ChooseAmongChoice
		{
			PlayerId = Controller,
			Ids = top,
			Zone = ZoneType.Library,
			Filter = Filter,
		};
		var take = Math.Min(Take.Value(s, InputContext), choice.GetOptions(s, context).Count);
		var where = To switch
		{
			ZoneType.Battlefield => "onto the battlefield",
			ZoneType.Library => "on top of your library",
			ZoneType.Hand => "into your hand",
			_ => $"into your {To.ToString().ToLowerInvariant()}",
		};
		var many =
			take == 1 ? "one"
			: Must ? $"{take}"
			: $"up to {take}";
		choice = choice with
		{
			Prompt = $"{(Must ? "Put" : "You may put")} {many} {where}",
			MinChoices = Must ? take : 0,
			MaxChoices = take,
		};
		var put = new PutLookedAtAction(Controller, top)
		{
			Reveal = Reveal,
			To = To,
			Tapped = Tapped,
			Rest = Rest,
		};
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					PipelineContext = context,
					// Nothing to take: no prompt with no options, but the rest still go.
					Steps = take <= 0 ? [put] : [choice, put],
				}
			)
		);
	}
}

/// <summary>
/// Where the cards a LookAndTake doesn't take go — Rutherford: "Put the rest on the bottom of your library in a
/// random order"; Pattern Buffer Preservation: "the other into your graveyard"; Ponder: "put them back in any order".
/// </summary>
public enum TheRest
{
	/// <summary>Rutherford: "Put the rest on the bottom of your library in a random order" — known to nobody.</summary>
	BottomAtRandom,

	/// <summary>Pattern Buffer Preservation: "the other into your graveyard".</summary>
	Graveyard,

	/// <summary>
	/// Ponder: "Look at the top three cards of your library, then put them back in any order" — they stay on top,
	/// their owner knowing each and ordering two or more (a surveil's choice, OrderLibraryTopChoice).
	/// </summary>
	TopInAnyOrder,
}

/// <summary>After LookAndTake's choice: the chosen cards where To says, the rest where Rest says.</summary>
public record PutLookedAtAction(int PlayerId, ImmutableList<int> Looked) : GameAction
{
	public bool Reveal { get; init; }

	public ZoneType To { get; init; } = ZoneType.Hand;

	public bool Tapped { get; init; }

	public TheRest Rest { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var taken = ChooseCardsFromHandChoice.Read(InputContext).ToHashSet();
		foreach (var id in taken)
		{
			s = To switch
			{
				ZoneType.Battlefield => s.EnterBattlefield(id, PlayerId, tapped: Tapped),
				// On top: it is among the top cards already and the rest leave from around it — no move, and
				// its owner has looked at it. ponytail: several taken keep their old order, and with
				// TopInAnyOrder the rest are ordered above them; no card says either yet.
				ZoneType.Library => s.MakeKnown(id, Knowers.Owner),
				_ => s.MoveCard(id, s.ZoneId(PlayerId, To)), // the hand; any other zone of yours as it is named
			};
			if (Reveal)
				s = s.MakeKnown(id, Knowers.Everyone);
		}
		var rest = Looked
			.Where(id => !taken.Contains(id) && s.IsIn(id, PlayerId, ZoneType.Library))
			.ToArray();
		if (Rest == TheRest.Graveyard)
			return new(
				rest.Aggregate(
					s,
					(st, id) => st.MoveCard(id, st.ZoneId(PlayerId, ZoneType.Graveyard))
				)
			);
		if (Rest == TheRest.TopInAnyOrder)
			// They stay where they are, each looked at (as a surveil's kept cards: magic-zones.md), then ordered.
			return new(
				OrderKeptOnTopAction.Ask(
					rest.Aggregate(s, (st, id) => st.MakeKnown(id, Knowers.Owner)),
					PlayerId,
					[.. rest]
				)
			);
		return new(s.ToBottomAtRandom(PlayerId, rest));
	}
}

/// <summary>
/// Equip: "Attach to target creature you control." — this Equipment (What = Ref.Source) to the target; Thieving
/// Skydiver: "If that artifact is an Equipment, attach it to this creature." — the target to this (To = Ref.Source).
/// </summary>
public record Attach(Ref What, Ref To) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		var attachment = Permanents(What, s).FirstOrDefault();
		var host = Permanents(To, s).FirstOrDefault();
		return new(
			attachment is null || host is null ? s : Attachments.Attach(s, attachment.Id, host.Id)
		);
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

/// <summary>
/// Each player named discards, choosing for themselves — the number counted for each player, Ref.It that player
/// (Balance: "Players discard cards … the same way", Count.BeyondTheFewest of the cards in Ref.It's hand).
/// </summary>
public record Discard(Ref Who, Count Count) : Effect
{
	/// <summary>Seasoned Cryomancer: "When you discard one or more nonland cards this way" — run after, reading the discarded.</summary>
	public ImmutableList<GameAction> Then { get; init; } = [];

	/// <summary>Tersa Lightshatter: "discard up to two cards, then draw that many cards" — none is an answer.</summary>
	public bool UpTo { get; init; }

	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s))
		{
			var n = Math.Min(
				Count.Value(s, InputContext.SetItem(ContextKeys.It, player)),
				s.CardsIn(player, ZoneType.Hand).Count()
			);
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
								MinChoices = UpTo ? 0 : n,
								MaxChoices = n,
								Prompt = UpTo ? $"Discard up to {n}" : $"Discard {n}",
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
				.Where(c => c.Is(Types) && s.CanSacrifice(c.Id)) // Quark's Treasures aren't candidates
				.OrderByDescending(c => c.ManaCost.ManaValue)
				.FirstOrDefault();
			if (victim is not null)
				s = s.Sacrifice(victim.Id);
		}
		return new(s);
	}
}

/// <summary>
/// The City on the Edge of Forever: "All artifacts and creatures phase out." — Rules.Phasing, each with what
/// is attached to it.
/// </summary>
public record PhaseOut(Ref What) : Effect
{
	public override ActionResult Execute(GameState s) =>
		new(Permanents(What, s).Select(c => c.Id).ToList().Aggregate(s, Rules.Phasing.PhaseOut));
}

/// <summary>
/// "You may sacrifice a [Filter]. If you do, [Then]" — the controller picks one or none; Then runs only if something
/// was sacrificed, reading it as Ref.ThisWay (Transmute
/// Artifact: "the sacrificed artifact's mana value"), as every "[done] this way".
/// </summary>
public record MaySacrifice(Filters.Filter Filter, ImmutableList<GameAction> Then) : Effect
{
	/// <summary>Genesis Device: "Sacrifice any number of lands you control, then [Then]" — Then reads how many (Count.PermanentsChosen).</summary>
	public bool AnyNumber { get; init; }

	/// <summary>The City on the Edge of Forever: "Sacrifice a creature token. If you do, …" — not a may: one if you can.</summary>
	public bool Must { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var candidates = Filters
			.FilterChoices.Among(
				Filter,
				s,
				Controller,
				ZoneType.Battlefield,
				GetInput<int>(ContextKeys.Source)
			)
			.Count(id => s.CanSacrifice(id));
		return new(
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
							Prompt =
								AnyNumber ? "Sacrifice any number"
								: Must ? "Sacrifice"
								: "You may sacrifice",
							Sacrifice = true,
							MinChoices = Must && candidates > 0 ? 1 : 0,
							MaxChoices = AnyNumber ? Math.Max(1, candidates) : 1,
						},
						new SacrificeChosenAction(),
						new IfChosenAction(Then) { ThisWay = true },
					],
				}
			)
		);
	}
}

/// <summary>
/// Winter: "each opponent sacrifices a creature of their choice" — each chooses their own (never what can't be
/// sacrificed). Emrakul's ward, "sacrifice three permanents", is (Ref.You, Is.AnyCard) { N = 3 }.
/// </summary>
public record EachSacrifices(Ref Who, Filters.Filter Filter) : Effect
{
	/// <summary>Garruk, Veiled Butcher: "If you sacrificed a creature this way, [IfYouDid]."</summary>
	public ImmutableList<GameAction> IfYouDid { get; init; } = [];

	/// <summary>
	/// Balance: "chooses a number of lands … equal to [the fewest], then sacrifices the rest" — how many each
	/// sacrifices, counted for each player (Ref.It that player: Count.BeyondTheFewest); as many of it as they can.
	/// One choice of that many, never a choice each.
	/// </summary>
	public Count N { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Where(id => s.GetObject(id) is MagicPlayer))
		{
			var choice = new ChoosePermanentChoice(
				player,
				GetInput<int>(ContextKeys.Source),
				Filter
			)
			{
				Sacrifice = true,
			};
			var n = Math.Min(
				N.Value(s, InputContext.SetItem(ContextKeys.It, player)),
				choice.GetOptions(s, InputContext).Count
			);
			if (n > 0)
				s = s.SpawnAction(
					new PipelineAction
					{
						PipelineContext = InputContext.Remove(ChoosePermanentChoice.Chosen),
						Steps =
						[
							choice with
							{
								MinChoices = n,
								MaxChoices = n,
								Prompt =
									n == 1
										? $"Sacrifice ({Source(s).Name})"
										: $"Sacrifice {n} ({Source(s).Name})",
							},
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
