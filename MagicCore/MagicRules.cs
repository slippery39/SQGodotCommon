using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;

namespace MagicCore;

/// <summary>
/// The API boundary for every front end, AI and test:
///   NewGame → loop { DecidingPlayerId; GetPendingChoice() ? Choose : LegalActions → Act } until IsOver.
/// </summary>
public static class MagicRules
{
	/// <param name="seed">Non-zero replays the same game; 0 is truly random.</param>
	/// <param name="handSmoothing">
	/// Arena's Bo1 opening-hand smoother. On by default; whether Arena applies it to limited Bo1
	/// is unconfirmed, so a run can turn it off.
	/// </param>
	/// <param name="names">What prompts and logs call the two players ("Attack the AI"); the keys by default.</param>
	/// <param name="sideboard1">Player 1's cards outside the game (sealed: the pool's unplayed cards).</param>
	/// <param name="sideboard2">Player 2's.</param>
	public static GameState NewGame(
		IEnumerable<CardDefinition> deck1,
		IEnumerable<CardDefinition> deck2,
		int seed = 0,
		bool handSmoothing = true,
		(string Player1, string Player2)? names = null,
		IEnumerable<CardDefinition>? sideboard1 = null,
		IEnumerable<CardDefinition>? sideboard2 = null
	)
	{
		var s = new GameState { RngSeed = seed };
		(s, var game) = s.AddObject(new MagicGame { Name = "Game" });
		s = s.RegisterWellKnownId(MagicKeys.Game, game.Id);
		(s, var stack) = s.AddObject(
			new Zone { Name = "Stack", ZoneType = ZoneType.Stack },
			game.Id
		);
		s = s.RegisterWellKnownId(MagicKeys.Stack, stack.Id);

		var p1 = 0;
		var p2 = 0;
		foreach (
			var (key, deck) in new[] { (MagicKeys.Player1, deck1), (MagicKeys.Player2, deck2) }
		)
		{
			var name = names is var (n1, n2) ? (key == MagicKeys.Player1 ? n1 : n2) : key;
			(s, var player) = s.AddObject(new MagicPlayer { Name = name }, game.Id);
			s = s.RegisterWellKnownId(key, player.Id);
			foreach (
				var type in Enum.GetValues<ZoneType>()
					.Where(t =>
						t is not (ZoneType.Stack or ZoneType.Sideboard or ZoneType.PhasedOut)
					)
			)
			{
				(s, var zone) = s.AddObject(
					new Zone { Name = $"{key} {type}", ZoneType = type },
					player.Id
				);
				s = s.RegisterWellKnownId(MagicKeys.Zone(player.Id, type), zone.Id);
			}
			var libraryId = s.ZoneId(player.Id, ZoneType.Library);
			foreach (var card in deck)
				s = s.AddObject(CardFactory.Create(card, player.Id), libraryId).GameState;
			(p1, p2) = p1 == 0 ? (player.Id, 0) : (p1, player.Id);
		}
		// After everything else: ids of the decks don't move, and the shuffles below don't change.
		s = s.WithSideboard(p1, sideboard1 ?? []).WithSideboard(p2, sideboard2 ?? []);

		(var rng, s) = s.TakeRng();
		var starting = rng.Next(2) == 0 ? p1 : p2; // Bo1: a coin flip decides who plays first
		s = s.WithGame(s.GetGame() with { StartingPlayerId = starting, ActivePlayerId = starting });

		return DrawOpeningHand(DrawOpeningHand(s, p1, handSmoothing), p2, handSmoothing);
	}

	/// <summary>
	/// Puts cards outside the game for this player — the ONE way a sideboard enters a game (NewGame
	/// and tests). Its zone is made the first time, so a game never given one has none (Extrapolate
	/// the Impossible: "from outside the game").
	/// </summary>
	public static GameState WithSideboard(
		this GameState s,
		int playerId,
		IEnumerable<CardDefinition> cards
	)
	{
		var list = cards.ToList(); // read once: a one-shot sequence must not add nothing
		if (list.Count == 0)
			return s;
		(s, var zoneId) = s.EnsureZone(playerId, ZoneType.Sideboard);
		foreach (var card in list)
			s = s.AddObject(CardFactory.Create(card, playerId), zoneId).GameState;
		return s;
	}

