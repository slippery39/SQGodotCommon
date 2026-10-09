using System;
using System.Linq;
using MagicCore;
using MagicCore.Sets.Fra;
using MtgGame;

namespace MagicTable;

/// <summary>
/// The real-rules Magic table (MagicCore). Step 1 placeholder: proves the engine and the FRA art
/// load inside Godot — a new game's opening hand as full cards, the first one also as a crop.
/// </summary>
public partial class MagicTableScene : Control
{
	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);
		var seed = Random.Shared.Next(1, int.MaxValue);
		var s = MagicRules.NewGame(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed);
		var hand = s.CardsIn(s.Player1Id(), ZoneType.Hand).ToList();

		var column = new VBoxContainer();
		column.SetAnchorsPreset(LayoutPreset.FullRect);
		column.AddThemeConstantOverride("separation", 16);
		AddChild(column);
		column.AddChild(
			MtgUiStyles.Title(
				$"seed {seed} · turn {s.GetGame().TurnNumber} · {s.GetGame().Step}",
				28
			)
		);

		var row = new HBoxContainer();
		column.AddChild(row);
		row.AddChild(Image(MagicArt.Crop(hand[0].Name), 220));
		foreach (var card in hand)
			row.AddChild(Image(MagicArt.Full(card.Name), 140));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
			Project.GameManager.Instance.GoToMainMenu();
	}

	private static TextureRect Image(Texture2D? texture, int width) =>
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Texture = texture,
			StretchMode = TextureRect.StretchModeEnum.KeepAspect,
			CustomMinimumSize = new Vector2(width, width * 1.4f),
		};
}
