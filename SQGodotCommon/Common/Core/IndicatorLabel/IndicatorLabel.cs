namespace Common.Core;

public partial class IndicatorLabel : Label
{
	public override void _Ready()
	{
		this.CallNextFrame(() =>
		{
			this.FadeOut(1f);

			var target = Position.AddY(-200);

			var tween = this.CreateTween();
			tween.TweenProperty(this, "position", target, 1).SetEase(Tween.EaseType.OutIn);
			tween.Finished += () => QueueFree();
		});
	}
}
