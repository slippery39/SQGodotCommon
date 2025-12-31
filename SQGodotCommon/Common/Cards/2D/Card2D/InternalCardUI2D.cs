namespace Common.Cards;

[Tool]
public partial class InternalCardUI2D : Node2D
{
	public string Id { get; set; }

	// Node references
	private Sprite2D _artSprite2D;
	private Sprite2D _mainFrameSprite2D;
	private Sprite2D _artFrameSprite2D;
	private Sprite2D _nameSprite2D;
	private Sprite2D _manaCostSprite2D;
	private Sprite2D _rulesTextSprite2D;
	private Label _nameLabel;
	private Label _rulesTextLabel;
	private Label _manaCostLabel;
	private Node2D _cardContainer;
	private Control _viewportContainer;

	// Store default textures from the scene
	private Texture2D _defaultArtworkTexture;
	private Texture2D _defaultMainFrameTexture;
	private Texture2D _defaultArtFrameTexture;
	private Texture2D _defaultNameFrameTexture;
	private Texture2D _defaultManaCostFrameTexture;
	private Texture2D _defaultRulesTextFrameTexture;

	// Private backing fields for exports
	private Texture2D _artworkTexture;
	private Texture2D _mainFrameTexture;
	private Texture2D _artFrameTexture;
	private Texture2D _nameFrameTexture;
	private Texture2D _manaCostFrameTexture;
	private Texture2D _rulesTextFrameTexture;
	private Color _outlineColor = new Color(0, 1.5f, 0, 1);
	private float _outlineThickness = 1.0f;
	private bool _holographic = false;
	private float _holographicIntensity = 0.75f;
	private float _twirlStrength = 4.0f;
	private float _noiseIntensity = 1.0f;
	private string _cardName = "";
	private string _manaCost = "";
	private string _rulesText = "";
	private Color _nameColor = Colors.White;
	private Color _manaCostColor = Colors.White;
	private Color _rulesTextColor = Colors.White;

	// Exported Textures with setters that trigger updates
	[ExportGroup("Card Textures")]
	[Export]
	public Texture2D ArtworkTexture
	{
		get => _artworkTexture;
		set
		{
			_artworkTexture = value;
			if (_artSprite2D != null)
				_artSprite2D.Texture = value ?? _defaultArtworkTexture;
		}
	}

	[Export]
	public Texture2D MainFrameTexture
	{
		get => _mainFrameTexture;
		set
		{
			_mainFrameTexture = value;
			if (_mainFrameSprite2D != null)
				_mainFrameSprite2D.Texture = value ?? _defaultMainFrameTexture;
		}
	}

	[Export]
	public Texture2D ArtFrameTexture
	{
		get => _artFrameTexture;
		set
		{
			_artFrameTexture = value;
			if (_artFrameSprite2D != null)
				_artFrameSprite2D.Texture = value ?? _defaultArtFrameTexture;
		}
	}

	[Export]
	public Texture2D NameFrameTexture
	{
		get => _nameFrameTexture;
		set
		{
			_nameFrameTexture = value;
			if (_nameSprite2D != null)
				_nameSprite2D.Texture = value ?? _defaultNameFrameTexture;
		}
	}

	[Export]
	public Texture2D ManaCostFrameTexture
	{
		get => _manaCostFrameTexture;
		set
		{
			_manaCostFrameTexture = value;
			if (_manaCostSprite2D != null)
				_manaCostSprite2D.Texture = value ?? _defaultManaCostFrameTexture;
		}
	}

	[Export]
	public Texture2D RulesTextFrameTexture
	{
		get => _rulesTextFrameTexture;
		set
		{
			_rulesTextFrameTexture = value;
			if (_rulesTextSprite2D != null)
				_rulesTextSprite2D.Texture = value ?? _defaultRulesTextFrameTexture;
		}
	}

	// Exported Visual Properties - Outline
	[ExportGroup("Outline Settings")]
	[Export]
	public Color OutlineColor
	{
		get => _outlineColor;
		set
		{
			_outlineColor = value;
			UpdateOutlineShader();
		}
	}

	[Export(PropertyHint.Range, "0,20")]
	public float OutlineThickness
	{
		get => _outlineThickness;
		set
		{
			_outlineThickness = value;
			UpdateOutlineShader();
		}
	}

	// Exported Visual Properties - Holographic
	[ExportGroup("Holographic Settings")]
	[Export]
	public bool Holographic
	{
		get => _holographic;
		set
		{
			_holographic = value;
			UpdateHolographicShader();
		}
	}

	[Export(PropertyHint.Range, "0,1")]
	public float HolographicIntensity
	{
		get => _holographicIntensity;
		set
		{
			_holographicIntensity = value;
			UpdateHolographicShader();
		}
	}

