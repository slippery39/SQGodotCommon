using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// THE phasing rules (CR 702.26) — The City on the Edge of Forever: "All artifacts and creatures phase out.
/// (Treat phased-out permanents and anything attached to them as though they don't exist.)" A phased-out
/// permanent sits in its controller's PhasedOut zone, moved there with MoveObject: no zone change (CR
/// 702.26d) — the same object, its counters, tapped state, attachments and summoning-sickness stamp kept,
/// nothing triggers, "until it leaves" never fires. What is attached to it goes with it (indirectly) and
/// comes back with it. It phases in as its controller's untap step begins, before anything untaps (CR
/// 502.1) — unless a permanent on the battlefield says "Permanents can't phase in" (the City's Human).
/// The zone is made the first time something phases out, as the Sideboard is: a game without phasing
/// keeps every id (the golden replays and the position suite hold ids). ponytail: cleanup reads the battlefield,
/// so an until-end-of-turn effect or damage on it as it phased out lasts until the next cleanup after it
/// phases in — clear them as it phases out if a game shows it (DesignNotes).
/// </summary>
public static class Phasing
{
	public static GameState PhaseOut(GameState s, int cardId)
	{
		if (!s.IsOnBattlefield(cardId))
			return s; // already gone, attached to something that phased out
		var card = s.GetCard(cardId);
		var attached = AllBattlefields(s).Where(c => c.AttachedToId == cardId).ToList();
		(s, var zone) = s.EnsureZone(card.ControllerId, ZoneType.PhasedOut);
		s = s.MoveObject(cardId, zone);
		foreach (var a in attached)
			s = PhaseOut(s, a.Id);
		return s;
	}

	/// <summary>The active player's untap step: what they controlled as it phased out phases in, with what is attached to it.</summary>
	public static GameState PhaseIn(GameState s, int playerId)
	{
		if (
			!s.WellKnownIds.ContainsKey(MagicKeys.Zone(playerId, ZoneType.PhasedOut))
			|| AllBattlefields(s).Any(s.HasAbility<PermanentsCantPhaseIn>)
		)
			return s;
		var phasedOut = AllPhasedOut(s).ToList();
		bool Indirect(Card c) => c.AttachedToId != 0 && phasedOut.Any(p => p.Id == c.AttachedToId);
		foreach (
			var card in s.CardsIn(playerId, ZoneType.PhasedOut).Where(c => !Indirect(c)).ToList()
		)
			s = PhaseIn(s, card, phasedOut);
		return s;
	}

	static GameState PhaseIn(GameState s, Card card, List<Card> phasedOut)
	{
		s = s.MoveObject(card.Id, s.ZoneId(card.ControllerId, ZoneType.Battlefield));
		foreach (var a in phasedOut.Where(c => c.AttachedToId == card.Id))
			s = PhaseIn(s, a, phasedOut);
		return s;
	}

	static IEnumerable<Card> AllBattlefields(GameState s) =>
		s.CardsIn(s.Player1Id(), ZoneType.Battlefield)
			.Concat(s.CardsIn(s.Player2Id(), ZoneType.Battlefield));

	static IEnumerable<Card> AllPhasedOut(GameState s) =>
		s.CardsIn(s.Player1Id(), ZoneType.PhasedOut)
			.Concat(s.CardsIn(s.Player2Id(), ZoneType.PhasedOut));
}
