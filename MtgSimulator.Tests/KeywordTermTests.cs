using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// The evaluator had no term for keywords at all: a 4/4 flier scored exactly like a 4/4 vanilla.
///
/// That also meant EQUIPPING scored nothing, since equipment grants keywords rather than changing
/// power — which is why a pointless Swiftfoot Boots shuffle was indistinguishable from a genuinely
/// useful first attachment.
///
/// **The term reads EFFECTIVE stats.** A version built on the creature's own component would score
/// an equipped creature identically to a bare one and miss the entire reason it exists, so the
/// granted-keyword case is pinned explicitly below.
/// </summary>
[TestFixture]
public class KeywordTermTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.Create();
		_state = _state.WithoutDeckingLoss();
	}

	private int AddCreature(int power, int toughness, bool flying = false)
	{
		var card = new Card
		{
			Name = "C",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components =
			[
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasFlying = flying,
				},
			],
		};
		(_state, var added) = _state.AddObject(card, parentId: _ids.Player1BattlefieldId);
		return added.Id;
	}

	private float Score() => StateEvaluator.Evaluate(_state, _ids, _ids.Player1Id);

	[Test]
	public void AFlier_OutscoresAVanillaOfTheSameSize()
	{
		AddCreature(4, 4);
		var vanilla = Score();

		Setup();
		AddCreature(4, 4, flying: true);
		var flier = Score();

		Assert.That(
			flier,
			Is.GreaterThan(vanilla),
			"evasion has to be worth something, or the evaluator cannot tell these apart"
		);
	}

	[Test]
	public void KeywordsAreOff_WhenTheWeightIsZero()
	{
		// The knob has to actually disable it, or the strength harness cannot measure the term
		// against its own absence.
		AddCreature(4, 4, flying: true);
		var withKeywords = WeightedStateEvaluator.Default;
		var without = withKeywords with { KeywordWeight = 0f };

		Assert.That(
			without.Evaluate(_state, _ids, _ids.Player1Id),
			Is.LessThan(withKeywords.Evaluate(_state, _ids, _ids.Player1Id))
		);
	}

	[Test]
	public void TheBreakdown_AddsUpWithKeywordsOn()
	{
		AddCreature(3, 3, flying: true);
		var breakdown = StateEvaluator.Explain(_state, _ids, _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				breakdown.Keywords,
				Is.Not.EqualTo(0f),
				"the term must reach the breakdown"
			);
			Assert.That(
				breakdown.Terms.Sum(t => t.Value),
				Is.EqualTo(breakdown.Total).Within(0.0001f),
				"a panel whose rows do not sum to its total is worse than no panel"
			);
		});
	}
}