	/// <summary>
	/// A per-player zone made only when first needed — the Sideboard (a game given one), PhasedOut (something
	/// phased out) — so a game without it keeps every id. Named as NewGame names zones.
	/// </summary>
	public static (GameState, int) EnsureZone(this GameState s, int playerId, ZoneType type)
	{
		var key = MagicKeys.Zone(playerId, type);
		if (s.WellKnownIds.TryGetValue(key, out var id))
			return (s, id);
		var who = playerId == s.Player1Id() ? MagicKeys.Player1 : MagicKeys.Player2;
		(s, var zone) = s.AddObject(new Zone { Name = $"{who} {type}", ZoneType = type }, playerId);
		return (s.RegisterWellKnownId(key, zone.Id), zone.Id);
	}

	/// <summary>
	/// Arena's Bo1 smoother: shuffle twice and keep whichever top seven is closer to the deck's
	/// average land count. Opening hand only — mulligans draw truly random.
	///
	/// ponytail: always takes the closer hand. Arena says it "leans towards" it with an
	/// unpublished weighting; add a weight here if Arena's distribution is ever measured.
	/// </summary>
	static GameState DrawOpeningHand(GameState s, int playerId, bool smoothing)
	{
		s = s.Shuffle(playerId);
		if (!smoothing)
			return s.Draw(playerId, Mulligan.HandSize);

		var library = s.CardsIn(playerId, ZoneType.Library).ToList();
		var expected =
			(double)Mulligan.HandSize * library.Count(c => c.Is(CardType.Land)) / library.Count;
		var first = library.Take(Mulligan.HandSize).ToList();

		s = s.Shuffle(playerId);
		var second = s.CardsIn(playerId, ZoneType.Library).Take(Mulligan.HandSize).ToList();

		double Off(List<Card> hand) => Math.Abs(hand.Count(c => c.Is(CardType.Land)) - expected);
		var hand = Off(first) < Off(second) ? first : second;

		var handId = s.ZoneId(playerId, ZoneType.Hand);
		foreach (var card in hand)
			s = s.MoveCard(card.Id, handId);
		return s;
	}

	/// <summary>Who must act now: a pending choice's owner, the mulligan decider, or the priority holder. 0 once over.</summary>
	public static int DecidingPlayerId(this GameState s)
	{
		if (s.IsOver())
			return 0;
		if (s.IsWaitingForChoice)
			return s.GetPendingChoiceDecidingPlayerId();
		return s.GetGame().Step == Step.PreGame
			? Mulligan.DecidingPlayerId(s)
			: s.GetGame().PriorityPlayerId;
	}

	/// <summary>Everything the deciding player may do. Empty while a choice is pending — answer it with Choose.</summary>
	public static ImmutableList<GameAction> LegalActions(this GameState s)
	{
		var player = s.DecidingPlayerId();
		if (player == 0 || s.IsWaitingForChoice)
			return [];

		IEnumerable<GameAction> candidates =
			s.GetGame().Step == Step.PreGame
				?
				[
					new KeepHandAction { PlayerId = player },
					new MulliganAction { PlayerId = player },
				]
				:
				[
					new PassPriorityAction { PlayerId = player },
					.. Rules
						.Playable.Candidates(s, player)
						.SelectMany(c =>
							c.Is(CardType.Land)
								? [new PlayLandAction { PlayerId = player, CardId = c.Id }]
								: CastCandidates(s, player, c)
						),
					.. s.CardsIn(player, ZoneType.Battlefield)
						.SelectMany(c =>
							ActivateCandidates(s, player, c)
								.Concat(LoyaltyCandidates(s, player, c))
								.Concat(ManaMoves(s, player, c))
						),
					.. s.CardsIn(player, ZoneType.Hand)
						.Concat(s.CardsIn(player, ZoneType.Graveyard))
						.Concat(s.CardsIn(player, ZoneType.Exile)) // Q's gift: "only if this card is exiled"
						.SelectMany(c => ActivateCandidates(s, player, c)),
				];
		return candidates.Where(a => a.ValidateAdd(s).IsValid).ToImmutableList();
	}

