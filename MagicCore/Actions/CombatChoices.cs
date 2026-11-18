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
		var oneEach = s.CardsIn(opponent.Id, ZoneType.Battlefield)
			.Any(s.HasAbility<Effects.OneAttackerPerPlaneswalkerYouControl>);
		return
		[
			// CR 508.1d: a goaded creature that can attack must (Combat.Goaded)
			.. Combat.IsGoaded(s.GetCard(CreatureId))
				? (ChoiceOption[])[]
				: [new ChoiceOption { Id = CombatOption.None, DisplayText = "Don't attack" }],
			new ChoiceOption { Id = opponent.Id, DisplayText = $"Attack {opponent.Name}" },
			// CR 506.3: a planeswalker the defending player controls can be attacked — once only, under
			// Tomik, by a creature declared before this one.
			.. s.CardsIn(opponent.Id, ZoneType.Battlefield)
				.Where(c =>
					c.Is(Cards.CardType.Planeswalker)
					&& !(
						oneEach
						&& !s.HasLostAllAbilities(c) // a granted ability: a stripped walker loses it
						&& Declarations.Read(context, KeyPrefix).Any(d => d.ChosenId == c.Id)
					)
				)
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = $"Attack {c.Name}" }),
		];
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	/// <summary>
	/// THE "don't attack" answer every answerer gives — "Don't attack", or for a goaded creature (Combat.Goaded),
	/// which isn't offered it, the first target offered.
	/// </summary>
	public static int NoAttack(ChoiceAction ask) =>
		ask.Options.Any(o => o.Id == CombatOption.None) ? CombatOption.None : ask.Options[0].Id;
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
				.Where(a => Combat.CanBlock(s, blocker, a))
				.Select(a => new ChoiceOption { Id = a.Id, DisplayText = $"Block {a.Name}" }),
		];
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>
/// MC-7: how much of an attacker's combat damage this blocker is assigned — asked of the attacker's
/// controller for each blocker but the last, which takes the rest (CR 510.1c: "divided as its controller
/// chooses among them"; no order since Foundations, 2024). Option id = the amount + 1, 0 to what is left.
/// The AI's quick answer is <see cref="Default"/>, the split the engine made before it was a choice.
/// </summary>
public record AssignCombatDamageChoice : ChoiceAction
{
	public const string KeyPrefix = "damage:";

	public int PlayerId { get; init; }
	public int AttackerId { get; init; }
	public int BlockerId { get; init; }

	public AssignCombatDamageChoice(int playerId, int attackerId, int blockerId)
	{
		PlayerId = playerId;
		AttackerId = attackerId;
		BlockerId = blockerId;
		MinChoices = 1;
		MaxChoices = 1;
		OutputKey = $"{KeyPrefix}{attackerId}:{blockerId}";
		Prompt = "Divide combat damage";
	}

	/// <summary>Each answer so far: (attacker, blocker) → the damage assigned it.</summary>
	public static IReadOnlyDictionary<(int Attacker, int Blocker), int> Read(
		ImmutableDictionary<string, object> context
	) =>
		context
			.Where(kv => kv.Key.StartsWith(KeyPrefix) && kv.Value is int)
			.ToDictionary(
				kv =>
					kv.Key[KeyPrefix.Length..].Split(':') is [var a, var b]
						? (int.Parse(a), int.Parse(b))
						: throw new FormatException(kv.Key),
				kv => (int)kv.Value - 1
			);

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	)
	{
		var left =
			Combat.Assigns(s, s.GetCard(AttackerId))
			- Read(context).Where(kv => kv.Key.Attacker == AttackerId).Sum(kv => kv.Value);
		var name = s.GetCard(BlockerId).Name;
		return
		[
			.. Enumerable
				.Range(0, Math.Max(left, 0) + 1)
				.Select(n => new ChoiceOption
				{
					Id = n + 1,
					DisplayText = $"{n} damage to {name}",
				}),
		];
	}

	/// <summary>The option of <see cref="Combat.DefaultSplit"/>'s share for this blocker, within what is left.</summary>
	public int Default(GameState s)
	{
		var attacker = s.GetCard(AttackerId);
		var share = Combat
			.DefaultSplit(s, attacker, Combat.Assigns(s, attacker))
			.Where(x => x.TargetId == BlockerId)
			.Sum(x => x.Amount);
		return Math.Min(share, Options.Count - 1) + 1;
	}

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>The combat damage step once the attackers' damage is divided (MC-7): dealt at once, then priority.</summary>
public record DealCombatDamageAction(bool FirstStrikeStep) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(
			TurnStructure.GivePriority(
				Combat.DealDamage(s, FirstStrikeStep, AssignCombatDamageChoice.Read(InputContext)),
				s.GetGame().ActivePlayerId
			)
		);
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
		var declared = Declarations.Read(InputContext, DeclareAttackerChoice.KeyPrefix).ToList();
		// Pet Sehlat: "can't attack alone" — two of them together are each not alone.
		if (declared is [var only] && s.Has(s.GetCard(only.CreatureId), Combat.CantAttackAlone))
			declared = [];
		foreach (var (id, target) in declared)
		{
			var card = s.GetCard(id);
			s = s.UpdateObject(
				id,
				card with
				{
					AttackingId = target,
					AttackedOnTurn = s.GetGame().TurnNumber,
				}
			);
			if (!s.Has(card, "Vigilance"))
				s = s.Tap(id);
			var subtypes = s.Subtypes(card);
			if (subtypes.Except(s.GetPlayer(card.ControllerId).ThisTurn.SubtypesAttacked).Any())
				s = s.Tally(
					card.ControllerId,
					t => t with { SubtypesAttacked = [.. t.SubtypesAttacked.Union(subtypes)] }
				);
			s = s.Raise(new AttackedEvent(id));
		}
		// "Whenever you attack": once for the whole declaration, however many attack.
		if (declared.Count > 0)
			s = s.Raise(new AttackersDeclaredEvent(s.GetGame().ActivePlayerId));
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
				!s.Has(s.GetCard(b.ChosenId), "Menace")
				|| blocks.Count(o => o.ChosenId == b.ChosenId) >= 2
			)
			.ToList();

		foreach (var (blocker, attacker) in blocks)
		{
			s = s.UpdateObject(blocker, s.GetCard(blocker) with { BlockingId = attacker });
			s = s.UpdateObject(attacker, s.GetCard(attacker) with { IsBlocked = true })
				.Raise(new BlocksEvent(blocker));
		}
		return new ActionResult(TurnStructure.GivePriority(s, s.GetGame().ActivePlayerId));
	}
}
