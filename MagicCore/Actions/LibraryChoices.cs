using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore.Actions;

/// <summary>Pick any of the top <see cref="Count"/> cards of your library — surveil's "put into your graveyard".</summary>
public record ChooseFromLibraryTopChoice : ChoiceAction
{
	public int PlayerId { get; init; }
	public int Count { get; init; }

	public ChooseFromLibraryTopChoice()
	{
		OutputKey = ChooseCardsFromHandChoice.Chosen;
		Prompt = "Put into your graveyard";
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		s.CardsIn(PlayerId, ZoneType.Library)
			.Take(Count)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;
}

/// <summary>
/// Surveil's "the rest on top of your library in any order" (CR 701.25a, TBL-9.2): the kept cards, every one
/// picked, the first picked going on top. Asked only when two or more are kept (<see cref="OrderKeptOnTopAction"/>).
/// The AI answers it by a rule, never branching on orders (LandUnlock.Order).
/// </summary>
public record OrderLibraryTopChoice : ChoiceAction
{
	public const string Order = "library order";
	public int PlayerId { get; init; }
	public ImmutableArray<int> Kept { get; init; } = [];

	public OrderLibraryTopChoice()
	{
		OutputKey = Order;
		Prompt = "Put back on top of your library in any order"; // the answer: top first
	}

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState s,
		ImmutableDictionary<string, object> context
	) =>
		Kept.Where(id => s.IsIn(id, PlayerId, ZoneType.Library))
			.Select(id => new ChoiceOption { Id = id, DisplayText = s.GetCard(id).Name })
			.ToImmutableList();

	public override int GetDecidingPlayerId(
		GameState s,
		ImmutableDictionary<string, object> context
	) => PlayerId;

	/// <summary>The answered order, top first: each kept card once; any left out keeps its place after them.</summary>
	public static IEnumerable<int> Read(ImmutableDictionary<string, object> context) =>
		context.GetValueOrDefault(Order) switch
		{
			int id => [id],
			ImmutableList<int> ids => ids,
			_ => [],
		};
}

/// <summary>After a surveil's binning: two or more cards kept on top — their owner orders them (TBL-9.2).</summary>
public record OrderKeptOnTopAction : GameAction
{
	public int PlayerId { get; init; }

	/// <summary>How many the surveil looked at; the kept are the top ones left after the binned went.</summary>
	public int Looked { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var binned = ChooseCardsFromHandChoice.Read(InputContext).Count();
		var kept = s.CardsIn(PlayerId, ZoneType.Library)
			.Take(Looked - binned)
			.Select(c => c.Id)
			.ToImmutableArray();
		if (kept.Length < 2)
			return new(s);
		return new(
			s.SpawnAction(
				new PipelineAction
				{
					Steps =
					[
						new OrderLibraryTopChoice
						{
							PlayerId = PlayerId,
							Kept = kept,
							MinChoices = kept.Length,
							MaxChoices = kept.Length,
						},
						new PutOnTopInOrderAction { PlayerId = PlayerId, Kept = kept },
					],
				}
			)
		);
	}
}

/// <summary>The kept cards on top in the answered order. Not a zone change: the owner still knows each one.</summary>
public record PutOnTopInOrderAction : GameAction
{
	public int PlayerId { get; init; }
	public ImmutableArray<int> Kept { get; init; } = [];

	public override ActionResult Execute(GameState s)
	{
		var libraryId = s.ZoneId(PlayerId, ZoneType.Library);
		var order = OrderLibraryTopChoice
			.Read(InputContext)
			.Where(Kept.Contains)
			.Distinct()
			.Concat(Kept) // any left out: after the answered ones, in their old order
			.Distinct()
			.Where(id => s.IsIn(id, PlayerId, ZoneType.Library))
			.ToList();
		// the last first, each to the front: the first answered ends on top
		foreach (var id in Enumerable.Reverse(order))
			s = s.MoveObjectToFront(id, libraryId);
		return new(s);
	}
}