	/// <summary>
	/// One cast per affordable X. The bound is loose on purpose — ValidateAdd's payment search
	/// decides; subtracting mana value would undercount, since one W pays a {2/W}.
	/// </summary>
	public static IEnumerable<GameAction> CastCandidates(GameState s, int player, Card card) =>
		// Kicker: each cast once unkicked and once kicked — its targets and costs may differ.
		card.HasComponent<KickerComponent>()
			? CastCandidates(s, player, card, false).Concat(CastCandidates(s, player, card, true))
			: CastCandidates(s, player, card, false);

	static IEnumerable<GameAction> CastCandidates(GameState s, int player, Card card, bool kicked)
	{
		var maxX = card.ManaCost.HasX ? AvailableMana(s, player, s.Has(card, "Convoke"), card) : 0;
		var text = card.GetComponent<SpellEffectsComponent>();
		List<(ImmutableArray<int> modes, ImmutableArray<int> targets)> ChoicesFor(int x) =>
			(text?.Choose ?? 1)
				.Choices(s, card, text?.Modes ?? [])
				.SelectMany(modes =>
					TargetSpec
						.Combinations(
							s,
							StackResolution.TextOf(card, modes, kicked, x).Specs,
							player,
							card.Id
						)
						.Select(targets => (modes, targets))
				)
				.ToList();
		// Dominion Infiltration's "X target creatures": one cast per X, the targets a choice as it is cast
		// (CastSpellAction.ChoosesTargets) — never a cast per set, 2^n against n creatures.
		var perX = text?.Targets.Any(t => t.PerX) == true;
		var once = perX ? null : ChoicesFor(0);
		var atZero = once ?? ChoicesFor(0);
		List<(ImmutableArray<int> modes, ImmutableArray<int> targets)> unchosen = [([], [])];
		// One cast per way to pay each cost that picks a card: "sacrifice … or pay {N}" (0 is paying),
		// a behold (any one is the same play), Twinned Vision's flashback discard.
		var costs = CastSpellAction.CostsOf(s, card, kicked);
		var sacrifices = CostList.Options(costs, CostSlot.Sacrifice, s, player, card.Id);
		var beholds = CostList.Options(costs, CostSlot.Behold, s, player, card.Id);
		var discards = CostList.Options(costs, CostSlot.Discard, s, player, card.Id);
		var returns = CostList.Options(costs, CostSlot.Return, s, player, card.Id);
		// Omnipresence: X = 0 is cast without paying its mana cost — never worse, so it replaces the paid one.
		var free = Rules.Playable.MayCastWithoutPaying(s, card, player);
		var payments = Enumerable.Range(0, maxX + 1).Select(x => (x, free: free && x == 0));
		// Replicate: one cast per number of payments, bounded loosely like X — ValidateAdd's payment decides.
		var maxReplicate = card.GetComponent<ReplicateComponent>() is { } replicate
			? Math.Max(
				0,
				(AvailableMana(s, player, s.Has(card, "Convoke"), card) - card.ManaCost.ManaValue)
					/ Math.Max(1, replicate.Generic)
			)
			: 0;
		return Enumerable
			.Range(0, maxReplicate + 1)
			.SelectMany(replicates =>
				payments.SelectMany(payment =>
					sacrifices.SelectMany(sacrifice =>
						discards.SelectMany(discard =>
							beholds.SelectMany(beheld =>
								returns.SelectMany(returned =>
									(once ?? (payment.x > 0 ? unchosen : atZero)).Select(
										c => new CastSpellAction
										{
											PlayerId = player,
											CardId = card.Id,
											X = payment.x,
											WithoutPayingManaCost = payment.free,
											Targets = c.targets,
											Modes = c.modes,
											SacrificeId = sacrifice,
											DiscardId = discard,
											BeholdId = beheld,
											ReturnId = returned,
											Replicate = replicates,
											Kicked = kicked,
											ChoosesTargets = perX && payment.x > 0,
										}
									)
								)
							)
						)
					)
				)
			);
	}

