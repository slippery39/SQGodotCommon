using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>The last rares and mythics (8g): type change, casting from exile, casting mid-resolution.</summary>
public class FraRareFinalTests : FraBoardTest
{
	static CardDefinition Trinket => TestGames.Inline("Trinket", "{1}", "Artifact");

	static CardDefinition Targeted(
		string name,
		string cost,
		string typeLine,
		TargetSpec spec,
		params GameAction[] effects
	) =>
		new(
			TestGames.Inline(name, cost, typeLine).Data,
			[new SpellEffectsComponent([.. effects]) { Targets = [spec] }]
		);

	static CardDefinition Kill =>
		Targeted("Kill", "{1}", "Instant", TargetSpec.Permanent(), new Destroy(Ref.Target()));

	int Put(CardDefinition card, int player, ZoneType zone)
	{
		int id;
		(s, id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	int UntappedLands() =>
		s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land) && !c.IsTapped);

	// ===== Puppet Crafting =====

	[Test]
	public void PuppetCrafting_EnchantedArtifactIsAFiveFiveConstruct_UntilItLeaves()
	{
		var trinket = Field(Trinket, me);
		var aura = Hand("Puppet Crafting", me);
		Cast(aura, [], trinket);
		var card = s.GetCard(trinket);
		Assert.That(
			(
				card.Is(CardType.Creature | CardType.Artifact),
				card.Is(CardType.Artifact),
				card.Subtypes.Contains("Construct"),
				s.Power(card),
				s.Toughness(card)
			),
			Is.EqualTo((true, true, true, 5, 5)),
			"a creature in addition to an artifact"
		);
		s = s.AddPlusOneCounters(trinket, 1); // TEST SETUP
		Assert.That(s.Power(s.GetCard(trinket)), Is.EqualTo(5 + 1), "base 5/5, counters on top");

		Cast(Hand(Kill, me), [], aura);
		card = s.GetCard(trinket);
		Assert.That(
			(Alive(trinket), card.Is(CardType.Creature), card.Subtypes.Contains("Construct")),
			Is.EqualTo((true, false, false)),
			"the Aura gone: an artifact again"
		);
	}

	[Test]
	public void PuppetCrafting_EnchantsArtifactsAndNonAuraEnchantments_Only()
	{
		var trinket = Field(Trinket, me);
		var shrine = Field(TestGames.Inline("Shrine", "{1}", "Enchantment"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var other = Hand("Puppet Crafting", me);
		Cast(Hand("Puppet Crafting", me), [], trinket);
		var aura = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Puppet Crafting").Id;
		var targets = Casts(other).Select(c => c.Targets[0]).ToList();
		Assert.That(targets, Does.Contain(trinket).And.Contain(shrine));
		Assert.That(
			targets,
			Does.Not.Contain(bear).And.Not.Contain(aura),
			"not a creature, not an Aura"
		);
	}

	[Test]
	public void PuppetCrafting_TheAnimatedArtifactDies_AndIsAnArtifactCardInTheGraveyard()
	{
		var trinket = Field(Trinket, me);
		Cast(Hand("Puppet Crafting", me), [], trinket);
		Cast(Hand(Kill, me), [], trinket);
		var card = s.GetCard(trinket);
		Assert.That(
			(s.IsIn(trinket, me, ZoneType.Graveyard), card.Is(CardType.Creature), card.BasePower),
			Is.EqualTo((true, false, 0))
		);
	}

	[Test]
	public void PuppetCrafting_ReturnsFromTheGraveyardToHand()
	{
		var crafting = Put(FraCards.Get("Puppet Crafting"), me, ZoneType.Graveyard);
		s = s.Act(Activations(crafting).Single());
		Resolve();
		Assert.That(s.IsIn(crafting, me, ZoneType.Hand));
	}

	// ===== Emrakul, the Exigent Doom =====

	[Test]
	public void Emrakul_CastingHerUntapsMyLands()
	{
		var emrakul = Hand("Emrakul, the Exigent Doom", me);
		var all = UntappedLands();
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = emrakul });
		Assert.That(UntappedLands(), Is.EqualTo(all - 10), "paid {10}");
		Resolve();
		Assert.That(
			(UntappedLands(), Alive(emrakul)),
			Is.EqualTo((all, true)),
			"the cast trigger untapped them"
		);
	}

