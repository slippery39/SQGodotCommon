using System;
using Logging;
using Project;

namespace Common.Core;

//UPDATE MAIN TEMPLATE
[Tool]
public partial class CharacterUI : Node2D
{
	public int IdReference { get; set; }
	private Sprite2D Sprite { get; set; }

	public HealthBar HealthBar { get; set; }
	private Area2D ClickArea { get; set; }
	private DamageFlashController DamageFlashController { get; set; }

	private float _spritePositionGap = 10f;

	[Export]
	public float SpritePositionGap
	{
		get => _spritePositionGap;
		set
		{
			_spritePositionGap = value;
			if (Engine.IsEditorHint())
			{
				PositionSprite();
			}
		}
	}

	private SpritePositioningType _spritePositioningType;

	[Export]
	public SpritePositioningType SpritePositioningType
	{
		get => _spritePositioningType;
		set
		{
			_spritePositioningType = value;
			if (Engine.IsEditorHint())
				PositionSprite();
		}
	}

	public override void _Ready()
	{
		Sprite =
			GetNodeOrNull<Sprite2D>("Sprite")
			?? throw new InvalidOperationException("Sprite node not found");

		HealthBar =
			GetNodeOrNull<HealthBar>("HealthBar")
			?? throw new InvalidOperationException("HealthBar node not found");

		ClickArea =
			GetNodeOrNull<Area2D>("%ClickArea")
			?? throw new InvalidOperationException("ClickArea node not found");

		if (!Engine.IsEditorHint())
		{
			DamageFlashController =
				GetNodeOrNull<DamageFlashController>("%DamageFlashController")
				?? throw new InvalidOperationException("DamageFlashController node not found");

			ClickArea.InputEvent += ClickAreaClicked;
		}

		var material = Sprite.Material as ShaderMaterial;
		material.SetShaderParameter("outline_thickness", 0);

		PositionSprite();
	}

	private void ClickAreaClicked(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (@event is InputEventMouseButton)
		{
			if (@event.IsPressed())
			{
				GameManager.Instance.AddEvent("character-clicked", IdReference);
				LogManager.Instance.Debug($"Character : {IdReference} has been clicked");
			}
		}
	}

	private void PositionSprite()
	{
		switch (SpritePositioningType)
		{
			case SpritePositioningType.Manual:
				//Do nothing as we are allowing the sprite to be positioned by other means
				break;
			case SpritePositioningType.AboveHealthBar:
				PositionAboveLocal(Sprite, HealthBar, SpritePositionGap);
				break;
			case SpritePositioningType.BelowHealthBar:
				PositionBelowLocal(Sprite, HealthBar, SpritePositionGap);
				break;
		}
	}

	/// <summary>
	/// Positions a sprite above a control with a certain gap.
	/// </summary>
	/// <param name="sprite"></param>
	/// <param name="target"></param>
	/// <param name="gap"></param>
	private static void PositionAboveLocal(Sprite2D sprite, Control target, float gap = 10f)
	{
		if (sprite?.Texture == null)
			return;

		// Use local scale and positioning
		Vector2 spriteSize = sprite.Texture.GetSize() * sprite.Scale.Abs();

		// Convert target position to sprite's parent coordinate system
		Node2D spriteParent = sprite.GetParent<Node2D>();
		Vector2 targetLocalPos = spriteParent.ToLocal(target.GlobalPosition);
		Vector2 targetSize = target.Size * spriteParent.Scale;

		// Position in local coordinates
		// The targetLocalPos is correctly transformed, but we need to account for scale
		// when calculating offsets from that position
		sprite.Position = new Vector2(
			targetLocalPos.X + (targetSize.X / 2f / spriteParent.Scale.X),
			targetLocalPos.Y - (gap / spriteParent.Scale.Y) - (spriteSize.Y / 2f)
		);
	}

	/// <summary>
	/// Positions a sprite below a control with a certain gap.
	/// </summary>
	/// <param name="sprite"></param>
	/// <param name="target"></param>
	/// <param name="gap"></param>
	private static void PositionBelowLocal(Sprite2D sprite, Control target, float gap = 10f)
	{
		if (sprite?.Texture == null)
			return;

		// Use local scale and positioning
		Vector2 spriteSize = sprite.Texture.GetSize() * sprite.Scale.Abs();

		// Convert target position to sprite's parent coordinate system
		Node2D spriteParent = sprite.GetParent<Node2D>();
		Vector2 targetLocalPos = spriteParent.ToLocal(target.GlobalPosition);
		Vector2 targetSize = target.Size * spriteParent.Scale;

		// Position in local coordinates
		// For below positioning, we add the target height and gap to move below it
		sprite.Position = new Vector2(
			targetLocalPos.X + (targetSize.X / 2f / spriteParent.Scale.X),
			targetLocalPos.Y
				+ (targetSize.Y / spriteParent.Scale.Y)
				+ (gap / spriteParent.Scale.Y)
				+ (spriteSize.Y / 2f)
		);
	}

	public override void _ExitTree()
	{
		if (!Engine.IsEditorHint())
		{
			if (ClickArea != null)
				ClickArea.InputEvent -= ClickAreaClicked;
		}
	}

	public void PlayTakeDamageAnimation()
	{
		DamageFlashController.Flash();
	}
}

public enum SpritePositioningType
{
	Manual,
	AboveHealthBar,
	BelowHealthBar,
}
