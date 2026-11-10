using System;
using System.Collections.Generic;
using MtgGame;

namespace MagicTable;

/// <summary>
/// Arena's visual deck (LIM-5.3): the deck as columns of overlapping cards, each card's top strip showing,
/// the last one whole; a header per column with its count. A click takes a card out; a card dragged here
/// from the pool goes in; a deck card dragged onto another column moves there (LIM-5.5). Godot's own
/// drag-and-drop (<see cref="Wire"/>): a card stops the mouse, so every card takes the drop as well as the
/// space around it.
/// </summary>
public partial class DeckView : ScrollContainer
{
	public const float CardWidth = 125;
	private const float CardHeight = CardWidth * 1.395f;
	private const float Strip = 32; // how much of each covered card shows: its name line

	private readonly VBoxContainer _rows = new();

	public DeckView()
	{
		HorizontalScrollMode = ScrollMode.Auto;
		VerticalScrollMode = ScrollMode.Auto;
		_rows.AddThemeConstantOverride("separation", 12);
		AddChild(_rows);
	}

	/// <summary>
	/// Redrawn whole from the rows (the scene's "redraw, don't patch"). <paramref name="dropped"/>: a card
	/// dropped on a column (its key), or on the view's open space (null).
	/// </summary>
	public void Show(
		IReadOnlyList<IReadOnlyList<DeckColumn>> rows,
		string set,
		Action<string?> preview,
		Action<string> takeOut,
		Action<(bool FromDeck, string Card), string?> dropped
	)
	{
		CardViews.Clear(_rows);
		Wire(this, null, what => dropped(what, null));
		foreach (var row in rows)
		{
			var line = new HBoxContainer();
			line.AddThemeConstantOverride("separation", 8);
			foreach (var column in row)
				line.AddChild(
					Column(column, set, preview, takeOut, what => dropped(what, column.Key))
				);
			_rows.AddChild(line);
		}
	}

	private static Control Column(
		DeckColumn column,
		string set,
		Action<string?> preview,
		Action<string> takeOut,
		Action<(bool FromDeck, string Card)> dropped
	)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		var header = SealedScene.Text($"{column.Key}  ·  {column.Count}", 18);
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.MouseFilter = MouseFilterEnum.Pass;
		Wire(header, null, dropped);
		box.AddChild(header);
		// placed by hand: a container can't overlap its children
		var stack = new Control
		{
			CustomMinimumSize = new Vector2(
				CardWidth,
				CardHeight + Math.Max(0, column.Cards.Count - 1) * Strip
			),
			MouseFilter = MouseFilterEnum.Pass,
		};
		Wire(stack, null, dropped);
		for (var i = 0; i < column.Cards.Count; i++)
		{
			var (card, copies) = column.Cards[i];
			var tile = Card(card, set);
			tile.Position = new Vector2(0, i * Strip);
			tile.Size = new Vector2(CardWidth, CardHeight);
			if (copies > 1)
			{
				var badge = new PanelContainer
				{
					Position = new Vector2(CardWidth - 46, 2),
					MouseFilter = MouseFilterEnum.Ignore,
				};
				badge.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
				var count = SealedScene.Text($"×{copies}", 16);
				count.MouseFilter = MouseFilterEnum.Ignore;
				badge.AddChild(count);
				tile.AddChild(badge);
			}
			CardViews.Hook(tile, card, preview, () => takeOut(card));
			Wire(tile, (true, card), dropped);
			stack.AddChild(tile);
		}
		box.AddChild(stack);
		return box;
	}

	/// <summary>A card's full image, sized by its parent.</summary>
	public static TextureRect Card(string card, string set) =>
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // before Texture (godot-frontend.md)
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = MagicArt.Full(MagicArt.Key(card, set)),
		};

	/// <summary>
	/// Godot's drag-and-drop on <paramref name="target"/>: it picks up <paramref name="carries"/> (null: nothing
	/// — a drop target only), showing the card under the pointer, and takes any card dropped on it.
	/// </summary>
	public static void Wire(
		Control target,
		(bool FromDeck, string Card)? carries,
		Action<(bool FromDeck, string Card)> drop
	) =>
		target.SetDragForwarding(
			Callable.From(
				(Vector2 _) =>
				{
					if (carries is not { } what)
						return default; // nothing to pick up
					target.SetDragPreview(
						new TextureRect
						{
							ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
							StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
							Texture = (target as TextureRect)?.Texture,
							Size = new Vector2(CardWidth, CardHeight),
							Modulate = new Color(1, 1, 1, 0.85f),
						}
					);
					return (Variant)$"{(what.FromDeck ? "deck" : "pool")}:{what.Card}";
				}
			),
			Callable.From((Vector2 _, Variant data) => Read(data) is not null),
			Callable.From(
				(Vector2 _, Variant data) =>
				{
					if (Read(data) is { } what)
						drop(what);
				}
			)
		);

	private static (bool FromDeck, string Card)? Read(Variant data) =>
		data.VariantType == Variant.Type.String
		&& data.AsString().Split(':', 2) is [var from, var card]
			? (from == "deck", card)
			: null;
}
