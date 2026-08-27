using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Positions with an obvious right answer, scored with and without card values.
///
/// **This is the right instrument for this feature and a win rate is not.** `ChoiceCensus` measures
/// 2.33 choices per game and card values change 86 decisions across 224 games, which a 1120-game
/// head-to-head reports as 50.0% whatever happens. Counting correct answers on positions where the
/// answer is not in doubt measures the thing directly.
///
/// **Real cards and real sandbox values**, not an inline table. An inline table would prove the
/// plumbing carries a number, which `DiscardQualityTests` already covers; this asks whether the
/// numbers the sandbox actually produced pick the right card.
///
/// Every scenario pairs cards of the SAME mana cost with mana set so neither is castable. That is
/// deliberate and it is the whole design constraint: where a card is reachable the rollout already
/// gets the answer right (`DiscardQualityTests` case A, spread 27.68), so a scenario there would
/// measure nothing about card values. Equal cost and both out of reach is the region where the
/// rollout is provably flat AND the answer is still obvious.
/// </summary>
[TestFixture]
public class ChoiceAccuracyTests
{
	/// <summary>
	/// Pairs where the better card is not a matter of taste IN THIS ENGINE. Planeswalkers rate
	/// poorly here because there is no blocking and they can be attacked freely; equipment needs a
	/// body already on board. Each pair is same-cost so only quality separates them.
	/// </summary>
	private static readonly (string Better, string Worse, int Cost)[] Pairs =
	[
		("Grave Titan", "Sorin Markov", 6),
		("Massacre Wurm", "Woodland Bellower", 6),
		("Baneslayer Angel", "Gideon Jura", 5),
		("Archangel of Thune", "Nissa, Worldwaker", 5),
		("Plague Mare", "Ajani, Caller of the Pride", 3),
		("Harbinger of the Tides", "Greatsword", 2),
	];

	private static CardValueTable Values() =>
		CardValueTable.TryLoad(CoresetCube.Set.Code, weight: 0.5f)
		?? throw new InvalidOperationException(
			"no sim_results/card_values_csc.json — run CardValueSweep in this configuration first"
		);

	private static Card Named(string name, int ownerId) =>
		CoresetCube.Set.Cards.Single(c => c.Name == name) with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	[Test]
	public void CardValues_PickTheBetterCardMoreOftenThanTheRolloutAlone()
	{
		var table = Values();
		var rows = new List<(string Scenario, bool Without, bool With)>();

		foreach (var (better, worse, cost) in Pairs)
		{
			// Both cards out of reach, so the rollout cannot cast either and is flat.
			var mana = Math.Max(1, cost - 3);

			rows.Add(
				(
					$"discard {cost}cc: keep {better}",
					Discard(better, worse, mana, null),
					Discard(better, worse, mana, table)
				)
			);
			rows.Add(
				(
					$"scry {cost}cc: bottom {worse}",
					Scry(better, worse, mana, null),
					Scry(better, worse, mana, table)
				)
			);
			rows.Add(
				(
					$"tutor {cost}cc: fetch {better}",
					Tutor(better, worse, mana, null),
					Tutor(better, worse, mana, table)
				)
			);
		}

		var without = rows.Count(r => r.Without);
		var with = rows.Count(r => r.With);

		TestContext.Out.WriteLine($"{"scenario", -46} {"rollout", 8} {"+values", 8}");
		foreach (var (scenario, w0, w1) in rows)
			TestContext.Out.WriteLine(
				$"{scenario, -46} {(w0 ? "ok" : "WRONG"), 8} {(w1 ? "ok" : "WRONG"), 8}"
			);
		TestContext.Out.WriteLine(
			$"\n  rollout alone   {without}/{rows.Count}"
				+ $"\n  with values     {with}/{rows.Count}"
		);

		Assert.That(
			with,
			Is.GreaterThanOrEqualTo(without),
			"card values must not make the AI pick worse on positions with an obvious answer"
		);
	}

	/// <summary>Hand holds both; the AI must pitch the worse one.</summary>
	private static bool Discard(string better, string worse, int mana, CardValueTable? table)
	{
		var (state, ids) = Position(mana);
		(state, _) = state.AddObject(Named(better, ids.Player1Id), ids.Player1HandId);
		(state, _) = state.AddObject(Named(worse, ids.Player1Id), ids.Player1HandId);

		var spell = CardFactory.Spell("Pitch", manaCost: 0).WithDiscard().Build();
		var (options, chosen) = Resolve(state, ids, spell, table);

		Assert.That(options, Does.Contain(better).And.Contain(worse), "both cards must be offered");

		return chosen is [var pitched] && pitched == worse;
	}

