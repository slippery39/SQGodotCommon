using System.Collections.Generic;
using System.Linq;
using MtgCore;

namespace MtgGame;

/// <summary>
/// Fills a container with a curve-ordered card list — the shape both the draft sidebar and the
/// end-of-tournament decklists want. Text only: these lists run to 45 entries, and card art
/// would cost far more than it tells you.
/// </summary>
public static class CardListView
{
	public static void Fill(
		VBoxContainer target,
		IEnumerable<Card> cards,
		int fontSize = 12,
		bool dim = false
	)
	{
		foreach (var child in target.GetChildren())
			child.QueueFree();

		// Duplicates are collapsed to "2x Lightning Bolt" — a drafted pool can hold several
		// copies of one template, and one line each reads as noise.
		foreach (var costGroup in cards.GroupBy(c => c.ManaCost).OrderBy(g => g.Key))
		{
			var heading = new Label { Text = $"{costGroup.Key} mana  ({costGroup.Count()})" };
			heading.AddThemeFontSizeOverride("font_size", fontSize);
			heading.Modulate = MtgUiStyles.GoldBorder;
			target.AddChild(heading);

			var byName = costGroup.GroupBy(c => c.Name).OrderBy(g => g.Key);
			foreach (var nameGroup in byName)
			{
				var count = nameGroup.Count();
				var label = new Label
				{
					Text = count > 1 ? $"  {count}x {nameGroup.Key}" : $"  {nameGroup.Key}",
				};
				label.AddThemeFontSizeOverride("font_size", fontSize + 1);
				if (dim)
					label.Modulate = new Color(0.6f, 0.6f, 0.65f, 1f);
				target.AddChild(label);
			}
		}
	}
}
