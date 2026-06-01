using System.Text.Json;
using System.Text.RegularExpressions;
using MtgCore;

const string ScryfallApi = "https://api.scryfall.com";
const int BaseDelayMs = 200;
const int MaxRetries = 4;

var outputPath =
	args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "card_art");
Directory.CreateDirectory(outputPath);

// Cards not in CardLibrary.All that still need art
var extraCards = new List<string> { "Plains" };

// Token cards — fetched via Scryfall search (t:token) rather than named lookup
var tokenCards = new List<string> { "Goblin", "Clue" };

var cardNames = CardLibrary.All.Select(c => c.Name).Distinct().OrderBy(n => n).ToList();
var allNamed = cardNames.Concat(extraCards).Distinct().OrderBy(n => n).ToList();

Console.WriteLine(
	$"Scraping art for {allNamed.Count} cards + {tokenCards.Count} tokens -> {outputPath}"
);
Console.WriteLine();

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("User-Agent", "SQGodotCardGame/1.0 (personal project)");
http.DefaultRequestHeaders.Add("Accept", "application/json");
http.Timeout = TimeSpan.FromSeconds(30);

var notFound = new List<string>();
var errors = new List<(string Name, string Message)>();

foreach (var name in allNamed)
{
	var slug = Slugify(name);
	var destPath = Path.Combine(outputPath, $"{slug}.jpg");

	if (File.Exists(destPath))
	{
		Console.WriteLine($"[SKIP]  {name}");
		continue;
	}

	Console.Write($"[FETCH] {name}... ");

	try
	{
		var artUrl = await GetArtCropUrl(http, name);
		if (artUrl is null)
		{
			Console.WriteLine("not found");
			notFound.Add(name);
		}
		else
		{
			var imgBytes = await http.GetByteArrayAsync(artUrl);
			await File.WriteAllBytesAsync(destPath, imgBytes);
			Console.WriteLine($"saved ({imgBytes.Length / 1024} KB)");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"ERROR: {ex.Message}");
		errors.Add((name, ex.Message));
	}

	await Task.Delay(BaseDelayMs);
}

foreach (var name in tokenCards)
{
	var slug = Slugify(name);
	var destPath = Path.Combine(outputPath, $"{slug}.jpg");

	if (File.Exists(destPath))
	{
		Console.WriteLine($"[SKIP]  {name} (token)");
		continue;
	}

	Console.Write($"[FETCH] {name} (token)... ");

	try
	{
		var artUrl = await GetTokenArtCropUrl(http, name);
		if (artUrl is null)
		{
			Console.WriteLine("not found");
			notFound.Add($"{name} (token)");
		}
		else
		{
			var imgBytes = await http.GetByteArrayAsync(artUrl);
			await File.WriteAllBytesAsync(destPath, imgBytes);
			Console.WriteLine($"saved ({imgBytes.Length / 1024} KB)");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"ERROR: {ex.Message}");
		errors.Add(($"{name} (token)", ex.Message));
	}

	await Task.Delay(BaseDelayMs);
}

Console.WriteLine();
Console.WriteLine($"Done. {allNamed.Count + tokenCards.Count} cards processed.");

if (notFound.Count > 0)
{
	Console.WriteLine($"\n=== NOT FOUND ({notFound.Count}) ===");
	foreach (var n in notFound)
		Console.WriteLine($"  {n}");
}

if (errors.Count > 0)
{
	Console.WriteLine($"\n=== ERRORS ({errors.Count}) ===");
	foreach (var (n, msg) in errors)
		Console.WriteLine($"  {n}: {msg}");
}

static string Slugify(string name) =>
	Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');

static async Task<string?> GetArtCropUrl(HttpClient http, string cardName)
{
	var encoded = Uri.EscapeDataString(cardName);
	var url = $"{ScryfallApi}/cards/named?exact={encoded}";

	for (var attempt = 0; attempt <= MaxRetries; attempt++)
	{
		using var resp = await http.GetAsync(url);

		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
			return null;

		if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
		{
			var retryAfter =
				resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
			Console.Write($"[rate limited, waiting {retryAfter.TotalSeconds:F0}s]... ");
			await Task.Delay(retryAfter);
			continue;
		}

		resp.EnsureSuccessStatusCode();

		var json = await resp.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		if (
			root.TryGetProperty("image_uris", out var imageUris)
			&& imageUris.TryGetProperty("art_crop", out var artCrop)
		)
			return artCrop.GetString();

		// Double-faced card — use front face
		if (
			root.TryGetProperty("card_faces", out var faces)
			&& faces.GetArrayLength() > 0
			&& faces[0].TryGetProperty("image_uris", out var faceUris)
			&& faceUris.TryGetProperty("art_crop", out var faceArtCrop)
		)
			return faceArtCrop.GetString();

		return null;
	}

	throw new Exception($"exceeded {MaxRetries} retries due to rate limiting");
}

// Tokens share names with real cards; use the search endpoint with t:token filter.
static async Task<string?> GetTokenArtCropUrl(HttpClient http, string tokenName)
{
	var encoded = Uri.EscapeDataString($"!\"{tokenName}\" t:token");
	var url = $"{ScryfallApi}/cards/search?q={encoded}&order=released&dir=asc";

	for (var attempt = 0; attempt <= MaxRetries; attempt++)
	{
		using var resp = await http.GetAsync(url);

		if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
			return null;

		if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
		{
			var retryAfter =
				resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
			Console.Write($"[rate limited, waiting {retryAfter.TotalSeconds:F0}s]... ");
			await Task.Delay(retryAfter);
			continue;
		}

		resp.EnsureSuccessStatusCode();

		var json = await resp.Content.ReadAsStringAsync();
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		if (root.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
		{
			var first = data[0];
			if (
				first.TryGetProperty("image_uris", out var imageUris)
				&& imageUris.TryGetProperty("art_crop", out var artCrop)
			)
				return artCrop.GetString();
		}

		return null;
	}

	throw new Exception($"exceeded {MaxRetries} retries due to rate limiting");
}
