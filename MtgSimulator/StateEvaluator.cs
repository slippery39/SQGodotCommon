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

	public static float Evaluate(GameState state, MtgGameIds ids, int playerId) =>
		Explain(state, ids, playerId).Total;

	/// <summary>
	/// The same score as <see cref="Evaluate"/>, split into its terms.
	///
	/// This is the implementation and <see cref="Evaluate"/> is the one-line wrapper, rather than
	/// the other way round — a second copy of the weighted sum written for display would drift
	/// from the one the search uses, and an inspector showing terms that do not sum to the real
	/// score is worse than no inspector. <c>EvaluationBreakdownTests</c> pins the two together.
	///
	/// Returns a struct, so this allocates nothing and the hot path pays only for the terms it
	/// was already computing.
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

		// Use known zone IDs directly — no child list scanning
		var playerBattlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;
		var playerHandId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var opponentHandId = playerId == ids.Player1Id ? ids.Player2HandId : ids.Player1HandId;

		var life = (player.Life - opponent.Life) * LifeWeight;

		var playerCreatureCount = 0;
		var playerPower = 0;
		var playerCreatureDamage = 0;
		var playerNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(playerBattlefieldId))
		{
			var creature = c.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				playerCreatureCount++;
				// Permanent power only — UntilEndOfTurn buffs (Giant Growth etc.) evaporate next turn
				// and should not count as lasting board advantage.
				playerPower += state.GetEffectivePermanentPower(c.Id);
				playerCreatureDamage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				playerNonCreatureCount++;
			}
		}

		var opponentCreatureCount = 0;
		var opponentPower = 0;
		var opponentCreatureDamage = 0;
		var opponentNonCreatureCount = 0;
		foreach (var c in state.GetCardsInZone(opponentBattlefieldId))
		{
			var creature = c.GetComponent<CreatureComponent>();
			if (creature != null)
			{
				opponentCreatureCount++;
				opponentPower += state.GetEffectivePermanentPower(c.Id);
				opponentCreatureDamage += creature.Damage;
			}
			else if (c.HasComponent<PermanentComponent>())
			{
				opponentNonCreatureCount++;
			}
		}

		var creatures = (playerCreatureCount - opponentCreatureCount) * CreatureCountWeight;
		var power = (playerPower - opponentPower) * TotalPowerWeight;
		// Damage on surviving creatures is a hidden disadvantage the board snapshot misses.
		// A creature with lethal-minus-one damage is much more fragile than a fresh one.
		var creatureDamage = (opponentCreatureDamage - playerCreatureDamage) * CreatureDamageWeight;

		var nonCreaturePermanents =
			(playerNonCreatureCount - opponentNonCreatureCount) * NonCreaturePermanentWeight;

		// Whose clock is shorter. Without this the board terms above are the only thing that knows
		// creatures exist, and they weigh a 2/2 the same whether the player facing it is at 20 or
		// at 2. See RacePressure.
		var race =
			RacePressure(playerPower, opponent.Life) - RacePressure(opponentPower, player.Life);

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
