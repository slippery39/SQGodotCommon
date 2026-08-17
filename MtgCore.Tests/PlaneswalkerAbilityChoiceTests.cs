using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The engine half of the "I didn't get a choice of which planeswalker ability to activate" bug.
///
/// The scene took legalAbilities[0], so two of every walker's three abilities were unreachable.
/// The chooser itself is Godot UI and cannot be tested here, but what it is fed CAN be: if the
/// generator does not offer all three, no amount of UI work would help.
/// </summary>
[TestFixture]
public class PlaneswalkerAbilityChoiceTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	[Test]
	public void AllThreeLoyaltyAbilities_AreOfferedOnAFreshPlaneswalker()
	{
		// Jace Beleren is +2 / -1 / -10 at 3 loyalty, so the ultimate is correctly unavailable —
		// the other two must both be there.
		var (state, walker) = PutWalkerOnBattlefield(_state, "Jace Beleren");

		var offered = MtgActionGenerator
			.GetLegalActions(state, _ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == walker.Id)
			.Select(a => a.AbilityIndex)
			.Distinct()
			.ToList();

		Assert.That(
			offered,
			Is.EquivalentTo(new[] { 0, 1 }),
			"The +2 and the -1 must both be offered; only the -10 is unaffordable at 3 loyalty"
		);
	}

	[Test]
	public void EveryPlaneswalkerInTheSet_OffersMoreThanOneAbility()
	{
		// If a walker only ever offers one, the chooser never appears and the bug is invisible
		// again — this is the condition that makes the UI fix reachable at all.
		foreach (var template in CoresetCube.Cards.Where(c => c.HasType(CardType.Planeswalker)))
		{
			var (state, walker) = PutWalkerOnBattlefield(_state, template.Name);

			var offered = MtgActionGenerator
				.GetLegalActions(state, _ids.Player1Id)
				.OfType<ActivateAbilityAction>()
				.Where(a => a.CardId == walker.Id)
				.Select(a => a.AbilityIndex)
				.Distinct()
				.Count();

			Assert.That(
				offered,
				Is.GreaterThan(1),
				$"{template.Name} offers only {offered} ability — the chooser would never show"
			);
		}
	}

	[Test]
	public void UsingOneAbility_RemovesAllOfThemForTheTurn()
	{
		// The limit is per WALKER. After using one, the chooser must not appear again.
		var (state, walker) = PutWalkerOnBattlefield(_state, "Jace Beleren");

		var (after, _) = state
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = walker.Id,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
				}
			)
			.ProcessAllActions();

		var stillOffered = MtgActionGenerator
			.GetLegalActions(after, _ids.Player1Id)
			.OfType<ActivateAbilityAction>()
			.Any(a => a.CardId == walker.Id);

		Assert.That(stillOffered, Is.False);
	}

	private (GameState, Card) PutWalkerOnBattlefield(GameState state, string name)
	{
		var template = CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (final, _) = withCard
			.AddAction(
				new CastPermanentAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		return (final, card);
	}
}
