using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4i4–i6, LSVPower — counters on a player and transforming double-faced cards, FIRING against their printed
/// text through real casting and the real stack: Galvanic Discharge's energy, Fable of the Mirror-Breaker // Reflection
/// of Kiki-Jiki, and Etali, Primal Conqueror // Etali, Primal Sickness (transform by an activation, poison).
/// </summary>
public class LsvBatch2i4Tests : FraBoardTest
{
	const string Fable = "Fable of the Mirror-Breaker // Reflection of Kiki-Jiki";
	const string Etali = "Etali, Primal Conqueror // Etali, Primal Sickness";

	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Peek =>
		TestGames.Inline("Peek", "{1}", "Instant", null, new DrawCards(Ref.You, 1));

	static IEnumerable<int> Defaults(ChoiceAction c) =>
		c.Options.Take(c.MinChoices).Select(o => o.Id);

	/// <summary>A 1/5 of theirs: it shows the damage and lives.</summary>
	int Ox(string name) => Field(TestGames.Creature(name, "1/5"), them);

	int Energy(int p) => s.GetPlayer(p).CountersOf(CounterKinds.Energy);

	int Poison(int p) => s.GetPlayer(p).CountersOf(CounterKinds.Poison);

	// ===== Galvanic Discharge: energy =====

	/// <summary>I cast Galvanic Discharge at the target, paying that much {E}; the most I was offered.</summary>
	int Discharge(int target, int pay)
	{
		var most = -1;
		var card = Hand(Lsv("Galvanic Discharge"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = card,
				Targets = [target],
			}
		);
		ResolveAsking(c =>
			c switch
			{
				ChooseNumberChoice n => Offer(n, pay, out most),
				_ => Defaults(c),
			}
		);
		return most;
	}

	static IEnumerable<int> Offer(ChooseNumberChoice n, int pay, out int most)
	{
		most = n.Max;
		return [pay + 1]; // option id = the number + 1
	}

	[Test]
	public void GalvanicDischarge_YouGetThreeEnergy_PayAllThree_ThreeDamage()
	{
		var ox = Ox("Ox");
		var most = Discharge(ox, 3);
		Assert.That(most, Nu.EqualTo(3), "{E}{E}{E}, then pay any amount of {E}");
		Assert.That((s.GetCard(ox).Damage, Energy(me)), Nu.EqualTo((3, 0)), "that much damage");
		Assert.That(
			s.GetPlayer(me).Counters.ContainsKey(CounterKinds.Energy),
			Nu.False,
			"none left is no counter — as a permanent's"
		);
	}

	[Test]
	public void GalvanicDischarge_EnergyKept_TheNextPaysFour()
	{
		var ox = Ox("Ox");
		Discharge(ox, 2);
		Assert.That((s.GetCard(ox).Damage, Energy(me)), Nu.EqualTo((2, 1)), "one {E} left");

		var other = Ox("Other");
		var most = Discharge(other, 4);
		Assert.That(
			(most, s.GetCard(other).Damage, Energy(me)),
			Nu.EqualTo((4, 4, 0)),
			"the {E} kept is paid with the next three"
		);
	}

	// ===== Poison =====

	[Test]
	public void Poison_TenCountersLoses_NineDoesNot()
	{
		s = s.PutCounters(them, CounterKinds.Poison, 9);
		s = StateBasedActions.Check(s);
		Assert.That(s.GetPlayer(them).HasLost, Nu.False);
		s = s.PutCounters(them, CounterKinds.Poison, 1);
		s = StateBasedActions.Check(s);
		Assert.That(s.GetPlayer(them).HasLost, Nu.True, "ten or more poison counters (CR 704.5c)");
	}

	// ===== Fable of the Mirror-Breaker // Reflection of Kiki-Jiki =====

	Card? Mine(string name) =>
		s.CardsIn(me, ZoneType.Battlefield).SingleOrDefault(c => c.Name == name);

	[Test]
	public void Fable_IsRed_ChapterI_ARedGoblinShaman_ThatMakesATreasureAsItAttacks()
	{
		var fable = Hand(Lsv(Fable), me);
		Assert.That(
			s.GetCard(fable).Colors,
			Nu.EqualTo(new[] { ManaColor.Red }),
			"its front face's colour, from {2}{R}"
		);
		CastAsking(fable, Defaults);

		var shaman = Mine("Goblin Shaman");
		Assert.That(shaman, Nu.Not.Null, "chapter I as it enters");
		Assert.That(
			(s.Power(shaman!), s.Toughness(shaman!), shaman!.Colors.Single()),
			Nu.EqualTo((2, 2, ManaColor.Red))
		);

		s = s.UpdateObject(shaman.Id, shaman with { ControlledSinceTurn = 0 }); // TEST SETUP: not summoning sick
		AttackWith(Defaults, shaman.Id);
		Assert.That(
			Mine("Treasure"),
			Nu.Not.Null,
			"whenever this token attacks, create a Treasure"
		);
	}

