using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

/// <summary>
/// The basics suite (AI-17.5): plays any player makes, each a hand-built position with Shayne's answer
/// (2026-10-29, numbered as he answered them — AI-17's Decisions). A failure here is a missed basic play,
/// found before the table finds it. A test that fails today is a task in AI-17.6; once fixed, the fix's
/// switch becomes its control. Test cards are inline — the answer is about the play, not a card — except
/// where the position is about one (Infinite Coursework, a landcycler, Chandra).
/// </summary>
public class BasicsTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	int Put(int player, CardDefinition card, ZoneType zone = ZoneType.Battlefield)
	{
		(s, var id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	void Lands(int n, string land = "Forest")
	{
		for (var i = 0; i < n; i++)
			Put(me, TestGames.Card(land));
	}

	void Life(int player, int life) => s = s.WithPlayer(s.GetPlayer(player) with { Life = life });

	static CardDefinition Spell(
		string name,
		string cost,
		string type,
		TargetSpec? target,
		params GameAction[] effects
	) =>
		new(
			TestGames.Inline(name, cost, type).Data,
			[new SpellEffectsComponent([.. effects]) { Targets = target is null ? [] : [target] }]
		);

	static readonly CardDefinition Pump = Spell(
		"Pump",
		"{1}",
		"Instant",
		TargetSpec.Creature,
		new ModifyUntilEndOfTurn(Ref.Target(), 2, 2)
	);

	static readonly CardDefinition Kill = Spell(
		"Kill",
		"{1}",
		"Sorcery",
		TargetSpec.Creature,
		new Destroy(Ref.Target())
	);

	/// <summary>The search plays our turn (and, to <paramref name="lastTurn"/>, theirs as GreedyAi); every creature of ours that attacked.</summary>
	HashSet<int> Play(int? lastTurn = null, BeamSearchAi.Settings? settings = null)
	{
		var attacked = new HashSet<int>();
		s = TestGames.PlayAis(
			s,
			lastTurn ?? s.GetGame().TurnNumber,
			new BeamSearchAi(me, settings),
			new GreedyAi(them),
			(_, after) =>
				attacked.UnionWith(
					after
						.CardsIn(me, ZoneType.Battlefield)
						.Where(c => c.AttackingId != 0)
						.Select(c => c.Id)
				)
		);
		return attacked;
	}

	// (1) "Yes easy attack, almost 0 downside."
	[Test]
	public void Lethal_ThroughOneBlocker()
	{
		Put(me, TestGames.Creature("A", "3/3"));
		Put(me, TestGames.Creature("B", "3/3"));
		Put(me, TestGames.Creature("C", "2/2"));
		Put(them, TestGames.Creature("Blocker", "2/2"));
		Life(them, 5);

		Play();

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// (2) "Yes this is an attack here."
	[Test]
	public void Lethal_WithMenace()
	{
		Put(me, TestGames.Creature("Brute", "4/4", "Menace"));
		Put(me, TestGames.Creature("Bear", "2/2"));
		Put(them, TestGames.Creature("Blocker", "3/3"));
		Life(them, 4);

		Play();

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// (3) "Only the flyer attacks, the 2/2 would be blocked and no reason to attack (in real life, your
	// opponent might be able to remove the flyer and you wouldn't want your 2/2 to die)." The look-ahead's
	// defender, dead to the flier whatever it blocks, blocked nothing until a finished game kept its board
	// (Evaluator.Terms.EndBoard; Shayne: "it should block anyways"). The CONTROL: without it, the 2/2 attacks.
	[TestCase(Evaluator.Terms.None, ExpectedResult = false)]
	[TestCase(Evaluator.Terms.EndBoard, ExpectedResult = true)]
	public bool Lethal_InTheAir_TheGroundCreatureStaysHome(Evaluator.Terms without)
	{
		Put(me, TestGames.Creature("Flier", "3/3", "Flying"));
		var bear = Put(me, TestGames.Creature("Bear", "2/2"));
		Put(them, TestGames.Creature("Wall A", "4/4"));
		Put(them, TestGames.Creature("Wall B", "4/4"));
		Life(them, 3);

		var attacked = Play(settings: new() { Without = without });

		Assert.That(s.WinnerId(), Is.EqualTo(me));
		return attacked.Contains(bear); // the 2/2 attacked
	}

	// (4) "Yes attack."
	[Test]
	public void Lethal_WithTrample()
	{
		Put(me, TestGames.Creature("Trampler", "6/6", "Trample"));
		Put(them, TestGames.Creature("Blocker", "2/2"));
		Life(them, 4);

		Play();

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// (5) "Yes attack and pump."
	[Test]
	public void Lethal_WithAPump()
	{
		Put(me, TestGames.Creature("Bear", "2/2"));
		Lands(1);
		Put(me, Pump, ZoneType.Hand);
		Life(them, 4);

		Play();

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// (6) "Yes kill it and attack." An instant in hand beside the kill spell (review item 4: every window
	// it could be cast in is a level of the search).
	[Test]
	public void Lethal_AfterKillingTheOnlyBlocker()
	{
		Put(me, TestGames.Creature("A", "3/3"));
		Put(me, TestGames.Creature("B", "3/3"));
		Lands(2);
		Put(me, Kill, ZoneType.Hand);
		Put(me, Pump, ZoneType.Hand);
		Put(them, TestGames.Creature("Blocker", "2/2"));
		Life(them, 6);

		Play();

		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	// (7) "Yes play the 2/2." At 5 with one 3/3 against three 2/2s: unanswered, their swing puts us at 1.
	// Two fixes, each needed: the look-ahead judged their attackers one by one (each 2/2 alone dies to our
	// 3/3), so it never imagined the swing — now it chooses their attack whole (Rollout.Attacks.AsOne); and
	// from 5 life to 1 cost 8 points, under a dead 2/2 and their weaker clock — Shayne: "As your life total
	// gets lower you take on more risk of your opponent having something that can just kill you"
	// (Evaluator.Terms.LowLifeRisk). The CONTROLS: either one off, Draw Two.
	[TestCase(Evaluator.Terms.None, Rollout.Attacks.AsOne, ExpectedResult = true)]
	[TestCase(Evaluator.Terms.LowLifeRisk, Rollout.Attacks.AsOne, ExpectedResult = false)]
	[TestCase(Evaluator.Terms.None, Rollout.Attacks.EachAlone, ExpectedResult = false)]
	public bool FacingASwingToOneLife_ABlockerIsCast_NotACardDraw(
		Evaluator.Terms without,
		Rollout.Attacks attacks
	)
	{
		Put(me, TestGames.Creature("Ours", "3/3"));
		for (var i = 0; i < 3; i++)
			Put(them, TestGames.Creature($"Cadet {i}", "2/2"));
		Lands(2);
		var blocker = Put(me, TestGames.CreatureCosting("{2}", "Blocker", "2/2"), ZoneType.Hand);
		Put(
			me,
			Spell("Draw Two", "{2}", "Sorcery", null, new DrawCards(Ref.You, 2)),
			ZoneType.Hand
		);
		Life(me, 5);

		Play(
			settings: new()
			{
				Without = without,
				RolloutSettings = BeamSearchAi.DefaultRollout with { Attacks = attacks },
			}
		);

		return s.IsOnBattlefield(blocker); // the 2/2 is cast
	}

	// (8) "Yes stay home."
	[Test]
	public void NoAttack_IntoALosingTrade()
	{
		var bear = Put(me, TestGames.Creature("Bear", "2/2"));
		Put(them, TestGames.Creature("Bigger", "3/3"));

		var attacked = Play();

		Assert.That(attacked, Does.Not.Contain(bear));
	}

	// (11) "Yeah thats a good use for it." Infinite Coursework on their only threat (review item 6: a
	// creature that never untaps is still counted whole).
	[Test]
	public void InfiniteCoursework_GoesOnTheirOnlyThreat()
	{
		Lands(3, "Island");
		var coursework = Put(me, CardRegistry.Get("Infinite Coursework"), ZoneType.Hand);
		var threat = Put(them, TestGames.Creature("Threat", "5/5"));

		Play();

		Assert.That(s.IsOnBattlefield(coursework), Is.True, "Coursework is cast");
		Assert.That(s.GetCard(coursework).AttachedToId, Is.EqualTo(threat));
	}

	// (12) "Yes surveil excess lands that are not needed." Eight lands in play, two more on top. A choice of
	// several picks got one cheap answer, the minimum (BeamSearchAi.Settings.BranchPicks). The CONTROL:
	// without it, nothing goes to the graveyard.
	[TestCase(true, ExpectedResult = true)]
	[TestCase(false, ExpectedResult = false)]
	public bool Surveil_BinsLandsWhenFlooded(bool branchPicks)
	{
		Lands(8);
		foreach (var card in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: a library we choose
		int[] lands =
		[
			Put(me, TestGames.Card("Forest"), ZoneType.Library),
			Put(me, TestGames.Card("Forest"), ZoneType.Library),
		];
		for (var i = 0; i < 5; i++)
			Put(me, TestGames.CreatureCosting("{4}", $"Spell {i}", "4/4"), ZoneType.Library);
		var surveil = Put(me, Spell("Look", "{1}", "Sorcery", null, new Surveil(2)), ZoneType.Hand);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = surveil });
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is not null);

		s = new BeamSearchAi(me, new() { BranchPicks = branchPicks }).Decide(s).State;

		return lands.All(id => s.IsIn(id, me, ZoneType.Graveyard));
	}

	// (13) Shayne: "Tutors should fetch the highest potential value card in theory. This is kind of the same
	// problem as surveil and scry, but just with more potential cards to choose from." Claude's reading for a
	// landcycler: two Plains in play and a {1}{B} creature stuck in hand — the Swamp, the one basic that
	// unlocks a card. The search offers the basics by name (Forest, Plains, Swamp), so the CONTROL — one ply,
	// which sees three equal lands in hand — takes the first.
	[Test]
	public void Landcycling_FetchesTheColourTheHandNeeds()
	{
		Lands(2, "Plains");
		foreach (var card in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveCard(card.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: a library we choose
		foreach (var basic in new[] { "Plains", "Forest", "Swamp" })
			Put(me, TestGames.Card(basic), ZoneType.Library);
		for (var i = 0; i < 5; i++)
			Put(me, TestGames.CreatureCosting("{4}", $"Filler {i}", "4/4"), ZoneType.Library);
		Put(me, TestGames.CreatureCosting("{1}{B}", "Rat", "3/3"), ZoneType.Hand);
		var cycler = Put(me, CardRegistry.Get("Apex Witchstalker"), ZoneType.Hand);
		s = s.Act(s.LegalActions().OfType<ActivateAbilityAction>().First(a => a.CardId == cycler));
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is not null);
		var search = s.GetPendingChoice()!;
		string Picked(IEnumerable<int> ids) =>
			string.Join(",", ids.Select(id => s.GetCard(id).Name));

		Assert.That(search, Is.InstanceOf<ChooseFromLibraryChoice>());
		Assert.That(
			Picked(new GreedyAi(me, BeamSearchAi.DefaultRollout.Held).Answer(s, search)),
			Is.Not.EqualTo("Swamp"),
			"the control: one ply sees three equal lands"
		);
		Assert.That(Picked(new BeamSearchAi(me).Decide(s).Answer!), Is.EqualTo("Swamp"));
	}

	// (14) Shayne: "like Chandra for example, even if it has 1 loyalty, you will want to attack it still,
	// because it could draw the opponent a card still (with its first +1) or give your opponent mana … Even
	// with like a 4 or 5 power attacker, the risk of not being able to remove it later on is super high and
	// can be game losing. This is not the same risk as not attacking a jace planeswalker token."
	[
		Test,
		Ignore(
			"AI-9.3: Chandra is priced 2.35, her body and loyalty — her abilities unpriced, and in the look-ahead her +1 only deals us 2 — so 5 at their face (23.60) beats killing her (21.63)"
		)
	]
	public void AWalkerAtOneLoyalty_IsAttacked_OverTheirFace()
	{
		Put(me, TestGames.Creature("Big", "5/5"));
		var chandra = Put(them, CardRegistry.Get("Chandra, Torch of Defiance"));
		s = s.UpdateObject(chandra, s.GetCard(chandra) with { Loyalty = 1 }); // TEST SETUP

		Play();

		Assert.That(s.IsOnBattlefield(chandra), Is.False, "Chandra is attacked and dies");
	}

	// Review item 1 (AI-17.4): below the root only 5 children get a rollout, ranked by the cheap look-ahead
	// (Lookahead.Settle), which aimed every trigger at its first legal target — ours are listed first. The
	// destroyer ("When this enters, destroy target creature", 3 mana) needs the land drop first, then
	// competes with six 1-mana 1/1s; their only creature is a 5/5. The land's line scored as a pass — Settle
	// had the destroyer kill itself, so our rollout never cast it, even on turn 3 — and fell out of the
	// beam's 4. Two fixes, each needed: Settle answers a target by its chooser's best
	// (Evaluator.Terms.SettleChoices); and the rollout makes more than one play a turn
	// (Rollout.Settings.ActionsPerTurn — at 1 the land's line cast one 1-drop on turn 3 with four lands
	// up). The CONTROLS: either one off, a 1-mana creature. ActionsPerTurn stays 1 by default: from 2 the
	// agreed #547 hold casts Unsummon (AI-17.6, for Shayne) — so the default AI still misses this.
	[TestCase(Evaluator.Terms.None, 20, ExpectedResult = true)]
	[TestCase(Evaluator.Terms.SettleChoices, 20, ExpectedResult = false)]
	[TestCase(Evaluator.Terms.None, 1, ExpectedResult = false)]
	public bool AnEntersTrigger_BehindALandDrop_DestroysTheirThreat(
		Evaluator.Terms without,
		int plays
	)
	{
		Lands(2);
		Put(me, TestGames.Card("Forest"), ZoneType.Hand);
		var destroyer = Put(
			me,
			new CardDefinition(
				TestGames.CreatureCosting("{3}", "Destroyer", "2/2").Data,
				[
					new TriggeredAbilityComponent(
						TriggerEvent.SelfEnters,
						[new Destroy(Ref.Target())]
					)
					{
						Targets = [TargetSpec.Creature],
					},
				]
			),
			ZoneType.Hand
		);
		for (var i = 0; i < 6; i++)
			Put(me, TestGames.Creature($"Cheap {i}", "1/1"), ZoneType.Hand);
		var threat = Put(them, TestGames.Creature("Threat", "5/5"));

		Play(
			settings: new()
			{
				Without = without,
				RolloutSettings = BeamSearchAi.DefaultRollout with { ActionsPerTurn = plays },
			}
		);

		return s.IsOnBattlefield(destroyer) && !s.IsOnBattlefield(threat); // cast, and their 5/5 destroyed
	}

	// Shayne, 2026-10-30: a card that can return itself from the graveyard is "basically like paying a cost
	// to be able to have access to a card. So that should be priced in" (Evaluator.Terms.GraveyardAccess):
	// Blessed Ghoul ("{2}{W/B}: Return this card from your graveyard to your hand") at its price in hand less
	// the mana to bring it back; Tinybones, which can't come back, at nothing. The CONTROL: off, nothing.
	[TestCase(Evaluator.Terms.None, "Blessed Ghoul", true)]
	[TestCase(Evaluator.Terms.None, "Tinybones, Pocket Nuisance", false)]
	[TestCase(Evaluator.Terms.GraveyardAccess, "Blessed Ghoul", false)]
	public void ACardThatReturnsItself_KeepsAValueInTheGraveyard(
		Evaluator.Terms without,
		string name,
		bool valued
	)
	{
		var held = BeamSearchAi.DefaultRollout.Held!;
		var card = Put(me, CardRegistry.Get(name), ZoneType.Graveyard);
		var access = s.ActivatedAbilitiesOf(s.GetCard(card))
			.Where(a => a.From == ZoneType.Graveyard)
			.Select(a => a.Cost.ManaValue)
			.DefaultIfEmpty(0)
			.Min();

		var value = Evaluator.Without(
			without,
			() => Evaluator.Explain(s, me, held).Single(x => x.Term == "our graveyard").Value
		);

		Assert.That(
			value,
			Is.EqualTo(valued ? held.Of(name) - Evaluator.RecastPerMana * access : 0).Within(1e-9)
		);
	}

	// Review item 6 (AI-17.4), for Shayne's answer: their 5/5 under our Infinite Coursework ("tap enchanted
	// creature … loses all abilities and doesn't untap during its controller's untap step") and their
	// untapped 3/3; we hold a 1-mana "destroy target creature". Claude's expectation: the 3/3 — the 5/5 can
	// neither attack nor block. Passes as built (2026-10-30): the evaluator still prices the locked 5/5 whole,
	// but the look-ahead never sees it attack, so the kill goes on the 3/3 — item 6 doesn't bite here.
	[Test]
	public void Removal_GoesOnTheLiveThreat_NotOneCourseworkHolds()
	{
		Lands(4, "Island");
		var coursework = Put(me, CardRegistry.Get("Infinite Coursework"), ZoneType.Hand);
		var locked = Put(them, TestGames.Creature("Locked", "5/5"));
		var live = Put(them, TestGames.Creature("Live", "3/3"));
		s = s.Act(
			s.LegalActions()
				.OfType<CastSpellAction>()
				.First(a => a.CardId == coursework && a.Targets.Contains(locked))
		);
		s = TestGames.PassUntil(s, x => x.StackIsEmpty() && x.DecidingPlayerId() == me);
		Assume.That(
			s.GetCard(coursework).AttachedToId == locked
				&& s.GetCard(locked).IsTapped
				&& s.GetGame().Step == Step.PreCombatMain,
			"Coursework holds the 5/5, still our first main"
		);
		Put(me, Kill, ZoneType.Hand);

		Play();

		Assert.That(s.IsOnBattlefield(live), Is.False, "the live 3/3 is destroyed");
		Assert.That(s.IsOnBattlefield(locked), Is.True);
	}
}
