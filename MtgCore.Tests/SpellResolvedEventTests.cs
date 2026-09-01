using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// **`SpellResolvedEvent` was declared and emitted by nobody.** No instant or sorcery in the game
/// had ever announced that it resolved — the event record, its `CardId` field and every consumer
/// that might have read it were all in place around a hole.
///
/// It was found by an execution metric that reads the event log (`MtgSimulator/EngineProbe.cs`):
/// every permanent payoff registered and every spell payoff read exactly zero, which is
/// indistinguishable from a broken metric. That is the sixth instance of this project's recurring
/// event bug and the first where nothing was staged at all.
///
/// Pinned here rather than through the simulator because a dead event is silent everywhere: the
/// engine behaves identically with and without it, so only an assertion on the event list can say
/// whether it exists.
/// </summary>
[TestFixture]
public class SpellResolvedEventTests
{
	private static (GameState State, MtgGameIds Ids, int CardId) Fixture(string name)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		var spell = new Card
		{
			Name = name,
			ManaCost = 1,
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new DrawCardsAction
							{
								Amount = 1,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
						}
					),
				}
			),
		};

		var (withCard, added) = state.AddObject(spell, parentId: ids.Player1HandId);
		return (withCard, ids, added.Id);
	}

	[Test]
	public void ASpellThatResolves_AnnouncesThatItResolved()
	{
		var (state, ids, cardId) = Fixture("Test Cantrip");

		var (_, events) = state
			.AddAction(
				new CastSpellAction
				{
					CardId = cardId,
					CastingPlayerId = ids.Player1Id,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				}
			)
			.ProcessAllActions();

		Assert.That(
			events.OfType<SpellResolvedEvent>().Select(e => e.CardId),
			Does.Contain(cardId),
			"casting a spell must produce a SpellResolvedEvent naming it"
		);
	}

	[Test]
	public void ASpellSittingInHand_AnnouncesNothing()
	{
		// The negative half. Without it this suite would pass on an implementation that emits the
		// event unconditionally, which measures nothing — the trap this project has hit three
		// times. The card is never cast, so nothing may be announced about it.
		var (state, _, cardId) = Fixture("Test Cantrip");

		var (_, events) = state.ProcessAllActions();

		Assert.That(
			events.OfType<SpellResolvedEvent>().Select(e => e.CardId),
			Does.Not.Contain(cardId)
		);
	}
}
