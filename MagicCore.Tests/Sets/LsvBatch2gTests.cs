using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4g, LSVPower — blue's tier 3: each card's ability FIRES, against its PRINTED text, through real casting and
/// the real stack. g1: protection (CR 702.16) — True-Name Nemesis.
/// </summary>
public class LsvBatch2gTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	/// <summary>"{1} sorcery: 2 damage to each creature" — damage that targets nothing.</summary>
	static CardDefinition Pyroclasm =>
		new(
			TestGames.Inline("Pyroclasm", "{1}", "Sorcery").Data,
			[new SpellEffectsComponent([new DealDamage(Ref.AllCreatures, 2)])]
		);

	/// <summary>I cast True-Name Nemesis and choose a player: "An opponent" (option 1) or "You" (option 2).</summary>
	int Nemesis(bool opponent)
	{
		var nemesis = Hand(Lsv("True-Name Nemesis"), me);
		var asked = CastAsking(nemesis, On<ChooseKeywordChoice>(opponent ? 1 : 2));
		Assert.That(asked.OfType<ChooseKeywordChoice>().Count(), Nu.EqualTo(1), "choose a player");
		Assert.That(
			s.GetCard(nemesis).GetComponent<ChosenPlayer>()?.PlayerId,
			Nu.EqualTo(opponent ? them : me)
		);
		return nemesis;
	}

	[Test]
	public void TrueNameNemesis_TheChosenPlayersSpellsCantTargetIt()
	{
		var nemesis = Nemesis(opponent: true);
		var theirs = Hand(TestCards.Kill(), them);
		var mine = Hand(TestCards.Kill(), me);

		Assert.That(
			TargetSpec.Creature.IsLegal(s, them, theirs, nemesis),
			Nu.False,
			"protection from them: not a legal target for their spell"
		);
		Assert.That(TargetSpec.Creature.IsLegal(s, me, mine, nemesis), "my own spell still can");
		Assert.That(
			TargetSpec.Creature.IsLegal(s, me, theirs, nemesis),
			"their card cast by ME (Robber of the Rich) is my spell — still in their hand as it is cast"
		);
	}

	[Test]
	public void TrueNameNemesis_DamageFromTheChosenPlayersSourcesIsPrevented()
	{
		var nemesis = Nemesis(opponent: false); // protection from ME: my own Pyroclasm
		var bear = Field(Bear, me);

		Cast(Hand(Pyroclasm, me), []);

		Assert.That(s.GetCard(nemesis).Damage, Nu.EqualTo(0), "no damage marked");
		Assert.That(Alive(nemesis));
		Assert.That(Alive(bear), Nu.False, "a 2/2 without protection dies");
	}

	[Test]
	public void TrueNameNemesis_CantBeBlockedByTheChosenPlayersCreatures()
	{
		var nemesis = Nemesis(opponent: true);
		var other = Field(Bear, me);
		var blocker = Field(Bear, them);

		Assert.That(
			Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(nemesis)),
			Nu.False,
			"their creature can't block it"
		);
		Assert.That(Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(other)), "another, it can");
	}

	/// <summary>TEST SETUP: attached as they were before it gained protection (Attach itself refuses).</summary>
	void AttachedTo(int attachment, int host) =>
		s = s.UpdateObject(
			attachment,
			s.GetCard(attachment) with
			{
				AttachedToId = host,
				AttachedToIncarnation = s.GetCard(host).Incarnation,
			}
		);

	[Test]
	public void TrueNameNemesis_TheChosenPlayersAurasAndEquipmentFallOff()
	{
		var nemesis = Nemesis(opponent: false); // protection from ME: my own attachments
		var kitesail = Field(CardRegistry.Get("Medic's Kitesail"), me);
		var aura = Field(CardRegistry.Get("De-evolve"), me);
		AttachedTo(kitesail, nemesis);
		AttachedTo(aura, nemesis);

		s = StateBasedActions.Check(s);

		Assert.That(s.GetCard(kitesail).AttachedToId, Nu.EqualTo(0), "the Equipment falls off");
		Assert.That(InGraveyard(aura), "the Aura has no legal host: it dies");
		Assert.That(Alive(kitesail));
	}

	[Test]
	public void TrueNameNemesis_AnAttachToItDoesNothing()
	{
		var nemesis = Nemesis(opponent: false);
		var kitesail = Field(CardRegistry.Get("Medic's Kitesail"), me);
		var bear = Field(Bear, me);
		s = Attachments.Attach(s, kitesail, bear);

		s = Attachments.Attach(s, kitesail, nemesis); // Cori-Steel Cutter's "attach it to" — not targeting

		Assert.That(
			s.GetCard(kitesail).AttachedToId,
			Nu.EqualTo(bear),
			"CR 701.3b: it stays where it was"
		);
	}
}
