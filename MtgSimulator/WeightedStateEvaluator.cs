using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// The weighted-sum evaluator, with its weights as data so they can be varied and measured.
///
/// A <c>record</c> for <c>with</c>: <c>Default with { ToughnessWeight = 1.33f }</c> is a complete
/// experiment arm, and <c>StrengthHarness</c> can play it against <c>Default</c> over 1120 games
/// without a second build. Every weight here was hand-tuned at some point, and three tuning
/// changes in one session were argued rather than measured and were wrong.
///
/// The weights were <c>private const</c> before this. Consts are baked into the IL while
/// properties are field loads, on the most-called function in the engine — so the change was
/// re-measured over 300 games rather than assumed free. See the note in <c>StateEvaluator</c>.
/// </summary>
public sealed record WeightedStateEvaluator : IStateEvaluator
{
	public static readonly WeightedStateEvaluator Default = new();

	public float LifeWeight { get; init; } = 0.2f;
	public float CreatureCountWeight { get; init; } = 3.0f;
	public float TotalPowerWeight { get; init; } = 2.0f;
	public float CreatureDamageWeight { get; init; } = 0.1f;
	public float CardsInHandWeight { get; init; } = 1.4f;
	public float ManaWeight { get; init; } = 2.0f;
	public float NonCreaturePermanentWeight { get; init; } = 1.5f;

	/// <summary>
	/// Weight on remaining toughness (printed toughness minus damage marked).
	///
	/// **Defaults to 0, which is the behaviour that existed before this term did.** The evaluator
	/// has never scored toughness at all: a creature is worth `CreatureCountWeight +
	/// TotalPowerWeight * power`, so a 5/1 and a 5/5 are indistinguishable — including as removal
	/// targets, where with three damage in hand one is killable and the other is not.
	///
	/// Toughness is not cosmetic here despite there being no blocking: <c>AttackAction</c> can
	/// target creatures as well as players, so toughness decides whether a creature survives being
	/// attacked. Churchill's Attack-Value script targets by <c>dpf/hp</c> — power over toughness —
	/// and that ordering is provably optimal in 1-vs-n attrition (Furtak &amp; Buro 2010).
	///
	/// Forge prices toughness at roughly two thirds of power (Power*15 + Toughness*10), which
	/// against this file's TotalPowerWeight of 2.0 suggests ~1.33. That is a starting point for the
	/// harness, not a recommendation — turn it on through an arm and measure.
	/// </summary>
	public float ToughnessWeight { get; init; } = 0f;

	/// <summary>
	/// How hard the race term pushes. See <see cref="RacePressure"/>.
	///
	/// Large next to the other weights on purpose: at a healthy life total it is a mild nudge
	/// (a 2-power board against 20 life contributes 2.0), but as either player nears death it
	/// dominates every board-quality term — which is correct, because at that point nothing else
	/// decides the game.
	/// </summary>
	public float RacePressureWeight { get; init; } = 20f;

	/// <summary>
	/// Clamp on turns-to-kill, so "dead next turn" and "dead twice over next turn" score the same
	/// rather than diverging toward infinity.
	/// </summary>
	public float MinTurnsToKill { get; init; } = 0.5f;

	public float Evaluate(GameState state, MtgGameIds ids, int playerId) =>
		Explain(state, ids, playerId).Total;

	/// <summary>
	/// How threatening a board of the given power is against the given life total, as a number
	/// that grows sharply as the clock shortens.
	///
	/// This is the piece the evaluator was missing. Board power was scored symmetrically and life
	/// as a flat weight on the difference, so a point of life was worth the same at 6 as at 20 and
	/// nothing knew that the opponent's creatures convert into YOUR death. Reported from a real
	/// game: the AI at 6 with a 3/1 haste, the player at 20 with a 2/2. It went face for 3 —
	/// worth +0.6 to a player at 20 — instead of trading the 3/1 into the 2/2, which kills both
	/// and removes the clock that was actually killing it. It died two turns later. Trading scored
	/// 2.6 WORSE, purely for giving up a power-2 board edge.
	///
	/// Deliberately symmetric: the same term that makes the AI respect a clock pointed at it makes
	/// it press one pointed at the opponent, so this is not a blanket shift toward defence.
	///
	/// Approximate on purpose. It counts total power rather than what can legally attack this
	/// turn — summoning sickness, Taunt and "can't attack" are all ignored — because it is a
	/// heuristic for how fast a board kills, and the search itself covers the exact lines.
	/// </summary>
	private float RacePressure(int power, int lifeThreatened)
	{
		if (power <= 0)
			return 0f;

		var turnsToKill = MathF.Max(lifeThreatened / (float)power, MinTurnsToKill);
		return RacePressureWeight / turnsToKill;
	}

