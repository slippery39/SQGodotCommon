using Godot;
using Logging;

namespace Common;

/// <summary>
/// Logger for Godot's built-in logging
/// </summary>
public class GodotLogger : BaseLogger
{
	protected override void WriteLog(LogLevel level, string message)
	{
		switch (level)
		{
			case LogLevel.Debug:
				GD.Print($"[DEBUG] {message}");
				break;
			case LogLevel.Info:
				GD.Print($"[INFO] {message}");
				break;
			case LogLevel.Warning:
				GD.PushWarning($"[WARNING] {message}");
				break;
			case LogLevel.Error:
			case LogLevel.Critical:
				GD.PushError($"[{level}] {message}");
				break;
		}
	}
}
