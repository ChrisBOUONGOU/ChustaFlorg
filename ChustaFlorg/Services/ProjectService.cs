using System.Text.Json;
using ChustaFlorg.Models;

namespace ChustaFlorg.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(PresentationProject project, string path, CancellationToken cancellationToken = default)
    {
        project.ModifiedAtUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(project, Options);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    public async Task<PresentationProject> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var project = await JsonSerializer.DeserializeAsync<PresentationProject>(stream, Options, cancellationToken);
        return project ?? throw new InvalidDataException("Le fichier de présentation est vide ou invalide.");
    }
}