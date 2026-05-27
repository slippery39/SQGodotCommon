namespace Common.Cards;

[Tool]
public partial class InternalCardUI2DCanvasGroup : InternalCardUI2D
{
	private CanvasGroup _canvasGroup;
	private Sprite2D _outlineSprite;

	public override void _Ready()
	{
		// Must be set before base._Ready() because base calls UpdateVisuals() → UpdateOutlineShader()
		// and virtual dispatch will invoke our override, which needs this reference ready.
		_canvasGroup = GetNodeOrNull<CanvasGroup>("%CanvasGroup");
		_outlineSprite = GetNodeOrNull<Sprite2D>("%OutlineSprite");
		base._Ready();
	}

	protected override void UpdateOutlineShader()
	{
		if (_outlineSprite?.Material is ShaderMaterial outlineMaterial)
		{
			outlineMaterial.SetShaderParameter("outline_color", OutlineColor);
			outlineMaterial.SetShaderParameter("outline_thickness", OutlineThickness);
		}
	}
}
