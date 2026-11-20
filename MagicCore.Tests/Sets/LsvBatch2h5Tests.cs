using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h5, LSVPower — Auras that return a creature from a graveyard (Animate Dead, Necromancy): an Aura attached to
/// a graveyard card, its enchant changed for good (ModifyUntilEndOfTurn.Enchant), and Necromancy's flash with the
/// cleanup sacrifice (CastAsThoughItHadFlash).
/// </summary>
public class LsvBatch2h5Tests : FraBoardTest
{
	static CardDefinition Ogre => TestGames.Creature("Ogre", "3/3");

	static CardDefinition Disenchant =>
		TestCards.Kill(new TargetSpec(From.Battlefield, global::MagicCore.Filters.Is.Enchantment));

	void ToMyEndStep() =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.End && x.GetGame().PriorityPlayerId == me
		);

	/// <summary>To their turn, with my priority in it — the cleanup behind us.</summary>
	void ToTheirTurn() =>
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().PriorityPlayerId == me
		);

	[Test]
	public void AnimateDead_AnOpponentsCreatureCard_ReturnsUnderYourControl_Enchanted_MinusOneZero()
	{
		var ogre = Put(Ogre, them, ZoneType.Graveyard);
		var animate = Hand(Lsv("Animate Dead"), me);
		Assert.That(
			Casts(animate).Select(c => c.Targets.Single()),
			Nu.EqualTo(new[] { ogre }),
			"enchant creature card in a graveyard — any graveyard"
		);
		Cast(animate, [], ogre);
		Assert.That(Alive(ogre) && s.GetCard(ogre).ControllerId == me, "under your control");
		Assert.That(
			Attachments.HostOf(s, s.GetCard(animate))?.Id,
			Nu.EqualTo(ogre),
			"attached to it"
		);
		Assert.That(
			(s.Power(s.GetCard(ogre)), s.Toughness(s.GetCard(ogre))),
			Nu.EqualTo((2, 3)),
			"enchanted creature gets -1/-0"
		);
	}

	[Test]
	public void AnimateDead_LeavesTheBattlefield_ThatCreaturesControllerSacrificesIt()
	{
		var ogre = Put(Ogre, them, ZoneType.Graveyard);
		var animate = Hand(Lsv("Animate Dead"), me);
		Cast(animate, [], ogre);
		var kill = Hand(Disenchant, me);
		Cast(kill, [], animate);
		Assert.That(InGraveyard(ogre), "sacrificed — to its owner's graveyard");
	}

	[Test]
	public void AnimateDead_RemovedWithItsTriggerOnTheStack_ReturnsNothing()
	{
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		var animate = Hand(Lsv("Animate Dead"), me);
		var kill = Hand(Disenchant, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = animate,
				Targets = [ogre],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new PassPriorityAction { PlayerId = them }); // it enters on the graveyard card; its trigger goes up
		Assert.That(Alive(animate), "an Aura on a graveyard card isn't binned by the state check");
		Assert.That(
			Attachments.HostOf(s, s.GetCard(animate)),
			Nu.Null,
			"a front end asks for permanents only: the table draws it on the battlefield"
		);
		Cast(kill, [], animate);
		Assert.That(InGraveyard(ogre), "\"if it's on the battlefield\" — it isn't");
	}

	[Test]
	public void Necromancy_CastAsASorcery_ReturnsTheCreature_AndItStays()
	{
		var ogre = Put(Ogre, them, ZoneType.Graveyard);
		var necromancy = Hand(Lsv("Necromancy"), me);
		CastWithTrigger(necromancy, [], ogre);
		Assert.That(Alive(ogre) && s.GetCard(ogre).ControllerId == me, "under your control");
		Assert.That(
			Attachments.HostOf(s, s.GetCard(necromancy))?.Id,
			Nu.EqualTo(ogre),
			"it became an Aura on it"
		);
		Assert.That(
			s.HasSubtype(s.GetCard(necromancy), "Aura"),
			"an Aura — a non-Aura filter misses it"
		);
		ToTheirTurn();
		Assert.That(
			Alive(ogre) && Alive(necromancy),
			"no cleanup sacrifice for a sorcery-time cast"
		);
		var kill = Hand(Disenchant, me);
		Cast(kill, [], necromancy);
		Assert.That(InGraveyard(ogre), "when it leaves, that creature's controller sacrifices it");
	}

	[Test]
	public void Necromancy_CastAtInstantSpeed_SacrificedAtTheNextCleanup_AndTheCreatureWithIt()
	{
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		var necromancy = Hand(Lsv("Necromancy"), me);
		ToMyEndStep();
		Assert.That(Casts(necromancy), Nu.Not.Empty, "as though it had flash");
		CastWithTrigger(necromancy, [], ogre);
		Assert.That(Alive(ogre), "it returns in the end step");
		ToTheirTurn();
		Assert.That(
			InGraveyard(necromancy),
			"sacrificed at the beginning of the next cleanup step"
		);
		Assert.That(InGraveyard(ogre), "and its leaving takes the creature");
	}

	[Test]
	public void Necromancy_NoCreatureCard_StaysAPlainEnchantment()
	{
		var necromancy = Hand(Lsv("Necromancy"), me);
		Cast(necromancy, []);
		Assert.That(
			Alive(necromancy),
			"no target: the trigger is removed, and it never became an Aura"
		);
		Assert.That(Attachments.EnchantOf(s.GetCard(necromancy)), Nu.Null);
	}

	[Test]
	public void Necromancy_ItsCreatureExiledInstead_TheAuraWithNothingToEnchant_GoesToTheGraveyard()
	{
		Field(Lsv("Containment Priest"), them);
		var ogre = Put(Ogre, me, ZoneType.Graveyard);
		var necromancy = Hand(Lsv("Necromancy"), me);
		CastWithTrigger(necromancy, [], ogre);
		Assert.That(InExile(ogre), "a nontoken creature that wasn't cast is exiled instead");
		Assert.That(
			InGraveyard(necromancy),
			"an Aura now, attached to nothing — the state check takes it"
		);
	}
}
