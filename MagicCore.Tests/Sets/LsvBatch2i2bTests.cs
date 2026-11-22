using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4i2b + i3, LSVPower — red's casting options and an additional combat phase, FIRING against their printed text
/// through real casting and the real stack: Batroc the Leaper's multikicker, dash (Ragavan, Death-Greeter's Champion),
/// backup, and Fear of Missing Out's "attacks for the first time each turn … an additional combat phase".
/// </summary>
public class LsvBatch2i2bTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Peek =>
		TestGames.Inline("Peek", "{1}", "Instant", null, new DrawCards(Ref.You, 1));

	static IEnumerable<int> Defaults(ChoiceAction c) =>
		c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>A 1/5 of theirs: it shows the damage and lives.</summary>
	int Ox(string name) => Field(TestGames.Creature(name, "1/5"), them);

	// ===== Batroc the Leaper: multikicker =====

	List<ChoiceAction> CastBatroc(int kicks, int[] targets)
	{
		var batroc = Hand(Lsv("Batroc the Leaper"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = batroc,
				Replicate = kicks,
			}
		);
		return ResolveAsking(c =>
			c switch
			{
				ChooseTargetChoice t => t.Index < targets.Length ? [targets[t.Index]] : [],
				_ => Defaults(c),
			}
		);
	}

	int Batroc => s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Batroc the Leaper").Id;

	[Test]
	public void Batroc_KickedTwice_TwoCounters_ItsPowerToEachOfUpToTwoTargets()
	{
		var ox = Ox("Ox");
		var lands = UntappedLands();
		var life = Life(them);

		var asked = CastBatroc(2, [ox, them]);

		Assert.That(lands - UntappedLands(), Nu.EqualTo(6), "{1}{R}, and {2} each time");
		Assert.That(
			(s.GetCard(Batroc).PlusOneCounters, s.Power(s.GetCard(Batroc))),
			Nu.EqualTo((2, 4)),
			"a +1/+1 counter for each time he was kicked"
		);
		Assert.That(
			asked.OfType<ChooseTargetChoice>().Count(),
			Nu.EqualTo(2),
			"X targets, X the times kicked — one word at a time"
		);
		Assert.That(
			(s.GetCard(ox).Damage, life - Life(them)),
			Nu.EqualTo((4, 4)),
			"damage equal to his power to each"
		);
	}

	[Test]
	public void Batroc_KickedTwice_UpToX_OneTargetChosen()
	{
		var ox = Ox("Ox");
		var life = Life(them);
		CastBatroc(2, [ox]);
		Assert.That((s.GetCard(ox).Damage, Life(them)), Nu.EqualTo((4, life)), "up to X");
	}

	[Test]
	public void Batroc_Unkicked_A2x2_NoTargets()
	{
		var asked = CastBatroc(0, []);
		Assert.That(
			(s.GetCard(Batroc).PlusOneCounters, asked.OfType<ChooseTargetChoice>().Count()),
			Nu.EqualTo((0, 0))
		);
	}

	[Test]
	public void XTargets_Required_FewerObjectsThanX_TheTriggerIsRemoved_NeverAnUnanswerablePick()
	{
		var tapper = TestGames.Creature("Tapper", "1/1") with
		{
			Components =
			[
				Scripting.When(
					TriggerEvent.SelfEnters,
					[
						Scripting.Target(
							MagicCore.Filters.Is.Creature & MagicCore.Filters.Is.Opponents
						) with
						{
							PerX = (Count)2,
						},
					],
					new Tap(Ref.EachTarget)
				),
			],
		};
		var ox = Ox("Ox");
		var card = Hand(tapper, me);
		var asked = CastAsking(card, Defaults);

		Assert.That(
			(asked.OfType<ChooseTargetChoice>().Count(), s.GetCard(ox).IsTapped, s.StackIsEmpty()),
			Nu.EqualTo((0, false, true)),
			"two different targets needed, one exists: removed (CR 603.3d)"
		);
	}

	// ===== Dash: Ragavan, Death-Greeter's Champion =====

	int CastFromHand(string name, bool dash)
	{
		var card = Hand(Lsv(name), me);
		s = s.Act(Casts(card).Single(c => c.WithoutPayingManaCost == dash));
		Resolve(c =>
			c switch
			{
				ChooseTargetChoice => [card], // backup: on itself
				_ => Defaults(c),
			}
		);
		return card;
	}

	bool InHand(int card) => s.IsIn(card, me, ZoneType.Hand);

	[Test]
	public void Ragavan_Dashed_ForOneAndRed_Haste_ReturnedToHandAtTheEndStep()
	{
		var lands = UntappedLands();
		var ragavan = CastFromHand("Ragavan, Nimble Pilferer", dash: true);
		Assert.That(
			(lands - UntappedLands(), s.Has(s.GetCard(ragavan), "Haste")),
			Nu.EqualTo((2, true)),
			"cast for {1}{R}: it gains haste"
		);

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Cleanup || x.IsIn(ragavan, me, ZoneType.Hand)
		);
		Assert.That(InHand(ragavan), Nu.True, "returned to its owner's hand at the next end step");
	}

	[Test]
	public void Ragavan_CastForItsManaCost_NoHaste_Stays()
	{
		var ragavan = CastFromHand("Ragavan, Nimble Pilferer", dash: false);
		Assert.That(s.Has(s.GetCard(ragavan), "Haste"), Nu.False);

		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);
		Assert.That(Alive(ragavan), Nu.True);
	}

	[Test]
	public void Ragavan_CombatDamageToAPlayer_ATreasure_TheirTopCardCastableThisTurn()
	{
		var ragavan = Field(Lsv("Ragavan, Nimble Pilferer"), me);
		var peek = OnTop(Peek, them);

		AttackWith(Defaults, ragavan);

		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Name == "Treasure"),
			Nu.EqualTo(1),
			"create a Treasure token"
		);
		Assert.That(
			(InExile(peek), Playable.IsImpulsePlayableBy(s, s.GetCard(peek), me)),
			Nu.EqualTo((true, true)),
			"exile the top card of that player's library; you may cast it this turn"
		);
	}

	[Test]
	public void DeathGreetersChampion_Dashed_ForThreeAndRed_HasteAndBackToHand()
	{
		var lands = UntappedLands();
		var champion = CastFromHand("Death-Greeter's Champion", dash: true);
		Assert.That(
			(lands - UntappedLands(), s.Has(s.GetCard(champion), "Haste")),
			Nu.EqualTo((4, true))
		);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Cleanup || x.IsIn(champion, me, ZoneType.Hand)
		);
		Assert.That(InHand(champion), Nu.True);
	}

	// ===== Backup =====

	[Test]
	public void Backup_OnAnotherCreature_ACounter_AndDoubleStrikeUntilEndOfTurn()
	{
		var bear = Field(Bear, me);
		var champion = Hand(Lsv("Death-Greeter's Champion"), me);
		CastAsking(
			champion,
			c =>
				c switch
				{
					ChooseTargetChoice => [bear],
					_ => Defaults(c),
				}
		);

		Assert.That(
			(s.GetCard(bear).PlusOneCounters, s.Has(s.GetCard(bear), "Double strike")),
			Nu.EqualTo((1, true)),
			"a +1/+1 counter, and it gains the ability below until end of turn"
		);
		s = TestGames.PassUntil(s, x => x.GetGame().TurnNumber > 1);
		Assert.That(s.Has(s.GetCard(bear), "Double strike"), Nu.False, "until end of turn");
	}

	[Test]
	public void Backup_OnItself_TheCounterOnly()
	{
		var champion = CastFromHand("Death-Greeter's Champion", dash: false);
		Assert.That(
			(
				s.GetCard(champion).PlusOneCounters,
				s.GetCard(champion).Components.OfType<UntilEndOfTurn>().Count()
			),
			Nu.EqualTo((1, 0)),
			"if that's another creature — it isn't: nothing granted"
		);
	}

	// ===== Fear of Missing Out: an additional combat phase =====

	/// <summary>TEST SETUP: four card types in my graveyard — creature, sorcery, artifact, land: delirium.</summary>
	void Delirium()
	{
		Put(Bear, me, ZoneType.Graveyard);
		Put(TestCards.Fodder, me, ZoneType.Graveyard);
		Put(TestCards.Trinket, me, ZoneType.Graveyard);
		Put(TestGames.Card("Forest"), me, ZoneType.Graveyard);
	}

	/// <summary>
	/// To my second main phase: Fear of Missing Out attacks in every combat it can, its trigger untapping it. How many
	/// combats it attacked in. <paramref name="alongside"/> attacks too, whenever it can.
	/// </summary>
	int AttackInEveryCombat(int fomo, int alongside = 0)
	{
		var attacks = 0;
		for (var i = 0; i < 500 && s.GetGame().Step != Step.PostCombatMain; i++)
			switch (s.GetPendingChoice())
			{
				case DeclareAttackerChoice d:
					attacks += d.CreatureId == fomo ? 1 : 0;
					s = s.Choose([d.CreatureId == fomo || d.CreatureId == alongside ? them : 0]);
					break;
				case ChooseTargetChoice:
					s = s.Choose([fomo]);
					break;
				case { } c:
					s = s.Choose(Defaults(c));
					break;
				default:
					s = s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
					break;
			}
		return attacks;
	}

	[Test]
	public void FearOfMissingOut_Delirium_FirstAttack_UntapsIt_AnAdditionalCombat_ItsSecondAttackTriggersNothing()
	{
		Delirium();
		var fomo = Field(Lsv("Fear of Missing Out"), me);
		var bear = Field(Bear, me);
		var life = Life(them);

		Assert.That(
			AttackInEveryCombat(fomo, alongside: bear),
			Nu.EqualTo(2),
			"untapped, it attacks again in the additional combat — and only the first attack triggers"
		);
		Assert.That(
			life - Life(them),
			Nu.EqualTo(2 + 2 + 2),
			"both in the first combat; the tapped Bear was removed from combat, so only Fear in the second"
		);
	}

	[Test]
	public void Evaluator_AnotherCombatOwed_TheirTappedBlockerDoesNotBlockIt()
	{
		Field(Bear, me);
		var blocker = Field(Bear, them);
		TapIt(blocker);
		s = s.WithGame(s.GetGame() with { Step = Step.EndCombat });
		var over = global::MagicCore.Ai.Evaluator.TurnsToKill(s, me, them);
		s = s.WithGame(s.GetGame() with { AdditionalCombats = 1 });

		Assert.That(
			(over, global::MagicCore.Ai.Evaluator.TurnsToKill(s, me, them)),
			Nu.EqualTo((int.MaxValue, 10)),
			"combat over: they untap first; another combat owed: the tapped Bear can't block it"
		);
	}

	[Test]
	public void FearOfMissingOut_NoDelirium_OneCombat()
	{
		var fomo = Field(Lsv("Fear of Missing Out"), me);
		Assert.That(AttackInEveryCombat(fomo), Nu.EqualTo(1));
		Assert.That(s.GetCard(fomo).IsTapped, Nu.True, "nothing untapped it");
	}

	static CardDefinition Rally =>
		TestGames.Inline("Rally", "{0}", "Instant", null, new AdditionalCombatPhase());

	/// <summary>To my second main phase, passing; how many combat phases began.</summary>
	int CombatsToSecondMain()
	{
		var combats = 0;
		var last = s.GetGame().Step;
		for (var i = 0; i < 200 && s.GetGame().Step != Step.PostCombatMain; i++)
		{
			s = s.GetPendingChoice() is { } c
				? s.Choose(Defaults(c))
				: s.Act(new PassPriorityAction { PlayerId = s.GetGame().PriorityPlayerId });
			if (s.GetGame().Step == Step.BeginCombat && last != Step.BeginCombat)
				combats++;
			last = s.GetGame().Step;
		}
		return combats;
	}

	[Test]
	public void AdditionalCombatPhase_InTheFirstMainPhase_TwoCombats()
	{
		Cast(Hand(Rally, me), []);
		Assert.That(
			CombatsToSecondMain(),
			Nu.EqualTo(2),
			"after this main phase: a combat, then the turn's own"
		);
	}

	[Test]
	public void AdditionalCombatPhase_AfterCombat_RefusedLoudly_NeverLostInSilence()
	{
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
		Assert.Throws<InvalidOperationException>(() => Cast(Hand(Rally, me), []));
	}

	// ===== The rest of the batch's ceilings, closed =====

	[TestCase(0, 0)]
	[TestCase(2, 1)]
	public void Multikicker_PaidAtLeastOnce_IsKicked(int kicks, int draws)
	{
		var kicky = TestGames.Inline("Kicky", "{1}", "Sorcery") with
		{
			Components =
			[
				Scripting.Multikicker("{1}"),
				new SpellEffectsComponent([new If(new WasKicked(), [new DrawCards(Ref.You, 1)])]),
			],
		};
		var card = Hand(kicky, me);
		var hand = HandCount(me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = card,
				Replicate = kicks,
			}
		);
		Resolve();
		Assert.That(
			HandCount(me) - (hand - 1),
			Nu.EqualTo(draws),
			"kicked once paid, however many times (CR 702.33d)"
		);
	}

	[Test]
	public void Backup_SeveralKeywords_EachGranted()
	{
		var backer = TestGames.Creature("Backer", "1/1") with
		{
			Components = [Scripting.Backup(1, "Flying", "Trample")],
		};
		var bear = Field(Bear, me);
		CastAsking(
			Hand(backer, me),
			c =>
				c switch
				{
					ChooseTargetChoice => [bear],
					_ => Defaults(c),
				}
		);
		Assert.That(
			(s.Has(s.GetCard(bear), "Flying"), s.Has(s.GetCard(bear), "Trample")),
			Nu.EqualTo((true, true))
		);
	}

	[Test]
	public void XTargets_OnASpell_AnotherCountThanX_RefusedLoudly()
	{
		var odd = TestGames.Inline("Odd", "{1}", "Sorcery") with
		{
			Components =
			[
				new SpellEffectsComponent([new Tap(Ref.EachTarget)])
				{
					Targets = [Scripting.Creature with { PerX = (Count)2 }],
				},
			],
		};
		Ox("Ox");
		var card = Hand(odd, me);
		Assert.Throws<InvalidOperationException>(() => Casts(card).ToList());
	}

	[Test]
	public void XTargets_OnAnActivatedAbility_AnotherCountThanX_RefusedLoudly()
	{
		// Its X is built (Candelabra of Tawnos, MC-4.4l3c: LsvBatch2l3cTests); another count still throws, as a spell's.
		var candle = TestGames.Inline("Candle", "{1}", "Artifact") with
		{
			Components =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{X}"), [new Untap(Ref.EachTarget)])
				{
					Targets =
					[
						Scripting.Target(MagicCore.Filters.Is.Land) with
						{
							PerX = (Count)2,
						},
					],
				},
			],
		};
		var card = Field(candle, me);
		Assert.Throws<InvalidOperationException>(() => Activations(card).ToList());
	}
}
