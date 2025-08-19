using System.Data;

namespace Logging;

/// <summary>
/// Singleton manager for global access to logging functionality
/// </summary>
public class LogManager
{
	private static LogManager _instance;

	public static LogManager Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = new LogManager();
			}
			return _instance;
		}
	}

	public ILogger Logger { get; private set; }

	private LogManager()
	{
		Logger = new ConsoleLogger();
	}

	public void SetLogger(ILogger logger)
	{
		Logger = logger;
	}

	// Convenience pass-through methods
	public void Debug(string message) => Logger.Debug(message);

	public void Info(string message) => Logger.Info(message);

	public void Warning(string message) => Logger.Warning(message);

	public void Error(string message) => Logger.Error(message);

	public void Critical(string message) => Logger.Critical(message);
}
