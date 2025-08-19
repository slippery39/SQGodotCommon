namespace Common.Core;

public static class ControlExtensions
{
	public static void FadeIn(this Control control, float fadeinTime)
	{
		var tween = control.CreateTween();

		control.Modulate = new Color(
			control.Modulate.R,
			control.Modulate.G,
			control.Modulate.B,
			0f
		);

		tween
			.TweenProperty(
				control,
				"modulate",
				new Color(control.Modulate.R, control.Modulate.G, control.Modulate.B, 1f),
				fadeinTime
			)
			.SetTrans(Tween.TransitionType.Linear)
			.SetEase(Tween.EaseType.InOut);
	}

	public static void FadeOut(this Control control, float fadeOutTime)
	{
		var tween = control.CreateTween();

		tween
			.TweenProperty(
				control,
				"modulate",
				new Color(control.Modulate.R, control.Modulate.G, control.Modulate.B, 0f),
				fadeOutTime
			)
			.SetTrans(Tween.TransitionType.Linear)
			.SetEase(Tween.EaseType.InOut);
	}
}
