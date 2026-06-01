using System.Text.RegularExpressions;

namespace MtgGame;

public static class CardArtLoader
{
	private const string ArtBasePath = "res://MtgGame/Assets/Card_Art/";

	public static Texture2D? Load(string cardName)
	{
		var slug = Regex.Replace(cardName.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
		var path = $"{ArtBasePath}{slug}.jpg";
		if (!ResourceLoader.Exists(path))
			return null;
		return ResourceLoader.Load<Texture2D>(path);
	}
}
