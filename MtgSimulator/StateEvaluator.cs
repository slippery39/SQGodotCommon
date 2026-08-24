using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Evaluates a game state from a given player's perspective.
///
/// Returns a float score where higher is better for the given player.
/// Terminal states (win/loss) return the maximum or minimum score.
/// Non-terminal states use a weighted sum of game factors.
///
/// Uses MtgGameIds zone IDs directly to avoid GetPlayerZoneId lookups,
/// which scan the child list on every call.
/// </summary>
public static class StateEvaluator
{
	public const float WinScore = 10000f;
	public const float LossScore = -10000f;

	/// <summary>
	/// The bar a score must clear to mean "someone won", for scores that have been through a
	/// rollout rather than come straight out of <see cref="Evaluate"/>.
	///
	/// **Never compare a rollout result against <see cref="WinScore"/> directly.**
	/// <c>MultiTurnBeamSearchAiStrategy.DiscountTerminal</c> decays terminals by how many
	/// half-turns they took, so a real win comes back as 9500 or 9025, and <c>>= 10000</c> is
	/// false for every win the search will ever find. That regression shipped: <c>FindWinner</c>
	/// silently stopped returning winners and both <c>ResolveChoice</c> early-outs stopped firing,
	/// which cost 8.8% of run time and, far worse, stopped the AI taking a winning line the moment
	/// it found one.
	///
	/// 2500 sits far above any board score the weights can produce (~150 at the extreme) and far
	/// below the most-decayed win at any sane lookahead (0.95^20 ≈ 3585), so it separates the two
	/// populations with room on both sides.
	/// </summary>
	public const float WinThreshold = WinScore * 0.25f;

	/// <summary>True if this score — raw or discounted — means the player won.</summary>
	public static bool IsWin(float score) => score >= WinThreshold;

	/// <summary>True if this score — raw or discounted — means the game ended either way.</summary>
	public static bool IsDecisive(float score) => MathF.Abs(score) >= WinThreshold;

	private const float LifeWeight = 0.2f;
	private const float CreatureCountWeight = 3.0f;
	private const float TotalPowerWeight = 2.0f;
	private const float CreatureDamageWeight = 0.1f;
	private const float CardsInHandWeight = 1.4f;
	private const float ManaWeight = 2.0f;
	private const float NonCreaturePermanentWeight = 1.5f;

	/// <summary>
	/// How hard the race term pushes. See <see cref="RacePressure"/>.
	///
	/// Large next to the other weights on purpose: at a healthy life total it is a mild nudge
	/// (a 2-power board against 20 life contributes 2.0), but as either player nears death it
	/// dominates every board-quality term — which is correct, because at that point nothing else
	/// decides the game.
	/// </summary>
	private const float RacePressureWeight = 20f;

	/// <summary>
	/// Clamp on turns-to-kill, so "dead next turn" and "dead twice over next turn" score the same
	/// rather than diverging toward infinity.
	/// </summary>
	private const float MinTurnsToKill = 0.5f;

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
	private static float RacePressure(int power, int lifeThreatened)
	{
		if (power <= 0)
			return 0f;

		var turnsToKill = MathF.Max(lifeThreatened / (float)power, MinTurnsToKill);
		return RacePressureWeight / turnsToKill;
	}

	/// <summary>
	/// The hot path — one line over <see cref="Explain"/>, so there is exactly one copy of the
	/// weighted sum and a display path cannot drift from the scoring path.
	///
	/// **This delegation was measured, and the measurement is worth knowing about, because the
	/// first three attempts said the opposite.** Delegating appeared to cost +11.8% wall time over
	/// 300 games; rewriting it to share only the battlefield walk still showed +8.0%; restoring the
	/// original body byte-for-byte showed +8.8%. Against a +3% budget that read as damning, and the
	/// sum was duplicated to work around it.
	///
	/// All three numbers were measuring a different bug. The terminal discount had broken every
	/// win-detection comparison in the search (see <see cref="WinThreshold"/>), so the beam stopped
	/// short-circuiting on a found win and burned its entire rollout budget on every move. With
	/// that fixed, delegation costs **~0%** (57.75s vs 58.78s over three interleaved rounds, the
	/// difference sitting inside first-run JIT warmup).
	///
	/// The tell was <c>Avg actions/game</c>, not the clock: actions went DOWN while time went UP,
	/// which is impossible for a per-call cost and pointed straight at the search doing more work
	/// per decision. **Wall time alone would have shipped duplicated evaluator logic to work around
	/// a bug two commits earlier** — the third time in this project a clock reading has accused the
	/// wrong code, after the stale-binary trap and the parallel-batch draw disaster.
	///
	/// Do not re-split these on an unmeasured hunch, and if you do measure, read the action count
	/// beside the time.
	/// </summary>
	public static float Evaluate(GameState state, MtgGameIds ids, int playerId) =>
		Explain(state, ids, playerId).Total;

	/// <summary>
	/// One battlefield walk, factored out of <see cref="Explain"/> so the two zone loops are
	/// written once. Four ints, so it returns in registers rather than through a buffer.
	/// </summary>
	private static (int Creatures, int Power, int Damage, int NonCreatures) ScanBattlefield(
		GameState state,
		int battlefieldId
	)
	{
		var creatures = 0;
		var power = 0;
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
				damage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				nonCreatures++;
			}
		}

		return (creatures, power, damage, nonCreatures);
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

	/// <summary>
	/// The score, split into the terms that produced it — for the AI inspector and the scenario
	/// viewer.
	///
	/// **This is the implementation; <see cref="Evaluate"/> is a one-line wrapper over
	/// <c>.Total</c>.** That direction is deliberate. A second copy of the weighted sum written for
	/// display would drift from the one the search uses, and an inspector showing terms that do not
	/// add up to the real score sends you hunting a discrepancy that exists only in the renderer.
	/// It costs nothing measurable — see the note on <c>Evaluate</c> for the measurement and for
	/// why the first three attempts at it said otherwise.
	///
	/// Returns a <c>readonly record struct</c>, so producing it on the hottest call in the engine
	/// allocates nothing.
	/// </summary>
	public static EvaluationBreakdown Explain(GameState state, MtgGameIds ids, int playerId)
	{
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var player = state.GetPlayer(playerId);
		var opponent = state.GetPlayer(opponentId);

		if (player.HasLost)
			return Terminal(LossScore);
		if (opponent.HasLost)
			return Terminal(WinScore);

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

		return new EvaluationBreakdown(
			life,
			creatures,
			power,
			creatureDamage,
			nonCreaturePermanents,
			hand,
			mana,
			race,
			life + creatures + power + creatureDamage + nonCreaturePermanents + race + hand + mana,
			IsTerminal: false
		);
	}

	// Terminal states short-circuit before any term is computed, so every term is zero and the
	// total carries the whole score. IsTerminal is what lets the inspector say "loss" rather than
	// printing eight zeroes next to -10000.
	private static EvaluationBreakdown Terminal(float score) =>
		new(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, score, IsTerminal: true);

	private static int CountNonLand(GameState state, int handId) =>
		state.GetCardsInZone(handId).Count(c => !c.HasSubtype("Land"));
}
