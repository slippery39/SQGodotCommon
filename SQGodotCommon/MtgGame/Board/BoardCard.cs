using System;
using Common.Cards;
using ImmutableGameObjects;
using MtgCore;

namespace MtgGame;

public enum BoardCardHighlight
{
	None,
	Selected,
	Target,
	AdditionalCost,
	SummoningSick,
	Flashback,
}

public partial class BoardCard : Control
{
	/// The scale board_card.tscn's CustomMinimumSize (150x186) was authored against.
	private const float BaseScale = 0.42f;

	[Export]
	public PackedScene InternalCardScene { get; set; }

	/// <summary>
	/// Card visual scale. The node's minimum size scales with it, so layout follows.
	/// Must be set before the node enters the tree — <see cref="_Ready"/> applies it.
	/// Defaults to the battlefield's size, which is too small to read a rules box at.
	/// </summary>
	[Export]
	public float CardScale { get; set; } = BaseScale;

	private InternalCardUI2D _cardNode;
	private int _cardId;

	public event Action<int> Clicked;
	public event Action<int> RightClicked;
	public event Action<int> Hovered;
	public event Action<int> HoverEnded;

	public override void _Ready()
	{
		// The authored minimum size assumes BaseScale, so grow it by the same factor rather
		// than hardcoding a second set of dimensions. At CardScale = BaseScale this is a no-op,
		// which is what keeps the battlefield pixel-identical.
		CustomMinimumSize *= CardScale / BaseScale;

		if (InternalCardScene != null)
		{
			_cardNode = InternalCardScene.Instantiate<InternalCardUI2D>();
			_cardNode.Scale = new Vector2(CardScale, CardScale);
			_cardNode.Position = CustomMinimumSize / 2;
			AddChild(_cardNode);
		}
		else
		{
			GD.PushWarning($"{Name}: InternalCardScene is not set.");
		}

		GuiInput += OnGuiInput;
		MouseEntered += () => Hovered?.Invoke(_cardId);
		MouseExited += () => HoverEnded?.Invoke(_cardId);
	}

	private void OnGuiInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.Left)
				Clicked?.Invoke(_cardId);
			else if (mb.ButtonIndex == MouseButton.Right)
				RightClicked?.Invoke(_cardId);
		}
	}

	/// <param name="state">
	/// Null for a card that is not in a game — a draft pack, for instance. Stats then come from
	/// the printed P/T, since a card outside a GameState can carry no modifiers or damage.
	/// </param>
	public void Refresh(
		Card card,
		GameState state,
		BoardCardHighlight highlight = BoardCardHighlight.None
	)
	{
		_cardId = card.Id;

		if (_cardNode != null)
		{
			var details = new InternalCardUI2D.Details
			{
				CardName = card.Name,
				ManaCost = card.ManaCost.ToString(),
				TypeLine = MtgCardMapper.GetTypeLine(card),
				PowerToughness = MtgCardMapper.GetPowerToughness(card, state),
				RulesText = MtgCardMapper.GetRulesText(card, state),
				ArtworkTexture = CardArtLoader.Load(card.Name),
				FrameColor = MtgCardTheme.FrameColor(card),
				NamePlateColor = MtgCardTheme.NamePlateColor(card),
			};
			details.ApplyTo(_cardNode);
		}

		// SummoningSick greys the whole card. All other highlights tint only the card visual.
		Modulate =
			highlight == BoardCardHighlight.SummoningSick
				? new Color(0.6f, 0.6f, 0.6f, 1f)
				: Colors.White;

		if (_cardNode != null)
		{
			_cardNode.Modulate = highlight switch
			{
				BoardCardHighlight.Target => new Color(1f, 1f, 0.3f, 1f),
				BoardCardHighlight.Selected => new Color(0.4f, 1f, 0.4f, 1f),
				BoardCardHighlight.AdditionalCost => new Color(1f, 0.65f, 0.1f, 1f),
				BoardCardHighlight.Flashback => new Color(0.8f, 0.5f, 1f, 1f),
				_ => Colors.White,
			};
		}
	}
}
