namespace Common.Core;


public partial class DebugGrid : Node2D
{
	[Export]
	public Vector2 TileSize { get; set; } = new Vector2(80, 80);

	[Export]
	public Color GridColor { get; set; } = new Color(1, 1, 1, 0.5f);

	[Export]
	public int GridWidth { get; set; } = 20;

	[Export]
	public int GridHeight { get; set; } = 15;

	public override void _Draw()
	{
		// Loop through the grid and draw horizontal and vertical lines
		for (int x = 0; x <= GridWidth; x++)
		{
			float posX = x * TileSize.X;
			DrawLine(new Vector2(posX, 0), new Vector2(posX, GridHeight * TileSize.Y), GridColor);
		}

		for (int y = 0; y <= GridHeight; y++)
		{
			float posY = y * TileSize.Y;
			DrawLine(new Vector2(0, posY), new Vector2(GridWidth * TileSize.X, posY), GridColor);
		}
	}
}