	/// <summary>
	/// Phyrexian Tower: "{T}, Sacrifice a creature: Add {B}{B}." — a mana ability whose cost picks a card
	/// is never auto-paid (Shayne, 2026-11-06): a move, one per creature. Every other mana ability stays
	/// out of LegalActions (pure branching for the AI).
	/// </summary>
	static IEnumerable<GameAction> ManaMoves(GameState s, int player, Card card) =>
		ManaAbilities
			.AbilitiesOf(s, card)
			.Where(m => m.ChoosesCost)
			.Take(1) // ponytail: TapForManaAction.SacrificeId names the first such ability
			.SelectMany(m =>
				from id in CostList.Options(m.Costs, CostSlot.Sacrifice, s, player, card.Id)
				where id != 0
				from color in m.AnyOneColor ? ManaAbilities.AnyColor : [m.Color]
				select new TapForManaAction
				{
					PlayerId = player,
					CardId = card.Id,
					Color = color,
					SacrificeId = id,
				}
			);

	/// <summary>Abilities activated from the zone the card is in — one candidate per mode, target and card-cost choice.</summary>
	static IEnumerable<GameAction> ActivateCandidates(GameState s, int player, Card card)
	{
		var zone = ((Zone)s.GetObject(s.GetParent(card.Id)!.Value)).ZoneType;
		return s.ActivatedAbilitiesOf(card)
			.Select((ability, index) => (ability, index))
			.Where(a => a.ability.From == zone)
			.SelectMany(a =>
				from taps in TapSets(a.ability.Costs, s, player, card.Id).ToList() // once per ability, not per target
				from modes in a.ability.Choose.Choices(s, card, a.ability.Modes)
				from targets in TargetSpec.Combinations(
					s,
					ActivateAbilityAction.TextOf(a.ability, modes).Specs,
					player,
					card.Id
				)
				from sacrifice in CostList.Options(
					a.ability.Costs,
					CostSlot.Sacrifice,
					s,
					player,
					card.Id
				)
				from discard in CostList.Options(
					a.ability.Costs,
					CostSlot.Discard,
					s,
					player,
					card.Id
				)
				from exile in CostList.Options(a.ability.Costs, CostSlot.Exile, s, player, card.Id)
				from life in Enumerable.Range(0, a.ability.Cost.PhyrexianCount + 1) // Drivnod: {B/P} paid with mana or 2 life
				from tap in CostList.Options(a.ability.Costs, CostSlot.Tap, s, player, card.Id)
				from secondTap in CostList.Options(
					a.ability.Costs,
					CostSlot.SecondTap,
					s,
					player,
					card.Id
				)
				where secondTap == 0 || secondTap != tap // both orders: the table clicks either first; MoveKey makes them one move
				select (GameAction)
					new ActivateAbilityAction
					{
						PlayerId = player,
						CardId = card.Id,
						AbilityIndex = a.index,
						Modes = modes,
						Targets = targets,
						SacrificeId = sacrifice,
						DiscardId = discard,
						ExileId = exile,
						TapId = tap,
						SecondTapId = secondTap,
						TapIds = taps,
						LifeForPhyrexian = life,
					}
			);
	}

	/// <summary>
	/// "Tap three" or more (Collation Trimatrix): one action per SET the player may tap — never per
	/// order; the table clicks them in any order (Targeting). One empty set when no cost taps three.
	/// ponytail: every combination — Picard with 9 Officers is 36 moves, with 12 it is 792; the story
	/// on tap choices (Station, tap two, tap N) looks at narrowing them.
	/// </summary>
	static IEnumerable<ImmutableArray<int>> TapSets(
		IEnumerable<AdditionalCost> costs,
		GameState s,
		int player,
		int sourceId
	) =>
		CostList.In(costs, CostSlot.TapSet) is TapUntappedCost set
			? Combinations([.. set.Candidates(s, player, sourceId)], set.Count)
			:
			[
				[],
			];

