using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **EMBER — every rule of draft 2 FIRES** (`KinFamiliesPlan.md`, "EMBER — the family draft"): Spell
/// Power for the turn and the fight, Burn, the chains, the energy, the auras and the passives. Inline
/// cards; amounts read from the steps, never restated.
/// </summary>
public partial class PartyTests
{
	private static KinCard Spark(int amount = 3) =>
		Card("Spark", 0, new SpellDamageAction { Amount = amount });

	private static GameState Sparks(GameState s, int count)
	{
		for (var i = 0; i < count; i++)
			s = Play(s, "Spark", 0, foeRow: true);
		return s;
	}

	// ===== Spell Power

	[Test]
	public void SpellPowerForTheTurnFadesAndForTheFightStays()
	{
		var turn = new SpellPowerAction { Amount = 3 };
		var fight = new SpellPowerAction { Amount = 2, ForFight = true };
		var s = Solo(Mon("Caster"), [Card("Kindle", 0, turn), Card("Stoke", 0, fight), Spark()]);

		s = Play(Play(s, "Kindle", 0), "Stoke", 0);
		s = Play(s, "Spark", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (3 + turn.Amount + fight.Amount)));

		s = EndTurn(s);
		Assert.That(s.SpellBonus(), Is.EqualTo(fight.Amount));
	}

	[Test]
	public void AStokerAddsToEveryGainOfSpellPower()
	{
		var stoker = new Stoker();
		var kindle = new SpellPowerAction { Amount = 3 };
		var s = Solo(With(Mon("Emberling"), stoker), [Card("Kindle", 0, kindle)]);

		s = Play(s, "Kindle", 0);

		Assert.That(s.SpellBonus(), Is.EqualTo(kindle.Amount + stoker.Extra));
	}

	[Test]
	public void SpellbladeAttacksAddYourSpellPower()
	{
		var kindle = new SpellPowerAction { Amount = 3 };
		var s = Solo(
			With(Mon("Pike"), new Spellblade()),
			[Card("Kindle", 0, kindle), Card("Strike", 0, new StrikeAction { Amount = 2 })]
		);

		s = Play(Play(s, "Kindle", 0), "Strike", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 - kindle.Amount));
	}

	[Test]
	public void InnerFireGivesSpellPowerForTheFightAtEachTurnStart()
	{
		var s = Solo(
			Mon("Caster"),
			[Card("Inner Fire", 0, new AuraAction { Aura = new InnerFireAura() })]
		);

		s = Play(s, "Inner Fire", 0);
		Assert.That(s.SpellBonus(), Is.Zero, "not on the turn it is played");
		s = EndTurn(EndTurn(s));

		Assert.That(s.SpellBonus(), Is.EqualTo(2));
	}

	// ===== BURN

	[Test]
	public void BurnHurtsAtTheFoesTurnThenDropsByOne()
	{
		var singe = new BurnAction { Amount = 4 };
		var s = Solo(Mon("Caster"), [Card("Singe", 0, singe)]);

		s = Play(s, "Singe", 0, foeRow: true);
		Assert.That(
			CanPlay(Solo(Mon("C"), [Card("Singe", 0, singe)]), "Singe", 0),
			Is.False,
			"on a foe"
		);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - singe.Amount));
		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(singe.Amount - 1));
	}

	[Test]
	public void EverburnKeepsTheBurn()
	{
		var singe = new BurnAction { Amount = 4 };
		var s = Solo(
			Mon("Caster"),
			[
				Card("Singe", 0, singe),
				Card("Everburn", 0, new AuraAction { Aura = new EverburnAura() }),
			]
		);

		s = Play(Play(s, "Singe", 0, foeRow: true), "Everburn", 0);
		s = EndTurn(s);

		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(singe.Amount));
	}

	[Test]
	public void IgniteDoublesAndSpreadingFlamesLiftsEveryFoeToTheHighest()
	{
		var singe = new BurnAction { Amount = 4 };
		var spread = new BurnAction { RiseToHighest = true, Amount = 2 };
		var s = Solo(
			Mon("Caster"),
			[
				Card("Singe", 0, singe),
				Card("Ignite", 0, new BurnAction { Double = true }),
				Card("Spread", 0, spread),
			],
			Foe(0),
			Foe(1)
		);

		s = Play(Play(s, "Singe", 0, foeRow: true), "Ignite", 0, foeRow: true);
		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(singe.Amount * 2));
		s = Play(s, "Spread", 1, foeRow: true);

		Assert.That(FoeIn(s, 1).Burn, Is.EqualTo(singe.Amount * 2 + spread.Amount));
		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(singe.Amount * 2 + spread.Amount));
	}

	[Test]
	public void FlashpointDealsPerBurnAndTheBurnStays()
	{
		var singe = new BurnAction { Amount = 4 };
		var flash = new SpellDamageAction { PerBurn = 3 };
		var s = Solo(Mon("Caster"), [Card("Singe", 0, singe), Card("Flashpoint", 0, flash)]);

		s = Play(Play(s, "Singe", 0, foeRow: true), "Flashpoint", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - singe.Amount * flash.PerBurn));
		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(singe.Amount));
	}

	[Test]
	public void SmoulderLeavesBurnOnEveryFoeASpellHits()
	{
		var smoulder = new Smoulder();
		var s = Solo(With(Mon("Newt"), smoulder), [Spark(), Spark()]);

		s = Sparks(s, 2);

		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(2 * smoulder.Burn));
	}

	// ===== CHAINS

	[Test]
	public void EchoCastsTheThirdSpellTwice()
	{
		var s = Solo(
			With(Mon("Owl"), new EchoNthSpell { Nth = 3 }),
			[Spark(), Spark(), Spark(), Spark()]
		);

		s = Sparks(s, 3);
		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 * 3), "the third, twice");
		s = Sparks(s, 1);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 5 * 3), "the fourth, once");
	}

	[Test]
	public void FanTheFlamesCastsTheNextTwoSpellsTwice()
	{
		var fan = new CastTwiceAction { Count = 2 };
		var s = Solo(Mon("Caster"), [Card("Fan", 0, fan), Spark(), Spark(), Spark()]);

		s = Sparks(Play(s, "Fan", 0), 3);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - (2 * fan.Count + 1) * 3));
	}

	[Test]
	public void SpellSurgeMakesSpellsCheaperThisTurn()
	{
		var zap = Card("Zap", 1, new SpellDamageAction { Amount = 4 });
		var s = Solo(
			Mon("Caster"),
			[Card("Surge", 0, new SpellDiscountAction { Amount = 1 }), zap]
		);
		var energy = s.GetParty().Energy;

		s = Play(Play(s, "Surge", 0), "Zap", 0, foeRow: true);

		Assert.That(s.GetParty().Energy, Is.EqualTo(energy));
	}

	[Test]
	public void WildfireIsFreeOnceTwoSpellsAreCast()
	{
		var wildfire = Card("Wildfire", 1, new SpellDamageAction { Amount = 6 }) with
		{
			Components = [new FreeAfterSpells { Spells = 2 }],
		};
		var s = Solo(Mon("Caster"), [Spark(), Spark(), wildfire]);
		var energy = s.GetParty().Energy;

		s = Play(Sparks(s, 2), "Wildfire", 0, foeRow: true);

		Assert.That(s.GetParty().Energy, Is.EqualTo(energy));
	}

	[Test]
	public void ChainLightningDealsPerSpellCastThisOneIncluded()
	{
		var chain = new SpellDamageAction { PerSpellThisTurn = 3 };
		var s = Solo(Mon("Caster"), [Spark(), Spark(), Card("Chain", 0, chain)]);

		s = Play(Sparks(s, 2), "Chain", 0, foeRow: true);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 2 * 3 - 3 * chain.PerSpellThisTurn));
	}

	[Test]
	public void KindlingAddsCardsToYourHand()
	{
		var add = new AddCardsAction { Card = Spark(), Count = 2 };
		var s = Solo(Mon("Caster"), [Card("Kindling", 0, add)]);
		var before = s.CardsIn(ZoneType.Hand).Count();

		s = Play(s, "Kindling", 0);

		Assert.That(s.CardsIn(ZoneType.Hand).Count(), Is.EqualTo(before - 1 + add.Count));
		Assert.That(s.CardsIn(ZoneType.Hand).Count(c => c.Name == "Spark"), Is.EqualTo(add.Count));
	}

	[Test]
	public void FirestormRecastsEveryZeroCostSpellInTheDiscard()
	{
		var s = Solo(
			Mon("Caster"),
			[Spark(), Spark(), Card("Firestorm", 0, new FirestormAction())]
		);

		s = Play(Sparks(s, 2), "Firestorm", 0);

		Assert.That(FoeIn(s, 0).Hp, Is.EqualTo(50 - 4 * 3));
	}

	[Test]
	public void SpellweaverDrawsAndGivesEnergyOnTheThirdSpell()
	{
		var s = Solo(
			Mon("Caster"),
			[Card("Weaver", 0, new AuraAction { Aura = new SpellweaverAura() }), Spark(), Spark()]
		);
		s = Sparks(Play(s, "Weaver", 0), 1);
		var hand = s.CardsIn(ZoneType.Hand).Count();
		var energy = s.GetParty().Energy;

		s = Sparks(s, 1);

		Assert.That(
			s.CardsIn(ZoneType.Hand).Count(),
			Is.EqualTo(hand - 1 + 2),
			"the aura was the 1st spell"
		);
		Assert.That(s.GetParty().Energy, Is.EqualTo(energy + 1));
	}

	[Test]
	public void EveryCardButAnAttackIsASpell()
	{
		var fan = new CastTwiceAction { Count = 1 };
		var guard = new GuardAction { Amount = 5 };
		var s = Solo(
			Mon("Caster"),
			[
				Card("Fan", 0, fan),
				Card("Brace", 0, guard),
				Card("Strike", 0, new StrikeAction { Amount = 2 }),
			]
		);

		s = Play(Play(Play(s, "Fan", 0), "Brace", 0), "Strike", 0);

		Assert.That(
			Named(s, "Caster").Block,
			Is.EqualTo(2 * guard.Amount),
			"a Block card is a spell: cast twice"
		);
		Assert.That(
			s.GetParty().SpellsThisTurn,
			Is.EqualTo(2),
			"Fan and Brace; the Strike is an attack"
		);
	}

	[Test]
	public void FirestormRecastsAnySpellNotOnlyDamage()
	{
		var surge = new GainEnergyAction { Amount = 2 };
		var s = Solo(
			Mon("Caster"),
			[Card("Heat Surge", 0, surge), Card("Firestorm", 0, new FirestormAction())]
		);

		s = Play(s, "Heat Surge", 0);
		var energy = s.GetParty().Energy;
		s = Play(s, "Firestorm", 0);

		Assert.That(s.GetParty().Energy, Is.EqualTo(energy + surge.Amount));
	}

	// ===== ENERGY

	[Test]
	public void ChargeUpGivesEnergyNextTurnAndCostsNothingThisTurn()
	{
		var charge = new EnergyNextTurnAction { Amount = 2 };
		var s = Solo(Mon("Caster"), [Card("Charge", 0, charge)]);
		var energy = s.GetParty().Energy;

		s = Play(s, "Charge", 0);
		Assert.That(s.GetParty().Energy, Is.EqualTo(energy));
		s = EndTurn(s);

		Assert.That(s.GetParty().Energy, Is.EqualTo(s.GetParty().MaxEnergy + charge.Amount));
	}

	[Test]
	public void BankCarriesUnspentEnergyUpToItsMost()
	{
		var bank = new Bank { Most = 2 };
		var s = Solo(With(Mon("Ironhorn"), bank), []);

		s = EndTurn(s);

		Assert.That(s.GetParty().Energy, Is.EqualTo(s.GetParty().MaxEnergy + bank.Most));
	}

	// ===== Block that asks for an engine

	[Test]
	public void EmberBlockReadsSpellPowerSpellsAndBurn()
	{
		var kindle = new SpellPowerAction { Amount = 3 };
		var singe = new BurnAction { Amount = 4 };
		var ward = new EmberBlockAction { Amount = 6, PerSpellPower = 3 };
		var haze = new EmberBlockAction
		{
			Amount = 6,
			IfSpellsCast = 2,
			Bonus = 12,
		};
		var smoke = new EmberBlockAction { PerSpellThisTurn = 5 };
		var cinder = new EmberBlockAction { PerBurnOnTheirLine = 2 };
		var s = Solo(
			Mon("Caster"),
			[
				Card("Kindle", 0, kindle),
				Spark(),
				Spark(),
				Card("Ward", 0, ward),
				Card("Haze", 0, haze),
			]
		);

		s = Sparks(Play(s, "Kindle", 0), 2);
		s = Play(Play(s, "Ward", 0), "Haze", 0);
		var expected = ward.Amount + ward.PerSpellPower * kindle.Amount + haze.Amount + haze.Bonus;
		Assert.That(Named(s, "Caster").Block, Is.EqualTo(expected));

		var t = Solo(
			Mon("Caster"),
			[Card("Singe", 0, singe), Spark(), Card("Smoke", 0, smoke), Card("Cinder", 0, cinder)]
		);
		t = Play(Sparks(Play(t, "Singe", 0, foeRow: true), 1), "Smoke", 0);
		t = Play(t, "Cinder", 0);
		Assert.That(
			Named(t, "Caster").Block,
			Is.EqualTo(smoke.PerSpellThisTurn * 3 + cinder.PerBurnOnTheirLine * singe.Amount),
			"Singe, the Spark and Smoke Screen itself are all spells"
		);
	}

	// ===== First-attack bonuses that feed spells

	[Test]
	public void AFirstAttackCanGiveFightSpellPowerBurnAndEnergy()
	{
		var bonus = new FirstAttack
		{
			FightSpellPower = 1,
			Burn = 2,
			Energy = 1,
		};
		var s = Solo(
			With(Mon("Newt"), bonus),
			[Card("Strike", 0, new StrikeAction { Amount = 2 })]
		);
		var energy = s.GetParty().Energy;

		s = Play(s, "Strike", 0);

		Assert.That(s.SpellBonus(), Is.EqualTo(bonus.FightSpellPower));
		Assert.That(FoeIn(s, 0).Burn, Is.EqualTo(bonus.Burn));
		Assert.That(s.GetParty().Energy, Is.EqualTo(energy + bonus.Energy));
	}

	/// <summary>The number a dragged attack card shows is the number it lands for — every add-on in.</summary>
	[Test]
	public void AnAttacksPreviewIsWhatItLands()
	{
		var kindle = new SpellPowerAction { Amount = 3 };
		var s = Solo(
			With(With(Mon("Pike"), new Spellblade()), new FirstAttack { Damage = 4 }),
			[Card("Kindle", 0, kindle), Card("Strike", 0, new StrikeAction { Amount = 2 })]
		);
		s = Play(s, "Kindle", 0);
		var hp = FoeIn(s, 0).Hp;

		var preview = s.AttackPreview(InHand(s, "Strike"), 0);
		s = Play(s, "Strike", 0);

		Assert.That(preview, Is.EqualTo(hp - FoeIn(s, 0).Hp));
		Assert.That(preview, Is.GreaterThan(2), "the add-ons are in it");
		Assert.That(s.AttackPreview(Spark(), 0), Is.Null, "not an attack");
	}

	// ===== The content

	[Test]
	public void EveryEmberCardIsEmberAndHasAPlusVersion()
	{
		foreach (var card in EmberCards.Pool)
		{
			Assert.That(card.Family, Is.EqualTo(Family.Ember), card.Name);
			Assert.That(card.Upgraded, Is.Not.Null, card.Name);
			Assert.That(card.Upgraded!.Family, Is.EqualTo(Family.Ember), card.Name);
		}
	}

	[Test]
	public void EveryEmberMonsterHasAPassiveAndAFirstAttackBonus()
	{
		foreach (var monster in EmberCards.Monsters.Append(PartyContent.Pike))
		{
			Assert.That(monster.Family, Is.EqualTo(Family.Ember), monster.Name);
			Assert.That(monster.Passive, Is.Not.Empty, monster.Name);
			Assert.That(monster.Abilities.OfType<FirstAttack>(), Is.Not.Empty, monster.Name);
		}
	}
}
