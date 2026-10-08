using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Targeting;

namespace MagicCore.Actions;

/// <summary>
/// Pick up to one permanent matching a filter — "you may sacrifice a creature". Not targeting: the
/// filter is a TargetSpec only because that is the one place "a creature you control" is decided.
/// </summary>
public record ChoosePermanentChoice : ChoiceAction
{
	public const string Chosen = "permanent";

	public static IEnumerable<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Chosen) switch
		{
			int id => [id],
			ImmutableList<int> ids => ids,
			_ => [],
		};

	public int PlayerId { get; init; }
	public int SourceId { get; init; }
	public TargetSpec Filter { get; init; }

	/// <param name="required">"Sacrifices a creature" must; "you may sacrifice" need not.</param>
	public ChoosePermanentChoice(
		int playerId,
		int sourceId,
		TargetSpec filter,
		bool required = false
	)
	{
		PlayerId = playerId;
		SourceId = sourceId;
		Filter = filter;
		MinChoices = required ? 1 : 0;
		MaxChoices = 1;
		OutputKey = Chosen;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Filter
			.Candidates(s, PlayerId, SourceId)
			.Where(id => s.GetObject(id) is Cards.Card)
			.Select(id => new ChoiceOption { Id = id, DisplayText = s.GetObject(id).Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>"You may …": Yes is picking the one option, No is picking none. Read by IfChosenAction.</summary>
public record YesNoChoice : ChoiceAction
{
	public int PlayerId { get; init; }

	public YesNoChoice(int playerId, string prompt)
	{
		PlayerId = playerId;
		Prompt = prompt;
		MinChoices = 0;
		MaxChoices = 1;
		OutputKey = ChoosePermanentChoice.Chosen;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) => [new ChoiceOption { Id = 1, DisplayText = "Yes" }];

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>"Counter it unless its controller pays {N}" — Yes pays. Its own type so the AI's look-ahead can pay.</summary>
public record PayToKeepChoice : YesNoChoice
{
	public PayToKeepChoice(int playerId, string prompt)
		: base(playerId, prompt) { }
}

/// <summary>An Aura entering without being cast: its controller chooses what it enchants (CR 303.4f).</summary>
public record ChooseHostChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public int AuraId { get; init; }

	public ChooseHostChoice(int playerId, int auraId)
	{
		PlayerId = playerId;
		AuraId = auraId;
		Prompt = "Choose what it enchants";
		OutputKey = ChoosePermanentChoice.Chosen;
		MinChoices = 1;
		MaxChoices = 1;
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		!s.IsOnBattlefield(AuraId)
			? []
			: new[] { s.Player1Id(), s.Player2Id() }
				.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
				.Where(c =>
					c.Id != AuraId && Rules.Attachments.CanEnchant(s, s.GetCard(AuraId), c.Id)
				)
				.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
				.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

public record AttachChosenHostAction(int AuraId) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		new(
			ChoosePermanentChoice.Read(InputContext).FirstOrDefault() is var host && host != 0
				? Rules.Attachments.Attach(s, AuraId, host)
				: s
		);
}

/// <summary>Sacrifices what the previous choice picked (to its owner's graveyard — it dies).</summary>
public record SacrificeChosenAction : GameAction
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var id in ChoosePermanentChoice.Read(InputContext))
			s = s.Sacrifice(id);
		return new ActionResult(s);
	}
}

/// <summary>"If you do, …": runs the effects only when the previous choice picked something.</summary>
public record IfChosenAction(ImmutableList<GameAction> Then) : GameAction
{
	/// <summary>Which choice's answer to read — a permanent or yes/no (default), or cards from a hand.</summary>
	public string Key { get; init; } = ChoosePermanentChoice.Chosen;

	public override ActionResult Execute(GameState s)
	{
		if (
			InputContext.GetValueOrDefault(Key) switch
			{
				int => false,
				ImmutableList<int> ids => ids.IsEmpty,
				_ => true,
			}
		)
			return new ActionResult(s);
		foreach (var effect in Then)
			s = (effect with { InputContext = InputContext }).Execute(s).GameState;
		return new ActionResult(s);
	}
}