	static IEnumerable<ImmutableArray<int>> Combinations(ImmutableArray<int> from, int k) =>
		k == 0
			?
			[
				[],
			]
		: from.Length < k ? []
		: Combinations(from[1..], k - 1)
			.Select(rest => rest.Insert(0, from[0]))
			.Concat(Combinations(from[1..], k));

	/// <summary>One per target choice — and for "−X", one per X it can pay.</summary>
	static IEnumerable<GameAction> LoyaltyCandidates(GameState s, int player, Card card) =>
		Loyalty
			.AbilitiesOf(s, card)
			.SelectMany(
				(ability, index) =>
					from targets in TargetSpec.Combinations(s, ability.Targets, player, card.Id)
					from x in ability.MinusX ? Enumerable.Range(0, card.Loyalty + 1) : [0]
					select new ActivateLoyaltyAction
					{
						PlayerId = player,
						CardId = card.Id,
						AbilityIndex = index,
						Targets = targets,
						X = x,
					}
			);

	/// <summary>An upper bound on the mana this player could pay — every untapped creature too, for convoke.</summary>
	static int AvailableMana(GameState s, int player, bool convoke, Card spell) =>
		s.GetPlayer(player).ManaPool.Total
		+ s.GetPlayer(player)
			.ManaPool.Restricted.Count(r => ManaAbilities.Allows(r.Restriction, s, spell))
		+ s.CardsIn(player, ZoneType.Battlefield)
			.Sum(c =>
				(ManaAbilities.CanTap(s, c) ? ManaAbilities.YieldOf(s, c) : null) is { } yield
				&& yield.CanPayFor(s, spell)
					? yield.Units.Length
				: convoke && c.Is(CardType.Creature) && !c.IsTapped ? 1
				: 0
			)
		// Starfleet Academy's "{5}, {T}: add N" — its N on top, still an upper bound (ManaPayment.WithCostlyMana)
		+ s.CardsIn(player, ZoneType.Battlefield)
			.Where(c => ManaAbilities.CanPayTapCost(s, c))
			.Sum(c =>
				ManaAbilities
					.AbilitiesOf(s, c)
					.Where(m => m.Cost is not null && !m.ChoosesCost)
					.Sum(m => Math.Max(0, m.Amount.Value(s, Conditions.Of(c))))
			);

	/// <summary>
	/// Passes for whoever must act while passing is their ONLY legal action — Arena's auto-pass,
	/// and the first cut to the AI's decision points. Stops at any real decision or game over.
	/// </summary>
	public static GameState AutoPass(this GameState s)
	{
		while (!s.IsOver() && s.LegalActions() is [PassPriorityAction pass])
			s = s.Act(pass);
		return s;
	}

	/// <summary>
	/// One move per distinct choice: copies of a card in hand are interchangeable, so three Plains
	/// are one "play Plains". A front-end and AI convenience — LegalActions itself stays complete.
	/// </summary>
	public static ImmutableList<GameAction> DistinctMoves(
		this GameState s,
		IEnumerable<GameAction> actions
	) => actions.DistinctBy(a => s.MoveKey(a)).ToImmutableList();

