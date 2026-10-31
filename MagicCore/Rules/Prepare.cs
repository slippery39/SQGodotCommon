using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// Prepare (Secrets of Strixhaven's mechanic, reused by FRA): a creature with a prepare spell that
/// becomes prepared puts a COPY of that spell in exile. Its controller may cast the copy while the
/// creature stays on the battlefield and prepared; casting it unprepares the creature. A creature
/// can't be prepared twice at once. The ONE place each of those is decided.
/// </summary>
public static class Prepare
{
	/// <summary>"[It] becomes prepared" / "enters prepared". Does nothing unless it has a prepare spell and isn't prepared.</summary>
	public static GameState Become(GameState s, int creatureId)
	{
		if (!s.IsOnBattlefield(creatureId))
			return s;
		var creature = s.GetCard(creatureId);
		if (creature.IsPrepared || creature.GetComponent<PrepareSpellComponent>() is not { } spell)
			return s;
		s = s.UpdateObject(creatureId, creature with { IsPrepared = true });

		var face = spell.Face;
		var data = new CardData(
			face.Name,
			"normal",
			face.ManaCost,
			0,
			face.TypeLine,
			face.OracleText,
			null,
			null,
			null,
			[.. "WUBRG".Where(c => face.ManaCost?.Contains(c) == true).Select(c => c.ToString())],
			[],
			null
		);
		var copy = CardFactory.Create(
			new CardDefinition(
				data,
				[spell.Text, new PreparedCopyOf(creatureId, creature.Incarnation)]
			),
			creature.ControllerId
		) with
		{
			IsCopy = true,
		};
		(s, _) = s.AddObject(copy, s.ZoneId(creature.ControllerId, ZoneType.Exile));
		return s;
	}

	/// <summary>The creature a prepared copy belongs to, if that copy may still be cast: same object, still prepared.</summary>
	public static Card? CreatureOf(GameState s, Card copy) =>
		copy.GetComponent<PreparedCopyOf>() is { } link
		&& s.IsOnBattlefield(link.CreatureId)
		&& s.GetCard(link.CreatureId) is { IsPrepared: true } creature
		&& creature.Incarnation == link.Incarnation
			? creature
			: null;

	/// <summary>"Only the current controller of a prepared creature can cast the copy."</summary>
	public static bool IsCastableBy(GameState s, Card copy, int playerId) =>
		s.IsIn(copy.Id, copy.OwnerId, ZoneType.Exile)
		&& CreatureOf(s, copy)?.ControllerId == playerId;

	/// <summary>Casting the copy unprepares its creature.</summary>
	public static GameState Unprepare(GameState s, int creatureId) =>
		s.UpdateObject(creatureId, s.GetCard(creatureId) with { IsPrepared = false });

	/// <summary>
	/// State-based (CR 704.5e, swept with tokens in StateBasedActions): a copy of a spell off the
	/// stack ceases to exist — unless it is a prepared copy in exile whose creature is still there and
	/// prepared.
	/// </summary>
	public static bool IsDeadCopy(GameState s, Card card) =>
		card.IsCopy
		&& !(s.IsIn(card.Id, card.OwnerId, ZoneType.Exile) && CreatureOf(s, card) is not null);
}
