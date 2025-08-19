using Godot;

namespace Common;

[Tool]
public partial class HealthBar : ProgressBar
{
	[Export]
	public int MaxHealth { get; set; } = 500;

	[Export]
	public int CurrentHealth { get; set; } = 250;

	private Tween CurrentTween { get; set; }

	private Subject<Unit> CurrentObservable { get; set; }

	public override void _Process(double delta)
	{
		var healthLabel = GetNode<Label>("HealthLabel");
		healthLabel.Text = $"{CurrentHealth}/{MaxHealth}";

		float percValue = (float)CurrentHealth / MaxHealth;
		Value = percValue * 100;
	}

	public Observable<Unit> PlayHealthChangeAnimation(int toValue, float time = 0.2f)
	{
		if (CurrentTween != null)
		{
			CurrentTween.Kill();
		}

		if (CurrentObservable == null)
		{
			CurrentObservable = new Subject<Unit>();
		}

		CurrentTween = CreateTween();
		CurrentTween.TweenProperty(this, "CurrentHealth", toValue, time);
		CurrentTween.Finished += () =>
		{
			CurrentObservable.OnNext(Unit.Default);
			CurrentObservable.OnCompleted();
			CurrentObservable = null;
		};

		return CurrentObservable;
	}
}
