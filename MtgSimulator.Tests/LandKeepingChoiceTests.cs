using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Three lands in play, several four-drops in hand, and one land left to draw into them.
/// Which card does the AI pitch?
///
/// The land is the only card in that hand that does anything, and the four-drops are redundant with
/// each other — you can cast at most one per turn even after the land lands. So keeping the land is
/// the play, and pitching a spare four-drop costs almost nothing.
///
/// **A land in hand is priced at exactly ZERO in all three places that value a hand**, which is
/// what makes this worth testing rather than assuming:
///
/// | Where | Treatment of a land in hand |
/// |---|---|
/// | `WeightedStateEvaluator` hand term | `CountNonLand` excludes it — deliberate, see `AiLandDropTests` |
/// | `CardValueSandbox` | skipped entirely, recorded as `NotMeasured = "land"` |
/// | `CardValueTable.Discounted` | returns 0 for anything with the Land subtype |
///
/// `LandDiscardCostTests` already documents the same pricing hole for discard as a *cost*
/// (Molten Vortex charges a land, which the evaluator prices at +0.000 against the +2.000 that
/// land was worth played). This is the *choice* surface of the same defect, and it is the one
/// place card values could plausibly make things WORSE: every non-land in hand carries a positive
/// value and the land carries zero, so maximising hand value means pitching the land.
///
/// Only the rollout pushes back, by playing the land for `ManaWeight`.
/// </summary>
[TestFixture]
public class LandKeepingChoiceTests
{
	/// <summary>Real four-drops, so the values are the ones the sandbox actually measured.</summary>
	private static readonly string[] FourDrops =
	[
		"Master of the Wild Hunt",
		"Overrun",
		"Chandra's Outrage",
	];

	private static Card Plains(int ownerId) =>
		new()
		{
			Name = "Plains",
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableHashSet.Create("Land"),
		};

	[Test]
	public void WithThreeLandsAndFourDrops_WhatDoesItPitch()
	{
		TestContext.Out.WriteLine("3 lands in play, three 4-drops + 1 land in hand:\n");

		var withoutValues = Pitched(null);
		var withValues = Pitched(CardValueTable.TryLoad(CoresetCube.Set.Code, weight: 0.5f));

		TestContext.Out.WriteLine($"  rollout alone   pitched: {withoutValues}");
		TestContext.Out.WriteLine($"  with values     pitched: {withValues}");

		Assert.Multiple(() =>
		{
			Assert.That(
				withoutValues,
				Is.Not.EqualTo("Plains"),
				"the land is the only card that unlocks the rest of the hand; pitching it with "
					+ "three redundant four-drops held is the worst option available"
			);
			Assert.That(
				withValues,
				Is.Not.EqualTo("Plains"),
				"card values price a land at 0 and every other card above it, so maximising hand "
					+ "value means pitching the land — if this fails, the feature has made a "
					+ "known pricing hole actively worse"
			);
		});
	}

	/// <summary>
	/// At what weight does the anti-land bias actually win?
	///
	/// The bias is structural, not incidental: a land contributes 0 to `HandValue` and every spell
	/// contributes positively, so **pitching the land always maximises hand value**. Only the
	/// rollout argues the other way, by playing that land for `ManaWeight`. The shipped weight is
	/// 0.5, so this measures how much headroom that leaves before the feature starts throwing away
	/// land drops — the exact defect `AiLandDropTests` exists to prevent.
	/// </summary>
	[Test]
	public void HowMuchHeadroomBeforeTheAntiLandBiasWins()
	{
		var full = CardValueSandbox.Lookup(
			CardValueSandbox.Load(CardValueSandbox.PathFor(CoresetCube.Set.Code)),
			underPressure: true
		);

		string? flipped = null;
		foreach (var w in new[] { 0.25f, 0.5f, 1.0f, 1.5f, 2.0f, 3.0f })
		{
			var pitched = Pitched(new CardValueTable(full, weight: w), quiet: true);
			TestContext.Out.WriteLine($"  weight {w, 4:0.##}  pitched {pitched}");
			if (pitched == "Plains" && flipped == null)
				flipped = $"{w:0.##}";
		}

		TestContext.Out.WriteLine(
			flipped == null
				? "  never pitches the land in this weight range"
				: $"  first pitches the land at weight {flipped} (shipped: 0.5)"
		);
		// Was an open margin: before lands were priced through what they unlock, the AI began
		// pitching the land at weight 1.5 against a shipped 0.5 — 3x of headroom on the defect
		// AiLandDropTests exists to prevent. Now it holds across the whole range, so this is an
		// assertion rather than a characterisation.
		Assert.That(
			flipped,
			Is.Null,
			$"the anti-land bias returned at weight {flipped}; TopdeckDiscount is what keeps a "
				+ "land in hand worth what it unlocks, and without it pitching the land always "
				+ "maximises hand value"
		);
	}

	/// <summary>
	/// **The inverse, and the half that stops the fix from being a bias.** One land later the
	/// four-drops are castable, so a further land unlocks nothing and is genuinely the most
	/// expendable card in hand. A rule that simply never pitches lands would pass every other test
	/// in this file and fail this one.
	/// </summary>
	[Test]
	public void WithFourLands_TheSurplusLandIsThePitch()
	{
		var pitched = Pitched(CardValueTable.TryLoad(CoresetCube.Set.Code, weight: 0.5f), lands: 4);
		TestContext.Out.WriteLine($"4 lands in play, hand of four-drops — pitched: {pitched}");
		Assert.That(
			pitched,
			Is.EqualTo("Plains"),
			"with every card in hand already castable the extra land does nothing; keeping it over "
				+ "a real card would mean the land fix had become a blanket bias"
		);
	}

