using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore;

/// <summary>
/// The ONE place a permanent's power, toughness and keywords are known (CR 613): its own —
/// printed, counters, until-end-of-turn effects (Card.OwnPower / HasOwn) — plus every static
/// ability on the battlefield that applies to it ("creatures you control have haste"). Every rule
/// reads these; reading OwnPower in a rule silently ignores every anthem on the board.
///
/// P/T statics and keyword statics are separate passes, so a keyword static may filter on real
/// power (Tetsuko: "power or toughness 1 or less") without power ever asking about keywords.
///
/// ponytail: no layer dependency ordering (CR 613.8) — statics add up — and a card that has left
/// keeps only its OWN characteristics in last-known information, not what statics gave it then.
/// </summary>
public static class Characteristics
{
	public static int Power(this GameState s, Card card) =>
		card.OwnPower + Statics(s, card, ptOnly: true).Sum(a => a.Power);

	public static int Toughness(this GameState s, Card card) =>
		card.OwnToughness + Statics(s, card, ptOnly: true).Sum(a => a.Toughness);

	public static bool Has(this GameState s, Card card, string keyword) =>
		card.HasOwn(keyword) || Statics(s, card, ptOnly: false).Any(a => a.Keyword == keyword);

	record Applied(int Power, int Toughness, string? Keyword);

	/// <summary>Every static on the battlefield, found once per (immutable) state — this sits under the AI's simulations.</summary>
	/// An array (never mutated once built), because the table needs a reference-type value.
	static readonly ConditionalWeakTable<
		GameState,
		(Card Source, StaticEffectComponent Effect)[]
	> Sources = new();

	static (Card Source, StaticEffectComponent Effect)[] StaticsOnBattlefield(GameState s) =>
		Sources.GetValue(
			s,
			state =>
				new[] { state.Player1Id(), state.Player2Id() }
					.SelectMany(p => state.CardsIn(p, ZoneType.Battlefield))
					.SelectMany(c => c.GetComponents<StaticEffectComponent>().Select(e => (c, e)))
					.ToArray()
		);

	/// <summary>Statics apply to permanents on the battlefield — not to a card in hand or a dead card's last-known copy.</summary>
	static IEnumerable<Applied> Statics(GameState s, Card card, bool ptOnly)
	{
		var all = StaticsOnBattlefield(s);
		if (
			all.Length == 0
			|| !s.IsOnBattlefield(card.Id)
			|| s.GetCard(card.Id).Incarnation != card.Incarnation
		)
			yield break;
		foreach (var (source, effect) in all)
		{
			// A P/T pass reads only statics that change P/T; a keyword pass only those that grant one.
			var relevant = ptOnly
				? effect.Power != 0 || effect.Toughness != 0
				: effect.Keyword is not null;
			if (!relevant)
				continue;
			if (!AppliesTo(s, source, effect, card))
				continue;
			var context = ImmutableDictionary<string, object>
				.Empty.Add(ContextKeys.Controller, source.ControllerId)
				.Add(ContextKeys.Source, source.Id);
			yield return new(
				effect.Power.Value(s, context),
				effect.Toughness.Value(s, context),
				effect.Keyword
			);
		}
	}

	static bool AppliesTo(GameState s, Card source, StaticEffectComponent effect, Card card)
	{
		var yours = card.ControllerId == source.ControllerId;
		var affected = effect.Affects switch
		{
			Affects.Self => card.Id == source.Id,
			Affects.CreaturesYouControl => yours && card.Is(CardType.Creature),
			Affects.OtherCreaturesYouControl => yours
				&& card.Is(CardType.Creature)
				&& card.Id != source.Id,
			_ => false,
		};
		if (!affected)
			return false;
		// Real power and toughness (CR 613): safe, because only keyword statics carry this filter.
		if (
			effect.OnlyPowerOrToughnessAtMost is { } most
			&& s.Power(card) > most
			&& s.Toughness(card) > most
		)
			return false;
		return effect.While switch
		{
			StaticCondition.DuringYourTurn => s.GetGame().ActivePlayerId == source.ControllerId,
			StaticCondition.YouControlAJacePlaneswalker => s.CardsIn(
					source.ControllerId,
					ZoneType.Battlefield
				)
				.Any(c => c.Is(CardType.Planeswalker) && c.Subtypes.Contains("Jace")),
			_ => true,
		};
	}
}
