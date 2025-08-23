using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Core;
using DynamicExpresso;
using Project;

namespace Common;

public partial class DebugConsole : Control
{
	// Node references (will be automatically assigned from scene)
	private RichTextLabel _outputLabel;
	private LineEdit _inputField;
	private ScrollContainer _scrollContainer;
	private Button _closeButton;
	private Button _minimizeButton;
	private Button _resizeHandle;
	private Control _resizeGrip;
	private Panel _windowPanel;

	private bool _isVisible = false;
	private bool _isMinimized = false;
	private Interpreter _interpreter;
	private List<string> _commandHistory = new List<string>();
	private int _historyIndex = -1;

	// Resizing functionality
	private bool _isDragging = false;
	private bool _isResizing = false;
	private Vector2 _dragOffset;
	private Vector2 _originalSize;
	private Vector2 _minimumSize = new Vector2(400, 400);
	private Vector2 _maximumSize = new Vector2(1600, 1200);
	private Vector2 _minimizedSize;
	private Vector2 _normalSize;

	// Console commands dictionary
	private Dictionary<string, Func<string[], string>> _commands =
		new Dictionary<string, Func<string[], string>>();

	// Watch functionality
	private Dictionary<string, string> _watchExpressions = new Dictionary<string, string>(); // Runtime expression watches
	private Dictionary<string, Func<object>> _watchDelegates =
		new Dictionary<string, Func<object>>(); // Code-based watches
	private Dictionary<string, object> _lastWatchValues = new Dictionary<string, object>();
	private Dictionary<string, string> _watchDescriptions = new Dictionary<string, string>(); // Optional descriptions
	private RichTextLabel _watchLabel;
	private bool _watchEnabled = true;

	public override void _Ready()
	{
		// Get node references from scene
		_windowPanel = GetNode<Panel>("WindowPanel");
		_outputLabel = GetNode<RichTextLabel>(
			"WindowPanel/MainContainer/ContentContainer/OutputContainer/ScrollContainer/OutputLabel"
		);
		_inputField = GetNode<LineEdit>("WindowPanel/MainContainer/InputContainer/InputField");
		_scrollContainer = GetNode<ScrollContainer>(
			"WindowPanel/MainContainer/ContentContainer/OutputContainer/ScrollContainer"
		);
		_closeButton = GetNode<Button>("WindowPanel/MainContainer/TopBar/CloseButton");
		_minimizeButton = GetNode<Button>("WindowPanel/MainContainer/TopBar/MinimizeButton");
		_resizeHandle = GetNode<Button>("WindowPanel/MainContainer/TopBar/ResizeHandle");
		_resizeGrip = GetNode<Control>("ResizeBorder/ResizeGrip");

		// Try to get watch label (optional)
		try
		{
			_watchLabel = GetNode<RichTextLabel>(
				"WindowPanel/MainContainer/ContentContainer/WatchContainer/WatchLabel"
			);
		}
		catch
		{
			// Watch label is optional, continue without it
			_watchLabel = null;
		}

		SetProcess(true);
		SetProcessUnhandledKeyInput(true);

		InitializeInterpreter();
		RegisterDefaultCommands();

		// Store initial size
		_normalSize = Size;
		_minimizedSize = new Vector2(Size.X, 60);

		// Start hidden
		Visible = false;
		_isVisible = false;

		// Focus input field when console becomes visible
		VisibilityChanged += OnVisibilityChanged;

		SetupInputHandling();

		// Setup resize functionality
		SetupResizing();

		// Add some default watch expressions
		AddDefaultWatchExpressions();
	}

	private void SetupResizing()
	{
		// Make the title bar draggable
		_resizeHandle.GuiInput += OnTitleBarInput;

		// Make the resize grip work
		_resizeGrip.GuiInput += OnResizeGripInput;

		// Set up mouse cursor changes
		_resizeGrip.MouseEntered += () => Input.SetDefaultCursorShape(Input.CursorShape.Fdiagsize);
		_resizeGrip.MouseExited += () => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
	}

