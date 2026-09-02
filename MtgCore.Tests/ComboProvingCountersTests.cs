using System.Collections.Immutable;
using ImmutableGameObjects;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// The counters package and the shipped drain pair, asserted on the real set cards.
/// </summary>
[TestFixture]
public class ComboProvingCountersTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	private static Card Card(string name) => ComboProving.Cards.Single(c => c.Name == name);

	// ===== counters =====

	/// <summary>
	/// The multiplier, and the reason the package is a package: one enabler makes every counter the
	/// deck ADDS bigger. A REPLACEMENT rather than a trigger, so it cannot feed itself.
	/// </summary>
	[Test]
	public void TheRiteMakesAddedCountersBigger()
	{
		var (bare, ballista) = Play(_state, Card("Sporeback Ballista"), xValue: 1);
		var grafted = Activate(bare, ballista.Id, 0, abilityIndex: 1);
		Assert.That(Power(grafted, ballista.Id), Is.EqualTo(2), "1 counter plus the graft");

		var (withRite, _) = Play(_state, Card("Ironscale Rite"));
		var (b2, ballista2) = Play(withRite, Card("Sporeback Ballista"), xValue: 1);
		var grafted2 = Activate(b2, ballista2.Id, 0, abilityIndex: 1);

		Assert.That(
			Power(grafted2, ballista2.Id),
			Is.EqualTo(3),
			"the same graft puts on two counters instead of one"
		);
	}

	/// <summary>
	/// **KNOWN ENGINE LIMITATION, characterised rather than asserted as desirable.** A counter
	/// bonus does NOT apply to a creature ENTERING with counters, only to counters added afterwards.
	///
	/// `EntersWithCountersComponent` is stamped by `PutIntoBattlefieldAction`'s ETB ceremony rather
	/// than going through `AddCountersAction`, and the `CountersPlaced` replacement lives inside
	/// that action — so the ceremony never consults it. Real Magic's Hardened Scales and Conclave
	/// Mentor DO apply on entry, so this is a divergence, and it makes both cards weaker than
	/// printed.
	///
	/// **Deliberately not fixed here.** Routing entry counters through the replacement would change
	/// a live CSC interaction (Conclave Mentor plus every Hydra and Hangarback Walker), which is a
	/// measured-set behaviour change and the user's call, not a side effect of adding a test set.
	/// This test exists so the limitation is recorded and a future fix has something to flip.
	/// </summary>
	[Test]
	public void EntryCountersAreNotBoostedByACounterBonus()
	{
		var (bare, servitor) = Play(_state, Card("Scrapyard Servitor"));
		Assert.That(Power(bare, servitor.Id), Is.EqualTo(1), "0/0 entering with one counter");

		var (withRite, _) = Play(_state, Card("Ironscale Rite"));
		var (boosted, servitor2) = Play(withRite, Card("Scrapyard Servitor"));

		Assert.That(
			Power(boosted, servitor2.Id),
			Is.EqualTo(1),
			"still one — entry counters bypass the CountersPlaced replacement"
		);
	}

	/// <summary>
	/// **The Ballista's ability must be UNPAYABLE at zero counters.** Written as an effect rather
	/// than a cost it would still resolve — AddCountersAction floors at zero while the damage half
	/// fires regardless — which is free repeatable damage and an infinite loop the AI would take.
	/// </summary>
	[Test]
	public void TheBallistaCannotFireWithoutACounter()
	{
		// X = 1 so it survives arrival at all — a 0/0 with no counters is destroyed on entry by the
		// zero-toughness rule, which is correct for the printed card and would otherwise make this
		// test pass for the wrong reason. The counter is then spent, leaving it at 0/0 and alive
		// only for as long as the state-based check has not run again.
		var (s, ballista) = Play(_state, Card("Sporeback Ballista"), xValue: 1);
		s = SpendEveryCounter(s, ballista.Id);

		var attempt = new ActivateAbilityAction
		{
			CardId = ballista.Id,
			AbilityIndex = 0,
			ActivatingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableList.Create(_ids.Player2Id),
		};

		Assert.That(
			attempt.ValidateAdd(s).IsValid,
			Is.False,
			"a Ballista with no counters must not be able to shoot"
		);
	}

	/// <summary>And the paired positive: with a counter, it fires and shrinks.</summary>
	[Test]
	public void TheBallistaSpendsACounterToDealDamage()
	{
		var (s, ballista) = Play(_state, Card("Sporeback Ballista"), xValue: 2);

		var lifeBefore = Life(s, _ids.Player2Id);
		var after = Activate(s, ballista.Id, _ids.Player2Id);

		Assert.Multiple(() =>
		{
			Assert.That(Life(after, _ids.Player2Id), Is.EqualTo(lifeBefore - 1), "it dealt 1");
			Assert.That(Power(after, ballista.Id), Is.EqualTo(1), "and spent a counter doing it");
		});
	}

	/// <summary>Modular: the counter is handed on rather than lost when the body dies.</summary>
	[Test]
	public void ModularMovesACounterOnDeath()
	{
		var (s, servitor) = Play(_state, Card("Scrapyard Servitor"));
		var (s2, ravager) = Play(s, Card("Scrapyard Ravager"));

		var before = Power(s2, ravager.Id);
		var after = Destroy(s2, servitor.Id);

		Assert.That(
			Power(after, ravager.Id),
			Is.EqualTo(before + 1),
			"the dying Servitor's counter moved to the Ravager"
		);
	}

	// ===== the drain pair, on the SHIPPED cards =====

	/// <summary>
	/// The same result `LifeDrainLoopTests` pins on inline definitions, re-asserted on the cards
	/// that actually ship. The inline version proves the mechanic; this proves the SET has it.
	/// </summary>
	[Test]
	public void TheShippedDrainPairKillsFromOneLifeGained()
	{
		var (s, _) = Play(_state, Card("Covenant of Thorns"));
		var (s2, _) = Play(s, Card("Sanguine Reciprocity"));

		var after = GainLife(s2, 1);

		Assert.Multiple(() =>
		{
			Assert.That(Life(after, _ids.Player2Id), Is.LessThanOrEqualTo(0));
			Assert.That(((MtgPlayer)after.GetObject(_ids.Player2Id)).HasLost, Is.True);
		});
	}

	/// <summary>
	/// The control: either half alone must be harmless, or the pair result says nothing about the
	/// interaction. This is the check that caught an inert Sanguine Bond in the first place.
	/// </summary>
	[Test]
	public void NeitherHalfOfTheDrainPairKillsAlone()
	{
		foreach (var name in new[] { "Covenant of Thorns", "Sanguine Reciprocity" })
		{
			var (s, _) = Play(_state, Card(name));
			var after = GainLife(s, 1);

			Assert.That(
				Life(after, _ids.Player2Id),
				Is.GreaterThan(0),
				$"{name} alone should not kill anyone"
			);
		}
	}

	// ===== helpers =====

	private static int Power(GameState s, int cardId) => s.GetEffectivePower(cardId);

	private static int Life(GameState s, int playerId) => ((MtgPlayer)s.GetObject(playerId)).Life;

	/// Strips every +1/+1 counter directly, so "no counters" is the state under test rather than a
	/// card that never had any and died on arrival.
	private static GameState SpendEveryCounter(GameState state, int cardId)
	{
		var card = (Card)state.GetObject(cardId);
		var counters = card.GetComponent<PlusOneCounterComponent>();
		return counters is null
			? state
			: state.UpdateObject(cardId, card.WithComponentReplaced(counters with { Count = 0 }));
	}

	private static GameState Destroy(GameState state, int cardId)
	{
		var (next, _) = state
			.AddAction(new DestroyCreatureAction { TargetIds = ImmutableList.Create(cardId) })
			.ProcessAllActions();
		return next;
	}

	private GameState GainLife(GameState state, int amount)
	{
		var (next, _) = state
			.AddAction(
				new GainLifeAction
				{
					Amount = amount,
					TargetIds = ImmutableList.Create(_ids.Player1Id),
				}
			)
			.ProcessAllActions();
		return next;
	}

	private GameState Activate(GameState state, int sourceId, int targetId, int abilityIndex = 0)
	{
		var (next, _) = state
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = sourceId,
					AbilityIndex = abilityIndex,
					ActivatingPlayerId = _ids.Player1Id,
					TargetIds =
						targetId == 0 ? ImmutableList<int>.Empty : ImmutableList.Create(targetId),
				}
			)
			.ProcessAllActions();
		return next;
	}

	/// Through PutIntoBattlefieldAction so the ETB ceremony runs — EntersWithCountersComponent is
	/// applied there, so a test placing cards directly would see every 0/0 as a 0/0 and die to the
	/// zero-toughness rule.
	private (GameState, Card) Play(GameState state, Card card, int xValue = 0)
	{
		var action = new PutIntoBattlefieldAction
		{
			CardTemplate = card with { OwnerId = _ids.Player1Id, ControllerId = _ids.Player1Id },
		};

		// EntersWithCountersComponent{FromXValue} reads ContextKeys.XValue off the entry action, so
		// an {X} creature played without it arrives as a 0/0 and is destroyed on the spot.
		if (xValue > 0)
			action = action with
			{
				InputContext = action.InputContext.SetItem(ContextKeys.XValue, xValue),
			};

		var (next, _) = state.AddAction(action).ProcessAllActions();

		var placed = next.GetCardsInZone(_ids.Player1BattlefieldId).Last(c => c.Name == card.Name);
		return (next, placed);
	}
}
