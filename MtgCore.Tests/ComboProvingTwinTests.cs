using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// **The untap/copy loop, asserted as a mechanism rather than through a game.**
///
/// The pieces are real set cards here, not inline definitions, and that is the deliberate exception
/// to the inline-cards rule: what is under test IS whether these particular printed cards combo, so
/// a test built on stand-ins would pass while the shipped cards did nothing. Nothing here asserts a
/// rate, so card balance cannot break it.
/// </summary>
[TestFixture]
public class ComboProvingTwinTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	/// <summary>
	/// **The control, and it has to copy something INERT.** The copier alone is not repeatable: it
	/// exhausts itself and nothing readies it. Copying an untapper is what closes the loop, so this
	/// uses a plain Illusionist with no trigger — otherwise the "combo" could just be the copier
	/// being repeatable on its own, and the pair test would prove nothing about the interaction.
	///
	/// The vanilla Illusionist is inline because no such card ships in the package; every printed
	/// one is an untapper, which is the point.
	/// </summary>
	[Test]
	public void TheCopierAloneCannotRepeat()
	{
		var (s, copier) = Play(_state, Card("Twinflame Artisan"));
		var (s2, inert) = Play(
			s,
			Cards
				.Builders.CardFactory.Creature(
					"Practice Dummy",
					manaCost: 1,
					power: 1,
					toughness: 1
				)
				.WithSubtype(ComboProving.Illusionist)
				.Build()
		);

		var after = Activate(s2, copier.Id, inert.Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				CountNamed(after, "Practice Dummy"),
				Is.EqualTo(2),
				"exactly one copy was made"
			);
			Assert.That(
				IsExhausted(after, copier.Id),
				Is.True,
				"and it stays exhausted — copying an inert creature does not close the loop"
			);
		});
	}

	/// <summary>
	/// **The combo.** The token copy carries the original's ETB trigger, so it readies the copier
	/// that made it — and the copier can immediately go again.
	/// </summary>
	[Test]
	public void TheTokenCopyReadiesTheCopierThatMadeIt()
	{
		var (s, copier) = Play(_state, Card("Twinflame Artisan"));
		var (s2, deceiver) = Play(s, Card("Mirevale Deceiver"));

		var after = Activate(s2, copier.Id, deceiver.Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				CountNamed(after, "Mirevale Deceiver"),
				Is.EqualTo(2),
				"the copy was created"
			);
			Assert.That(
				IsExhausted(after, copier.Id),
				Is.False,
				"and its enters-the-battlefield trigger readied the copier — this is the loop"
			);
		});
	}

	/// <summary>
	/// Turning the loop over three times, which is what separates "the trigger fired once" from
	/// "this is repeatable". Each pass must add a body and leave the copier ready again.
	/// </summary>
	[Test]
	public void TheLoopTurnsOverRepeatedly()
	{
		var (s, copier) = Play(_state, Card("Twinflame Artisan"));
		var (s2, deceiver) = Play(s, Card("Mirevale Deceiver"));
		var state = s2;

		for (var i = 1; i <= 3; i++)
		{
			Assert.That(IsExhausted(state, copier.Id), Is.False, $"ready before iteration {i}");
			state = Activate(state, copier.Id, deceiver.Id);
			Assert.That(
				CountNamed(state, "Mirevale Deceiver"),
				Is.EqualTo(1 + i),
				$"iteration {i} added a body"
			);
		}

		Assert.That(
			IsExhausted(state, copier.Id),
			Is.False,
			"and it is ready to go again — the loop is unbounded"
		);
	}

	/// <summary>
	/// The copies must arrive able to ACT, or the loop produces a board that does nothing until
	/// next turn and is not a win condition. Haste is granted by CreateTokenCopyAction; the
	/// exhausted state of the original must not carry across either.
	/// </summary>
	[Test]
	public void CopiesArriveHastyAndReady()
	{
		var (s, copier) = Play(_state, Card("Kilnmother Vess"));
		var (s2, sprite) = Play(s, Card("Tidebinder Sprite"));

		// Exhaust the ORIGINAL, so a naive copy would inherit a tapped body.
		var exhausted = Exhaust(s2, sprite.Id);
		var after = Activate(exhausted, copier.Id, sprite.Id);

		var token = after
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Single(c => c.Name == "Tidebinder Sprite" && c.Id != sprite.Id);
		var creature = token.GetComponent<CreatureComponent>()!;

		Assert.Multiple(() =>
		{
			Assert.That(creature.HasHaste, Is.True, "the token can attack immediately");
			Assert.That(creature.IsExhausted, Is.False, "it did not inherit the original's tap");
			Assert.That(creature.HasFlying, Is.True, "and it is a real copy — flying carried over");
		});
	}

	// ===== helpers =====

	private static bool IsExhausted(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<CreatureComponent>()!.IsExhausted;

	private int CountNamed(GameState state, string name) =>
		state.GetCardsInZone(_ids.Player1BattlefieldId).Count(c => c.Name == name);

	private (GameState, Card) Play(GameState state, Card card)
	{
		var (next, placed) = state.AddObject(
			card with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: _ids.Player1BattlefieldId
		);

		// Straight onto the battlefield, so clear the sickness a cast would have worn off by now.
		var placedCard = (Card)next.GetObject(placed.Id);
		var creature = placedCard.GetComponent<CreatureComponent>();
		if (creature is not null)
			next = next.UpdateObject(
				placed.Id,
				placedCard.WithComponentReplaced(creature with { HasSummoningSickness = false })
			);

		return (next, (Card)next.GetObject(placed.Id));
	}

	private static GameState Exhaust(GameState state, int cardId)
	{
		var (next, _) = state
			.AddAction(new ExhaustCreatureAction { TargetIds = ImmutableList.Create(cardId) })
			.ProcessAllActions();
		return next;
	}

	private GameState Activate(GameState state, int sourceId, int targetId)
	{
		var (next, _) = state
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = sourceId,
					AbilityIndex = 0,
					ActivatingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableList.Create(targetId),
				}
			)
			.ProcessAllActions();
		return next;
	}
}