	private void OnTitleBarInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (mouseButton.Pressed)
				{
					_isDragging = true;
					_dragOffset = GetGlobalMousePosition() - GlobalPosition;
				}
				else
				{
					_isDragging = false;
				}
			}
		}
		else if (@event is InputEventMouseMotion && _isDragging)
		{
			var newPosition = GetGlobalMousePosition() - _dragOffset;
			var viewportSize = GetViewport().GetVisibleRect().Size;

			// Constrain to viewport bounds
			newPosition.X = Mathf.Clamp(newPosition.X, 0, viewportSize.X - Size.X);
			newPosition.Y = Mathf.Clamp(newPosition.Y, 0, viewportSize.Y - Size.Y);

			GlobalPosition = newPosition;
		}
	}

	private void OnResizeGripInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (mouseButton.Pressed)
				{
					_isResizing = true;
					_originalSize = Size;
				}
				else
				{
					_isResizing = false;
				}
			}
		}
		else if (@event is InputEventMouseMotion mouseMotion && _isResizing)
		{
			var newSize = _originalSize + mouseMotion.Relative;

			// Constrain size to minimum and maximum
			newSize.X = Mathf.Clamp(newSize.X, _minimumSize.X, _maximumSize.X);
			newSize.Y = Mathf.Clamp(newSize.Y, _minimumSize.Y, _maximumSize.Y);

			// Constrain to viewport bounds
			var viewportSize = GetViewport().GetVisibleRect().Size;
			if (GlobalPosition.X + newSize.X > viewportSize.X)
			{
				newSize.X = viewportSize.X - GlobalPosition.X;
			}
			if (GlobalPosition.Y + newSize.Y > viewportSize.Y)
			{
				newSize.Y = viewportSize.Y - GlobalPosition.Y;
			}

			Size = newSize;
			_originalSize = newSize;
			_minimizedSize = _minimizedSize.WithX(Size.X);

			// Update normal size if not minimized
			if (!_isMinimized)
			{
				_normalSize = newSize;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (_watchEnabled && (_watchDelegates.Count > 0 || _watchExpressions.Count > 0))
		{
			UpdateWatches();
		}
	}

	private void InitializeInterpreter()
	{
		_interpreter = new Interpreter();

		// Add common namespaces
		_interpreter.Reference(typeof(Godot.Node));
		_interpreter.Reference(typeof(System.Math));
		_interpreter.Reference(typeof(System.Linq.Enumerable));
		_interpreter.Reference(typeof(Engine));

		// Set up common variables that might be useful
		_interpreter.SetVariable("console", this);
		_interpreter.SetVariable("tree", GetTree());
		_interpreter.SetVariable("root", GetTree().Root);
	}

	private void RegisterDefaultCommands()
	{
		_commands["help"] = (args) => GetHelpText();
		_commands["clear"] = (args) =>
		{
			_outputLabel.Text = "";
			return "Console cleared.";
		};
		_commands["quit"] = (args) =>
		{
			GetTree().Quit();
			return "Quitting...";
		};
		_commands["scene"] = (args) => GetCurrentSceneInfo();
		_commands["nodes"] = (args) => ListNodes();
		_commands["fps"] = (args) => $"FPS: {Engine.GetFramesPerSecond()}";
		_commands["history"] = (args) => string.Join("\n", _commandHistory.TakeLast(10));
		_commands["version"] = (args) => $"Godot {Engine.GetVersionInfo()["string"]}";
		_commands["memory"] = (args) => $"Memory Usage: {OS.GetStaticMemoryUsage}";

		// Watch commands
		_commands["watch"] = (args) => HandleWatchCommand(args);
		_commands["unwatch"] = (args) => HandleUnwatchCommand(args);
		_commands["watches"] = (args) => ListWatches();
		_commands["clearwatches"] = (args) => ClearAllWatches();
		_commands["togglewatch"] = (args) => ToggleWatchDisplay();

		// Window commands
		_commands["minimize"] = (args) =>
		{
			ToggleMinimize();
			return "";
		};
		_commands["resize"] = (args) => HandleResizeCommand(args);
		_commands["move"] = (args) => HandleMoveCommand(args);
		_commands["reset"] = (args) => ResetWindowSettings();
	}

	// Add this to your _Ready() method after getting node references:
	private void SetupInputHandling()
	{
		// Connect to the LineEdit's input events directly
		_inputField.GuiInput += OnInputFieldInput;
	}

	// Add this new method to handle LineEdit input
	private void OnInputFieldInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			switch (keyEvent.Keycode)
			{
				case Key.Up:
					NavigateHistory(-1);
					AcceptEvent();
					break;
				case Key.Down:
					NavigateHistory(1);
					AcceptEvent();
					break;
				case Key.Escape:
					ToggleConsole();
					AcceptEvent();
					break;
			}
		}
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (
			@event is InputEventKey keyEvent
			&& keyEvent.Pressed
			&& keyEvent.Keycode == Key.Quoteleft
		)
		{
			ToggleConsole();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ToggleConsole()
	{
		_isVisible = !_isVisible;
		Visible = _isVisible;

		if (_isVisible)
		{
			AddToOutput("[color=green]Debug Console Opened - Type 'help' for commands[/color]");
		}
	}

	private void ToggleMinimize()
	{
		_isMinimized = !_isMinimized;

		// Get references to the containers we want to hide/show
		var contentContainer = GetNode<HBoxContainer>("WindowPanel/MainContainer/ContentContainer");
		var inputContainer = GetNode<HBoxContainer>("WindowPanel/MainContainer/InputContainer");

		if (_isMinimized)
		{
			// Hide the main content and input when minimized
			contentContainer.Visible = false;
			inputContainer.Visible = false;

			// Set minimized size
			Size = _minimizedSize;
			_minimizeButton.Text = "□";
		}
		else
		{
			// Show the content and input when restored
			contentContainer.Visible = true;
			inputContainer.Visible = true;

			// Restore normal size
			Size = _normalSize;
			_minimizeButton.Text = "_";
		}
	}

	private void OnVisibilityChanged()
	{
		if (Visible && _inputField != null)
		{
			// Delay focus grab to ensure the scene is fully ready
			CallDeferred(nameof(GrabInputFocus));
		}
	}

	private void GrabInputFocus()
	{
		_inputField.GrabFocus();
	}

	// Signal handlers (connected in scene)
	private void _on_input_field_text_submitted(string command)
	{
		if (string.IsNullOrWhiteSpace(command))
			return;

		// Add to history
		_commandHistory.Add(command);
		_historyIndex = _commandHistory.Count;

		// Display the command
		AddToOutput($"[color=yellow]> {command}[/color]");

		// Process the command
		string result = ProcessCommand(command);
		if (!string.IsNullOrEmpty(result))
		{
			AddToOutput(result);
		}

		// Clear input and scroll to bottom
		_inputField.Text = "";
		CallDeferred(nameof(ScrollToBottom));

		// Keep focus on input field
		_inputField.GrabFocus();
	}

	private void _on_close_button_pressed()
	{
		ToggleConsole();
	}

	private void _on_minimize_button_pressed()
	{
		ToggleMinimize();
	}

	private void _on_resize_handle_pressed()
	{
		// This could show resize options or do something else
	}

	// Window management commands
	private string HandleResizeCommand(string[] args)
	{
		if (args.Length != 2)
		{
			return "[color=red]Usage: resize <width> <height>[/color]";
		}

		if (int.TryParse(args[0], out int width) && int.TryParse(args[1], out int height))
		{
			var newSize = new Vector2(width, height);
			newSize.X = Mathf.Clamp(newSize.X, _minimumSize.X, _maximumSize.X);
			newSize.Y = Mathf.Clamp(newSize.Y, _minimumSize.Y, _maximumSize.Y);

			Size = newSize;
			if (!_isMinimized)
			{
				_normalSize = newSize;
			}

			_minimizedSize = _minimizedSize.WithX(Size.X);

			return $"[color=green]Resized to {newSize.X}x{newSize.Y}[/color]";
		}

		return "[color=red]Invalid width or height values[/color]";
	}

	private string HandleMoveCommand(string[] args)
	{
		if (args.Length != 2)
		{
			return "[color=red]Usage: move <x> <y>[/color]";
		}

		if (int.TryParse(args[0], out int x) && int.TryParse(args[1], out int y))
		{
			var newPosition = new Vector2(x, y);
			var viewportSize = GetViewport().GetVisibleRect().Size;

			newPosition.X = Mathf.Clamp(newPosition.X, 0, viewportSize.X - Size.X);
			newPosition.Y = Mathf.Clamp(newPosition.Y, 0, viewportSize.Y - Size.Y);

			GlobalPosition = newPosition;
			return $"[color=green]Moved to {newPosition.X},{newPosition.Y}[/color]";
		}

		return "[color=red]Invalid x or y values[/color]";
	}

	private string ResetWindowSettings()
	{
		// Reset to default size and position
		Size = new Vector2(800, 400);
		_normalSize = Size;
		_minimizedSize = new Vector2(Size.X, 60);

		// Center on screen
		var viewportSize = GetViewport().GetVisibleRect().Size;
		GlobalPosition = (viewportSize - Size) / 2;

		// Reset minimized state
		if (_isMinimized)
		{
			_isMinimized = false;
			_minimizeButton.Text = "_";
		}

		return "[color=green]Window settings reset to default[/color]";
	}

	static double ConvertBytesToMegabytes(long bytes)
	{
		return bytes / (1024.0 * 1024.0); // 1 MB = 1024 * 1024 bytes
	}

	private string ProcessCommand(string input)
	{
		try
		{
			string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
				return "";

			string commandName = parts[0].ToLower();

			// Check if it's a registered command
			if (_commands.ContainsKey(commandName))
			{
				string[] args = parts.Length > 1 ? parts[1..] : new string[0];
				return _commands[commandName](args);
			}

			// If not a command, try to evaluate as C# expression
			var result = _interpreter.Eval(input);
			return result?.ToString() ?? "null";
		}
		catch (Exception ex)
		{
			return $"[color=red]Error: {ex.Message}[/color]";
		}
	}

	private void AddToOutput(string message)
	{
		_outputLabel.Text += message + "\n";
	}

	private void ScrollToBottom()
	{
		if (_scrollContainer != null)
		{
			_scrollContainer.ScrollVertical = (int)_scrollContainer.GetVScrollBar().MaxValue;
		}
	}

	private void NavigateHistory(int direction)
	{
		if (_commandHistory.Count == 0)
			return;

		_historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _commandHistory.Count);

		if (_historyIndex < _commandHistory.Count)
		{
			_inputField.Text = _commandHistory[_historyIndex];
			_inputField.CaretColumn = _inputField.Text.Length;
		}
		else
		{
			_inputField.Text = "";
		}
	}

	private static string GetHelpText()
	{
		var help = "[color=cyan]Available Commands:[/color]\n";
		help += "help - Show this help text\n";
		help += "clear - Clear console output\n";
		help += "quit - Exit the game\n";
		help += "scene - Show current scene information\n";
		help += "nodes - List nodes in current scene\n";
		help += "fps - Show current FPS\n";
		help += "history - Show command history\n";
		help += "version - Show Godot version\n";
		help += "memory - Show memory usage\n";
		help += "\n[color=cyan]Window Commands:[/color]\n";
		help += "minimize - Toggle minimize/restore\n";
		help += "resize <width> <height> - Resize window\n";
		help += "move <x> <y> - Move window position\n";
		help += "reset - Reset window to default size/position\n";
		help += "\n[color=cyan]Watch Commands:[/color]\n";
		help += "watch <name> <expression> - Add a watch variable\n";
		help += "unwatch <name> - Remove a watch variable\n";
		help += "watches - List all watch variables\n";
		help += "clearwatches - Remove all watch variables\n";
		help += "togglewatch - Enable/disable watch display\n";
		help += "\n[color=cyan]Watch Examples:[/color]\n";
		help += "watch fps Engine.GetFramesPerSecond()\n";
		help += "watch time DateTime.Now.ToString(\"HH:mm:ss\")\n";
		help += "watch player_pos GetNode(\"Player\").GlobalPosition\n";
		help += "\n[color=cyan]C# Expression Examples:[/color]\n";
		help += "Math.Sqrt(16) - Evaluate math expressions\n";
		help += "DateTime.Now - Get current time\n";
		help += "tree.CurrentScene.Name - Access scene properties\n";
		help += "\n[color=green]Controls:[/color]\n";
		help += "` (backtick) - Toggle console\n";
		help += "Up/Down arrows - Navigate command history\n";
		help += "Escape - Close console\n";
		help += "Drag title bar - Move window\n";
		help += "Drag bottom-right corner - Resize window\n";
		help += "_ button - Minimize/restore\n";
		help += "X button - Close console";
		return help;
	}

	private string GetCurrentSceneInfo()
	{
		var scene = GetTree().CurrentScene;
		return $"Current Scene: {scene.Name} ({scene.GetType().Name})\nScene Path: {scene.SceneFilePath}\nChildren: {scene.GetChildCount()}";
	}

	private string ListNodes()
	{
		var scene = GetTree().Root;
		var nodes = new List<string>();
		CollectNodeNames(scene, nodes, 0);
		return string.Join("\n", nodes.Take(1000))
			+ (
				nodes.Count > 1000
					? $"\n[color=gray]... and {nodes.Count - 1000} more nodes[/color]"
					: ""
			);
	}

	private void CollectNodeNames(Node node, List<string> names, int depth)
	{
		string indent = new string(' ', depth * 2);
		names.Add($"{indent}{node.Name} ({node.GetType().Name})");

		if (depth < 10) // Limit depth to prevent overwhelming output
		{
			foreach (Node child in node.GetChildren())
			{
				CollectNodeNames(child, names, depth + 1);
			}
		}
	}

	// Public API for adding custom commands
	public void RegisterCommand(string name, Func<string[], string> command)
	{
		_commands[name.ToLower()] = command;
	}

	// Public API for adding interpreter variables
	public void SetVariable(string name, object value)
	{
		_interpreter.SetVariable(name, value);
	}

	// Public API for programmatically adding output
	public void Log(string message, string color = "white")
	{
		AddToOutput($"[color={color}]{message}[/color]");
		CallDeferred(nameof(ScrollToBottom));
	}

	#region Watch Functionality

	private void AddDefaultWatchExpressions()
	{
		// Add some useful default watches using delegates (more performant)
		RegisterWatch("fps", () => Engine.GetFramesPerSecond(), "Current FPS");
		RegisterWatch(
			"memory",
			() => $"{ConvertBytesToMegabytes((long)OS.GetStaticMemoryUsage()):N0} MB",
			"Static memory usage"
		);
		RegisterWatch("time", () => DateTime.Now.ToString("HH:mm:ss"), "Current time");

		// Example of more complex watch
		RegisterWatch(
			"scene",
			() => GameManager.Instance.CurrentScene?.Name ?? "None",
			"Current scene name"
		);
	}

	private void UpdateWatches()
	{
		var watchTextBuilder = new StringBuilder();

		watchTextBuilder.AppendLine("[color=cyan][b]WATCHES[/b][/color]");

		// Update delegate-based watches (code watches)
		foreach (var kvp in _watchDelegates)
		{
			try
			{
				var result = kvp.Value.Invoke();
				var currentValue = result?.ToString() ?? "null";

				// Check if value changed
				bool valueChanged = false;
				if (_lastWatchValues.TryGetValue(kvp.Key, out var lastValue))
				{
					valueChanged = !currentValue.Equals(lastValue?.ToString());
				}
				else
				{
					valueChanged = true; // First time seeing this value
				}

				if (valueChanged)
				{
					_lastWatchValues[kvp.Key] = result;
				}

				// Color the value if it changed recently
				var color = valueChanged ? "yellow" : "white";
				var description = _watchDescriptions.ContainsKey(kvp.Key)
					? $" [color=gray]({_watchDescriptions[kvp.Key]})[/color]"
					: "";
				watchTextBuilder.AppendLine(
					$"[color=lightblue]{kvp.Key}:[/color] [color={color}]{currentValue}[/color]{description}"
				);
			}
			catch (Exception ex)
			{
				watchTextBuilder.AppendLine(
					$"[color=lightblue]{kvp.Key}:[/color] [color=red]Error: {ex.Message}[/color]"
				);
			}
		}

		// Update expression-based watches (runtime watches)
		foreach (var kvp in _watchExpressions)
		{
			try
			{
				var result = _interpreter.Eval(kvp.Value);
				var currentValue = result?.ToString() ?? "null";

				// Check if value changed
				bool valueChanged = false;
				if (_lastWatchValues.TryGetValue(kvp.Key, out var lastValue))
				{
					valueChanged = !currentValue.Equals(lastValue?.ToString());
				}
				else
				{
					valueChanged = true; // First time seeing this value
				}

				if (valueChanged)
				{
					_lastWatchValues[kvp.Key] = result;
				}

				// Color the value if it changed recently
				var color = valueChanged ? "yellow" : "white";
				watchTextBuilder.AppendLine(
					$"[color=gray]{kvp.Key}:[/color] [color={color}]{currentValue}[/color] [color=darkgray](expr)[/color]"
				);
			}
			catch (Exception ex)
			{
				watchTextBuilder.AppendLine(
					$"[color=gray]{kvp.Key}:[/color] [color=red]Error: {ex.Message}[/color]"
				);
			}
		}

		if (_watchExpressions.Count == 0 && _watchDelegates.Count == 0)
		{
			watchTextBuilder.AppendLine("[color=gray]No active watches[/color]");
		}

		_watchLabel.Text = watchTextBuilder.ToString();
	}

	private string HandleWatchCommand(string[] args)
	{
		if (args.Length < 2)
		{
			return "[color=red]Usage: watch <name> <expression>[/color]\nExample: watch fps Engine.GetFramesPerSecond()";
		}

		string name = args[0];
		string expression = string.Join(" ", args[1..]);

		// Test the expression first
		try
		{
			_interpreter.Eval(expression);
			_watchExpressions[name] = expression;
			return $"[color=green]Added watch '{name}': {expression}[/color]";
		}
		catch (Exception ex)
		{
			return $"[color=red]Invalid expression '{expression}': {ex.Message}[/color]";
		}
	}

	private string HandleUnwatchCommand(string[] args)
	{
		if (args.Length != 1)
		{
			return "[color=red]Usage: unwatch <name>[/color]";
		}

		string name = args[0];
		if (_watchExpressions.Remove(name))
		{
			_lastWatchValues.Remove(name);
			return $"[color=green]Removed watch '{name}'[/color]";
		}
		else
		{
			return $"[color=red]Watch '{name}' not found[/color]";
		}
	}

	private string ListWatches()
	{
		if (_watchExpressions.Count == 0 && _watchDelegates.Count == 0)
		{
			return "[color=gray]No active watches[/color]";
		}

		var result = new StringBuilder("[color=cyan]Active Watches:[/color]");

		// List code-based watches
		if (_watchDelegates.Count > 0)
		{
			result.AppendLine("[color=lightblue]Code Watches:[/color]");
			foreach (var key in _watchDelegates.Select(kvp => kvp.Key))
			{
				var description = _watchDescriptions.ContainsKey(key)
					? $" - {_watchDescriptions[key]}"
					: "";
				result.AppendLine($"[color=yellow]{key}[/color]{description}");
			}
		}

		// List expression-based watches
		if (_watchExpressions.Count > 0)
		{
			result.AppendLine("[color=gray]Expression Watches:[/color]");
			foreach (var kvp in _watchExpressions)
			{
				result.AppendLine($"  [color=yellow]{kvp.Key}[/color]: {kvp.Value}");
			}
		}

		return result.ToString();
	}

	private string ClearAllWatches()
	{
		int expressionCount = _watchExpressions.Count;
		int delegateCount = _watchDelegates.Count;
		int totalCount = expressionCount + delegateCount;

		_watchExpressions.Clear();
		_watchDelegates.Clear();
		_lastWatchValues.Clear();
		_watchDescriptions.Clear();

		return $"[color=green]Cleared {totalCount} watches ({delegateCount} code, {expressionCount} expression)[/color]";
	}

	private string ToggleWatchDisplay()
	{
		_watchEnabled = !_watchEnabled;
		if (_watchLabel != null)
		{
			_watchLabel.Visible = _watchEnabled;
		}
		return $"[color=green]Watch display {(_watchEnabled ? "enabled" : "disabled")}[/color]";
	}

	// Public API for watch functionality
	public void AddWatch(string name, string expression)
	{
		try
		{
			_interpreter.Eval(expression);
			_watchExpressions[name] = expression;
		}
		catch (Exception ex)
		{
			Log($"Failed to add watch '{name}': {ex.Message}", "red");
		}
	}

	// Public API for registering code-based watches
	public void RegisterWatch(string name, Func<object> valueDelegate, string description = "")
	{
		_watchDelegates[name] = valueDelegate;
		if (!string.IsNullOrEmpty(description))
		{
			_watchDescriptions[name] = description;
		}
	}

	// Generic version for type safety
	public void RegisterWatch<T>(string name, Func<T> valueDelegate, string description = "")
	{
		_watchDelegates[name] = () => valueDelegate();
		if (!string.IsNullOrEmpty(description))
		{
			_watchDescriptions[name] = description;
		}
	}

	public void RemoveWatch(string name)
	{
		_watchExpressions.Remove(name);
		_watchDelegates.Remove(name);
		_lastWatchValues.Remove(name);
		_watchDescriptions.Remove(name);
	}

	// Bulk registration helper
	public void RegisterWatches(Dictionary<string, Func<object>> watches)
	{
		foreach (var kvp in watches)
		{
			RegisterWatch(kvp.Key, kvp.Value);
		}
	}

	#endregion
}
