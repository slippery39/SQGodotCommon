namespace Common.Core;

/// <summary>
/// To be used with the damage and highlight shader or a shader that shares the same property names.
/// </summary>
public partial class DamageFlashController : Node
{
	[Export]
	private Color FlashColor = Colors.White;

	[Export]
	private float FlashSpeed = 30.0f;

	[Export]
	private int FlashCount = 2; // How many pulses to show

	[Export]
	private float FlashDuration = 0.2f;

	private ShaderMaterial _shaderMaterial;

	// This node should be attached to a sprite/mesh with the flash shader applied
	public override void _Ready()
	{
		// Get the shader material from the parent node (which should be a sprite or mesh)
		if (GetParent() is CanvasItem canvasItem && canvasItem.Material is ShaderMaterial material)
		{
			_shaderMaterial = material;
			// Set initial parameters
			_shaderMaterial.SetShaderParameter("flash_color", FlashColor);
			_shaderMaterial.SetShaderParameter("flash_intensity", 0.0f);
		}
		else
		{
			GD.PrintErr("DamageFlashController: Parent node doesn't have a ShaderMaterial!");
		}
	}

	public void Flash()
	{
		if (_shaderMaterial == null)
			return;

		Tween tween = CreateTween();

		// Calculate timing - each flash needs an "on" and "off" phase
		float singleFlashDuration = FlashDuration / (FlashCount * 2.0f);

		for (int i = 0; i < FlashCount; i++)
		{
			// Flash on
			tween.TweenMethod(
				Callable.From<float>(intensity =>
					_shaderMaterial.SetShaderParameter("flash_intensity", intensity)
				),
				0.0f,
				1.0f,
				singleFlashDuration
			);

			// Flash off
			tween.TweenMethod(
				Callable.From<float>(intensity =>
					_shaderMaterial.SetShaderParameter("flash_intensity", intensity)
				),
				1.0f,
				0.0f,
				singleFlashDuration
			);
		}

		tween.SetEase(Tween.EaseType.InOut);
		tween.SetTrans(Tween.TransitionType.Sine);
	}

	// Optional: Change flash color at runtime
	public void SetFlashColor(Color color)
	{
		FlashColor = color;
		if (_shaderMaterial != null)
		{
			_shaderMaterial.SetShaderParameter("flash_color", color);
		}
	}
}
