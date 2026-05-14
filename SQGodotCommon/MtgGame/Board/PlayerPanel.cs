using MtgCore;

namespace MtgGame;

public partial class PlayerPanel : PanelContainer
{
	private Label _nameLabel = null!;
	private Label _lifeLabel = null!;
	private Label _manaLabel = null!;
	private Label _libraryLabel = null!;

	public override void _Ready()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 32);
		margin.AddChild(hbox);

		_nameLabel = new Label();
		hbox.AddChild(_nameLabel);

		_lifeLabel = new Label();
		hbox.AddChild(_lifeLabel);

		_manaLabel = new Label();
		hbox.AddChild(_manaLabel);

		_libraryLabel = new Label();
		hbox.AddChild(_libraryLabel);
	}

	public void Refresh(string playerName, MtgPlayer player, int libraryCount)
	{
		_nameLabel.Text = playerName;
		_lifeLabel.Text = $"Life: {player.Life}";
		_manaLabel.Text = $"Mana: {player.CurrentMana}/{player.MaxMana}";
		_libraryLabel.Text = $"Library: {libraryCount}";
	}

	public void SetActive(bool isActive)
	{
		Modulate = isActive ? Colors.White : new Color(0.55f, 0.55f, 0.6f);
	}
}
