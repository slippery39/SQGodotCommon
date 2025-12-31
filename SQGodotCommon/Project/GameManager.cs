using System.Collections.Generic;
using System.Linq;
using Common;
using Common.Cards;
using Common.Core;
using Common.Logging;
using Serilog;

namespace Project;

/// <summary>
/// Main entry point for the game.
/// </summary>
public partial class GameManager : Singleton<GameManager>
{
	public Node CurrentScene { get; private set; }
	public Observable<GameEvent> Events => _eventManager.Events;

	private EventManager _eventManager;

	private readonly Dictionary<System.Type, object> _services = new();

	public override void _Ready()
	{
		_eventManager = new EventManager();
		AddChild(new CardUIManager());
	}

	public void ChangeScene(string scenePath)
	{
		if (CurrentScene != null)
		{
			CurrentScene.QueueFree();
		}

		PackedScene newScene = (PackedScene)ResourceLoader.Load(scenePath);

		if (newScene == null)
		{
			Log.Error($"Failed to load scene: {scenePath}");
		}

		Node sceneInstance = newScene.Instantiate();
		GetTree().Root.AddChild(sceneInstance);
		CurrentScene = sceneInstance;

		Log.Information($"Scene changed to: {scenePath}");
	}

	public void GoToMainMenu()
	{
		ChangeScene("res://Project/MainMenu/main_menu.tscn");
	}

	protected override void Initialize()
	{
		InitializeLogging();
		InitializeInputs();
		CallDeferred("LoadInitialScene");
	}

	private void InitializeLogging()
	{
		//Console is not needed here, since the Godot sink also seems to write to the console.
		Log.Logger = new LoggerConfiguration()
			.MinimumLevel.Debug()
			.WriteTo.Godot()
			//.WriteTo.InGameConsole() -this is something we could do later on to write to our in-game console
			.CreateLogger();

		Log.Information("Logging Initialized");
	}

	private void InitializeInputs()
	{
		AddInputActionIfMissing("move_left", new InputEventKey { Keycode = Key.A });
		AddInputActionIfMissing("move_right", new InputEventKey { Keycode = Key.D });

		Log.Information("Inputs Initialized");

		//Just an example of how to add a service.
		//manager.AddService<DynamicScriptingService<GameState>>();
	}

	private void AddInputActionIfMissing(string actionName, InputEventKey key)
	{
		if (!InputMap.HasAction(actionName))
		{
			InputMap.AddAction(actionName);
			InputMap.ActionAddEvent(actionName, key);
		}
	}

	private void LoadInitialScene()
	{
		Log.Information("Loading scene...");
		GoToMainMenu();
	}

	// Service management - simplified but type-safe
	public void RegisterService<T>(T service)
		where T : class
	{
		_services[typeof(T)] = service;
		Log.Debug("Registered service: {ServiceType}", typeof(T).Name);
	}

	public void RegisterService<TInterface, TImplementation>(TImplementation service)
		where TInterface : class
		where TImplementation : class, TInterface
	{
		_services[typeof(TInterface)] = service;
		Log.Debug(
			"Registered service: {Interface} -> {Implementation}",
			typeof(TInterface).Name,
			typeof(TImplementation).Name
		);
	}

	public bool HasService<T>()
		where T : class
	{
		return _services.ContainsKey(typeof(T));
	}

	public void AddEvent(string channel)
	{
		_eventManager.AddEvent(channel);
	}

	public void AddEvent<T>(string channel, T data)
	{
		_eventManager.AddEvent(channel, data);
	}

	public override void _ExitTree()
	{
		Log.Information("GameManager shutting down");

		// Dispose any services that implement IDisposable
		foreach (var service in _services.Values.OfType<System.IDisposable>())
		{
			service.Dispose();
		}

		Log.CloseAndFlush();
	}
}
