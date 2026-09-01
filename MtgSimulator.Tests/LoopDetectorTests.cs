using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator.Tests;

/// <summary>
/// **A detector that reports "no loops in this pool" is indistinguishable from a broken one**, so
/// the planted-combo test comes first and the clean-pool test is only meaningful after it passes.
/// This project has shipped a vacuous measurement three times — twice for LIFT, once for the
/// synergy gate — and every time the symptom was a plausible column of numbers that measured
/// nothing.
///
/// The plant is a free activated ability rather than a self-triggering token maker, and the reason
/// is mechanical: `CreateCardAction` spawns `PutIntoBattlefieldAction`, whose ETB event would
/// re-fire the trigger INSIDE `ProcessAllActions`, so the loop would hang the engine before the
/// detector ever saw two positions. An activated ability advances one iteration per legal action,
/// which is what a search can observe.
/// </summary>
[TestFixture]
public class LoopDetectorTests
{
	/// <summary>
	/// `MaxActivationsPerTurn` defaults to **1** and is the engine's existing guard against exactly
	/// this. Raising it is what makes the card a bug, and it is the realistic shape of the mistake
	/// this detector exists to catch on a set edit.
	/// </summary>
	private static Card FreeLoopEngine() =>
		CardFactory
			.Creature("Loop Engine", manaCost: 0, power: 1, toughness: 1)
			.WithActivatedAbility(
				"Free Life",
				manaCost: 0,
				effect: e => e.WithLifeGain(1),
				requiresTap: false,
				maxPerTurn: 99
			)
			.Build();

	private static Card Vanilla(string name) =>
		CardFactory.Creature(name, manaCost: 1, power: 1, toughness: 1).Build();

	/// <summary>
	/// A board with the given cards available to player 1, mana available, and the turn started so
	/// summoning sickness does not gate an ability that does not need it.
	///
	/// **Permanents go to the battlefield and everything else goes to HAND.** Putting an instant on
	/// the battlefield leaves it sitting there doing nothing, so a sweep built that way silently
	/// tests no spell at all and reports a clean pool for the wrong reason. A spell has to be
	/// castable to interact.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) Board(params Card[] cards)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		foreach (var card in cards)
			(state, _) = state.AddObject(
				card with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: card.HasComponent<PermanentComponent>()
					? ids.Player1BattlefieldId
					: ids.Player1HandId
			);

		(state, _) = state
			.AddActions(
				[
					new StartTurnAction
					{
						ActivePlayerId = ids.Player1Id,
						BattlefieldId = ids.Player1BattlefieldId,
						SkipDraw = true,
					},
				]
			)
			.ProcessAllActions();

