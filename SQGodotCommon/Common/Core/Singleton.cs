namespace Common.Core;

public abstract partial class Singleton<T> : Node
	where T : Node
{
	private static T _instance;

	public static T Instance
	{
		get
		{
			if (_instance == null)
			{
				GD.PrintErr($"Singleton instance of {typeof(T).Name} is not initialized!");
			}

			return _instance;
		}
	}

	public override void _EnterTree()
	{
		base._EnterTree();

		if (_instance != null && _instance != this)
		{
			GD.PrintErr(
				$"Another instance of singleton {typeof(T).Name} already exists! Destroying this instance."
			);
			QueueFree();
			return;
		}

		_instance = this as T;

		Initialize();
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		// The FIELD, not the Instance property — the property logs "is not initialized!" when it is
		// null, so reading it here printed that error on the way out of a tree whose singleton had
		// already gone. A teardown diagnostic that fires during normal teardown is noise.
		if (_instance == this)
		{
			_instance = null;
		}
	}

	protected virtual void Initialize() { }
}