	/// <summary>TEST SETUP: a lore counter on the Saga (as its main phase's turn-based action would), the chapter resolved.</summary>
	/// <summary>I cast Fable (chapter I as it enters); the turns pass until my Nth precombat main's chapter has resolved.</summary>
	int CastFable(int toChapter)
	{
		var fable = Hand(Lsv(Fable), me);
		CastAsking(fable, Defaults);
		var turn = s.GetGame().TurnNumber + 2 * (toChapter - 1);
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber == turn
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.StackIsEmpty()
				&& x.GetPendingChoice() is null
		);
		return fable;
	}

	[Test]
	public void Fable_ChapterII_DiscardUpToTwo_DrawThatMany()
	{
		var fable = Hand(Lsv(Fable), me);
		CastAsking(fable, Defaults);
		var a = Hand(Bear, me);
		var b = Hand(Bear, me);
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetPendingChoice() is ChooseCardsFromHandChoice
		);
		var hand = HandCount(me);

		Resolve(c =>
			c switch
			{
				ChooseCardsFromHandChoice => [a, b],
				_ => Defaults(c),
			}
		);
		Assert.That(
			(InGraveyard(a), InGraveyard(b), HandCount(me)),
			Nu.EqualTo((true, true, hand)),
			"two discarded, two drawn"
		);
	}

	[Test]
	public void Fable_ChapterIII_ExiledThenReturnedTransformed_AsReflectionOfKikiJiki()
	{
		var fable = CastFable(toChapter: 2);
		var incarnation = s.GetCard(fable).Incarnation;

		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().TurnNumber == 5
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.StackIsEmpty()
				&& x.GetPendingChoice() is null
		);

		var reflection = s.GetCard(fable);
		Assert.That(Alive(fable), Nu.True, "returned to the battlefield, not sacrificed after III");
		Assert.That(
			(reflection.Name, reflection.Types, s.Power(reflection), s.Toughness(reflection)),
			Nu.EqualTo(("Reflection of Kiki-Jiki", CardType.Enchantment | CardType.Creature, 2, 2)),
			"transformed"
		);
		Assert.That(
			(
				reflection.Incarnation > incarnation,
				reflection.CountersOf(CounterKinds.Lore),
				reflection.HasComponent<SagaComponent>(),
				reflection.Colors.Single()
			),
			Nu.EqualTo((true, 0, false, ManaColor.Red)),
			"a new object, no Saga, its colour indicator's red"
		);
	}

	/// <summary>TEST SETUP: Fable on my battlefield with its back face up, untapped and not summoning sick.</summary>
	int Reflection()
	{
		var fable = Field(Lsv(Fable), me);
		s = s.UpdateObject(fable, s.GetCard(fable).Transform());
		return fable;
	}

	[Test]
	public void Reflection_CopiesAnotherNonlegendaryCreatureYouControl_WithHaste_SacrificedAtTheEndStep()
	{
		var reflection = Reflection();
		var bear = Field(Bear, me);
		var legend = Field(TestGames.Creature("Legend", "3/3") with { }, me);
		s = s.UpdateObject(legend, s.GetCard(legend) with { Supertypes = Supertype.Legendary });

		var activation = Activations(reflection).ToList();
		Assert.That(
			activation.Select(a => a.Targets.Single()),
			Nu.EquivalentTo(new[] { bear }),
			"another target nonlegendary creature you control"
		);
		s = s.Act(activation.Single());
		Resolve();

		var copy = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Bear" && c.IsToken);
		Assert.That(s.Has(copy, "Haste"), Nu.True, "except it has haste");
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.Cleanup);
		Assert.That(Alive(copy.Id), Nu.False, "sacrificed at the beginning of the next end step");
	}

	[Test]
	public void Reflection_LeavesTheBattlefield_ItsFrontFaceUp()
	{
		var reflection = Reflection();
		s = s.MoveCard(reflection, s.ZoneId(me, ZoneType.Graveyard));
		Assert.That(
			(s.GetCard(reflection).Name, s.GetCard(reflection).HasComponent<Transformed>()),
			Nu.EqualTo((Fable, false)),
			"CR 712.14: a double-faced card off the battlefield is its front face"
		);
	}

	// ===== Etali, Primal Conqueror // Etali, Primal Sickness =====

	/// <summary>Every "cast one" and its cast answered with the first offered — or declined.</summary>
	Func<ChoiceAction, IEnumerable<int>> CastingAll(bool cast) =>
		c =>
			c switch
			{
				ChooseAmongChoice or ChooseCastChoice => cast
					? c.Options.Take(1).Select(o => o.Id)
					: [],
				_ => Defaults(c),
			};

	[Test]
	public void Etali_Enters_EachPlayerExilesUntilANonland_YouCastAnyNumberFree()
	{
		var land = OnTop(TestGames.Card("Mountain"), me);
		var peek = OnTop(Peek, me); // above it: the land is never reached
		var bear = OnTop(Bear, them);
		var etali = Hand(Lsv(Etali), me);
		var hand = HandCount(me);

		var asked = CastAsking(etali, CastingAll(cast: true));

		Assert.That(
			asked.OfType<ChooseAmongChoice>().Count(),
			Nu.EqualTo(2),
			"asked again after each cast — any number"
		);
		Assert.That(
			(InGraveyard(peek), s.IsIn(bear, me, ZoneType.Battlefield), InExile(land)),
			Nu.EqualTo((true, true, false)),
			"my spell and theirs cast free, theirs under my control; a nonland stops the exile"
		);
		Assert.That(HandCount(me), Nu.EqualTo(hand), "Etali cast, Peek drew one");
	}

	[Test]
	public void Etali_Enters_ExilesPastTheLands_Declined_NothingCast()
	{
		var peek = OnTop(Peek, me);
		var land = OnTop(TestGames.Card("Mountain"), me);
		var etali = Hand(Lsv(Etali), me);

		var asked = CastAsking(etali, CastingAll(cast: false));

		Assert.That(
			asked.OfType<ChooseAmongChoice>().Count(),
			Nu.EqualTo(1),
			"declined: asked once"
		);
		Assert.That(
			(InExile(land), InExile(peek)),
			Nu.EqualTo((true, true)),
			"exiled until a nonland"
		);
	}

	[Test]
	public void Etali_ACastDeclinedAfterThePick_EachCardOfferedOnce_NoLoop()
	{
		// a target to choose: several ways to cast it, so the cast is a choice that can be left empty
		var zap = TestCards.Targeted("Zap", TargetSpec.Creature, new Tap(Ref.Target()));
		Field(Bear, them);
		var peek = OnTop(zap, me);
		var bear = OnTop(zap, them);
		var etali = Hand(Lsv(Etali), me);

		var asked = CastAsking(
			etali,
			c =>
				c switch
				{
					ChooseAmongChoice => c.Options.Take(1).Select(o => o.Id),
					ChooseCastChoice => [], // picked, then not cast
					_ => Defaults(c),
				}
		);

		Assert.That(
			asked.OfType<ChooseCastChoice>().Count(),
			Nu.EqualTo(2),
			"each pick's cast declined"
		);
		Assert.That(asked.OfType<ChooseAmongChoice>().Count(), Nu.EqualTo(2), "each card once");
		Assert.That((InExile(peek), InExile(bear)), Nu.EqualTo((true, true)), "nothing cast");
	}

	[Test]
	public void Etali_Transform_NineAndGreenPhyrexian_AsASorcery_An11x11IndestructibleTrampler()
	{
		var etali = Field(Lsv(Etali), me);
		var incarnation = s.GetCard(etali).Incarnation;
		var life = Life(me);
		var main = s;
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.BeginCombat);
		Assert.That(Activations(etali), Nu.Empty, "activate only as a sorcery");
		s = main;

		var activation = Activations(etali).Single(a => a.LifeForPhyrexian == 1);
		s = s.Act(activation);
		Resolve();

		var sickness = s.GetCard(etali);
		Assert.That(Life(me), Nu.EqualTo(life - 2), "{G/P} paid with 2 life");
		Assert.That(
			(sickness.Name, s.Power(sickness), s.Toughness(sickness), sickness.Incarnation),
			Nu.EqualTo(("Etali, Primal Sickness", 11, 11, incarnation)),
			"transformed: the same object"
		);
		Assert.That(
			(s.Has(sickness, "Trample"), s.Has(sickness, "Indestructible")),
			Nu.EqualTo((true, true))
		);
		Assert.That(
			sickness.Colors,
			Nu.EquivalentTo(new[] { ManaColor.Red, ManaColor.Green }),
			"its colour indicator — the front is mono-red"
		);
	}

	[Test]
	public void Transform_ACopyByACopyEffectNever_ATokenCopyDoes()
	{
		var etali = Field(Lsv(Etali), me);
		var bear = Field(Bear, me);
		var metamorph = (Card)
			s.GetCard(bear)
				.WithCopiableValuesOf(s.GetCard(etali))
				.WithComponent(new EnteredAsCopy(s.GetCard(bear))); // TEST SETUP: Phyrexian Metamorph as Etali
		Assert.That(
			metamorph.Transform().Name,
			Nu.EqualTo(metamorph.Name),
			"not a double-faced card: nothing happens (CR 701.28c)"
		);
		var token = s.GetCard(etali) with { IsToken = true };
		Assert.That(
			token.Transform().Name,
			Nu.EqualTo("Etali, Primal Sickness"),
			"a token copy of one is a transforming token"
		);
	}

	[Test]
	public void EtaliPrimalSickness_CombatDamageToAPlayer_ThatManyPoisonCounters()
	{
		var etali = Field(Lsv(Etali), me);
		s = s.UpdateObject(etali, s.GetCard(etali).Transform()); // TEST SETUP: transformed
		var life = Life(them);

		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([d.CreatureId == etali ? them : 0]);
		s = TestGames.PassUntil(s, x => x.IsOver()); // they lose in the damage step's check

		Assert.That(
			(life - Life(them), Poison(them)),
			Nu.EqualTo((11, 11)),
			"the damage, and that many poison counters"
		);
		Assert.That(s.GetPlayer(them).HasLost, Nu.True);
	}
}