	/// <summary>Both on top of the library; the AI must bottom the worse one and keep the better.</summary>
	private static bool Scry(string better, string worse, int mana, CardValueTable? table)
	{
		// **Zero filler, because the scry looks at the FIRST cards in the zone.** Filling the
		// library first put twenty blank cards on top and the choice offered those instead — the
		// scenario measured nothing and reported a clean 0/6, which is indistinguishable from the
		// feature not working. The option assertion below is what makes that loud rather than
		// silent.
		var (state, ids) = Position(mana, libraryFiller: 0);
		(state, _) = state.AddObject(Named(better, ids.Player1Id), ids.Player1LibraryId);
		(state, _) = state.AddObject(Named(worse, ids.Player1Id), ids.Player1LibraryId);

		var spell = CardFactory.Spell("Peek", manaCost: 0).WithScry(2).Build();
		var (options, chosen) = Resolve(state, ids, spell, table);

		Assert.That(
			options,
			Is.EquivalentTo(new[] { better, worse }),
			"the scry must be looking at the two cards under test"
		);

		// Bottoming the worse card (and not the better one) is the only correct answer.
		return chosen.Contains(worse) && !chosen.Contains(better);
	}

	/// <summary>Both in the library; the AI must fetch the better one.</summary>
	private static bool Tutor(string better, string worse, int mana, CardValueTable? table)
	{
		var (state, ids) = Position(mana);
		(state, _) = state.AddObject(Named(better, ids.Player1Id), ids.Player1LibraryId);
		(state, _) = state.AddObject(Named(worse, ids.Player1Id), ids.Player1LibraryId);

		var spell = CardFactory.Spell("Seek", manaCost: 0).WithSearchLibrary().Build();
		var (options, chosen) = Resolve(state, ids, spell, table);

		Assert.That(options, Does.Contain(better).And.Contain(worse), "both cards must be offered");

		return chosen is [var fetched] && fetched == better;
	}

	/// <summary>
	/// Casts the choice-raising spell and returns the NAMES the AI selected. Returns empty if no
	/// choice was raised, which fails every scenario rather than passing one silently — an
	/// unraised choice is the failure mode that would make this whole file measure nothing.
	/// </summary>
	private static (IReadOnlyList<string> Options, IReadOnlyList<string> Chosen) Resolve(
		GameState state,
		MtgGameIds ids,
		Card spell,
		CardValueTable? table
	)
	{
		var (withSpell, cast) = state.AddObject(
			spell with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			ids.Player1HandId
		);

		var (atChoice, _) = withSpell
			.AddAction(new CastSpellAction { CardId = cast.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		Assert.That(atChoice.IsWaitingForChoice, Is.True, "no choice was raised at all");

		var choice = atChoice.GetPendingChoice()!;
		var ai = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(7), cardValues: table);

		IReadOnlyList<string> NamesOf(IEnumerable<int> ids2) =>
			[
				.. ids2.Where(atChoice.HasObject)
					.Select(atChoice.GetObject)
					.OfType<Card>()
					.Select(c => c.Name),
			];

		return (
			NamesOf(choice.Options.Select(o => o.Id)),
			NamesOf(ai.ResolveChoice(atChoice, choice, ids.Player1Id))
		);
	}

	/// <summary>
	/// A board both players share, so creature/power/race terms cancel and only the choice moves
	/// the score. Library filler so drawing works and decking cannot decide anything.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) Position(int mana, int libraryFiller = 20)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		foreach (
			var (pid, lib, bf) in new[]
			{
				(ids.Player1Id, ids.Player1LibraryId, ids.Player1BattlefieldId),
				(ids.Player2Id, ids.Player2LibraryId, ids.Player2BattlefieldId),
			}
		)
		{
			var player = state.GetPlayer(pid);
			state = state.UpdateObject(pid, player with { MaxMana = mana, CurrentMana = mana });

			for (var i = 0; i < libraryFiller; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = "F",
						ManaCost = 3,
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: lib
				);

			var bear = CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2).Build();
			var body = bear.GetComponent<CreatureComponent>()!;
			bear = (Card)bear.WithComponentReplaced(body with { HasSummoningSickness = false });
			(state, _) = state.AddObject(
				bear with
				{
					OwnerId = pid,
					ControllerId = pid,
				},
				parentId: bf
			);
		}

		return (state, ids);
	}
}