		return (state, ids);
	}

	// ===== The plant: it must FIRE =====

	[Test]
	public void APlantedFreeLoop_IsFound()
	{
		var (state, ids) = Board(FreeLoopEngine());

		var found = LoopDetector.Find(state, ids, ids.Player1Id);

		Assert.That(
			found,
			Is.Not.Null,
			"the detector did not find a deliberately planted free loop"
		);
		TestContext.Out.WriteLine(
			$"found in {found!.Iterations} action(s): {string.Join(" -> ", found.Line)}\n"
				+ $"  gain per iteration: {found.Gain}"
		);
		Assert.That(found.Gain, Does.Contain("life"), "the gain must name the resource that grew");
	}

	// ===== The control: it must NOT fire =====

	[Test]
	public void APlainBoard_HasNoLoop()
	{
		// Without this the test above passes on a detector that returns a loop for everything, which
		// is the same failure mode as a control deck that scores zero by construction.
		var (state, ids) = Board(Vanilla("Bear"), Vanilla("Ox"));

		Assert.That(LoopDetector.Find(state, ids, ids.Player1Id), Is.Null);
	}

	[Test]
	public void TheEnginesOwnActivationCap_ClosesTheLoop()
	{
		// **The guard, asserted as a guard.** The same card at the DEFAULT `maxPerTurn` of 1 is not
		// a loop, which is what makes the plant above a statement about the cap rather than about
		// activated abilities in general.
		var capped = CardFactory
			.Creature("Capped Engine", manaCost: 0, power: 1, toughness: 1)
			.WithActivatedAbility(
				"Free Life",
				manaCost: 0,
				effect: e => e.WithLifeGain(1),
				requiresTap: false
			)
			.Build();

		var (state, ids) = Board(capped);

		Assert.That(LoopDetector.Find(state, ids, ids.Player1Id), Is.Null);
	}

	// ===== The balance sweep =====

	/// <summary>
	/// **Does any single card in the pool loop on its own?** The set-edit regression question, and
	/// the one worth running after every balance pass.
	///
	/// A zero here is only meaningful because `APlantedFreeLoop_IsFound` passes — on its own it is
	/// indistinguishable from a detector that never fires.
	///
	/// Single cards only. Pairs are 306k fixtures and should not be attempted until a one-card
	/// sweep is trusted and fast.
	/// </summary>
	[Test]
	[Explicit("Balance sweep — one fixture per card in the pool.")]
	public void NoSingleCardInThePoolLoops()
	{
		var set = SetRegistry.Get(Environment.GetEnvironmentVariable("MTG_SET") ?? "ALL");
		var cards = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();

		var loops = new List<(string Name, LoopFound Loop)>();
		var threw = 0;

		foreach (var card in cards)
		{
			try
			{
				var (state, ids) = Board(card);
				if (LoopDetector.Find(state, ids, ids.Player1Id) is { } loop)
					loops.Add((card.Name, loop));
			}
			catch
			{
				threw++;
			}
		}

		TestContext.Out.WriteLine(
			$"{set.Code}: {cards.Count} cards, {loops.Count} looping, {threw} threw"
		);
		foreach (var (name, loop) in loops)
			TestContext.Out.WriteLine(
				$"  LOOP  {name}  [{loop.Iterations} action(s)]  gains {loop.Gain}\n"
					+ $"        {string.Join(" -> ", loop.Line)}"
			);

		Assert.That(loops, Is.Empty, "a card loops on its own — read the lines above");
	}

	[Test]
	public void TheTwoCardFixtureStillSeesALoop_AndThePruneKeepsThePairThatCanOne()
	{
		// **Validates the PAIR harness, not the detector** — the search is the same code the
		// single-card plant already exercises; what is new here is the two-card fixture and the
		// prune. A sweep whose fixture or filter is wrong comes back clean and looks like good news.
		var engine = FreeLoopEngine();
		var bear = Vanilla("Bear");

		var (withEngine, ids) = Board(engine, bear);

		static bool Repeatable(Card c) =>
			c.HasComponent<ActivatedAbilityComponent>()
			|| c.HasComponent<TriggeredAbilityComponent>();

		Assert.Multiple(() =>
		{
			Assert.That(
				LoopDetector.Find(withEngine, ids, ids.Player1Id),
				Is.Not.Null,
				"a two-card fixture containing the planted engine must still report it"
			);
			Assert.That(
				Repeatable(engine) || Repeatable(bear),
				Is.True,
				"and the prune must keep it"
			);
			Assert.That(
				Repeatable(bear) || Repeatable(Vanilla("Ox")),
				Is.False,
				"while two vanilla creatures are correctly pruned — nothing repeatable exists"
			);
		});
	}

	/// <summary>
	/// **Does any PAIR of cards loop?** The two-card combo question, asked as a balance check.
	///
	/// Pruned by a necessary condition rather than a heuristic: a loop needs something REPEATABLE,
	/// and casting is not repeatable — the card leaves your hand. The only repeatable sources in
	/// this engine are an activated ability and a triggered ability, so a pair where neither card
	/// has one cannot loop, whatever else it does. Every such pair is skipped and the count is
	/// reported, because a prune that quietly shrinks the search is how a sweep comes back clean
	/// for the wrong reason.
	///
	/// Games run in one parallel batch into a pre-allocated array and are folded sequentially, the
	/// same shape `DraftTrainer` and `MetagameEvolver` use, so thread scheduling cannot reach the
	/// result.
	/// </summary>
	[Test]
	[Explicit("Balance sweep — every ability-bearing pair in the pool. Minutes.")]
	public void NoPairOfCardsInThePoolLoops()
	{
		var set = SetRegistry.Get(Environment.GetEnvironmentVariable("MTG_SET") ?? "ALL");
		var cards = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();

		static bool Repeatable(Card c) =>
			c.HasComponent<ActivatedAbilityComponent>()
			|| c.HasComponent<TriggeredAbilityComponent>();

		var pairs = (
			from i in Enumerable.Range(0, cards.Count)
			from j in Enumerable.Range(i + 1, cards.Count - i - 1)
			where Repeatable(cards[i]) || Repeatable(cards[j])
			select (A: cards[i], B: cards[j])
		).ToList();

		var total = cards.Count * (cards.Count - 1) / 2;
		TestContext.Out.WriteLine(
			$"{set.Code}: {cards.Count} cards, {total} pairs, {pairs.Count} with a repeatable "
				+ $"source ({total - pairs.Count} pruned as unable to loop)"
		);

		var results = new (string Name, LoopFound Loop)?[pairs.Count];
		var threw = 0;

		Parallel.For(
			0,
			pairs.Count,
			i =>
			{
				try
				{
					var (state, ids) = Board(pairs[i].A, pairs[i].B);
					if (LoopDetector.Find(state, ids, ids.Player1Id) is { } loop)
						results[i] = ($"{pairs[i].A.Name} + {pairs[i].B.Name}", loop);
				}
				catch
				{
					Interlocked.Increment(ref threw);
				}
			}
		);

		var loops = results.OfType<(string Name, LoopFound Loop)>().ToList();

		TestContext.Out.WriteLine($"{loops.Count} looping pairs, {threw} threw");
		foreach (var (name, loop) in loops.Take(40))
			TestContext.Out.WriteLine(
				$"  LOOP  {name}  [{loop.Iterations} action(s)]  gains {loop.Gain}\n"
					+ $"        {string.Join(" -> ", loop.Line)}"
			);

		Assert.That(loops, Is.Empty, "a pair loops — read the lines above");
	}

	// ===== The fingerprint's own rules =====

	[Test]
	public void ATappedPermanentDoesNotDominateAnUntappedOne()
	{
		// A line that leaves a creature tapped where it began untapped has SPENT something, so it
		// is not repeatable. Keying permanents on name alone would call this a loop.
		var untapped = Print(("Dork|U", 1), mana: 0);
		var tapped = Print(("Dork|T", 1), mana: 5);

		Assert.Multiple(() =>
		{
			Assert.That(
				tapped.Dominates(untapped),
				Is.False,
				"the creature is no longer available"
			);
			Assert.That(untapped.Dominates(tapped), Is.False, "and it has less mana");
		});
	}

	[Test]
	public void GrowingTheBoardIsDominance_ButAnIdenticalRepeatIsNot()
	{
		// A token engine ends each iteration with strictly MORE permanents, so an equality test
		// would miss every one of them — dominance is the right relation, not sameness.
		var before = Print(("Token|U", 1), mana: 3);
		var after = Print(("Token|U", 2), mana: 3);
		var same = Print(("Token|U", 1), mana: 3);

		Assert.Multiple(() =>
		{
			Assert.That(after.Dominates(before), Is.True);
			Assert.That(before.Dominates(after), Is.False);
			Assert.That(same.Dominates(before), Is.False, "gaining nothing is not a loop");
		});
	}

	[Test]
	public void AGraveyardThatGrewIsNotByItselfAGain()
	{
		// Every spell cast grows the graveyard, so counting it as the gain would make any two casts
		// look like an engine.
		var before = Print(("Dork|U", 1), mana: 3, graveyard: 0);
		var after = Print(("Dork|U", 1), mana: 3, graveyard: 4);

		Assert.That(after.Dominates(before), Is.False);
	}

	private static ResourceFingerprint Print(
		(string Key, int Count) permanent,
		int mana = 0,
		int life = 20,
		int opponentLife = 20,
		int hand = 0,
		int graveyard = 0,
		int storm = 0
	) =>
		new(
			ImmutableSortedDictionary
				.Create<string, int>(StringComparer.Ordinal)
				.Add(permanent.Key, permanent.Count),
			mana,
			life,
			opponentLife,
			hand,
			graveyard,
			storm
		);
}
