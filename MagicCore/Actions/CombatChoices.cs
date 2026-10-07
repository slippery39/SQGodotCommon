using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>Option id meaning "this creature does not attack / block".</summary>
public static class CombatOption
{
	public const int None = 0;
}

/// <summary>Attack with this creature, and at whom. One per creature that can attack.</summary>
public record DeclareAttackerChoice : ChoiceAction
{
	public const string KeyPrefix = "attack:";

	public int PlayerId { get; init; }
	public int CreatureId { get; init; }

	public DeclareAttackerChoice(int playerId, int creatureId)
	{
		PlayerId = playerId;
		CreatureId = creatureId;
		OutputKey = KeyPrefix + creatureId;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		var opponent = s.GetPlayer(s.OpponentOf(PlayerId));
		return
		[
			new ChoiceOption { Id = CombatOption.None, DisplayText = "Don't attack" },
			new ChoiceOption { Id = opponent.Id, DisplayText = $"Attack {opponent.Name}" },
			// CR 506.3: a planeswalker the defending player controls can be attacked.
			.. s.CardsIn(opponent.Id, ZoneType.Battlefield)
				.Where(c => c.Is(Cards.CardType.Planeswalker))
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = $"Attack {c.Name}" }),
		];
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>Block one attacker with this creature, or not. One per creature that can block something.</summary>
public record DeclareBlockerChoice : ChoiceAction
{
	public const string KeyPrefix = "block:";

	public int PlayerId { get; init; }
	public int CreatureId { get; init; }

	public DeclareBlockerChoice(int playerId, int creatureId)
	{
		PlayerId = playerId;
		CreatureId = creatureId;
		OutputKey = KeyPrefix + creatureId;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		var blocker = s.GetCard(CreatureId);
		return
		[
			new ChoiceOption { Id = CombatOption.None, DisplayText = "Don't block" },
			.. Combat
				.Attackers(s)
				.Where(a => Combat.CanBlock(blocker, a))
				.Select(a => new ChoiceOption { Id = a.Id, DisplayText = $"Block {a.Name}" }),
		];
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

static class Declarations
{
	public static IEnumerable<(int CreatureId, int ChosenId)> Read(
		ImmutableDictionary<string, object> context,
		string prefix
	) =>
		context
			.Where(kv =>
				kv.Key.StartsWith(prefix) && kv.Value is int chosen && chosen != CombatOption.None
			)
			.Select(kv => (int.Parse(kv.Key[prefix.Length..]), (int)kv.Value));
}

public record FinishAttackersAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (
			var (id, target) in Declarations.Read(InputContext, DeclareAttackerChoice.KeyPrefix)
		)
		{
			var card = s.GetCard(id);
			s = s.UpdateObject(
					id,
					card with
					{
						AttackingId = target,
						IsTapped = card.IsTapped || !card.Has("Vigilance"),
					}
				)
				.Raise(new AttackedEvent(id));
		}
		return new ActionResult(TurnStructure.GivePriority(s, s.GetGame().ActivePlayerId));
	}
}

public record FinishBlockersAction : GameAction
{
	/// <summary>
	/// ponytail: a lone blocker on a menace attacker is dropped rather than refused, so every
	/// sequence of per-creature answers yields a legal declaration (CR 702.110b). Arena instead
	/// refuses to submit; the reachable outcomes are the same.
	/// </summary>
	public override ActionResult Execute(GameState s)
	{
		var blocks = Declarations.Read(InputContext, DeclareBlockerChoice.KeyPrefix).ToList();
		blocks = blocks
			.Where(b =>
				!s.GetCard(b.ChosenId).Has("Menace")
				|| blocks.Count(o => o.ChosenId == b.ChosenId) >= 2
			)
			.ToList();

		foreach (var (blocker, attacker) in blocks)
		{
			s = s.UpdateObject(blocker, s.GetCard(blocker) with { BlockingId = attacker });
			s = s.UpdateObject(attacker, s.GetCard(attacker) with { IsBlocked = true });
		}
		return new ActionResult(TurnStructure.GivePriority(s, s.GetGame().ActivePlayerId));
	}
}
