using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **GROVE — every rule of draft 1 FIRES** (`KinFamiliesPlan.md`, "GROVE — the family draft"): Rooted
/// Block, Thorns for the turn and the fight, Block read into damage, GROW, tokens as wall, fuel and
/// attacker, the auras and the passives. Inline cards; amounts read from the steps, never restated.
/// </summary>
public partial class PartyTests
{
	/// <summary>A token that stays (fades 0) unless told otherwise, with Power to attack with.</summary>
	private static TokenTemplate Sapling(int hp = 6, int power = 1, int fades = 0) =>
		new(Mon("Sapling", hp: hp, power: power), fades);

	private static KinCard Brace(int amount = 5) =>
		Card("Brace", 0, new GuardAction { Amount = amount });

	// ===== ROOTED

	[Test]
	public void RootedBlockStaysWhenYourTurnStartsAndPlainBlockDoesNot()
	{
		var root = new RootAction { Amount = 8 };
		var s = Solo(Mon("Wall"), [Card("Root", 0, root), Brace()]);

		s = Play(Play(s, "Root", 0), "Brace", 0);
		s = EndTurn(s);

		Assert.That(
			Named(s, "Wall").Block,
			Is.EqualTo(root.Amount),
			"the Root stays, the Brace goes"
		);
	}

	[Test]
	public void RootedBlockLastsOneExtraTurnThenGoes()
	{
		var s = Solo(Mon("Wall"), [Card("Root", 0, new RootAction { Amount = 8 })]);

		s = EndTurn(EndTurn(Play(s, "Root", 0)));

		Assert.That(Named(s, "Wall").Block, Is.Zero, "kept through one turn start, not two");
	}

	[Test]
	public void MossbackBlockAlsoLastsOnlyOneExtraTurn()
	{
		var s = Solo(With(Mon("Shell"), new Mossback()), [Brace()]);

		s = EndTurn(Play(s, "Brace", 0));
		Assert.That(Named(s, "Shell").Block, Is.EqualTo(5));
		s = EndTurn(s);

		Assert.That(Named(s, "Shell").Block, Is.Zero);
	}

