namespace MtgSimulator;

/// <summary>
/// The card-value table the simulator's AI plays with, loaded once.
///
/// A tiny holder rather than a parameter threaded through five runners: every simulator entry point
/// wants the same table for the same set, and the alternative is the same two lines copied into
/// `SimulatorRunner`, `PreconstructedSimulatorRunner`, `DraftRunner`, `DraftTrainer` and
/// `ScenarioConsole`.
///
/// **Null is a valid state and means the sandbox file is absent**, in which case the AI behaves
/// exactly as it did before the feature existed. That is deliberate degradation — but it is also
/// invisible, so <see cref="Describe"/> exists and the runners print it.
///
/// `sim_results/` is relative to the SHELL's working directory, not the project's, which has
/// silently cost this project a training run before. If this reports "off" when you expect values,
/// check where you ran from before checking the code.
/// </summary>
public static class AiCardValues
{
	private static CardValueTable? _table;
	private static bool _loaded;
	private static string _setCode = "CSC";

	/// <summary>
	/// Which set's values to load. Set before first use; changing it afterwards reloads.
	/// </summary>
	public static string SetCode
	{
		get => _setCode;
		set
		{
			if (_setCode == value)
				return;
			_setCode = value;
			_loaded = false;
			_table = null;
		}
	}

	/// <summary>
	/// Set <c>MTG_CARD_VALUES=off</c> to disable the feature for a whole run.
	///
	/// This exists so the values-on and values-off arms can be trained from the SAME binary and the
	/// SAME seed, differing in one variable. The alternatives are both traps this project has
	/// already paid for: renaming the data file leaves no record in the output of which arm ran,
	/// and rebuilding an old commit reintroduces the stale-<c>bin/</c> risk. <see cref="Describe"/>
	/// prints the result either way, so a run's own log says which arm it was.
	/// </summary>
	private static bool DisabledByEnvironment =>
		string.Equals(
			Environment.GetEnvironmentVariable("MTG_CARD_VALUES"),
			"off",
			StringComparison.OrdinalIgnoreCase
		);

	public static CardValueTable? Current
	{
		get
		{
			if (_loaded)
				return _table;
			_table = DisabledByEnvironment ? null : CardValueTable.TryLoad(_setCode);
			_loaded = true;
			return _table;
		}
	}

	/// <summary>Explicit override — for Godot, which must read its asset through FileAccess.</summary>
	public static void Use(CardValueTable? table)
	{
		_table = table;
		_loaded = true;
	}

	public static string Describe() =>
		Current != null ? $"card values: on ({SetCode}, weight {Current.Weight:0.##})"
		: DisabledByEnvironment ? "card values: OFF (MTG_CARD_VALUES=off)"
		: $"card values: OFF (no {CardValueSandbox.PathFor(SetCode)})";
}