	/// <summary>
	/// The same question as a TUTOR rather than a discard: offered a land or a spell, which is
	/// fetched? This is the shape the user described — "my fifth land" is a choice about what to
	/// take, not what to throw away, and it must answer differently depending on the board.
	/// </summary>
	[TestCase(3, "Plains", TestName = "Tutor_ShortOnLands_FetchesTheLand")]
	[TestCase(5, "Chandra's Outrage", TestName = "Tutor_Flooded_FetchesTheSpell")]
	public void TutorChoosesBetweenALandAndASpell(int lands, string expected)
	{
		var (state, ids) = Position(lands);

		// Library holds exactly the two candidates, so the tutor's options are the question itself.
		(state, _) = state.AddObject(Plains(ids.Player1Id), ids.Player1LibraryId);
		(state, _) = state.AddObject(
			CoresetCube.Set.Cards.Single(c => c.Name == "Chandra's Outrage") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			ids.Player1LibraryId
		);

		// A hand of four-drops is what makes the answer flip: at three lands they are stranded and
		// the land is the unlock; at five they are all castable and the land is surplus.
		foreach (var fourDrop in FourDrops)
			(state, _) = state.AddObject(
				CoresetCube.Set.Cards.Single(c => c.Name == fourDrop) with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				ids.Player1HandId
			);

		var spell = CardFactory.Spell("Seek", manaCost: 0).WithSearchLibrary().Build();
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

		Assert.That(atChoice.IsWaitingForChoice, Is.True, "the search should raise a choice");
		var choice = atChoice.GetPendingChoice()!;

		var offered = choice
			.Options.Where(o => atChoice.HasObject(o.Id))
			.Select(o => atChoice.GetObject(o.Id))
			.OfType<Card>()
			.Select(c => c.Name)
			.ToList();
		Assert.That(
			offered,
			Is.EquivalentTo(new[] { "Plains", "Chandra's Outrage" }),
			"the tutor must be choosing between exactly the land and the spell"
		);

		var ai = new MultiTurnBeamSearchAiStrategy(
			ids,
			rng: new Random(7),
			cardValues: CardValueTable.TryLoad(CoresetCube.Set.Code, weight: 0.5f)
		);
		var chosen = ai.ResolveChoice(atChoice, choice, ids.Player1Id);
		var name =
			chosen.Count > 0 && atChoice.GetObject(chosen[0]) is Card c2 ? c2.Name : "(none)";

		TestContext.Out.WriteLine($"{lands} lands, hand of four-drops — tutored: {name}");
		Assert.That(name, Is.EqualTo(expected));
	}

	/// <summary>
	/// Both players at <paramref name="lands"/> mana, empty libraries. **No filler**, because a
	/// tutor offers every card in the library — twenty blanks would bury the two cards under test,
	/// which is exactly how the scry scenario in ChoiceAccuracyTests silently measured nothing.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) Position(int lands)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		foreach (var pid in new[] { ids.Player1Id, ids.Player2Id })
		{
			var player = state.GetPlayer(pid);
			state = state.UpdateObject(pid, player with { MaxMana = lands, CurrentMana = lands });
		}

		return (state, ids);
	}

	private static string Pitched(CardValueTable? table, int lands = 3, bool quiet = false)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		foreach (var pid in new[] { ids.Player1Id, ids.Player2Id })
		{
			var player = state.GetPlayer(pid);
			state = state.UpdateObject(pid, player with { MaxMana = lands, CurrentMana = lands });

			for (var i = 0; i < 20; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = "F",
						ManaCost = 3,
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: state.GetPlayerZoneId(pid, ZoneType.Library)
				);
		}

		foreach (var name in FourDrops)
			(state, _) = state.AddObject(
				CoresetCube.Set.Cards.Single(c => c.Name == name) with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				ids.Player1HandId
			);

		(state, _) = state.AddObject(Plains(ids.Player1Id), ids.Player1HandId);

		var spell = CardFactory.Spell("Pitch", manaCost: 0).WithDiscard().Build();
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

		Assert.That(atChoice.IsWaitingForChoice, Is.True, "discard should pause the pipeline");
		var choice = atChoice.GetPendingChoice()!;

		var offered = choice
			.Options.Where(o => atChoice.HasObject(o.Id))
			.Select(o => atChoice.GetObject(o.Id))
			.OfType<Card>()
			.Select(c => c.Name)
			.ToList();
		Assert.That(offered, Does.Contain("Plains"), "the land must be a legal discard here");

		var ai = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(7), cardValues: table);

		// Printed so the answer is legible rather than merely correct: the gap between pitching
		// the land and pitching a spare four-drop is what says whether this is being reasoned
		// about or landed on by accident.
		foreach (var option in quiet ? [] : choice.Options)
		{
			var (after, _) = atChoice.ResolveChoice(ImmutableList.Create(option.Id));
			var name = atChoice.GetObject(option.Id) is Card c ? c.Name : "?";
			var rollout = ai.ScoreAfterCompletingTurn(after, ids.Player1Id);
			var hand = table?.HandValue(after, ids.Player1Id) ?? 0f;
			TestContext.Out.WriteLine(
				$"      pitch {name, -24} rollout {rollout, 8:F2}  hand {hand, 7:F2}  total {rollout + hand, 8:F2}"
			);
		}

		var chosen = ai.ResolveChoice(atChoice, choice, ids.Player1Id);

		return chosen.Count > 0 && atChoice.GetObject(chosen[0]) is Card card
			? card.Name
			: "(none)";
	}
}
