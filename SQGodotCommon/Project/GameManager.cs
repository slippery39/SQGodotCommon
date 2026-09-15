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

	[Export]
	public PackedScene InitialScene { get; set; }

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

	public void ChangeScene(PackedScene packedScene)
	{
		if (CurrentScene != null)
		{
			CurrentScene.QueueFree();
		}

		if (packedScene == null)
		{
			Log.Error($"Could not change scene, packed scene was null");
		}

		Node sceneInstance = packedScene.Instantiate();
		GetTree().Root.AddChild(sceneInstance);
		CurrentScene = sceneInstance;

		Log.Information($"Scene changed to: {packedScene.ResourceName}");
	}

	public void GoToMainMenu()
	{
		if (InitialScene == null)
			ChangeScene("res://Project/MainMenu/main_menu.tscn");
		else
			ChangeScene(InitialScene);
	}

	/// <summary>
	/// Runs from `_EnterTree`, and this is an AUTOLOAD, so it runs before whichever scene Godot is
	/// opening — the main scene, or a single scene opened straight from the editor or the command
	/// line.
	///
	/// **That is the point of it being an autoload.** Logging and the input map are set up here, so
	/// a scene opened on its own used to get neither: `Log` wrote nowhere and `move_left` /
	/// `move_right` did not exist. The visible symptom was `DebugConsole` printing "Singleton
	/// instance of GameManager is not initialized!" once a frame, but the console was only the
	/// loudest casualty of an app that was half booted.
	/// </summary>
	protected override void Initialize()
	{
		InitializeLogging();
		InitializeInputs();
		CallDeferred(nameof(AdoptLoadedScene));
	}

	/// <summary>
	/// Godot loads the first scene itself now, so the one on screen at startup is one this object
	/// never created. Adopt it, or <see cref="CurrentScene"/> stays null until the first
	/// <see cref="ChangeScene(string)"/> and the first scene is never freed when we leave it.
	///
	/// Deferred because `GetTree().CurrentScene` is not populated while autoloads are entering.
	/// </summary>
	private void AdoptLoadedScene()
	{
		CurrentScene ??= GetTree().CurrentScene;

		// ChangeScene logs every other transition; without this the FIRST scene is the only one
		// that arrives unannounced, which is exactly the one you want named when a scene is opened
		// directly and you are wondering what actually booted.
		Log.Information("Initial scene: {SceneName}", CurrentScene?.Name.ToString() ?? "none");
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

	/// <summary>
	/// Drops a service so <see cref="HasService{T}"/> reports false again. Needed for services
	/// that scope a run rather than the session — a finished draft tournament, for instance,
	/// would otherwise keep redirecting later games back to its standings screen.
	/// </summary>
	public void RemoveService<T>()
		where T : class
	{
		if (_services.Remove(typeof(T)))
			Log.Debug("Removed service: {ServiceType}", typeof(T).Name);
	}

	/// <summary>
	/// Retrieves a registered service. Throws if the service hasn't been registered.
	/// </summary>
	public T GetService<T>()
		where T : class
	{
		if (!_services.TryGetValue(typeof(T), out var service))
			throw new System.InvalidOperationException(
				$"Service {typeof(T).Name} has not been registered with GameManager."
			);

		return (T)service;
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
