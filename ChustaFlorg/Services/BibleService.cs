using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChustaFlorg.Models;
using Microsoft.Maui.Storage;

namespace ChustaFlorg.Services;

/// <summary>
/// Uses the same public Free.Bible JSON endpoints as StageFlow.
/// Downloaded index/chapter JSON is cached locally for offline reading.
/// </summary>
public sealed class BibleService
{
    private const string BaseUrl = "https://free.bible/bible";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "BibleCache");
    private readonly string _bookmarksPath = Path.Combine(FileSystem.AppDataDirectory, "bible-bookmarks.json");

    private static readonly List<BibleTranslation> AvailableTranslations =
    [
        new() { Id = "segond", Name = "Louis Segond", ShortName = "SEGOND", Language = "Français" },
        new() { Id = "web", Name = "World English Bible", ShortName = "WEB", Language = "English" },
        new() { Id = "rv1909", Name = "Reina-Valera 1909", ShortName = "RV1909", Language = "Español" },
        new() { Id = "pt", Name = "Bíblia Portuguesa Mundial", ShortName = "BPM", Language = "Português" }
    ];

    public BibleService() => Directory.CreateDirectory(_cacheDirectory);

    public IReadOnlyList<BibleTranslation> GetTranslations() => AvailableTranslations;

    public async Task<IReadOnlyList<BibleBook>> GetBooksAsync(BibleTranslation translation, CancellationToken cancellationToken = default)
    {
        var json = await GetCachedJsonAsync($"{BaseUrl}/{Uri.EscapeDataString(translation.Id)}/index.json", cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            JsonElement books;
            if (root.ValueKind == JsonValueKind.Array) books = root;
            else if (!TryGetProperty(root, "books", out books)) return [];

            var result = new List<BibleBook>();
            foreach (var item in books.EnumerateArray())
            {
                var id = GetString(item, "id", "book", "slug");
                if (string.IsNullOrWhiteSpace(id)) continue;
                result.Add(new BibleBook
                {
                    Id = id,
                    Name = GetString(item, "name", "title") is { Length: > 0 } name ? name : id,
                    Abbreviation = GetString(item, "abbreviation", "abbr", "shortName"),
                    ChapterCount = GetInt(item, "chapters", "chapterCount", "count")
                });
            }
            return result;
        }
        catch (JsonException) { return []; }
    }

    public async Task<BibleChapter?> GetChapterAsync(BibleTranslation translation, BibleBook book, int chapterNumber, CancellationToken cancellationToken = default)
    {
        if (chapterNumber < 1 || (book.ChapterCount > 0 && chapterNumber > book.ChapterCount)) return null;
        var url = $"{BaseUrl}/{Uri.EscapeDataString(translation.Id)}/{Uri.EscapeDataString(book.Id)}/{chapterNumber}.json";
        var json = await GetCachedJsonAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!TryGetProperty(root, "verses", out var verses) || verses.ValueKind != JsonValueKind.Array) return null;
            var result = new BibleChapter { BookId = book.Id, BookName = book.Name, Number = chapterNumber };
            foreach (var item in verses.EnumerateArray())
            {
                var number = GetInt(item, "v", "verse");
                if (number <= 0) continue;
                result.Verses.Add(new BibleVerse
                {
                    Verse = number,
                    Text = GetString(item, "t", "text"),
                    BookName = book.Name,
                    Chapter = chapterNumber
                });
            }
            return result;
        }
        catch (JsonException) { return null; }
    }

    public async Task<IReadOnlyList<BibleVerse>> SearchInChapterAsync(BibleTranslation translation, BibleBook book, int chapter, string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var content = await GetChapterAsync(translation, book, chapter, cancellationToken);
        return content?.Verses.Where(v => v.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToList() ?? [];
    }

    public async Task<List<BibleBookmark>> LoadBookmarksAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_bookmarksPath)) return [];
        try
        {
            await using var stream = File.OpenRead(_bookmarksPath);
            return await JsonSerializer.DeserializeAsync<List<BibleBookmark>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch (JsonException) { return []; }
    }

    public async Task SaveBookmarksAsync(IEnumerable<BibleBookmark> bookmarks, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        var temp = _bookmarksPath + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, bookmarks, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        File.Move(temp, _bookmarksPath, true);
    }

    private async Task<string?> GetCachedJsonAsync(string url, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_cacheDirectory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant() + ".json");
        if (File.Exists(path))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(30))
                    return await File.ReadAllTextAsync(path, cancellationToken);
            }
            catch (IOException) { }
        }

        try
        {
            using var response = await Http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            await File.WriteAllTextAsync(path, json, cancellationToken);
            return json;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            if (File.Exists(path)) return await File.ReadAllTextAsync(path, cancellationToken);
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                { value = property.Value; return true; }
        }
        value = default;
        return false;
    }

    private static string GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static int GetInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        }
        return 0;
    }
}