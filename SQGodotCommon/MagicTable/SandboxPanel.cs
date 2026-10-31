using System;
using System.Linq;
using MagicCore;
using MagicCore.Sets;
using MtgGame;

namespace MagicTable;

/// <summary>
/// The sandbox: search every playable FRA card, pick a seat and a zone, Put. Built once and kept,
/// so the search survives the table's redraws; the table enables Put while sandboxing is allowed.
/// </summary>
public partial class SandboxPanel : PanelContainer
{
	private static readonly string[] Names = CardRegistry
		.AllNames.Where(CardRegistry.IsImplemented)
		.Order()
		.ToArray();

	private readonly LineEdit _search = new() { PlaceholderText = "Search cards…" };
	private readonly ItemList _list = new() { CustomMinimumSize = new Vector2(560, 420) };
	private readonly OptionButton _seat = new();
	private readonly OptionButton _zone = new();
	private readonly Button _put = new() { Text = "Put", FocusMode = FocusModeEnum.None };

	/// <summary>(0 = you, 1 = the AI, card name, zone).</summary>
	public event Action<int, string, ZoneType>? Put;
	public event Action<string?>? Previewed;

	public SandboxPanel()
	{
		AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		Visible = false;
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		AddChild(column);

		var header = new HBoxContainer();
		var title = MtgUiStyles.Title("Sandbox", 34);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(title);
		var close = new Button { Text = "Close", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(close, 24);
		close.Pressed += () => Visible = false;
		header.AddChild(close);
		column.AddChild(header);

		_search.AddThemeFontSizeOverride("font_size", 24);
		_search.TextChanged += Filter;
		column.AddChild(_search);
		_list.AddThemeFontSizeOverride("font_size", 22);
		_list.ItemSelected += i => Previewed?.Invoke(_list.GetItemText((int)i));
		column.AddChild(_list);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		_seat.AddItem("You");
		_seat.AddItem("AI");
		foreach (var zone in Sandbox.Zones)
			_zone.AddItem(zone == ZoneType.Library ? "Library (top)" : zone.ToString());
		_zone.Select(Array.IndexOf(Sandbox.Zones, ZoneType.Hand));
		foreach (var option in new[] { _seat, _zone })
		{
			option.AddThemeFontSizeOverride("font_size", 22);
			row.AddChild(option);
		}
		row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		MtgUiStyles.StylePrimary(_put, 26);
		_put.Pressed += () =>
		{
			if (_list.GetSelectedItems() is [var item, ..])
				Put?.Invoke(_seat.Selected, _list.GetItemText(item), Sandbox.Zones[_zone.Selected]);
		};
		row.AddChild(_put);
		column.AddChild(row);
		Filter("");
	}

	/// <summary>Put works only while the table allows sandboxing (someone holds priority, nothing asked).</summary>
	public void Allow(bool allowed) => _put.Disabled = !allowed;

	private void Filter(string text)
	{
		_list.Clear();
		foreach (
			var name in Names.Where(n =>
				n.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
			)
		)
			_list.AddItem(name);
	}
}