	/// <summary>
	/// What makes two moves the SAME move: the card (a permanent by id, elsewhere any copy in the same
	/// zone), X, targets, modes and what a cost takes. DistinctMoves groups by it, and the search AI
	/// re-finds a planned move by it — anything that tells two legal moves apart must be in it, or a
	/// plan for "X=4" is replayed as X=0 (MtgSimulator shipped exactly that bug).
	/// </summary>
	public static object MoveKey(this GameState s, GameAction a) =>
		a switch
		{
			PlayLandAction p => $"land|{Identity(s, p.CardId)}",
			CastSpellAction c =>
				$"cast|{Identity(s, c.CardId)}|{c.X}|{string.Join(",", c.Targets)}|{string.Join(",", c.Modes)}|{Fodder(s, c.SacrificeId)}|{Fodder(s, c.DiscardId)}|{Fodder(s, c.BeholdId)}|{c.WithoutPayingManaCost}{(c.Replicate > 0 ? $"|r{c.Replicate}" : "")}{(c.ReturnId == 0 ? "" : $"|b{Fodder(s, c.ReturnId)}")}{(c.Kicked ? "|k" : "")}", // a beamed-up creature, kicked, only when chosen: older keys stay byte-identical
			ActivateAbilityAction activate =>
				$"activate|{Identity(s, activate.CardId)}|{activate.AbilityIndex}|{string.Join(",", activate.Targets)}|{Fodder(s, activate.SacrificeId)}|{Fodder(s, activate.DiscardId)}|{Fodder(s, activate.ExileId)}{(activate.Modes.IsEmpty ? "" : $"|m{string.Join(",", activate.Modes)}")}{Taps(s, activate)}{(activate.LifeForPhyrexian > 0 ? $"|p{activate.LifeForPhyrexian}" : "")}", // modes, a tapped creature and life for Phyrexian mana only when chosen: older keys (records, the suite) stay byte-identical
			ActivateLoyaltyAction loyalty =>
				$"loyalty|{loyalty.CardId}|{loyalty.AbilityIndex}|{loyalty.X}|{string.Join(",", loyalty.Targets)}",
			_ => a, // the rest have no collection fields, so a record's own equality is the identity
		};

	/// <summary>
	/// What a cost takes, by the same identity: a permanent by id — two of the same name can differ
	/// (damage, counters, auras), and the player picks which (TBL-5.3) — a card elsewhere by name.
	/// </summary>
	static string Fodder(GameState s, int id) => id == 0 ? "" : Identity(s, id);

	/// <summary>The permanents a cost taps, in MoveKey: Station's one as before; Worf's two in either order is one move.</summary>
	static string Taps(GameState s, ActivateAbilityAction a)
	{
		var set = a.TapIds.IsDefaultOrEmpty
			? ""
			: $"|T{string.Join(",", a.TapIds.Select(id => Fodder(s, id)).Order(StringComparer.Ordinal))}";
		if (a.SecondTapId == 0)
			return (a.TapId == 0 ? "" : $"|t{Fodder(s, a.TapId)}") + set;
		var pair = new[] { Fodder(s, a.TapId), Fodder(s, a.SecondTapId) }.Order(
			StringComparer.Ordinal
		);
		return $"|t{pair.First()}|u{pair.Last()}{set}";
	}

	/// <summary>Which card a move is "from" for DistinctMoves: a permanent by id; elsewhere any copy in the same zone.</summary>
	internal static string Identity(GameState s, int id) =>
		s.IsOnBattlefield(id) ? $"{id}" : $"{s.GetCard(id).Name}@{s.GetParent(id)}";

	/// <summary>
	/// Whether a choice's option ids are object ids (a card, a player) — or numbers of its own
	/// (modes, colours, keywords, yes/no, which cast). A front end shows the first kind ON the
	/// objects. Every choice type is listed in one set or the other; a test enforces it.
	/// </summary>
	public static bool OptionsAreObjects(ChoiceAction choice)
	{
		if (ObjectChoices.Contains(choice.GetType()))
			return true;
		if (NumberedChoices.Any(t => t.IsInstanceOfType(choice)))
			return false;
		throw new ArgumentException(
			$"{choice.GetType().Name}: add it to ObjectChoices or NumberedChoices"
		);
	}

	public static readonly IReadOnlySet<Type> ObjectChoices = new HashSet<Type>
	{
		typeof(ChooseTargetChoice),
		typeof(ChooseCardsFromHandChoice),
		typeof(ChooseFromLibraryChoice),
		typeof(ChooseFromLibraryTopChoice),
		typeof(ChooseAmongChoice),
		typeof(ChoosePermanentChoice),
		typeof(ChooseXTargetsChoice),
		typeof(ChooseCreatureToCopyChoice),
		typeof(ChooseHostChoice),
		typeof(DeclareAttackerChoice),
		typeof(DeclareBlockerChoice),
	};

