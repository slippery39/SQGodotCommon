using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE FAMILIES — every Grove and Ember rule FIRES** (`KinFamiliesPlan.md`): Grow, Root, the Grove
/// passives and cards; Kindle, the Ember passives and cards; rewards leaning to your families. Uses
/// the battle fixtures in `PartyTests.cs`; card amounts are read from the cards, never restated.
/// </summary>
public partial class PartyTests
{
	private static PartyCompanion With(PartyCompanion m, params GameComponent[] abilities) =>
		m with
		{
			Abilities = [.. abilities],
		};

	private static GameState Solo(PartyCompanion m, KinCard[] hand, params Foe[] foes) =>
		Deal([new PlacedCompanion(m, 0)], hand, foes.Length > 0 ? foes : [Foe(0)]);

	// ===== GROVE

	[Test]
	public void GrowGrowsAtTheStartOfEachLaterTurn()
	{
		var grow = new Grow();
		var s = Solo(With(Mon("Sapling", hp: 20, power: 1), grow), []);

		s = EndTurn(s);

		var sapling = Named(s, "Sapling");
		Assert.That(sapling.Power, Is.EqualTo(1 + grow.Power));
		Assert.That(sapling.MaxHp, Is.EqualTo(20 + grow.Hp));
	}

	[Test]
	public void RootedBlockStaysWhenYourTurnStartsAndPlainBlockDoesNot()
	{
		var root = (RootAction)PartyCards.Root.Effects[0].Template;
		var s = Solo(
			Mon("Wall"),
			[PartyCards.Root, Card("Brace", 1, new GuardAction { Amount = 5 })]
		);

		s = Play(s, "Root", 0);
		s = Play(s, "Brace", 0);
		s = EndTurn(s);

		Assert.That(
			Named(s, "Wall").Block,
			Is.EqualTo(root.Amount),
			"the Root stays, the Brace goes"
		);
	}