	[Export(PropertyHint.Range, "0,10")]
	public float TwirlStrength
	{
		get => _twirlStrength;
		set
		{
			_twirlStrength = value;
			UpdateHolographicShader();
		}
	}

	[Export(PropertyHint.Range, "0,2")]
	public float NoiseIntensity
	{
		get => _noiseIntensity;
		set
		{
			_noiseIntensity = value;
			UpdateHolographicShader();
		}
	}

	// Exported Text Properties
	[ExportGroup("Card Text")]
	[Export]
	public string CardName
	{
		get => _cardName;
		set
		{
			_cardName = value;
			if (_nameLabel != null)
				_nameLabel.Text = value;
		}
	}

	[Export]
	public string ManaCost
	{
		get => _manaCost;
		set
		{
			_manaCost = value;
			if (_manaCostLabel != null)
				_manaCostLabel.Text = value;
		}
	}

	[Export(PropertyHint.MultilineText)]
	public string RulesText
	{
		get => _rulesText;
		set
		{
			_rulesText = value;
			if (_rulesTextLabel != null)
				_rulesTextLabel.Text = value;
		}
	}

	// Font and Color customization
	[ExportGroup("Text Styling")]
	[Export]
	public Color NameColor
	{
		get => _nameColor;
		set
		{
			_nameColor = value;
			if (_nameLabel != null)
				_nameLabel.Modulate = value;
		}
	}

	[Export]
	public Color ManaCostColor
	{
		get => _manaCostColor;
		set
		{
			_manaCostColor = value;
			if (_manaCostLabel != null)
				_manaCostLabel.Modulate = value;
		}
	}

	[Export]
	public Color RulesTextColor
	{
		get => _rulesTextColor;
		set
		{
			_rulesTextColor = value;
			if (_rulesTextLabel != null)
				_rulesTextLabel.Modulate = value;
		}
	}

	public override void _Ready()
	{
		// Get nodes with unique names (%)
		_viewportContainer = GetNodeOrNull<Control>("%SubViewportContainer");
		_cardContainer = GetNodeOrNull<Node2D>("%CardContainer");
		_artSprite2D = GetNodeOrNull<Sprite2D>("%ArtSprite");
		_nameLabel = GetNodeOrNull<Label>("%NameLabel");
		_rulesTextLabel = GetNodeOrNull<Label>("%RulesTextLabel");
		_manaCostLabel = GetNodeOrNull<Label>("%ManaCostLabel");
		_mainFrameSprite2D = GetNodeOrNull<Sprite2D>("%MainFrame");

		// Get nodes without unique names - need relative paths from CardContainer
		if (_cardContainer != null)
		{
			_artFrameSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("ArtFrame");
			_nameSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("Name");
			_manaCostSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("ManaCost");
			_rulesTextSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("RulesText");
		}

		// Capture default textures from the scene
		CaptureDefaultTextures();

		// Apply initial values
		UpdateVisuals();
	}

	/// <summary>
	/// Captures the default textures from the scene so we can revert to them
	/// </summary>
	private void CaptureDefaultTextures()
	{
		if (_artSprite2D != null)
			_defaultArtworkTexture = _artSprite2D.Texture;

		if (_mainFrameSprite2D != null)
			_defaultMainFrameTexture = _mainFrameSprite2D.Texture;

		if (_artFrameSprite2D != null)
			_defaultArtFrameTexture = _artFrameSprite2D.Texture;

		if (_nameSprite2D != null)
			_defaultNameFrameTexture = _nameSprite2D.Texture;

		if (_manaCostSprite2D != null)
			_defaultManaCostFrameTexture = _manaCostSprite2D.Texture;

		if (_rulesTextSprite2D != null)
			_defaultRulesTextFrameTexture = _rulesTextSprite2D.Texture;
	}

	private void UpdateOutlineShader()
	{
		if (_viewportContainer?.Material is ShaderMaterial outlineMaterial)
		{
			outlineMaterial.SetShaderParameter("outline_color", _outlineColor);
			outlineMaterial.SetShaderParameter("outline_thickness", _outlineThickness);
		}
	}

	private void UpdateHolographicShader()
	{
		if (_cardContainer?.Material is ShaderMaterial holoMaterial)
		{
			holoMaterial.SetShaderParameter("enable_holographic", _holographic);
			holoMaterial.SetShaderParameter("holographic_intensity", _holographicIntensity);
			holoMaterial.SetShaderParameter("twirl_strength", _twirlStrength);
			holoMaterial.SetShaderParameter("noise_intensity", _noiseIntensity);
		}
	}

