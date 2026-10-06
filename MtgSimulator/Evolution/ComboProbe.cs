using MtgCore;

namespace MtgSimulator;

/// <summary>
/// **Does this payoff assemble an UNBOUNDED engine once its demands are answered?**
///
/// A different question from leverage, and the reason it exists is that leverage cannot answer it.
/// `MeasureLeverage` prices a card by rolling the game forward, and `PlayGreedyTurn` takes one
/// action per simulated turn — so an infinite loop prices as "one activation a turn". Splinter Twin
/// measures **leverage 0.00** that way and ranks 31st in a 44-engine report, while the hand-built
/// Twin deck beats the evolved field.
///
/// **Raising the rollout budget was tried and does not work** — see the leverage section of
/// CLAUDE.md. More actions per turn kills the opponent inside the lookahead for every card, not
/// only combos, so the whole report goes unmeasured. A fixture that can end cannot be used to
/// out-simulate an engine that never does.
///
/// `LoopDetector` answers it directly: it looks for a line that reaches a position dominating one
/// of its own ancestors, which is what "you may repeat this" means. It already finds this exact
/// combo (`LoopDetectorTests.TheShippedTwinCombo_IsFound`), so this is plumbing rather than new
/// detection.
/// </summary>
public sealed record ComboFound(IReadOnlyList<string> Line, string Gain, int Iterations);

public static class ComboProbe
{
	/// <summary>
	/// How many suppliers per demand are put on the board beside the payoff. Matches
	/// <c>MeasureLeverage</c>'s default so the two probes are asking about the same support.
	/// </summary>
	public const int DefaultSuppliersPerDemand = 2;

	/// <summary>
	/// **The loop must need the SUPPORT.** A payoff that loops on its own is a balance bug, not an
	/// archetype — `LoopDetectorTests.NoSingleCardInThePoolLoops` sweeps for exactly that — and
	/// reporting it here would credit a broken card as the pool's best engine.
	///
	/// This is the same control `NeitherHalfOfTheTwinCombo_LoopsAlone` applies by hand, made a
	/// precondition rather than an assumption: without it "found a loop" is a statement about one
	/// card, and the support slot it implies would be arbitrary.
	/// </summary>
	public static ComboFound? WithSupport(
		Card payoff,
		IReadOnlyList<Card> support,
		int maxDepth = LoopDetector.DefaultDepth,
		int maxBranching = LoopDetector.DefaultBranching,
		int nodeCap = LoopDetector.DefaultNodeCap
	)
	{
		if (support.Count == 0)
			return null;

		var alone = Find([payoff], maxDepth, maxBranching, nodeCap);
		if (alone is not null)
			return null;

		var together = Find([payoff, .. support], maxDepth, maxBranching, nodeCap);
		return together is null
			? null
			: new ComboFound(together.Line, together.Gain, together.Iterations);
	}

	/// <summary>
	/// The payoff's best suppliers, one demand at a time — the same selection
	/// <c>CardValueSandbox.MeasureLeverage</c> stocks, so a card that shows leverage and a card
	/// that shows a loop are being asked about the same deck.
	///
	/// A card never supplies its own demand, the rule `Satisfaction`, `EngineProbe.Read` and the
	/// leverage stocking all apply.
	/// </summary>
	public static IReadOnlyList<Card> SupportFor(
		string payoff,
		PoolFeatures features,
		IReadOnlyDictionary<string, Card> pool,
		int suppliersPerDemand = DefaultSuppliersPerDemand
	)
	{
		var support = new List<Card>();
		foreach (var d in features.DemandsOf(payoff).Where(features.Informative))
			support.AddRange(
				features
					.SuppliersOf(d)
					.Where(n => !string.Equals(n, payoff, StringComparison.Ordinal))
					.Take(suppliersPerDemand)
					.Where(pool.ContainsKey)
					.Select(n => pool[n])
			);
		return support;
	}

	/// <summary>
	/// A minimal board: the cards in play, one turn started, nothing else.
	///
	/// **Deliberately not the leverage fixture.** That one carries a dummy ladder on both sides and
	/// thirty library filler, all of which multiply this search's branching factor for no gain —
	/// the question here is whether a repeatable line EXISTS, which needs the pieces and a turn,
	/// not an opponent.
	/// </summary>
	private static LoopFound? Find(
		IReadOnlyList<Card> cards,
		int maxDepth,
		int maxBranching,
		int nodeCap
	)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		foreach (var card in cards)
			(state, _) = state.AddObject(
				card with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: card.HasComponent<PermanentComponent>()
					? ids.Player1BattlefieldId
					: ids.Player1HandId
			);

		(state, _) = state
			.AddActions(
				[
					new StartTurnAction
					{
						ActivePlayerId = ids.Player1Id,
						BattlefieldId = ids.Player1BattlefieldId,
						SkipDraw = true,
					},
				]
			)
			.ProcessAllActions();

		try
		{
			return LoopDetector.Find(state, ids, ids.Player1Id, maxDepth, maxBranching, nodeCap);
		}
		catch
		{
			// A card whose search throws is not a combo we can claim. Same rule the rest of the
			// harvest applies: understating is the safe direction.
			return null;
		}
	}
}