	[Test]
	public void RootedBlockIsLostWhereItIsHitThrough()
	{
		var root = (RootAction)PartyCards.Root.Effects[0].Template;
		var s = Solo(Mon("Wall"), [PartyCards.Root], Foe(0, 50, Hit(4)));

		s = Play(s, "Root", 0);
		s = EndTurn(s);

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(root.Amount - 4));
	}

	[Test]
	public void MossbackKeepsAllItsBlock()
	{
		var s = Solo(
			With(Mon("Shell"), new Mossback()),
			[Card("Brace", 1, new GuardAction { Amount = 5 })]
		);

		s = Play(s, "Brace", 0);
		s = EndTurn(s);

		Assert.That(Named(s, "Shell").Block, Is.EqualTo(5));
	}

	[Test]
	public void ThornwallHitsBackWithItsBlock()
	{
		var s = Solo(
			With(Mon("Bramble"), new Thornwall()),
			[Card("Brace", 1, new GuardAction { Amount = 8 })],
			Foe(0, 50, Hit(3))
		);

		s = Play(s, "Brace", 0);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 8), "its Block when the blow came in");
	}

	[Test]
	public void ANurseryGivesYourTokensGrow()
	{
		var s = Solo(With(Mon("Vine"), new Nursery()), [PartyCards.Sow]);

		s = Play(s, "Sow", 0);

		Assert.That(Named(s, "Sprout").HasComponent<Grow>(), Is.True);
	}

	[Test]
	public void AnAlphasTokensAttackTheFrontForTheirPower()
	{
		var boost = new TokenBoost { Hp = 0, Power = 2 };
		var s = Solo(With(Mon("Howler"), boost, new Alpha()), [PartyCards.Sow]);

		s = Play(s, "Sow", 0);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - boost.Power), "the Sprout pounces");
	}

	[Test]
	public void SporesDrawAndPayWhenATokenFalls()
	{
		var spores = new Spores();
		var offer = Card("Offer", 0, new SacrificeTokenAction());
		var s = Solo(With(Mon("Cap"), spores), [PartyCards.Sow, offer]);

		s = Play(s, "Sow", 0);
		var (energy, hand) = (s.GetParty().Energy, s.CardsIn(ZoneType.Hand).Count());
		s = Play(s, "Offer", 0);

		Assert.That(s.GetParty().Energy, Is.EqualTo(energy + spores.Energy));
		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(hand - 1 + spores.Draw));
	}

	[Test]
	public void ThicketShieldsOnlyYourGroveMonstersByTheirPower()
	{
		var s = Deal(
			[
				new PlacedCompanion(Mon("Oak", power: 3) with { Family = Family.Grove }, 0),
				new PlacedCompanion(Mon("Stranger", power: 3), 1),
			],
			[PartyCards.Thicket],
			Foe(0)
		);

		s = Play(s, "Thicket", 0);

		Assert.That(Named(s, "Oak").Block, Is.EqualTo(3));
		Assert.That(Named(s, "Stranger").Block, Is.Zero);
	}

	[Test]
	public void GraftAndOvergrowGrowAMonsterNow()
	{
		var overgrow = (GrowNowAction)PartyCards.Overgrow.Effects[0].Template;
		var s = Solo(Mon("Any", power: 0), [PartyCards.Graft, PartyCards.Overgrow]);

		s = Play(s, "Graft", 0);
		s = Play(s, "Overgrow", 0);

		Assert.That(Named(s, "Any").Power, Is.EqualTo(new Grow().Power * overgrow.Times));
	}

	[Test]
	public void HarvestFellsYourTokensAndEachHitsTheirFrontForItsHp()
	{
		var s = Solo(Mon("Keeper"), [PartyCards.Sow, PartyCards.Harvest]);
		s = Play(s, "Sow", 0);
		var sprout = Named(s, "Sprout").Hp;

		s = Play(s, "Harvest", 0);

		Assert.That(s.LivingAllies().Select(a => a.Name), Is.EqualTo(new[] { "Keeper" }));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - sprout));
	}

	[Test]
	public void DeepRootsDoublesRootedBlock()
	{
		var root = (RootAction)PartyCards.Root.Effects[0].Template;
		var s = Solo(Mon("Wall"), [PartyCards.Root, PartyCards.DeepRoots]);

		s = Play(s, "Root", 0);
		s = Play(s, "Deep Roots", 0);

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(root.Amount * 2));
		Assert.That(Named(s, "Wall").Rooted, Is.EqualTo(root.Amount * 2));
	}

	// ===== EMBER

	[Test]
	public void EverySpellAddsKindleAndEveryKindleAddsToSpells()
	{
		var s = Solo(Mon("Caster"), [Zap(), Zap()]);

		s = Play(s, "Zap", 0, foeRow: true);
		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 - (4 + 1)));
		Assert.That(s.GetParty().Kindle, Is.EqualTo(2));
	}

	[Test]
	public void AStokerAddsMoreKindlePerSpell()
	{
		var stoker = new Stoker();
		var s = Solo(With(Mon("Emberling"), stoker), [Zap()]);

		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(s.GetParty().Kindle, Is.EqualTo(1 + stoker.Extra));
	}

	[Test]
	public void AnEchoCastsOnlyTheFirstSpellEachTurnTwice()
	{
		var s = Solo(With(Mon("Owl"), new EchoFirstSpell()), [Zap(), Zap()]);

		s = Play(s, "Zap", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 - (4 + 1)), "cast twice");
		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 - 5 - (4 + 2)), "then once");
		Assert.That(s.GetParty().Kindle, Is.EqualTo(3));
	}

	[Test]
	public void FanTheFlamesCastsTheNextSpellTwice()
	{
		var s = Solo(Mon("Caster"), [PartyCards.FanTheFlames, Zap()]);

		s = Play(s, "Fan the Flames", 0);
		s = Play(s, "Zap", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 - (4 + 1)));
		Assert.That(s.GetParty().NextSpellTwice, Is.False, "used up");
	}

	[Test]
	public void StokeCinderwallAndEmberskinReadTheKindle()
	{
		var stoke = (KindleAction)PartyCards.Stoke.Effects[0].Template;
		var s = Solo(With(Mon("Newt"), new Emberskin()), [PartyCards.Stoke, PartyCards.Cinderwall]);

		s = Play(s, "Stoke", 0);
		Assert.That(s.GetParty().Kindle, Is.EqualTo(stoke.Amount));
		s = Play(s, "Cinderwall", 0);
		Assert.That(Named(s, "Newt").Block, Is.EqualTo(stoke.Amount));

		s = EndTurn(s);
		Assert.That(
			Named(s, "Newt").Block,
			Is.EqualTo(stoke.Amount),
			"Emberskin, at the turn start"
		);
	}

	[Test]
	public void PikesFinisherAddsTheKindleToItsAttacks()
	{
		var stoke = (KindleAction)PartyCards.Stoke.Effects[0].Template;
		var s = Solo(With(Mon("Pike", moves: Hit(2)), new KindleFinisher()), [PartyCards.Stoke]);

		s = Play(s, "Stoke", 0);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 - stoke.Amount));
	}

	[Test]
	public void FlashpointSpendsAllTheKindle()
	{
		var stoke = (KindleAction)PartyCards.Stoke.Effects[0].Template;
		var flash = (FlashpointAction)PartyCards.Flashpoint.Effects[0].Template;
		var s = Solo(Mon("Caster"), [PartyCards.Stoke, PartyCards.Flashpoint]);

		Assert.That(CanPlay(s, "Flashpoint", 0, foeRow: true), Is.False, "no Kindle yet");
		s = Play(s, "Stoke", 0);
		s = Play(s, "Flashpoint", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - stoke.Amount * flash.PerKindle));
		Assert.That(s.GetParty().Kindle, Is.Zero);
	}

	// ===== The content

	[Test]
	public void EveryGroveAndEmberMonsterOfTheSliceHasItsFamilyAndAKit()
	{
		Assert.That(PartyContent.Bramble.Family, Is.EqualTo(Family.Grove));
		Assert.That(PartyContent.Pike.Family, Is.EqualTo(Family.Ember));
		foreach (var name in new[] { "Broodvine", "Mosshell", "Hushcap", "Howler" })
			Assert.That(PartyWorld.Species(name)!.Family, Is.EqualTo(Family.Grove), name);
		foreach (var name in new[] { "Emberling", "Echo Owl", "Cinder Newt", "Ironhorn" })
			Assert.That(PartyWorld.Species(name)!.Family, Is.EqualTo(Family.Ember), name);
		foreach (
			var name in new[]
			{
				"Broodvine",
				"Mosshell",
				"Hushcap",
				"Howler",
				"Emberling",
				"Echo Owl",
				"Cinder Newt",
				"Ironhorn",
			}
		)
		{
			var caught = PartyRun.FromFoe(PartyWorld.Species(name)!);
			Assert.That(caught.Abilities, Is.Not.Empty, $"{name} brings an engine");
		}
	}

	// ===== ONE FAMILY PER RUN (KinFamiliesPlan.md, round 2)

	[Test]
	public void ARunIsItsStartersFamilyAndStartsWithTwoOfItsCards()
	{
		foreach (var starter in PartyContent.Roster)
		{
			var run = PartyRun.Start(starter, 1);

			Assert.That(run.Family, Is.EqualTo(starter.Family).And.Not.EqualTo(Family.None));
			Assert.That(
				run.Deck.Count(c => c.Family == starter.Family),
				Is.EqualTo(2),
				starter.Name
			);
			Assert.That(run.Deck.Count, Is.EqualTo(PartyContent.StarterDeck.Count + 2));
		}
	}

	[Test]
	public void RewardsAndTheShopOfferOnlyYourFamilyAndColourless()
	{
		foreach (var starter in PartyContent.Roster)
		{
			var offered = Enumerable
				.Range(0, 100)
				.Select(seed => PartyRun.Start(starter, seed))
				.SelectMany(run => run.RewardOffer().Concat(run.ShopCards()))
				.ToList();

			Assert.That(
				offered.Select(c => c.Family).Distinct(),
				Is.EquivalentTo(new[] { Family.None, starter.Family }),
				starter.Name
			);
		}
	}

	[Test]
	public void ABossesWinOffersARare()
	{
		foreach (var seed in Enumerable.Range(0, 20))
		{
			var after = PartyRun.Start(PartyContent.Bramble, seed) with { Phase = RunPhase.Town };

			Assert.That(after.RewardOffer().Any(c => c.Rarity == Rarity.Rare), Is.True);
		}
	}

	[Test]
	public void YouCatchOnlyYourFamilyAndColourless()
	{
		static Foe Weak(Family family) => Foe(0, hp: 30) with { Hp = 1, Family = family };

		string? Refusal(Family foe)
		{
			var s = Deal([new(Mon("Pike"), 0)], [], Weak(foe));
			var party = s.GetParty();
			s = s.UpdateObject(party.Id, party with { Snares = 1, Family = Family.Grove });
			return s.CatchRefusal(s.FoeAt(0)!);
		}

		Assert.That(Refusal(Family.Grove), Is.Null, "your family");
		Assert.That(Refusal(Family.None), Is.Null, "colourless");
		Assert.That(Refusal(Family.Ember), Does.StartWith("Not your family"));
	}
}