	[Test]
	public void Emrakul_WardSacrificeThree_DecliningCounters_PayingCostsThree()
	{
		var emrakul = Field(FraCards.Get("Emrakul, the Exigent Doom"), me);
		var kill = Hand(Kill, them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = kill,
				Targets = [emrakul],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Resolve(c =>
			c switch
			{
				PayToKeepChoice => [],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(Alive(emrakul), "declined: countered");

		var again = Hand(Kill, them);
		var permanents = s.CardsIn(them, ZoneType.Battlefield).Count();
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = again,
				Targets = [emrakul],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });
		Resolve(c =>
			c switch
			{
				PayToKeepChoice => [1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			(Alive(emrakul), s.CardsIn(them, ZoneType.Battlefield).Count()),
			Is.EqualTo((false, permanents - 3))
		);
	}

	[Test]
	public void Emrakul_FromHand_ALandTapsForTwoColorless_AndSheIsCastableFromExile()
	{
		var emrakul = Hand("Emrakul, the Exigent Doom", me);
		var forest = s.CardsIn(me, ZoneType.Battlefield)
			.First(c => c.Subtypes.Contains("Forest"))
			.Id;
		s = s.Act(Activations(emrakul).Single(a => a.Targets[0] == forest));
		Resolve();
		Assert.That(s.IsIn(emrakul, me, ZoneType.Exile));
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = forest,
				Color = ManaColor.Colorless,
			}
		);
		Assert.That(s.GetPlayer(me).ManaPool.Colorless, Is.EqualTo(2), "{C}{C}");

		Assert.That(Casts(emrakul), Is.Not.Empty, "castable while she stays exiled");
		s = s.Act(Casts(emrakul).First());
		Resolve();
		Assert.That(Alive(emrakul));
		var land = s.GetCard(forest);
		s = s.UpdateObject(forest, land with { IsTapped = false }); // TEST SETUP
		Assert.That(
			ManaAbilities.YieldOf(s, s.GetCard(forest)).Units.Select(u => u.ToArray()),
			Is.EqualTo(new[] { new[] { ManaColor.Green } }),
			"cast from exile: the Forest is a Forest again"
		);
	}

	// ===== Uldaros Theorix =====

	[Test]
	public void Uldaros_ExilesOneOfEachType_AndCastsCopiesFreeWithinSix()
	{
		var trinket = Put(Trinket, me, ZoneType.Graveyard);
		var bear = Put(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"), me, ZoneType.Graveyard);
		var bolt = Put(
			Targeted(
				"Bolt",
				"{R}",
				"Instant",
				TargetSpec.AnyTarget(),
				new DealDamage(Ref.Target(), 3)
			),
			me,
			ZoneType.Graveyard
		);
		var draw = Put(
			TestGames.Inline("Draw", "{1}{U}", "Sorcery", null, new DrawCards(Ref.You, 2)),
			me,
			ZoneType.Graveyard
		);
		var life = Life(them);
		var uldaros = Hand("Uldaros Theorix", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = uldaros });
		var hand = HandCount(me);
		Resolve(c =>
			c switch
			{
				ChooseAmongChoice among => [among.Options.First().Id],
				ChooseCastChoice how => [how.Casts.FindIndex(c => c.Targets.Contains(them)) + 1],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(
			new[] { trinket, bear, bolt, draw }.All(id => s.IsIn(id, me, ZoneType.Exile)),
			"the originals stay exiled"
		);
		var tokens = s.CardsIn(me, ZoneType.Battlefield)
			.Where(c => c.IsToken)
			.Select(c => c.Name)
			.Order();
		Assert.That(
			tokens,
			Is.EqualTo(new[] { "Bear", "Trinket" }),
			"permanent copies became tokens"
		);
		Assert.That(
			(Life(them), HandCount(me)),
			Is.EqualTo((life - 3, hand + 2)),
			"the spell copies resolved"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Exile).Any(c => c.HasComponent<ExiledBy>()),
			Is.False,
			"the link cleared"
		);
	}

	[Test]
	public void Uldaros_TheBudgetLimitsWhatIsOffered()
	{
		Put(TestGames.CreatureCosting("{3}{G}", "Big", "4/4"), me, ZoneType.Graveyard);
		Put(TestGames.Inline("Three", "{2}{U}", "Sorcery"), me, ZoneType.Graveyard);
		var uldaros = Hand("Uldaros Theorix", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = uldaros });
		var offers = new List<string>();
		Resolve(c =>
			c switch
			{
				ChooseAmongChoice among when among.Zone == ZoneType.Exile => Offer(among),
				ChooseAmongChoice among => [among.Options.First().Id],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			}
		);
		IEnumerable<int> Offer(ChooseAmongChoice among)
		{
			offers.Add(string.Join(",", among.Options.Select(o => o.DisplayText).Order()));
			return [among.Options.First(o => o.DisplayText == "Big").Id];
		}
		Assert.That(
			offers,
			Is.EqualTo(new[] { "Big,Three" }),
			"after Big (4), Three (3) no longer fits in 6"
		);
	}

	// ===== Casting mid-resolution goes through the one cast legality =====

	int ChandraOnTop(CardDefinition card)
	{
		var chandra = Field(FraCards.Get("Chandra, Torch of Defiance"), me);
		s = s.UpdateObject(chandra, s.GetCard(chandra) with { Loyalty = 4 }); // TEST SETUP
		int id;
		(s, id) = TestGames.Put(s, me, card, ZoneType.Library);
		s = s.MoveObjectToFront(id, s.ZoneId(me, ZoneType.Library)); // TEST SETUP: on top
		return chandra;
	}

	void ChandraPlusOne(int chandra, Func<ChoiceAction, IEnumerable<int>> answer)
	{
		s = s.Act(
			new ActivateLoyaltyAction
			{
				PlayerId = me,
				CardId = chandra,
				AbilityIndex = 0,
			}
		);
		Resolve(answer);
	}

	[Test]
	public void CastNow_AModalCard_ItsModeChosen()
	{
		var charm = new CardDefinition(
			TestGames.Inline("Charm", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([])
				{
					Modes =
					[
						new Mode("• Gain 2 life.", [], [new GainLife(Ref.You, 2)]),
						new Mode(
							"• Each opponent loses 3 life.",
							[],
							[new LoseLife(Ref.EachOpponent, 3)]
						),
					],
					Choose = 1,
				},
			]
		);
		var chandra = ChandraOnTop(charm);
		var life = Life(them);
		ChandraPlusOne(
			chandra,
			c =>
				c switch
				{
					YesNoChoice => [1],
					ChooseCastChoice how =>
					[
						how.Casts.FindIndex(x => x.Modes.SequenceEqual([1])) + 1,
					],
					_ => [],
				}
		);
		Assert.That(
			Life(them),
			Is.EqualTo(life - 3),
			"cast with its second mode — not Chandra's 2 damage"
		);
	}

	[Test]
	public void CastNow_AnAdditionalCostIsPaid()
	{
		var taxing = TestGames.Inline("Taxing", "{1}", "Sorcery") with
		{
			Components = [new SacrificeOrPayAdditionalCost(MagicCore.Filters.Is.Creature, 2)],
		};
		var chandra = ChandraOnTop(taxing);
		var untapped = UntappedLands();
		ChandraPlusOne(
			chandra,
			c =>
				c switch
				{
					YesNoChoice => [1],
					_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
				}
		);
		Assert.That(
			untapped - UntappedLands(),
			Is.EqualTo(1 + 2),
			"{1}, and {2} instead of a sacrifice"
		);
	}

	[Test]
	public void CastNow_CantCastUnless_IsNotOffered()
	{
		var never = TestGames.Inline("Never", "{1}", "Sorcery") with
		{
			Components = [new CastOnlyIf(new AtLeast(Count.NumberOf(Ref.CardsInYourHand), 99))],
		};
		var chandra = ChandraOnTop(never);
		var asked = false;
		var life = Life(them);
		ChandraPlusOne(
			chandra,
			c =>
			{
				asked |= c is YesNoChoice;
				return [];
			}
		);
		Assert.That((asked, Life(them)), Is.EqualTo((false, life - 2)));
	}

	[Test]
	public void PuppetCrafting_ACopyOfTheAnimatedArtifact_IsJustAnArtifact()
	{
		var trinket = Field(Trinket, me);
		Cast(Hand("Puppet Crafting", me), [], trinket);
		Cast(
			Hand(
				Targeted(
					"Mirror",
					"{1}",
					"Sorcery",
					TargetSpec.Permanent(),
					new CreateTokenCopy(Ref.Target())
				),
				me
			),
			[],
			trinket
		);
		var token = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.IsToken);
		Assert.That(
			(token.Name, token.Is(CardType.Creature)),
			Is.EqualTo(("Trinket", false)),
			"the type change isn't copied"
		);
	}

	[Test]
	public void PuppetCrafting_AnAnimatedEquipmentFallsOff()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var sword = Field(TestGames.Inline("Sword", "{1}", "Artifact — Equipment"), me);
		s = Rules.Attachments.Attach(s, sword, bear); // TEST SETUP: equipped
		Cast(Hand("Puppet Crafting", me), [], sword);
		Assert.That(
			(s.GetCard(sword).Is(CardType.Creature), s.GetCard(sword).AttachedToId),
			Is.EqualTo((true, 0))
		);
	}

	[Test]
	public void Emrakul_TargetsOnlyALandIControl()
	{
		var emrakul = Hand("Emrakul, the Exigent Doom", me);
		Assert.That(Activations(emrakul).All(a => s.GetCard(a.Targets[0]).ControllerId == me));
	}
}
