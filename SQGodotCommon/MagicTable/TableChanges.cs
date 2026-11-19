using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicTable;

/// <summary>
/// What one move changed, for the table's animations (TBL-4.5): a diff of the two states by id. A card keeps
/// its id across zones, so every move shows — MagicCore's staged events can't say it (cleared within the move,
/// raised only where a trigger needs one, a life gain without its amount). Plain C#: what changed, never a rule.
/// </summary>
public static class TableChanges
{
	public abstract record Change;

	/// <summary>A card changed zones; <paramref name="From"/> is null for one new to the game (a token, a copy).</summary>
	public sealed record Moved(Card Was, Card Card, ZoneType? From, ZoneType To) : Change;

	public sealed record Life(int PlayerId, int From, int To) : Change;

	/// <summary>A token or copy that left and ceased to exist (CR 704.5d-e): a token dying is gone from the state.</summary>
	public sealed record Gone(Card Was) : Change;

	/// <summary>A triggered or activated ability that went on the stack — its source pulses (A7).</summary>
	public sealed record Triggered(int StackId, int SourceId) : Change;

	/// <summary>Damage marked on a permanent still there — one that died lost its damage with it.</summary>
	public sealed record Damaged(int CardId, int Amount) : Change;

	public static List<Change> Between(GameState before, GameState after)
	{
		var changes = new List<Change>();
		foreach (var card in after.IdToGameObjectMap.Values.OfType<Card>())
		{
			if (ZoneOf(after, card.Id) is not { } to)
				continue;
			var was = before.HasObject(card.Id) ? before.GetObject(card.Id) as Card : null;
			var from = was is null ? null : ZoneOf(before, card.Id);
			if (
				from != to
				|| (
					was is not null
					&& was.ControllerId != card.ControllerId
					&& to == ZoneType.Battlefield
				)
			)
				changes.Add(new Moved(was ?? card, card, from, to));
			else if (to == ZoneType.Battlefield && card.Damage > was!.Damage)
				changes.Add(new Damaged(card.Id, card.Damage - was.Damage));
		}
		foreach (var was in before.IdToGameObjectMap.Values.OfType<Card>())
			if (
				!after.HasObject(was.Id)
				&& ZoneOf(before, was.Id) is ZoneType.Battlefield or ZoneType.Stack
			)
				changes.Add(new Gone(was));
		foreach (
			var ability in after
				.GetChildren(after.GetWellKnownId(MagicKeys.Stack))
				.OfType<StackAbility>()
				.Where(a => !before.HasObject(a.Id))
		)
			changes.Add(new Triggered(ability.Id, ability.SourceId));
		foreach (var id in new[] { after.Player1Id(), after.Player2Id() })
			if (
				before.GetPlayer(id).Life is var from
				&& after.GetPlayer(id).Life is var to
				&& from != to
			)
				changes.Add(new Life(id, from, to));
		return changes;
	}

	private static ZoneType? ZoneOf(GameState s, int id) =>
		s.GetParent(id) is int parent && s.GetObject(parent) is Zone zone ? zone.ZoneType : null;
}
