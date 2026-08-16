using Serilog;

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
	private Label _typeLineLabel;
	private Label _powerToughnessLabel;
	private Control _typeLineBand;
	private Sprite2D _powerToughnessSprite2D;
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
	private string _typeLine = "";
	private string _powerToughness = "";
	private Color _nameColor = Colors.White;
	private Color _manaCostColor = Colors.White;
	private Color _rulesTextColor = Colors.White;
	private Color _frameColor = Colors.White;
	private Color _namePlateColor = Colors.White;

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
			{
				var tex = value ?? _defaultArtworkTexture;
				_artSprite2D.Texture = tex;
				FitArtToFrame(tex);
			}
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
			{
				_nameLabel.Text = value;
				CallDeferred(nameof(FitNameToBox));
			}
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

	[Export]
	public string TypeLine
	{
		get => _typeLine;
		set
		{
			_typeLine = value;
			ApplyTypeLine();
		}
	}

	/// <summary>
	/// Displayed power/toughness, e.g. "2/2" or "4/4\n-2" when damaged. Empty hides the badge —
	/// a non-creature must not show an empty circle.
	/// </summary>
	[Export]
	public string PowerToughness
	{
		get => _powerToughness;
		set
		{
			_powerToughness = value;
			ApplyPowerToughness();
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
			{
				_rulesTextLabel.Text = value;
				CallDeferred(nameof(FitRulesTextToBox));
			}
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

	/// <summary>
	/// Tint applied to the frame art only. Uses SelfModulate rather than Modulate so it cannot
	/// bleed onto the labels sitting on top of the frame.
	/// </summary>
	[Export]
	public Color FrameColor
	{
		get => _frameColor;
		set
		{
			_frameColor = value;
			if (_mainFrameSprite2D != null)
				_mainFrameSprite2D.SelfModulate = value;
		}
	}

	/// <summary>Tint applied to the name plate art only. See <see cref="FrameColor"/>.</summary>
	[Export]
	public Color NamePlateColor
	{
		get => _namePlateColor;
		set
		{
			_namePlateColor = value;
			if (_nameSprite2D != null)
				_nameSprite2D.SelfModulate = value;
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
		// The band and both new labels must NOT set use_parent_material in the scene. The parent
		// holographic shader samples TEXTURE; a ColorRect has none, so it renders solid white and
		// swallows the band's colour. The scene's other labels leave it off for the same reason —
		// only the Sprite2D frame parts inherit that material.
		_typeLineLabel = GetNodeOrNull<Label>("%TypeLineLabel");
		_typeLineBand = GetNodeOrNull<Control>("%TypeLineBand");
		_powerToughnessLabel = GetNodeOrNull<Label>("%PowerToughnessLabel");
		_powerToughnessSprite2D = GetNodeOrNull<Sprite2D>("%PowerToughnessBadge");
		_mainFrameSprite2D = GetNodeOrNull<Sprite2D>("%MainFrame");

		// Get nodes without unique names - need relative paths from CardContainer
		if (_cardContainer != null)
		{
			_artFrameSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("ArtFrame");
			_nameSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("Name");
			_manaCostSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("ManaCost");
			_rulesTextSprite2D = _cardContainer.GetNodeOrNull<Sprite2D>("RulesText");
		}

		// Each card gets its own copy of the label settings up front. Without this the font size
		// written by the fit passes would land on the shared resource and resize every card.
		if (_rulesTextLabel?.LabelSettings != null)
			_rulesTextLabel.LabelSettings = (LabelSettings)
				_rulesTextLabel.LabelSettings.Duplicate();

		// NameLabel's settings come from a shared .tres, so this copy is what makes it safe to
		// shrink one card's name without shrinking every other card's.
		if (_nameLabel?.LabelSettings != null)
			_nameLabel.LabelSettings = (LabelSettings)_nameLabel.LabelSettings.Duplicate();

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

	protected virtual void UpdateOutlineShader()
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
		{
			var artTex = _artworkTexture ?? _defaultArtworkTexture;
			_artSprite2D.Texture = artTex;
			FitArtToFrame(artTex);
		}

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
			CallDeferred(nameof(FitNameToBox));
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

		ApplyTypeLine();
		ApplyPowerToughness();

		if (_mainFrameSprite2D != null)
			_mainFrameSprite2D.SelfModulate = _frameColor;

		if (_nameSprite2D != null)
			_nameSprite2D.SelfModulate = _namePlateColor;
	}

	private void ApplyTypeLine()
	{
		if (_typeLineLabel != null)
			_typeLineLabel.Text = _typeLine;
		if (_typeLineBand != null)
			_typeLineBand.Visible = !string.IsNullOrWhiteSpace(_typeLine);
	}

	private void ApplyPowerToughness()
	{
		if (_powerToughnessLabel != null)
			_powerToughnessLabel.Text = _powerToughness;
		if (_powerToughnessSprite2D != null)
			_powerToughnessSprite2D.Visible = !string.IsNullOrWhiteSpace(_powerToughness);
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

	private void FitArtToFrame(Texture2D artTexture)
	{
		if (_artFrameSprite2D?.Texture == null || artTexture == null)
			return;
		var frameSize = _artFrameSprite2D.Texture.GetSize();
		var artSize = artTexture.GetSize();
		var scale = Mathf.Max(frameSize.X / artSize.X, frameSize.Y / artSize.Y);
		_artSprite2D.Scale = new Vector2(scale, scale);
	}

	private void FitRulesTextToBox()
	{
		if (_rulesTextLabel == null)
			return;

		const int maxFontSize = 30;

		// A readable floor, not a fitting floor. Below this the text is technically present but
		// not usable at board scale; anything that still overflows at 14 is clipped and read via
		// the hover preview instead. Raised from 12 for the same reason.
		const int minFontSize = 14;

		var font = _rulesTextLabel.LabelSettings.Font ?? _rulesTextLabel.GetThemeFont("font");

		//Hard coded values because I can't actually get the size of the label to calculate properly.
		//Make sure that if you change the label size that you change these values accordingly.
		// The available height is the distance from the top of the rules text box to the bottom, minus a small padding to make sure the text properly fits without going out of bounds.
		// Reduced from 130 to clear the power/toughness badge, which overhangs the bottom-right
		// corner of the rules box.
		float availableHeight = 112;
		float availableWidth = 230;

		// Start at the floor, not at whatever the previous card left behind. The old loop only
		// assigned on a successful fit, so text that fit at no size kept a stale large font from
		// the node's previous occupant — which is why the same card rendered differently
		// depending on what was drawn there before it.
		var chosen = minFontSize;

		for (int size = maxFontSize; size >= minFontSize; size--)
		{
			var textSize = font.GetMultilineStringSize(
				_rulesTextLabel.Text,
				HorizontalAlignment.Left,
				availableWidth,
				size
			);

			if (textSize.Y <= availableHeight)
			{
				chosen = size;
				break;
			}
		}

		_rulesTextLabel.LabelSettings.FontSize = chosen;
	}

	/// <summary>
	/// Shrinks a long card name until it fits the name plate. Names run to 28 characters
	/// ("Sexton of the Drowned Chapel"), which overruns the plate at the authored size — and a
	/// clipped name is the one thing on a card you can never infer from context.
	/// </summary>
	private void FitNameToBox()
	{
		if (_nameLabel?.LabelSettings == null)
			return;

		const int maxFontSize = 27;
		const int minFontSize = 15;
		const float availableWidth = 250f;

		var font = _nameLabel.LabelSettings.Font ?? _nameLabel.GetThemeFont("font");
		if (font == null)
			return;

		var chosen = minFontSize;
		for (int size = maxFontSize; size >= minFontSize; size--)
		{
			if (
				font.GetStringSize(_cardName, HorizontalAlignment.Left, -1, size).X
				<= availableWidth
			)
			{
				chosen = size;
				break;
			}
		}

		_nameLabel.LabelSettings.FontSize = chosen;
	}

	/// <summary>
	/// Represents the details of a card
	/// </summary>
	public class Details
	{
		public string Id { get; set; }
		public string CardName { get; set; }
		public string ManaCost { get; set; }
		public string TypeLine { get; set; }
		public string PowerToughness { get; set; }
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
		public Color? FrameColor { get; set; }
		public Color? NamePlateColor { get; set; }
		public Color? OutlineColor { get; set; }
		public float? OutlineThickness { get; set; }
		public bool? Holographic { get; set; }
		public float? HolographicIntensity { get; set; }

		public void ApplyTo(InternalCardUI2D card)
		{
			card.Id = Id?.ToString() ?? card.Id;
			card.CardName = CardName ?? card.CardName;
			card.ManaCost = ManaCost ?? card.ManaCost;
			card.TypeLine = TypeLine ?? card.TypeLine;
			// Cleared rather than kept when null: null P/T means "not a creature", so a reused
			// node must drop the previous card's badge instead of inheriting it.
			card.PowerToughness = PowerToughness ?? "";
			card.RulesText = RulesText ?? card.RulesText;

			// Assigned unconditionally: the setter already falls back to the scene's placeholder
			// when null. Skipping the assignment left an art-less card showing the previous
			// card's art on any reused node, which is worse than a placeholder.
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
			if (FrameColor.HasValue)
				card.FrameColor = FrameColor.Value;
			if (NamePlateColor.HasValue)
				card.NamePlateColor = NamePlateColor.Value;
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
