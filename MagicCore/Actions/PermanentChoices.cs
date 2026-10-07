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
	public override ActionResult Execute(GameState s)
	{
		if (!ChoosePermanentChoice.Read(InputContext).Any())
			return new ActionResult(s);
		foreach (var effect in Then)
			s = (effect with { InputContext = InputContext }).Execute(s).GameState;
		return new ActionResult(s);
	}
}
