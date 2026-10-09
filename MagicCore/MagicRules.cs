using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
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
	public static GameState NewGame(
		IEnumerable<CardDefinition> deck1,
		IEnumerable<CardDefinition> deck2,
		int seed = 0,
		bool handSmoothing = true
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
			(s, var player) = s.AddObject(new MagicPlayer { Name = key }, game.Id);
			s = s.RegisterWellKnownId(key, player.Id);
			foreach (var type in Enum.GetValues<ZoneType>().Where(t => t != ZoneType.Stack))
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

		(var rng, s) = s.TakeRng();
		var starting = rng.Next(2) == 0 ? p1 : p2; // Bo1: a coin flip decides who plays first
		s = s.WithGame(s.GetGame() with { StartingPlayerId = starting, ActivePlayerId = starting });

		return DrawOpeningHand(DrawOpeningHand(s, p1, handSmoothing), p2, handSmoothing);
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
							ActivateCandidates(s, player, c).Concat(LoyaltyCandidates(s, player, c))
						),
					.. s.CardsIn(player, ZoneType.Hand)
						.Concat(s.CardsIn(player, ZoneType.Graveyard))
						.SelectMany(c => ActivateCandidates(s, player, c)),
				];
		return candidates.Where(a => a.ValidateAdd(s).IsValid).ToImmutableList();
	}

	/// <summary>
	/// One cast per affordable X. The bound is loose on purpose — ValidateAdd's payment search
	/// decides; subtracting mana value would undercount, since one W pays a {2/W}.
	/// </summary>
	static IEnumerable<GameAction> CastCandidates(GameState s, int player, Card card)
	{
		var maxX = card.ManaCost.HasX ? AvailableMana(s, player, s.Has(card, "Convoke"), card) : 0;
		var text = card.GetComponent<SpellEffectsComponent>();
		var choices = Modal
			.Choices(text?.Modes ?? [], text?.Choose ?? 1)
			.SelectMany(modes =>
				TargetSpec
					.Combinations(s, StackResolution.TextOf(card, modes).Specs, player, card.Id)
					.Select(targets => (modes, targets))
			)
			.ToList();
		// "Sacrifice … or pay {N}": 0 is paying; each permanent that may be sacrificed is another way.
		IEnumerable<int> sacrifices = card.GetComponent<SacrificeOrPayAdditionalCost>() is { } extra
			? [0, .. extra.Sacrifice.Candidates(s, player, card.Id)]
			: [0];
		// "Behold … or pay {N}": 0 is paying, or behold one — any one: beholding moves nothing, so every
		// candidate is the same play (a hand-built cast may still name another; ValidateAdd checks it).
		IEnumerable<int> beholds = card.GetComponent<BeholdOrPayAdditionalCost>() is { } behold
			? [0, .. behold.Candidates(s, player, card.Id).Take(1)]
			: [0];
		// Twinned Vision's flashback: one cast per card that could be discarded.
		IEnumerable<int> discards = CastSpellAction.OwesDiscard(s, card)
			? s.CardsIn(player, ZoneType.Hand).Select(c => c.Id)
			: [0];
		// Omnipresence: X = 0 is cast without paying its mana cost — never worse, so it replaces the paid one.
		var free = Rules.Playable.MayCastWithoutPaying(s, card, player);
		var payments = Enumerable.Range(0, maxX + 1).Select(x => (x, free: free && x == 0));
		return payments.SelectMany(payment =>
			sacrifices.SelectMany(sacrifice =>
				discards.SelectMany(discard =>
					beholds.SelectMany(beheld =>
						choices.Select(c => new CastSpellAction
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
						})
					)
				)
			)
		);
	}

	/// <summary>Abilities activated from the zone the card is in — one candidate per target and card-cost choice.</summary>
	static IEnumerable<GameAction> ActivateCandidates(GameState s, int player, Card card)
	{
		var zone = ((Zone)s.GetObject(s.GetParent(card.Id)!.Value)).ZoneType;
		return s.AbilitiesOf<ActivatedAbilityComponent>(card)
			.Select((ability, index) => (ability, index))
			.Where(a => a.ability.From == zone)
			.SelectMany(a =>
				from targets in TargetSpec.Combinations(s, a.ability.Targets, player, card.Id)
				from sacrifice in a.ability.Sacrifice?.Candidates(s, player, card.Id) ?? [0]
				from discard in a.ability.DiscardACard
					? s.CardsIn(player, ZoneType.Hand)
						.Where(c => a.ability.MayDiscard(c, card.Id))
						.Select(c => c.Id)
					: [0]
				from exile in a.ability.ExileFromGraveyard?.Candidates(s, player, card.Id) ?? [0]
				select (GameAction)
					new ActivateAbilityAction
					{
						PlayerId = player,
						CardId = card.Id,
						AbilityIndex = a.index,
						Targets = targets,
						SacrificeId = sacrifice,
						DiscardId = discard,
						ExileId = exile,
					}
			);
	}

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
	) =>
		actions
			.DistinctBy(a =>
				a switch
				{
					PlayLandAction p => $"land|{s.GetCard(p.CardId).Name}|{s.GetParent(p.CardId)}",
					CastSpellAction c =>
						$"cast|{s.GetCard(c.CardId).Name}|{s.GetParent(c.CardId)}|{c.X}|{string.Join(",", c.Targets)}|{string.Join(",", c.Modes)}|{Fodder(s, c.SacrificeId)}|{Fodder(s, c.DiscardId)}|{Fodder(s, c.BeholdId)}|{c.WithoutPayingManaCost}",
					ActivateAbilityAction activate =>
						$"activate|{(s.IsOnBattlefield(activate.CardId) ? activate.CardId : $"{s.GetCard(activate.CardId).Name}@{s.GetParent(activate.CardId)}")}|{activate.AbilityIndex}|{string.Join(",", activate.Targets)}|{Fodder(s, activate.SacrificeId)}|{Fodder(s, activate.DiscardId)}|{Fodder(s, activate.ExileId)}",
					_ => (object)a,
				}
			)
			.ToImmutableList();

	/// <summary>Sacrificing one Forest is the same move as sacrificing another.</summary>
	static string Fodder(GameState s, int id) => id == 0 ? "" : s.GetCard(id).Name;

	public static GameState Act(this GameState s, GameAction action) =>
		s.AddAction(action).ProcessAllActions().State;

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
		return s.ResolveChoice(chosen).State;
	}
}
