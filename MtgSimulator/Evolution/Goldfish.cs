using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// **How fast can this deck win if nobody interferes?** The solitaire test real players call
/// goldfishing: play against an opponent that does nothing and count the turns.
///
/// **This exists because win rate is the wrong fitness for a half-built combo deck.** A storm deck
/// missing two rituals loses every game, so a win-rate fitness reports it as garbage and prunes it
/// back toward good cards — which is exactly what every run in this project has done. It is not
/// that the deck is judged too early; it is that the wrong thing is being measured.
///
/// A goldfish measures whether the ENGINE ASSEMBLES, which is what a combo deck is trying to do
/// and which improves smoothly as the deck gets closer. Turn 12 to turn 9 to turn 6 is a gradient
/// a hill climber can follow; 0% to 0% to 0% win rate is not.
///
/// **It names no archetype.** "Turns to kill an inert opponent" covers storm (kills on the turn it
/// goes off), reanimator (a fatty in play is a fast clock) and affinity (a wide cheap board is a
/// fast clock) with one number, and it would cover an archetype nobody has thought of yet.
///
/// **What it deliberately cannot see:** interaction, resilience, and whether the plan survives
/// removal — a goldfish rates a deck that dies to a stiff breeze exactly as highly as one that does
/// not. It is an EXPLORATION signal for assembling an engine, never a substitute for the win rate
/// against a real field. Use it to find candidates, then measure them properly.
/// </summary>
public static class Goldfish
{
	/// Turns after which a deck that has not won is simply "too slow to matter".
	public const int MaxTurns = 20;

	/// Returned when the deck never wins inside <see cref="MaxTurns"/>.
	public const int NeverWins = 99;

	/// <summary>
	/// An opponent that does nothing at all — no plays, no attacks, no blocks (there are none).
	///
	/// A passive opponent rather than an absent one because the engine has no concept of a solo
	/// game: something has to hold the other seat, take turns and be killable.
	/// </summary>
	private sealed class InertStrategy : IAiStrategy
	{
		public GameAction SelectAction(GameState state, MtgGameIds ids, int playerId)
		{
			var legal = MtgActionGenerator.GetLegalActions(state, ids, playerId);
			return legal.FirstOrDefault(a => a is EndTurnAction) ?? legal[0];
		}

		/// Takes the minimum required, arbitrarily. An inert player's choices should never decide
		/// anything; if one does, the goldfish is measuring the wrong seat.
		public ImmutableList<int> ResolveChoice(
			GameState state,
			ChoiceAction choice,
			int playerId
		) =>
			choice
				.GetOptions(state, null)
				.Where(o => o.IsEnabled)
				.Take(Math.Max(0, choice.MinChoices))
				.Select(o => o.Id)
				.ToImmutableList();
	}

	public readonly record struct Result(int TurnsToWin, float FinalScore, bool Won)
	{
		/// Lower is better and 99 means "never". Ranking on this alone makes a deck that wins on
		/// turn 6 strictly better than one that wins on turn 7, which is the whole point.
		public int Speed => Won ? TurnsToWin : NeverWins;
	}

	/// <summary>
	/// Plays one solitaire game and reports how fast the deck killed an inert opponent.
	///
	/// <paramref name="seed"/> varies the shuffle; average several, because a combo deck's speed
	/// is a distribution and a single draw says very little about it.
	/// </summary>
	public static Result Play(
		Decklist deck,
		IReadOnlyDictionary<string, Card> pool,
		int seed,
		int aiDepth = 2
	)
	{
		// The inert seat needs a legal deck; it never casts anything, so contents do not matter.
		var filler = Decklist.Empty("Inert") with
		{
			Lands = Decklist.DeckSize,
		};

		var (state, ids, cardNames) = GameSetup.FromDecks(
			owner => deck.Materialize(owner, pool),
			owner => filler.Materialize(owner, pool)
		);

		var runner = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids,
				aiDepth,
				rng: new Random(seed + 1),
				cardValues: AiCardValues.Current
			),
			new InertStrategy()
		);

		var (result, finalState) = runner.Run(state, ids, cardNames, seed + 2, seed + 3);

		var won = result.IsPlayer1Win;
		var turns = won ? result.TurnCount : NeverWins;

		// Board score at the end, so a deck that did not kill is still ranked by how close it got —
		// reanimator with a fatty in play on turn 4 has clearly assembled even if the kill is later.
		var score = StateEvaluator.Evaluate(finalState, ids, ids.Player1Id);

		return new Result(turns, score, won);
	}

	/// <summary>
	/// Median speed and mean end-of-game score over several shuffles.
	///
	/// **Median, not mean, for the turn count.** `NeverWins` is 99, so one unlucky shuffle would
	/// drag a mean far more than it should; the median asks "how fast is this deck usually", which
	/// is the question.
	/// </summary>
	public static (double MedianSpeed, double MeanScore, int Wins) Measure(
		Decklist deck,
		IReadOnlyDictionary<string, Card> pool,
		int games = 10,
		int seed = 50_000,
		int aiDepth = 2
	)
	{
		var speeds = new List<int>(games);
		var scores = 0.0;
		var wins = 0;

		for (var i = 0; i < games; i++)
		{
			var r = Play(deck, pool, seed + i * 11, aiDepth);
			speeds.Add(r.Speed);
			scores += r.FinalScore;
			if (r.Won)
				wins++;
		}

		speeds.Sort();
		var median =
			speeds.Count % 2 == 1
				? speeds[speeds.Count / 2]
				: (speeds[speeds.Count / 2 - 1] + speeds[speeds.Count / 2]) / 2.0;

		return (median, scores / games, wins);
	}
}
