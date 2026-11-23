using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Rules;

/// <summary>
/// The ONE place a spell's effective cost is computed (CR 601.2f): printed cost, plus taxes, minus
/// reductions. Casting validates and pays through it, and so does any UI showing a cost in hand.
/// </summary>
public static class Costs
{
	/// <summary>
	/// A card in its owner's graveyard may be cast for this — its flashback, or its mana cost when
	/// it gained flashback this turn — or not at all (null). The ONE place either is decided.
	/// </summary>
	///
	/// ponytail: with both, the granted one (mana cost, no discard) is taken — cheaper for every FRA
	/// card. Make it a choice on the cast if a card's own flashback is ever the better deal.
	public static FlashbackComponent? Flashback(GameState s, Card card)
	{
		if (!s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard))
			return null;
		if (card.GetComponents<GrantedFlashback>().Any(g => g.OnTurn == s.GetGame().TurnNumber))
			return new FlashbackComponent(card.ManaCost);
		return card.GetComponent<FlashbackComponent>() ?? ScottysBeamMeUp(s, card);
	}

	/// <summary>Scotty, Miracle Worker: an instant or sorcery in your graveyard, during your main phase — beam me up for its mana cost, never returning Scotty.</summary>
	static FlashbackComponent? ScottysBeamMeUp(GameState s, Card card)
	{
		if (!card.Is(CardType.Instant | CardType.Sorcery))
			return null;
		var game = s.GetGame();
		if (
			game.ActivePlayerId != card.OwnerId
			|| game.Step is not (Step.PreCombatMain or Step.PostCombatMain)
		)
			return null;
		return
			s.CardsIn(card.OwnerId, ZoneType.Battlefield)
				.FirstOrDefault(s.HasAbility<GraveyardSpellsHaveBeamMeUp>)
				is { } scotty
			? new FlashbackComponent(card.ManaCost)
			{
				Costs = [new ReturnToHandCost(Filters.Is.Creature) { ExceptId = scotty.Id }],
			}
			: null;
	}

	/// <summary>
	/// An activated ability's effective cost for these targets — Warrior's Blades' equip is {1} less
	/// for each +1/+1 counter on the creature it targets.
	/// </summary>
	public static ManaCost OfAbility(
		GameState s,
		ActivatedAbilityComponent ability,
		System.Collections.Immutable.ImmutableArray<int> targets,
		int controllerId,
		int sourceId
	)
	{
		var source = s.GetCard(sourceId);
		var delta = 0;
		var floored = 0; // Forensic Gadgeteer: reductions with a floor, applied last
		var floor = 0;
		if (ability.CostChange is { } change)
		{
			var context = Conditions
				.Of(source with { ControllerId = controllerId })
				.Add(ContextKeys.Targets, targets);
			delta = change.If?.Holds(s, context) == false ? 0 : change.Amount.Value(s, context);
		}
		// Anointed Peacekeeper: a permanent's tax on the abilities of the sources its filter names — looked
		// for only while one is in play (the board's flag, found once per state).
		if (Characteristics.RuleWork(s).AbilityTaxes)
			foreach (var player in new[] { s.Player1Id(), s.Player2Id() })
			foreach (var permanent in s.CardsIn(player, ZoneType.Battlefield))
			foreach (var tax in s.AbilitiesOf<CostChange>(permanent))
				if (
					tax.Abilities
					&& tax.By.Includes(controllerId, permanent.ControllerId) // Zirda: "abilities you activate"
					&& Conditions.Of(permanent) is var asItsHolder
					&& Applies(s, tax, source, asItsHolder)
					&& tax.Amount.Value(s, asItsHolder) is var amount
				)
				{
					if (tax.Floor > 0 && amount < 0)
						(floored, floor) = (floored + amount, Math.Max(floor, tax.Floor));
					else
						delta += amount;
				}
		var cost = delta == 0 ? ability.Cost : ability.Cost.WithGeneric(delta);
		// "can't reduce the mana in that cost to less than one mana" — nor raise a cost already below it
		return floored == 0
			? cost
			: cost.WithGeneric(Math.Max(floored, Math.Min(0, floor - cost.ManaValue)));
	}

	/// <param name="addedGeneric">
	/// X and any additional generic cost ("or pay {N}"). Taxes and reductions apply to the TOTAL (CR
	/// 601.2f) — Geist's {1} off an {X}{R} spell would be lost on the printed cost, which has no generic.
	/// </param>
	/// <param name="free">"Without paying its mana cost" (Omnipresence) — taxes and additional costs still apply.</param>
	/// <param name="targets">The spell's chosen targets — Guidance Failure: "costs {1} less to cast if it targets an attacking creature".</param>
	/// <param name="added">An additional mana cost beyond generic — a kicker's {1}{U}; paid even when cast free.</param>
	/// <param name="flashback">False for a cast an effect makes as it resolves (Bilbo): from the graveyard by its
	/// permission, never flashback — the mana cost is paid, not the flashback cost.</param>
	public static ManaCost Of(
		GameState s,
		int casterId,
		Card card,
		int addedGeneric = 0,
		bool free = false,
		System.Collections.Immutable.ImmutableArray<int> targets = default,
		ManaCost? added = null,
		bool flashback = true
	)
	{
		var printed =
			free ? ManaCost.None
			: flashback ? Flashback(s, card)?.Cost ?? card.ManaCost // flashback is an alternative cost: taxes still apply
			: card.ManaCost;
		// Phlage's escape: "Escape—{R}{R}{W}{W}" — its owner's cast from the graveyard, never flashback or an effect's
		if (
			!free
			&& flashback
			&& casterId == card.OwnerId
			&& s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard)
			&& Flashback(s, card) is null
			&& !Playable.MarkedInGraveyard(s, card, casterId) // Emry's mark casts it for its mana cost
			&& s.AbilitiesOf<MayCastThisFrom>(card)
				.FirstOrDefault(m => m.From == ZoneType.Graveyard)
				?.Mana
				is { } escape
		)
			printed = escape;
		if (added is not null)
			printed = printed.Plus(added);
		// Cast from exile by impulse: Vulcan Mind-Meld's "mana of any type", Zero's "costs {2} more".
		var impulse = Playable.IsImpulsePlayableBy(s, card, casterId)
			? card.GetComponent<PlayableFromExile>()
			: null;
		// Null Summoner: "mana of any type can be spent to cast that spell".
		if (
			!free
			&& (
				impulse?.AnyType == true
				|| !s.IsIn(card.Id, casterId, ZoneType.Hand)
					&& Playable.ZonePermission(s, card, casterId) is { Rule.AnyType: true }
			)
		)
			printed = printed.OfAnyType();
		var delta = addedGeneric + (impulse?.Tax ?? 0);
		// Taxes and reductions on the battlefield, judged as their permanent's (Thalia, Geist).
		foreach (var player in new[] { casterId, s.OpponentOf(casterId) })
		foreach (var permanent in s.CardsIn(player, ZoneType.Battlefield))
		foreach (var change in s.AbilitiesOf<CostChange>(permanent))
		{
			var context = Conditions.Of(permanent);
			if (
				!change.OnItself
				&& !change.Abilities
				&& change.By.Includes(casterId, permanent.ControllerId)
				&& Applies(s, change, card, context)
			)
				delta += change.Amount.Value(s, context);
		}
		// "This spell costs … less", judged as its caster's.
		var asCast = Conditions
			.Of(card with { ControllerId = casterId })
			.SetItem(ContextKeys.Targets, targets.IsDefault ? [] : targets);
		foreach (var change in card.GetComponents<CostChange>())
			if (change.OnItself && Applies(s, change, card, asCast))
				delta += change.Amount.Value(s, asCast);
		return delta == 0 ? printed : printed.WithGeneric(delta);
	}

	static bool Applies(
		GameState s,
		CostChange change,
		Card spell,
		System.Collections.Immutable.ImmutableDictionary<string, object> context
	) =>
		(change.Spells?.Matches(s, spell, ContextKeys.Asking(context)) ?? true)
		&& (change.If?.Holds(s, context) ?? true)
		&& (
			change.NotFrom.IsEmpty
			|| s.GetParent(spell.Id) is not { } zone
			|| !change.NotFrom.Contains(((Zone)s.GetObject(zone)).ZoneType)
		);
}
