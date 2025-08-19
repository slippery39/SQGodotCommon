using System;

namespace Common.Cards;

[Tool]
public partial class InternalCardUI2D : Node2D
{
	public string Id { get; set; }
	private Sprite2D _artSprite2D;
	private Label _nameLabel;
	private Label _rulesTextLabel;
	private Label _manaCostLabel;
	private Node2D _cardContainer;

	private Control _viewportContainer;

	[Export]
	public Color OutlineColor { get; set; }

	[Export(PropertyHint.Range, "0,20")]
	public int OutlineThickness { get; set; }

	[Export]
	public bool Holographic { get; set; }

	public override void _Ready()
	{
		_viewportContainer =
			GetNodeOrNull<Control>("%SubViewportContainer")
			?? throw new InvalidOperationException("Could not find CardContainer in CardUI2D");
		_cardContainer =
			GetNodeOrNull<Node2D>("%CardContainer")
			?? throw new InvalidOperationException("Could not find CardContainer in CardUI2D");
		_artSprite2D =
			GetNodeOrNull<Sprite2D>("%ArtSprite")
			?? throw new InvalidOperationException("Could not find ArtSprite in CardUI2D");
		_nameLabel =
			GetNodeOrNull<Label>("%NameLabel")
			?? throw new InvalidOperationException("Could not find NameLabel in CardUI2D");
		_rulesTextLabel =
			GetNodeOrNull<Label>("%RulesTextLabel")
			?? throw new InvalidOperationException("Could not find RulesTextLabel in CardUI2D");
		_manaCostLabel =
			GetNodeOrNull<Label>("%ManaCostLabel")
			?? throw new InvalidOperationException("Could not find ManaCostLabel in CardUI2D");
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			var material = _viewportContainer.Material as ShaderMaterial;

			material.SetShaderParameter("outline_color", OutlineColor);
			material.SetShaderParameter("outline_thickness", OutlineThickness);

			var holoMaterial = _cardContainer.Material as ShaderMaterial;
			holoMaterial.SetShaderParameter("enabled", Holographic);
		}
	}

	public void SetCardName(string name)
	{
		_nameLabel.Text = name;
	}

	public void SetManaCost(string manaCost)
	{
		_manaCostLabel.Text = manaCost;
	}

	public void SetArtwork(Texture2D artTexture)
	{
		_artSprite2D.Texture = artTexture;
	}

	public void SetRulesText(string rulesText)
	{
		_rulesTextLabel.Text = rulesText;
	}

	/// <summary>
	/// Represents the details of a card, including its ID, name, mana cost,
	/// and rules text. Provides functionality to apply these details to a
	/// CardUI2D instance for display or usage.
	/// </summary>
	public class Details
	{
		public string Id { get; set; }
		public string CardName { get; set; }
		public string ManaCost { get; set; }

		public string RulesText { get; set; }

		public void ApplyTo(InternalCardUI2D card)
		{
			card.Id = Id.ToString();
			card.SetCardName(CardName);
			card.SetManaCost(ManaCost);
			card.SetRulesText(RulesText);
		}

		public void ApplyTo(CardUI2D card)
		{
			card.ApplyTo(this);
		}
	}
}