	/// <summary>
	/// The score, split into the terms that produced it. This is the implementation;
	/// <see cref="Evaluate"/> is a one-line wrapper over <c>.Total</c>, so a display path cannot
	/// drift from the scoring path. See <c>StateEvaluator.Evaluate</c> for why that delegation is
	/// free, and for the three measurements that said otherwise.
	/// </summary>
	public EvaluationBreakdown Explain(GameState state, MtgGameIds ids, int playerId)
	{
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var player = state.GetPlayer(playerId);
		var opponent = state.GetPlayer(opponentId);

		if (player.HasLost)
			return Terminal(StateEvaluator.LossScore);
		if (opponent.HasLost)
			return Terminal(StateEvaluator.WinScore);

		var (playerBattlefieldId, opponentBattlefieldId, playerHandId, opponentHandId) = ZoneIds(
			ids,
			playerId
		);

		var mine = ScanBattlefield(state, playerBattlefieldId);
		var theirs = ScanBattlefield(state, opponentBattlefieldId);

		var life = (player.Life - opponent.Life) * LifeWeight;
		var creatures = (mine.Creatures - theirs.Creatures) * CreatureCountWeight;
		var power = (mine.Power - theirs.Power) * TotalPowerWeight;
		// Damage on surviving creatures is a hidden disadvantage the board snapshot misses.
		// A creature with lethal-minus-one damage is much more fragile than a fresh one.
		var creatureDamage = (theirs.Damage - mine.Damage) * CreatureDamageWeight;

		// Remaining toughness, so a damaged creature is worth less than a fresh one of the same
		// printed size. Zero by default — see ToughnessWeight.
		var toughness =
			(mine.Toughness - mine.Damage - (theirs.Toughness - theirs.Damage)) * ToughnessWeight;

		var nonCreaturePermanents =
			(mine.NonCreatures - theirs.NonCreatures) * NonCreaturePermanentWeight;

		// Whose clock is shorter. Without this the board terms above are the only thing that knows
		// creatures exist, and they weigh a 2/2 the same whether the player facing it is at 20 or
		// at 2. See RacePressure.
		var race =
			RacePressure(mine.Power, opponent.Life) - RacePressure(theirs.Power, player.Life);

		// Lands in hand are deliberately NOT counted.
		//
		// Hand size is a proxy for options, and a land held is not an option — it is a resource
		// you have failed to deploy. Counting it made playing a land worth only +2.0 mana minus
		// 1.4 for the card leaving hand: a net +0.6, small enough that the beam would sometimes
		// prefer any other line and simply skip the land drop for a turn. Skipping an early land
		// drop is close to the worst play available, and it was costing the AI a third of a mana
		// step whenever the noise went the wrong way.
		var hand =
			(CountNonLand(state, playerHandId) - CountNonLand(state, opponentHandId))
			* CardsInHandWeight;

		// Count only permanent mana (MaxMana), not temporary fast mana (CurrentMana).
		// Fast mana should score 0 unless the depth search finds it enables something worthwhile.
		var mana = player.MaxMana * ManaWeight;

		// Summation order is load-bearing: float addition is not associative and DeterminismTests
		// compares exact results. Toughness is appended LAST so that at its default of 0 every
		// other term sums exactly as it did before this property existed.
		return new EvaluationBreakdown(
			life,
			creatures,
			power,
			creatureDamage,
			nonCreaturePermanents,
			hand,
			mana,
			race,
			toughness,
			life
				+ creatures
				+ power
				+ creatureDamage
				+ nonCreaturePermanents
				+ race
				+ hand
				+ mana
				+ toughness,
			IsTerminal: false
		);
	}

	// Terminal states short-circuit before any term is computed, so every term is zero and the
	// total carries the whole score. IsTerminal is what lets the inspector say "loss" rather than
	// printing nine zeroes next to -10000.
	private static EvaluationBreakdown Terminal(float score) =>
		new(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, score, IsTerminal: true);

	/// <summary>
	/// One battlefield walk, so the two zone loops are written once. Five ints, which still returns
	/// cheaply; the alternative of sharing the breakdown struct was measured and is not free.
	/// </summary>
	private static (
		int Creatures,
		int Power,
		int Toughness,
		int Damage,
		int NonCreatures
	) ScanBattlefield(GameState state, int battlefieldId)
	{
		var creatures = 0;
		var power = 0;
		var toughness = 0;
		var damage = 0;
		var nonCreatures = 0;

		foreach (var c in state.GetCardsInZone(battlefieldId))
		{
			var creature = c.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				creatures++;
				// Permanent power only — UntilEndOfTurn buffs (Giant Growth etc.) evaporate next
				// turn and should not count as lasting board advantage.
				power += state.GetEffectivePermanentPower(c.Id);
				toughness += creature.Toughness;
				damage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				nonCreatures++;
			}
		}

		return (creatures, power, toughness, damage, nonCreatures);
	}

	private static (
		int PlayerBattlefield,
		int OpponentBattlefield,
		int PlayerHand,
		int OpponentHand
	) ZoneIds(MtgGameIds ids, int playerId) =>
		playerId == ids.Player1Id
			? (
				ids.Player1BattlefieldId,
				ids.Player2BattlefieldId,
				ids.Player1HandId,
				ids.Player2HandId
			)
			: (
				ids.Player2BattlefieldId,
				ids.Player1BattlefieldId,
				ids.Player2HandId,
				ids.Player1HandId
			);

	private static int CountNonLand(GameState state, int handId) =>
		state.GetCardsInZone(handId).Count(c => !c.HasSubtype("Land"));
}
