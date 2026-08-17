using System;
using System.Collections.Generic;
using System.Linq;
using MtgCore;

namespace MtgGame;

/// <summary>
/// The visual counterpart to <see cref="CardListView"/>: one column per mana cost, the cards in a
/// column overlapped so only each one's name plate and a strip of art shows. Reading a card at
/// this size is not the point — recognising the shape of a curve is, and hovering raises the
/// caller's preview for anything you actually need to read.
/// </summary>
public static class CardGridView
{
	/// Big enough that the art reads at a glance, small enough that a 45-card pool fits across.
	private const float CardScale = 0.5f;

	/// How much of an overlapped card stays visible — the name plate plus a band of art.
	private const int VisibleBandPx = 54;

	public static void Fill(
		HBoxContainer target,
		IEnumerable<Card> cards,
		Action<Card> hovered,
		Action hoverEnded
	)
	{
		foreach (var child in target.GetChildren())
			child.QueueFree();

		var scene = ResourceLoader.Load<PackedScene>("res://MtgGame/Board/board_card.tscn");
		if (scene == null)
		{
			GD.PushWarning("CardGridView: board_card.tscn could not be loaded.");
			return;
		}

		foreach (var costGroup in cards.GroupBy(c => c.ManaCost).OrderBy(g => g.Key))
		{
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 6);
			column.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
			target.AddChild(column);

			var heading = new Label
			{
				Text = $"{costGroup.Key}  ({costGroup.Count()})",
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			heading.AddThemeFontSizeOverride("font_size", 14);
			heading.Modulate = MtgUiStyles.GoldBorder;
			column.AddChild(heading);

			var stack = new VBoxContainer();
			column.AddChild(stack);

			foreach (var card in costGroup.OrderBy(c => c.Name))
			{
				var node = scene.Instantiate<BoardCard>();
				// Set before entering the tree: BoardCard applies it in _Ready.
				node.CardScale = CardScale;
				stack.AddChild(node);
				// state: null — a drafted pool card is not in a game, so P/T is the printed value.
				node.Refresh(card, state: null);

				// Captured per iteration: the pool holds duplicate templates, and Card is a record,
				// so a by-value lookup would find the wrong copy.
				var captured = card;
				node.Hovered += _ => hovered(captured);
				node.HoverEnded += _ => hoverEnded();

				// Derived from the card's real height rather than hardcoded: BoardCard scales its
				// own minimum size in _Ready, so this is only knowable once it is in the tree.
				stack.AddThemeConstantOverride(
					"separation",
					-(int)(node.CustomMinimumSize.Y - VisibleBandPx)
				);
			}
		}
	}
}
