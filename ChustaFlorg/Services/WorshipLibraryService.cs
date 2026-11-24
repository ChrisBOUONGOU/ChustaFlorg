using System.Text.Json;
using ChustaFlorg.Models;

namespace ChustaFlorg.Services;

public sealed class WorshipLibraryService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string LibraryPath => Path.Combine(FileSystem.AppDataDirectory, "worship-library.json");

    public async Task<List<WorshipSong>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LibraryPath))
            return [];

        await using var stream = File.OpenRead(LibraryPath);
        return await JsonSerializer.DeserializeAsync<List<WorshipSong>>(stream, Options, cancellationToken) ?? [];
    }

    public async Task SaveAsync(IEnumerable<WorshipSong> songs, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        var temporaryPath = LibraryPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, songs, Options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        File.Move(temporaryPath, LibraryPath, overwrite: true);
    }
}
