using Common;
using Logging;

namespace Project;

/// <summary>
/// Change this class to configure settings and add services in the game manager for your specific game.
/// </summary>
public static class StartupSettings
{
	public static void ConfigureSettings(GameManager manager)
	{
		if (!InputMap.HasAction("move_left"))
		{
			InputMap.AddAction("move_left");
			InputMap.ActionAddEvent("move_left", new InputEventKey { Keycode = Key.A });
		}
		if (!InputMap.HasAction("move_right"))
		{
			InputMap.AddAction("move_right");
			InputMap.ActionAddEvent("move_right", new InputEventKey { Keycode = Key.D });
		}

		LogManager.Instance.SetLogger(new CompositeLogger(new ConsoleLogger(), new GodotLogger()));

		//Just an example of how to add a service.
		//manager.AddService<DynamicScriptingService<GameState>>();
	}
}
