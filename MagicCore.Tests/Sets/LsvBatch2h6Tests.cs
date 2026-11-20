using global::MagicCore.Ai;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h6, LSVPower — naming a card as a spell resolves (Demonic Consultation): ChooseCardName keeps the name on the
/// spell on the stack, read by RevealUntilOntoBattlefield { Matching, To = Hand }.
/// </summary>
public class LsvBatch2h6Tests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>Answers the name choice with this name; any other with its minimum.</summary>
	static Func<ChoiceAction, IEnumerable<int>> Naming(string name) =>
		c =>
			c is ChooseKeywordChoice { NamesACard: true } naming
				? [naming.Keywords.IndexOf(name) + 1]
				: c.Options.Take(c.MinChoices).Select(o => o.Id);

	[Test]
	public void DemonicConsultation_ExilesSix_ThenRevealsUntilTheName_IntoTheHand_TheRevealedExiled()
	{
		var below = OnTop(Bear);
		var grisel = OnTop(Lsv("Griselbrand"));
		var revealed = new[] { OnTop(Bear), OnTop(Bear) };
		var six = Enumerable.Range(0, 6).Select(_ => OnTop(Bear)).ToList();
		var consultation = Hand(Lsv("Demonic Consultation"), me);
		var asked = CastAsking(consultation, Naming("Griselbrand"));
		var naming = asked.OfType<ChooseKeywordChoice>().Single(c => c.NamesACard);
		Assert.That(naming.Keywords, Does.Contain("Ancient Tomb"), "a card name — the lands' too");
		Assert.That(
			s.IsIn(grisel, me, ZoneType.Hand) && s.GetCard(grisel).KnownTo == Knowers.Everyone,
			"put into your hand, revealed"
		);
		Assert.That(six.Concat(revealed).All(InExile), "the top six, and the others revealed");
		Assert.That(Library(me).First(), Nu.EqualTo(below), "nothing below it is touched");
		Assert.That(
			InGraveyard(consultation) && !s.GetCard(consultation).HasComponent<ChosenName>(),
			"the name was the spell's while it resolved"
		);
	}

	[Test]
	public void DemonicConsultation_TheNamedCardInTheTopSix_ExilesTheWholeLibrary()
	{
		var bottom = Put(Bear, me, ZoneType.Library);
		var grisel = OnTop(Lsv("Griselbrand"));
		for (var i = 0; i < 5; i++)
			OnTop(Bear);
		var consultation = Hand(Lsv("Demonic Consultation"), me);
		CastAsking(consultation, Naming("Griselbrand"));
		Assert.That(InExile(grisel), "exiled with the six — never found");
		Assert.That(Library(me), Nu.Empty, "revealed until the library ran out, all exiled");
		Assert.That(InExile(bottom));
	}

	[Test]
	public void DemonicConsultation_TheAi_NamesACardInItsOwnLibrary()
	{
		OnTop(Lsv("Griselbrand"));
		OnTop(Lsv("Dark Confidant"), them);
		var consultation = Hand(Lsv("Demonic Consultation"), me);
		var mine = s.CardsIn(me, ZoneType.Library).Select(c => c.Name).ToHashSet();
		string? named = null;
		var asked = CastAsking(
			consultation,
			c =>
			{
				if (c is not ChooseKeywordChoice { NamesACard: true } naming)
					return c.Options.Take(c.MinChoices).Select(o => o.Id);
				var pick = new GreedyAi(me).Answer(s, c).ToList();
				named = naming.Keywords[pick.Single() - 1];
				return pick;
			}
		);
		Assert.That(
			asked.OfType<ChooseKeywordChoice>().Single(c => c.NamesACard).NamesYourOwn,
			"a name to find in your own library"
		);
		Assert.That(mine, Does.Contain(named), "a card of its own");
		Assert.That(named, Nu.Not.EqualTo("Dark Confidant"), "never the opponent's");
	}
}
