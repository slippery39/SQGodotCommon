using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Sanity checks that the AI prefers playing non-creature permanents over holding them.
///
/// Uses CreateForTesting() (99/99 mana) so mana is never the bottleneck — the only
/// variable is whether playing the card improves the evaluated game state.
/// Each test gives P1 exactly one card in hand so the only choice is CastSpell vs EndTurn.
/// </summary>
[TestFixture]
public class PermanentPlayRateTests
{
	private GameState _state;
	private MtgGameIds _ids;
	private BeamSearchAiStrategy _ai;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
		_ai = new BeamSearchAiStrategy(_ids, rng: new Random(42));
	}

	[Test]
	public void AI_PlaysPhyrexianArena_WhenManaSufficient()
	{
		var arena = CardLibrary.PhyrexianArena() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(arena, parentId: _ids.Player1HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<CastPermanentAction>(),
			"AI should play Phyrexian Arena rather than hold it in hand"
		);
	}

	[Test]
	public void AI_PlaysMox_Immediately()
	{
		var mox = CardLibrary.Mox() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(mox, parentId: _ids.Player1HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<CastPermanentAction>(),
			"AI should play Mox immediately — zero cost with no downside"
		);
	}

	[Test]
	public void AI_PlaysExploration_WhenManaSufficient()
	{
		var exploration = CardLibrary.Exploration() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(_state, _) = _state.AddObject(exploration, parentId: _ids.Player1HandId);

		var action = _ai.SelectAction(_state, _ids, _ids.Player1Id);

		Assert.That(
			action,
			Is.InstanceOf<CastPermanentAction>(),
			"AI should play Exploration rather than hold it in hand"
		);
	}
}
