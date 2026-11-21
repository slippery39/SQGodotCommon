using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Rules;

/// <summary>The game's day/night designation (CR 730): neither, until a daybound permanent appears.</summary>
public enum Daytime
{
	Neither,
	Day,
	Night,
}

/// <summary>
/// Day and night (CR 730; daybound and nightbound, CR 702.145) — Outland Liberator // Frenzied Trapbreaker: "Daybound
/// (If a player casts no spells during their own turn, it becomes night next turn.)" and its back face's "Nightbound
/// (If a player casts at least two spells during their own turn, it becomes day next turn.)". The designation is the
/// GAME's (MagicGame.Daytime), not a player's: day as the first daybound permanent enters (MagicState.EnterBattlefield),
/// then judged as each turn ends by the spells its active player cast (TurnStructure.FinishCleanup → NextTurn); every
/// daybound permanent has its front face up by day and its back by night (Become), and one entering at night enters
/// transformed. ponytail: read where the designation or a permanent CHANGES — entering, a turn beginning — never by the
/// state-based check, so a permanent that becomes daybound without entering (a copy effect) waits for the next change,
/// and a Transform effect is not refused on a daybound permanent (CR 702.145b); the check if a card needs either.
/// </summary>
public static class DayNight
{
	/// <summary>A double-faced card with daybound, as it enters: its front face has the keyword and a back to turn to.</summary>
	public static bool EntersBound(Card card) =>
		card.Keywords.Contains("Daybound") && card.HasComponent<Effects.TransformsInto>();

	/// <summary>It becomes day or night: each daybound permanent that doesn't match transforms (CR 702.145c, f).</summary>
	public static GameState Become(GameState s, Daytime now)
	{
		s = s.WithGame(s.GetGame() with { Daytime = now });
		foreach (var player in (int[])[s.Player1Id(), s.Player2Id()])
		foreach (var card in s.CardsIn(player, ZoneType.Battlefield).ToList())
		{
			var back = card.HasComponent<Transformed>();
			// the front face's printed keywords are both faces' (Scryfall merges them): daybound is asked of a
			// front, nightbound of a back
			if (
				now == Daytime.Night && !back && s.Has(card, "Daybound")
				|| now == Daytime.Day && back && s.Has(card, "Nightbound")
			)
			{
				var turned = card.Transform();
				if (!ReferenceEquals(turned, card))
					s = s.UpdateObject(card.Id, turned).Raise(new TransformedEvent(card.Id));
			}
		}
		return s;
	}

	/// <summary>
	/// As a turn ends, for the next (CR 730.2a — the untap step's first act, before anything untaps): by day, a turn
	/// whose active player cast no spells makes it night; by night, one who cast two or more makes it day.
	/// </summary>
	public static GameState NextTurn(GameState s, int previousActivePlayerId)
	{
		var now = s.GetGame().Daytime;
		if (now == Daytime.Neither)
			return s;
		var spells = s.GetPlayer(previousActivePlayerId).ThisTurn.SpellsCast;
		return now == Daytime.Day && spells == 0 ? Become(s, Daytime.Night)
			: now == Daytime.Night && spells >= 2 ? Become(s, Daytime.Day)
			: s;
	}
}