	/// <summary>Subtypes count (PayToKeepChoice is a YesNoChoice).</summary>
	public static readonly IReadOnlyList<Type> NumberedChoices =
	[
		typeof(ChooseModesChoice),
		typeof(ChooseColorChoice),
		typeof(ChooseKeywordChoice),
		typeof(ChooseNumberChoice),
		typeof(YesNoChoice),
		typeof(ChooseCastChoice),
	];

	/// <summary>
	/// Manual tapping, for a front end (Arena's "click a land"): each colour this permanent can tap
	/// for now. Mana abilities stay out of LegalActions — for the AI they are pure branching.
	/// </summary>
	public static IEnumerable<TapForManaAction> TapOptions(
		this GameState s,
		int playerId,
		int cardId
	) =>
		Enum.GetValues<ManaColor>()
			.Select(color => new TapForManaAction
			{
				PlayerId = playerId,
				CardId = cardId,
				Color = color,
			})
			.Where(tap => tap.ValidateAdd(s).IsValid);

	/// <summary>The card a move is played, cast or activated from; 0 for pass, keep and mulligan.</summary>
	public static int CardOf(GameAction move) =>
		move switch
		{
			PlayLandAction p => p.CardId,
			CastSpellAction c => c.CardId,
			ActivateAbilityAction a => a.CardId,
			ActivateLoyaltyAction l => l.CardId,
			TapForManaAction t => t.CardId,
			_ => 0,
		};

	/// <summary>
	/// Is <paramref name="move"/> one of this card's? DistinctMoves keeps ONE of identical cards, so
	/// a move kept for one copy counts for every copy it stands for — a front end asking "what can
	/// I do with this card" gets the same answer for each.
	/// </summary>
	public static bool IsMoveFor(this GameState s, GameAction move, int cardId) =>
		CardOf(move) is var id and not 0 && Identity(s, id) == Identity(s, cardId);

	public static GameState Act(this GameState s, GameAction action)
	{
		CountStep();
		return s.AddAction(action).ProcessAllActions().State;
	}

	/// <summary>
	/// <paramref name="work"/>, and the engine steps it took — every action and choice applied (story
	/// AI-6): an AI's cost, identical on every run and machine where a stopwatch is not. Counted on
	/// every thread the work reaches: the count flows with the execution context into Parallel.For's
	/// workers, so nothing that runs in parallel needs to add its own.
	/// </summary>
	public static (T Result, long Steps) CountingSteps<T>(Func<T> work)
	{
		var outer = counting.Value;
		var count = new StrongBox<long>();
		counting.Value = count;
		try
		{
			var result = work();
			return (result, count.Value);
		}
		finally
		{
			counting.Value = outer;
		}
	}

	static readonly AsyncLocal<StrongBox<long>?> counting = new();

	static void CountStep()
	{
		if (counting.Value is { } count)
			Interlocked.Increment(ref count.Value);
	}

	/// <summary>
	/// Answers the pending choice. ResolveChoice checks only HOW MANY ids arrive, so this boundary
	/// checks WHICH: distinct, and each one of the offered options — a duplicate once kept an extra
	/// card through a mulligan, and an unoffered id let a ground creature block a flyer.
	/// </summary>
	public static GameState Choose(this GameState s, IEnumerable<int> ids)
	{
		var chosen = ids.ToImmutableList();
		var offered =
			s.GetPendingChoice()?.Options.Where(o => o.IsEnabled).Select(o => o.Id).ToHashSet()
			?? throw new InvalidOperationException("No pending choice");
		if (chosen.Distinct().Count() != chosen.Count || !chosen.All(offered.Contains))
			throw new InvalidOperationException(
				$"Not a selection of the offered options: [{string.Join(", ", chosen)}]"
			);
		CountStep();
		return s.ResolveChoice(chosen).State;
	}
}
