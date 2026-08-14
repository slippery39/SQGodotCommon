using System.Text.Json;
using System.Text.RegularExpressions;
using MtgCore;

const string ScryfallApi = "https://api.scryfall.com";
const int BaseDelayMs = 200;
const int MaxRetries = 4;

// Usage: MtgArtScraper [outputPath] [setCode]
// The set defaults to Legacy so the original invocation is unchanged. Pass HLM for Hollowmere.
var outputPath =
	args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "card_art");
Directory.CreateDirectory(outputPath);

var setCode = args.Length > 1 ? args[1] : SetRegistry.LegacyCode;
var set = SetRegistry.Get(setCode);

// Cards not in the set list that still need art
var extraCards = new List<string> { "Plains" };

// Token cards — fetched via Scryfall search (t:token) rather than named lookup
var tokenCards = new List<string> { "Goblin", "Clue", "Spirit", "Human", "Zombie", "Vampire" };

var cardNames = set.Cards.Select(c => c.Name).Distinct().OrderBy(n => n).ToList();

// Double-faced cards need their night face fetched separately — it is a distinct piece of
// art under a different name, and the card renders it when transformed.
var nightFaces = set
	.Cards.SelectMany(c => c.GetComponents<TransformComponent>())
	.Select(t => t.OtherFaceName)
	.Distinct()
	.ToList();

var allNamed = cardNames.Concat(nightFaces).Concat(extraCards).Distinct().OrderBy(n => n).ToList();

Console.WriteLine(
	$"Scraping art for {set.Name}: {allNamed.Count} cards "
		+ $"(incl. {nightFaces.Count} night faces) + {tokenCards.Count} tokens -> {outputPath}"
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
	// These are the original designs — no real card shares the name, so Scryfall has nothing.
	// This list is the exact input for generating the remaining art, and it is written to a
	// file so it can be fed straight to a generator without re-running the scrape.
	var missingPath = Path.Combine(outputPath, "_needs_art.txt");
	await File.WriteAllLinesAsync(missingPath, notFound);

	Console.WriteLine($"\n=== NOT FOUND ({notFound.Count}) — written to {missingPath} ===");
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