	[Test]
	public void RootedBlockIsLostWhereItIsHitThrough()
	{
		var root = new RootAction { Amount = 8 };
		var s = Solo(Mon("Wall"), [Card("Root", 0, root)], Foe(0, 50, Hit(4)));

		s = EndTurn(Play(s, "Root", 0));

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(root.Amount - 4));
	}

	[Test]
	public void DeepRootsDoublesRootedBlock()
	{
		var root = new RootAction { Amount = 8 };
		var s = Solo(Mon("Wall"), [Card("Root", 0, root), Card("Deep", 0, new DeepRootsAction())]);

		s = Play(Play(s, "Root", 0), "Deep", 0);

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(root.Amount * 2));
		Assert.That(Named(s, "Wall").Rooted, Is.EqualTo(root.Amount * 2));
	}

	[Test]
	public void BraceRootsRootsAllItsBlock()
	{
		var brace = new GroveBlockAction { Amount = 4, RootAll = true };
		var s = Solo(Mon("Wall"), [Brace(), Card("Brace Roots", 0, brace)]);

		s = EndTurn(Play(Play(s, "Brace", 0), "Brace Roots", 0));

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(5 + brace.Amount));
	}

	[Test]
	public void AncientBarkKeepsAllYourBlock()
	{
		var s = Solo(
			Mon("Wall"),
			[Brace(), Card("Bark", 0, new AuraAction { Aura = new AncientBarkAura() })]
		);

		s = EndTurn(Play(Play(s, "Bark", 0), "Brace", 0));

		Assert.That(Named(s, "Wall").Block, Is.EqualTo(5));
	}

	[Test]
	public void MossbackKeepsAllItsBlockAndGrowsWhenItStopsAHit()
	{
		var moss = new Mossback();
		var s = Solo(With(Mon("Shell", power: 1), moss), [Brace()], Foe(0, 50, Hit(3)));

		s = EndTurn(Play(s, "Brace", 0));

		Assert.That(Named(s, "Shell").Block, Is.EqualTo(5 - 3), "kept, less the hit");
		Assert.That(Named(s, "Shell").Power, Is.EqualTo(1 + moss.Grow));
	}

	[Test]
	public void HeartwoodDealsTheRootedBlockOnYourLineAndKeepsIt()
	{
		var root = new RootAction { Amount = 8 };
		var s = Solo(
			Mon("Wall"),
			[
				Card("Root", 0, root),
				Card("Heartwood", 0, new SpellDamageAction { PerRootedOnLine = 1 }),
			]
		);

		s = Play(Play(s, "Root", 0), "Heartwood", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - root.Amount));
		Assert.That(Named(s, "Wall").Rooted, Is.EqualTo(root.Amount));
	}

	// ===== THORNS

	[Test]
	public void ThornsForTheTurnFadeAndForTheFightStay()
	{
		var turn = new ThornsAction { Amount = 8 };
		var fight = new ThornsAction { Amount = 3, ForFight = true };
		var s = Solo(Mon("Spiny"), [Card("Bristle", 0, turn), Card("Needles", 0, fight)]);

		s = Play(Play(s, "Bristle", 0), "Needles", 0);
		Assert.That(Named(s, "Spiny").TotalThorns, Is.EqualTo(turn.Amount + fight.Amount));
		s = EndTurn(s);

		Assert.That(Named(s, "Spiny").TotalThorns, Is.EqualTo(fight.Amount));
	}

	[Test]
	public void SporecapAddsToEveryThornsCardAndBriarPatchCoversTheLine()
	{
		var cap = new Sporecap();
		var patch = new ThornsAction { Amount = 5, AllLine = true };
		var s = Solo(With(Mon("Cap"), cap), [Summon(Sapling()), Card("Patch", 0, patch)]);

		s = Play(Play(s, "Summon", 0), "Patch", 0);

		Assert.That(Named(s, "Cap").TotalThorns, Is.EqualTo(patch.Amount + cap.Extra));
		Assert.That(Named(s, "Sapling").TotalThorns, Is.EqualTo(patch.Amount + cap.Extra));
	}

	[Test]
	public void ThornmailHurtsAFoeThatHitsAnyoneOnYourLine()
	{
		var mail = new ThornmailAura { Damage = 3 };
		var s = Solo(
			Mon("Any"),
			[Card("Mail", 0, new AuraAction { Aura = mail })],
			Foe(0, 50, Hit(2))
		);

		s = EndTurn(Play(s, "Mail", 0));

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - mail.Damage));
	}

	// ===== BLOCK INTO DAMAGE — read, never spent

	[Test]
	public void BarkSlamAttacksForItsBlockAndKeepsIt()
	{
		var s = Solo(Mon("Wall"), [Brace(8), Card("Slam", 0, new StrikeAction { PerBlock = 1 })]);

		s = Play(Play(s, "Brace", 0), "Slam", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 8));
		Assert.That(Named(s, "Wall").Block, Is.EqualTo(8));
	}

	[Test]
	public void ThornLashAttacksForItsThorns()
	{
		var lash = new StrikeAction { Amount = 4, PerThorns = 1 };
		var thorns = new ThornsAction { Amount = 5 };
		var s = Solo(Mon("Spiny"), [Card("Bristle", 0, thorns), Card("Lash", 0, lash)]);

		s = Play(Play(s, "Bristle", 0), "Lash", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - lash.Amount - thorns.Amount));
	}

	// ===== Grove Block

	[Test]
	public void GroveBlockCoversTheLineRewardsBlockAndReadsPower()
	{
		var thicket = new GroveBlockAction { Amount = 5, AllLine = true };
		var hardwood = new GroveBlockAction { Amount = 7, IfHadBlock = 7 };
		var barkskin = new GroveBlockAction { PerPower = 3 };
		var s = Solo(
			Mon("Oak", power: 2),
			[
				Summon(Sapling()),
				Card("Thicket", 0, thicket),
				Card("Hardwood", 0, hardwood),
				Card("Barkskin", 0, barkskin),
			]
		);

		s = Play(Play(s, "Summon", 0), "Thicket", 0);
		Assert.That(Named(s, "Sapling").Block, Is.EqualTo(thicket.Amount), "tokens too");
		s = Play(Play(s, "Hardwood", 1), "Barkskin", 1);

		Assert.That(
			Named(s, "Oak").Block,
			Is.EqualTo(
				thicket.Amount + hardwood.Amount + hardwood.IfHadBlock + barkskin.PerPower * 2
			)
		);
	}

	// ===== GROWTH

	[Test]
	public void GrowAddsPowerForTheWholeFight()
	{
		var grow = new GrowAction { Amount = 3 };
		var s = Solo(Mon("Sapling", power: 1), [Card("Overgrow", 0, grow)]);

		s = EndTurn(Play(s, "Overgrow", 0));

		Assert.That(Named(s, "Sapling").Power, Is.EqualTo(1 + grow.Amount));
	}

	[Test]
	public void WildGrowthGrowsTheLineAndRampantGrowthDoublesPower()
	{
		var wild = new GrowAction { Amount = 1, AllLine = true };
		var s = Solo(
			Mon("Oak", power: 2),
			[
				Summon(Sapling()),
				Card("Wild", 0, wild),
				Card("Rampant", 0, new GrowAction { ByOwnPower = true }),
			]
		);

		s = Play(Play(s, "Summon", 0), "Wild", 0);
		Assert.That(Named(s, "Sapling").Power, Is.EqualTo(1 + wild.Amount));
		s = Play(s, "Rampant", 1);

		Assert.That(Named(s, "Oak").Power, Is.EqualTo((2 + wild.Amount) * 2));
	}

	[Test]
	public void WildHeartGrowsYourMonstersButNotYourTokensEachTurn()
	{
		var s = Solo(
			Mon("Oak", power: 2),
			[Summon(Sapling()), Card("Heart", 0, new AuraAction { Aura = new WildHeartAura() })]
		);

		s = EndTurn(Play(Play(s, "Summon", 0), "Heart", 0));

		Assert.That(Named(s, "Oak").Power, Is.EqualTo(3));
		Assert.That(Named(s, "Sapling").Power, Is.EqualTo(1));
	}

	// ===== TOKENS — wall, fuel and attacker

	[Test]
	public void ALastingTokenStaysAndAWitheringOneGoes()
	{
		var s = Solo(Mon("Keeper"), [Summon(Sapling()), Summon(Token(hp: 4, fades: 1))]);

		s = Play(Play(s, "Summon", 0), "Summon", 0);
		s = EndTurn(EndTurn(s));

		Assert.That(
			s.LivingAllies().Select(a => a.Name),
			Is.EquivalentTo(new[] { "Keeper", "Sapling" })
		);
	}

	[Test]
	public void AnAttackCardOnATokenSwingsWithItsPower()
	{
		var token = Sapling(power: 3);
		var s = Solo(
			Mon("Keeper"),
			[Summon(token), Card("Strike", 0, new StrikeAction { Amount = 2 })]
		);

		s = Play(Play(s, "Summon", 0), "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 - token.Creature.Power));
	}

	[Test]
	public void PackChargeSwingsEveryToken()
	{
		var token = Sapling(power: 3);
		var s = Solo(
			Mon("Keeper"),
			[Summon(token, count: 2), Card("Charge", 0, new TokensAttackAction())]
		);

		s = Play(Play(s, "Summon", 0), "Charge", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 * token.Creature.Power));
	}

	[Test]
	public void GraftGrowsTheMonsterByTheTokenAndShieldsItWithItsHp()
	{
		var token = Sapling(hp: 6, power: 2);
		var s = Solo(Mon("Oak", power: 1), [Summon(token), Card("Graft", 0, new GraftAction())]);

		s = Play(s, "Summon", 0);
		Assert.That(
			CanPlay(Solo(Mon("Lone"), [Card("Graft", 0, new GraftAction())]), "Graft", 0),
			Is.False,
			"no token"
		);
		s = Play(s, "Graft", 1);

		Assert.That(Named(s, "Sapling").IsDown, Is.True);
		Assert.That(Named(s, "Oak").Power, Is.EqualTo(1 + token.Creature.Power));
		Assert.That(Named(s, "Oak").Block, Is.EqualTo(token.Creature.Hp));
	}

	[Test]
	public void HarvestSacrificesEveryTokenIntoOneFoe()
	{
		var token = Sapling(hp: 6);
		var s = Solo(
			Mon("Keeper"),
			[Summon(token, count: 2), Card("Harvest", 0, new HarvestAction())],
			Foe(0),
			Foe(1)
		);

		s = Play(Play(s, "Summon", 0), "Harvest", 1, foeRow: true);

		Assert.That(s.LivingAllies().Select(a => a.Name), Is.EqualTo(new[] { "Keeper" }));
		Assert.That(FoeIn(s, 1).Hp, Is.EqualTo(50 - 2 * token.Creature.Hp));
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50));
	}

	[Test]
	public void ALogDrawsWhenItFalls()
	{
		var log = new TokenTemplate(
			Mon("Log", hp: 12) with
			{
				Abilities = [new DrawOnFall { Count = 2 }],
			},
			0
		);
		var s = Solo(Mon("Keeper"), [Summon(log), Card("Compost", 0, new SacrificeTokenAction())]);
		s = Play(s, "Summon", 0);
		var hand = s.CardsIn(ZoneType.Hand).Count();

		s = Play(s, "Compost", 0);

		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(hand - 1 + 2));
	}

	[Test]
	public void PackLeaderGrowsYourMonstersWhenATokenFalls()
	{
		var pack = new PackLeader();
		var s = Solo(
			With(Mon("Howler", power: 4), pack),
			[Summon(Sapling()), Card("Compost", 0, new SacrificeTokenAction())]
		);

		s = Play(Play(s, "Summon", 0), "Compost", 0);

		Assert.That(Named(s, "Howler").Power, Is.EqualTo(4 + pack.Grow));
	}

	[Test]
	public void LifeCycleDrawsAndRootsYourFrontWhenATokenFalls()
	{
		var cycle = new LifeCycleAura();
		var s = Solo(
			Mon("Keeper"),
			[
				Card("Cycle", 0, new AuraAction { Aura = cycle }),
				Summon(Sapling()),
				Card("Compost", 0, new SacrificeTokenAction()),
			]
		);
		s = Play(Play(s, "Cycle", 0), "Summon", 0);
		var hand = s.CardsIn(ZoneType.Hand).Count();

		s = Play(s, "Compost", 0);

		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(hand - 1 + cycle.Draw));
		Assert.That(
			Named(s, "Keeper").Rooted,
			Is.EqualTo(cycle.Rooted),
			"the Keeper is the front now"
		);
	}

	[Test]
	public void ANurseryMakesYourTokensArriveBigger()
	{
		var nursery = new TokenBoost { Hp = 3, Power = 1 };
		var token = Sapling();
		var s = Solo(With(Mon("Vine"), nursery), [Summon(token)]);

		s = Play(s, "Summon", 0);

		Assert.That(Named(s, "Sapling").MaxHp, Is.EqualTo(token.Creature.Hp + nursery.Hp));
		Assert.That(Named(s, "Sapling").Power, Is.EqualTo(token.Creature.Power + nursery.Power));
	}

	// ===== First-attack bonuses

	[Test]
	public void AFirstAttackCanSummonATokenAndGrowTheMonster()
	{
		var bonus = new FirstAttack { Grow = 1, Summons = Sapling() };
		var s = Solo(
			With(Mon("Vine", power: 1), bonus),
			[Card("Strike", 0, new StrikeAction { Amount = 2 })]
		);

		s = Play(s, "Strike", 0);

		Assert.That(Named(s, "Vine").Power, Is.EqualTo(1 + bonus.Grow));
		Assert.That(s.LivingAllies().First().Name, Is.EqualTo("Sapling"), "at the front");
	}

	// ===== The content

	[Test]
	public void EveryGroveCardIsGroveAndHasAPlusVersion()
	{
		foreach (var card in GroveCards.Pool)
		{
			Assert.That(card.Family, Is.EqualTo(Family.Grove), card.Name);
			Assert.That(card.Upgraded, Is.Not.Null, card.Name);
		}
	}

	[Test]
	public void EveryGroveMonsterHasAPassiveAndAFirstAttackBonus()
	{
		foreach (var monster in GroveCards.Monsters.Append(PartyContent.Bramble))
		{
			Assert.That(monster.Family, Is.EqualTo(Family.Grove), monster.Name);
			Assert.That(monster.Passive, Is.Not.Empty, monster.Name);
			Assert.That(monster.Abilities.OfType<FirstAttack>(), Is.Not.Empty, monster.Name);
		}
	}
}
