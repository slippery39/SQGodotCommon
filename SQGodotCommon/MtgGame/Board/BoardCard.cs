using System;
using System.Linq;
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
	[Export]
	public PackedScene InternalCardScene { get; set; }

	private InternalCardUI2D _cardNode;
	private Label _statsLabel;
	private int _cardId;

	public event Action<int> Clicked;
	public event Action<int> RightClicked;
	public event Action<int> Hovered;
	public event Action<int> HoverEnded;

	public override void _Ready()
	{
		if (InternalCardScene != null)
		{
			_cardNode = InternalCardScene.Instantiate<InternalCardUI2D>();
			_cardNode.Scale = new Vector2(0.42f, 0.42f);
			_cardNode.Position = new Vector2(65, 93);
			AddChild(_cardNode);
		}
		else
		{
			GD.PushWarning($"{Name}: InternalCardScene is not set.");
		}

		_statsLabel = new Label();
		_statsLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		_statsLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_statsLabel.VerticalAlignment = VerticalAlignment.Bottom;
		_statsLabel.AddThemeFontSizeOverride("font_size", 14);
		_statsLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.75f, 1f));
		_statsLabel.AddThemeColorOverride("font_shadow_color", Colors.Black);
		_statsLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_statsLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_statsLabel.Visible = false;
		_statsLabel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_statsLabel);

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
				RulesText = MtgCardMapper.GetRulesText(card),
			};
			details.ApplyTo(_cardNode);
		}

		// SummoningSick greys the whole card (visual + stats). All other highlights tint only
		// the card visual so the P/T label stays white.
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

		var creature = card.GetComponent<CreatureComponent>();
		if (creature != null)
		{
			var stats = state.GetEffectiveStats(card.Id);
			var statsText = $"{stats.Power}/{stats.Toughness}";
			if (creature.Damage > 0)
				statsText += $" -{creature.Damage}";
			_statsLabel.Text = statsText;
			_statsLabel.Visible = true;
		}
		else
		{
			_statsLabel.Visible = false;
		}

		TooltipText = MtgCardMapper.GetRulesText(card);
	}
}
