using System.Collections.Generic;
using System.Linq;
using Common;
using Common.Cards;
using Common.Core;
using Logging;

namespace Project;

public partial class GameManager : Singleton<GameManager>
{
	public Node CurrentScene { get; private set; }
	public Observable<GameEvent> Events => _eventManager.Events;

	private EventManager _eventManager;

	private List<object> Services { get; set; } = new();

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
			LogManager.Instance.Error($"Failed to load scene: {scenePath}");
		}

		Node sceneInstance = newScene.Instantiate();
		GetTree().Root.AddChild(sceneInstance);
		CurrentScene = sceneInstance;

		LogManager.Instance.Info($"Scene changed to: {scenePath}");
	}

	public void GoToMainMenu()
	{
		ChangeScene("res://Project/MainMenu/main_menu.tscn");
	}

	protected override void Initialize()
	{
		ConfigureSettings();
		CallDeferred("LoadInitialScene");
	}

	private void ConfigureSettings()
	{
		StartupSettings.ConfigureSettings(this);
	}

	private void LoadInitialScene()
	{
		LogManager.Instance.Info("Loading scene...");
		GoToMainMenu();
	}

	public void AddService<T>()
		where T : class, new()
	{
		Services.Add(new T());
	}

	public T GetService<T>()
	{
		return Services.OfType<T>().FirstOrDefault();
	}

	public void AddEvent(string channel)
	{
		_eventManager.AddEvent(channel);
	}

	public void AddEvent<T>(string channel, T data)
	{
		_eventManager.AddEvent(channel, data);
	}
}
