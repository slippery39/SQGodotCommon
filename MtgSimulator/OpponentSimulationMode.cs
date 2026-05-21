namespace MtgSimulator;

public enum OpponentSimulationMode
{
	PassTurn, // opponent does nothing — just ends turn immediately
	Greedy, // opponent plays their single best action by StateEvaluator, then ends turn
	Random, // opponent plays one random non-EndTurn action, then ends turn
}
