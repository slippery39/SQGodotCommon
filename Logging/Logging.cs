namespace Logging;

public enum LogLevel
{
	Debug,
	Info,
	Warning,
	Error,
	Critical,
}

/// <summary>
/// Interface for all loggers
/// </summary>
public interface ILogger
{
	void Log(LogLevel level, string message);
	void Debug(string message);
	void Info(string message);
	void Warning(string message);
	void Error(string message);
	void Critical(string message);
	void SetMinimumLogLevel(LogLevel level);
}

/// <summary>
/// Base logger implementation with common functionality
/// </summary>
public abstract class BaseLogger : ILogger
{
	protected LogLevel MinimumLogLevel { get; private set; } = LogLevel.Debug;

	public void SetMinimumLogLevel(LogLevel level)
	{
		MinimumLogLevel = level;
	}

	public void Log(LogLevel level, string message)
	{
		if (level >= MinimumLogLevel)
		{
			WriteLog(level, message);
		}
	}

	protected abstract void WriteLog(LogLevel level, string message);

	public void Debug(string message) => Log(LogLevel.Debug, message);

	public void Info(string message) => Log(LogLevel.Info, message);

	public void Warning(string message) => Log(LogLevel.Warning, message);

	public void Error(string message) => Log(LogLevel.Error, message);

	public void Critical(string message) => Log(LogLevel.Critical, message);
}

/// <summary>
/// Logger that writes to the console (useful for VS Code debugging)
/// </summary>
public class ConsoleLogger : BaseLogger
{
	protected override void WriteLog(LogLevel level, string message)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		var formattedMessage = $"[{timestamp}] [{level}] {message}";

		switch (level)
		{
			case LogLevel.Debug:
			case LogLevel.Info:
				Console.ForegroundColor = ConsoleColor.Blue;
				Console.WriteLine(formattedMessage);
				break;
			case LogLevel.Warning:
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Error.WriteLine(formattedMessage);
				break;
			case LogLevel.Error:
			case LogLevel.Critical:
				Console.ForegroundColor = ConsoleColor.Red;
				Console.Error.WriteLine(formattedMessage);
				break;
		}
	}
}

public class CriticalOnlyLogger : BaseLogger
{
	protected override void WriteLog(LogLevel level, string message)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		var formattedMessage = $"[{timestamp}] [{level}] {message}";

		switch (level)
		{
			case LogLevel.Critical:
				Console.Error.WriteLine(formattedMessage);
				break;
			default:
				break;
		}
	}
}

/// <summary>
/// Logger that writes to a file
/// </summary>
public class FileLogger : BaseLogger, IDisposable
{
	private readonly StreamWriter _writer;
	private bool _disposed = false;

	public FileLogger(string filePath)
	{
		var directory = Path.GetDirectoryName(filePath);
		if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		_writer = new StreamWriter(filePath, true);
		_writer.AutoFlush = true;
	}

	protected override void WriteLog(LogLevel level, string message)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		_writer.WriteLine($"[{timestamp}] [{level}] {message}");
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_writer?.Dispose();
			}
			_disposed = true;
		}
	}

	~FileLogger()
	{
		Dispose(false);
	}
}

/// <summary>
/// Composite logger that distributes logs to multiple loggers
/// </summary>
public class CompositeLogger : ILogger
{
	private readonly List<ILogger> _loggers = new List<ILogger>();

	public CompositeLogger(params ILogger[] loggers)
	{
		_loggers.AddRange(loggers);
	}

	public void AddLogger(ILogger logger)
	{
		_loggers.Add(logger);
	}

	public void RemoveLogger(ILogger logger)
	{
		_loggers.Remove(logger);
	}

	public void Log(LogLevel level, string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Log(level, message);
		}
	}

	public void Debug(string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Debug(message);
		}
	}

	public void Info(string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Info(message);
		}
	}

	public void Warning(string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Warning(message);
		}
	}

	public void Error(string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Error(message);
		}
	}

	public void Critical(string message)
	{
		foreach (var logger in _loggers)
		{
			logger.Critical(message);
		}
	}

	public void SetMinimumLogLevel(LogLevel level)
	{
		foreach (var logger in _loggers)
		{
			logger.SetMinimumLogLevel(level);
		}
	}
}
