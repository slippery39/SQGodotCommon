using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Extrapolate the Impossible (story LIM-1.6): "You may reveal exactly two cards you own with different
/// names from outside the game. An opponent chooses one of them. You put that card into your hand."
/// Outside the game is the sideboard — sealed's unplayed pool.
/// </summary>
public class FraOutsideTheGameTests : FraBoardTest
{
	static readonly CardDefinition Bear = TestGames.Creature("Bear", "2/2");
	static readonly CardDefinition Elf = TestGames.Creature("Elf", "1/1");

	List<Card> Outside(int player) => [.. s.CardsIn(player, ZoneType.Sideboard)];

	[Test]
	public void TwoAreRevealed_TheOpponentChoosesOne_ItGoesToYourHand()
	{
		s = s.WithSideboard(me, [Bear, Elf]);
		var (bear, elf) = (Outside(me)[0].Id, Outside(me)[1].Id);
		var chooser = 0;
		var extrapolate = Hand("Extrapolate the Impossible", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = extrapolate });
		Resolve(choice =>
			choice switch
			{
				YesNoChoice => [1],
				ChooseAmongChoice { MinChoices: 2 } => [bear, elf],
				ChooseAmongChoice c => Picked(c, elf),
				_ => [],
			}
		);
		IEnumerable<int> Picked(ChooseAmongChoice c, int pick)
		{
			chooser = c.GetDecidingPlayerId(s, c.InputContext);
			return [pick];
		}

		Assert.That(chooser, Is.EqualTo(them), "an OPPONENT chooses");
		Assert.That(s.IsIn(elf, me, ZoneType.Hand));
		Assert.That(s.IsIn(bear, me, ZoneType.Sideboard), "the other stays outside the game");
		Assert.That(s.GetCard(bear).KnownTo, Is.EqualTo(Knowers.Everyone), "it was revealed");
	}

	[Test]
	public void TheTwoHaveDifferentNames_OneOptionPerName()
	{
		s = s.WithSideboard(me, [Bear, Bear, Elf]);
		var offered = new List<string>();
		var extrapolate = Hand("Extrapolate the Impossible", me); // its own line: the test trap
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = extrapolate });
		Resolve(choice =>
		{
			if (choice is ChooseAmongChoice { MinChoices: 2 } c)
				offered.AddRange(c.Options.Select(o => o.DisplayText));
			return choice is YesNoChoice ? [1] : choice.Options.Take(choice.MinChoices).Select(o => o.Id);
		});
		Assert.That(offered, Is.EquivalentTo(new[] { "Bear", "Elf" }));
	}

	[Test]
	public void TheAi_RevealsItsPriciestTwo()
	{
		var cheap = TestGames.CreatureCosting("{1}", "Cheap", "1/1");
		var big = TestGames.CreatureCosting("{5}", "Big", "5/5");
		var bigger = TestGames.CreatureCosting("{6}", "Bigger", "6/6");
		s = s.WithSideboard(me, [cheap, big, bigger]);
		var extrapolate = Hand("Extrapolate the Impossible", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = extrapolate });
		var ai = new MagicCore.Ai.GreedyAi(me);
		IEnumerable<int>? revealed = null;
		Resolve(choice =>
			choice switch
			{
				YesNoChoice => [1],
				ChooseAmongChoice { MinChoices: 2 } c => revealed = [.. ai.Answer(s, c)],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			revealed!.Select(id => s.GetCard(id).Name),
			Is.EquivalentTo(new[] { "Big", "Bigger" })
		);
	}

	[Test]
	public void OnlyTheSideboardMayBeMissing_AnyOtherZoneStillThrows()
	{
		Assert.That(s.CardsIn(me, ZoneType.Sideboard), Is.Empty);
		Assert.Throws<KeyNotFoundException>(() => s.CardsIn(int.MaxValue, ZoneType.Hand).ToList());
	}

	[Test]
	public void FewerThanTwoNamesOutside_NothingIsAsked()
	{
		s = s.WithSideboard(me, [Bear, Bear]);
		var asked = 0;
		var extrapolate = Hand("Extrapolate the Impossible", me); // its own line: the test trap
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = extrapolate });
		Resolve(choice =>
		{
			asked++;
			return [];
		});
		Assert.That(asked, Is.Zero);
		Assert.That(Outside(me), Has.Count.EqualTo(2));
	}

	[Test]
	public void AGameWithoutASideboard_HasNoZone_AndKeepsEveryIdItHad()
	{
		var plain = MagicRules.NewGame(TestGames.Deck(), TestGames.Deck(), seed: 3);
		var with = MagicRules.NewGame(TestGames.Deck(), TestGames.Deck(), seed: 3, sideboard1: [Bear, Elf]);

		Assert.That(plain.CardsIn(plain.Player1Id(), ZoneType.Sideboard), Is.Empty);
		Assert.That(with.CardsIn(with.Player1Id(), ZoneType.Sideboard).Count(), Is.EqualTo(2));
		foreach (var (id, obj) in plain.IdToGameObjectMap)
			Assert.That(with.GetObject(id).Name, Is.EqualTo(obj.Name), $"object {id} moved");
		Assert.That(
			with.CardsIn(with.Player1Id(), ZoneType.Hand).Select(c => c.Id),
			Is.EqualTo(plain.CardsIn(plain.Player1Id(), ZoneType.Hand).Select(c => c.Id)),
			"the same shuffles"
		);
	}
}
