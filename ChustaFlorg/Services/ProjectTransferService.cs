using System.IO.Compression;
using System.Text.Json;
using ChustaFlorg.Models;

namespace ChustaFlorg.Services;

public sealed class ProjectTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> ExportPresentationAsync(
        PresentationProject project,
        string mediaDirectory,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var copy = JsonSerializer.Deserialize<PresentationProject>(
            JsonSerializer.Serialize(project, JsonOptions), JsonOptions)
            ?? throw new InvalidDataException("Le projet ne peut pas être préparé pour l'exportation.");

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var included = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mediaCount = 0;

        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var slide in copy.Slides)
            {
                slide.BackgroundPath = await AddMediaAsync(slide.BackgroundPath, "Media", archive, included, cancellationToken);
                slide.BackgroundAudioPath = await AddMediaAsync(slide.BackgroundAudioPath, "Media", archive, included, cancellationToken);
            }

            var projectEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
            await using var projectStream = projectEntry.Open();
            await JsonSerializer.SerializeAsync(projectStream, copy, JsonOptions, cancellationToken);
            mediaCount = included.Count;
        }

        return mediaCount;
    }

    public async Task<PresentationProject> ImportPresentationAsync(
        string sourcePath,
        string destinationRoot,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationRoot);
        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);
        var projectEntry = archive.GetEntry("project.json")
            ?? throw new InvalidDataException("Ce fichier ne contient pas de présentation ChustaFlorg.");

        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("Media/", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = GetSafeDestination(destinationRoot, entry.FullName);
            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = entry.Open();
            await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(target, cancellationToken);
        }

        await using var projectStream = projectEntry.Open();
        var project = await JsonSerializer.DeserializeAsync<PresentationProject>(projectStream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Le fichier de présentation est vide ou invalide.");

        foreach (var slide in project.Slides)
        {
            slide.BackgroundPath = ResolveImportedMedia(slide.BackgroundPath, destinationRoot);
            slide.BackgroundAudioPath = ResolveImportedMedia(slide.BackgroundAudioPath, destinationRoot);
        }

        return project;
    }

    public async Task<int> ExportMediaLibraryAsync(
        string mediaDirectory,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        var files = Directory.Exists(mediaDirectory)
            ? Directory.EnumerateFiles(mediaDirectory, "*", SearchOption.AllDirectories).ToList()
            : new List<string>();

        await using var output = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(mediaDirectory, file).Replace('\\', '/');
            var entry = archive.CreateEntry("MediaLibrary/" + relative, CompressionLevel.Optimal);
            await using var source = File.OpenRead(file);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
        }

        return files.Count;
    }

    public async Task<int> ImportMediaLibraryAsync(
        string sourcePath,
        string mediaDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(mediaDirectory);
        var imported = 0;
        await using var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(input, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("MediaLibrary/", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/'))
                continue;

            var originalName = Path.GetFileName(entry.FullName);
            if (string.IsNullOrWhiteSpace(originalName))
                continue;

            var destination = Path.Combine(mediaDirectory, $"{Guid.NewGuid():N}_{originalName}");
            await using var source = entry.Open();
            await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(target, cancellationToken);
            imported++;
        }

        if (imported == 0)
            throw new InvalidDataException("Aucun fichier de médiathèque ChustaFlorg n'a été trouvé dans cette archive.");

        return imported;
    }

    private static async Task<string?> AddMediaAsync(
        string? sourcePath,
        string folder,
        ZipArchive archive,
        IDictionary<string, string> included,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;

        var fullPath = Path.GetFullPath(sourcePath);
        if (!included.TryGetValue(fullPath, out var relativePath))
        {
            var extension = Path.GetExtension(fullPath);
            var fileName = Guid.NewGuid().ToString("N") + extension;
            relativePath = folder + "/" + fileName;
            included[fullPath] = relativePath;

            var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
            await using var source = File.OpenRead(fullPath);
            await using var target = entry.Open();
            await source.CopyToAsync(target, cancellationToken);
        }

        return relativePath;
    }

    private static string? ResolveImportedMedia(string? path, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Media/", StringComparison.OrdinalIgnoreCase))
            return path;

        var resolved = GetSafeDestination(destinationRoot, path);
        return File.Exists(resolved) ? resolved : null;
    }

    private static string GetSafeDestination(string root, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized));
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("L'archive contient un chemin de fichier non autorisé.");
        return fullPath;
    }
}