	/// <summary>
	/// Updates all visual elements based on exported properties
	/// </summary>
	private void UpdateVisuals()
	{
		UpdateOutlineShader();
		UpdateHolographicShader();

		// Update textures - use defaults if current is null
		if (_artSprite2D != null)
			_artSprite2D.Texture = _artworkTexture ?? _defaultArtworkTexture;

		if (_mainFrameSprite2D != null)
			_mainFrameSprite2D.Texture = _mainFrameTexture ?? _defaultMainFrameTexture;

		if (_artFrameSprite2D != null)
			_artFrameSprite2D.Texture = _artFrameTexture ?? _defaultArtFrameTexture;

		if (_nameSprite2D != null)
			_nameSprite2D.Texture = _nameFrameTexture ?? _defaultNameFrameTexture;

		if (_manaCostSprite2D != null)
			_manaCostSprite2D.Texture = _manaCostFrameTexture ?? _defaultManaCostFrameTexture;

		if (_rulesTextSprite2D != null)
			_rulesTextSprite2D.Texture = _rulesTextFrameTexture ?? _defaultRulesTextFrameTexture;

		// Update text
		if (_nameLabel != null)
		{
			_nameLabel.Text = _cardName;
			_nameLabel.Modulate = _nameColor;
		}

		if (_manaCostLabel != null)
		{
			_manaCostLabel.Text = _manaCost;
			_manaCostLabel.Modulate = _manaCostColor;
		}

		if (_rulesTextLabel != null)
		{
			_rulesTextLabel.Text = _rulesText;
			_rulesTextLabel.Modulate = _rulesTextColor;
		}
	}

	// Public setters for runtime updates
	public void SetCardName(string name)
	{
		CardName = name;
	}

	public void SetManaCost(string manaCost)
	{
		ManaCost = manaCost;
	}

	public void SetArtwork(Texture2D artTexture)
	{
		ArtworkTexture = artTexture;
	}

	public void SetMainFrame(Texture2D frameTexture)
	{
		MainFrameTexture = frameTexture;
	}

	public void SetRulesText(string rulesText)
	{
		RulesText = rulesText;
	}

	public void SetHolographic(bool enabled)
	{
		Holographic = enabled;
	}

	/// <summary>
	/// Represents the details of a card
	/// </summary>
	public class Details
	{
		public string Id { get; set; }
		public string CardName { get; set; }
		public string ManaCost { get; set; }
		public string RulesText { get; set; }

		// Optional visual properties
		public Texture2D ArtworkTexture { get; set; }
		public Texture2D MainFrameTexture { get; set; }
		public Texture2D ArtFrameTexture { get; set; }
		public Texture2D NameFrameTexture { get; set; }
		public Texture2D ManaCostFrameTexture { get; set; }
		public Texture2D RulesTextFrameTexture { get; set; }

		public Color? NameColor { get; set; }
		public Color? ManaCostColor { get; set; }
		public Color? RulesTextColor { get; set; }
		public Color? OutlineColor { get; set; }
		public float? OutlineThickness { get; set; }
		public bool? Holographic { get; set; }
		public float? HolographicIntensity { get; set; }

		public void ApplyTo(InternalCardUI2D card)
		{
			card.Id = Id?.ToString() ?? card.Id;
			card.CardName = CardName ?? card.CardName;
			card.ManaCost = ManaCost ?? card.ManaCost;
			card.RulesText = RulesText ?? card.RulesText;

			// Apply textures if provided
			if (ArtworkTexture != null)
				card.ArtworkTexture = ArtworkTexture;
			if (MainFrameTexture != null)
				card.MainFrameTexture = MainFrameTexture;
			if (ArtFrameTexture != null)
				card.ArtFrameTexture = ArtFrameTexture;
			if (NameFrameTexture != null)
				card.NameFrameTexture = NameFrameTexture;
			if (ManaCostFrameTexture != null)
				card.ManaCostFrameTexture = ManaCostFrameTexture;
			if (RulesTextFrameTexture != null)
				card.RulesTextFrameTexture = RulesTextFrameTexture;

			// Apply colors if provided
			if (NameColor.HasValue)
				card.NameColor = NameColor.Value;
			if (ManaCostColor.HasValue)
				card.ManaCostColor = ManaCostColor.Value;
			if (RulesTextColor.HasValue)
				card.RulesTextColor = RulesTextColor.Value;
			if (OutlineColor.HasValue)
				card.OutlineColor = OutlineColor.Value;
			if (OutlineThickness.HasValue)
				card.OutlineThickness = OutlineThickness.Value;
			if (Holographic.HasValue)
				card.Holographic = Holographic.Value;
			if (HolographicIntensity.HasValue)
				card.HolographicIntensity = HolographicIntensity.Value;
		}

		public void ApplyTo(CardUI2D card)
		{
			card.ApplyTo(this);
		}
	}
}
