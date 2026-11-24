using System.Text;
using ChustaFlorg.Models;

namespace ChustaFlorg.Services;

public sealed class ReliabilityService
{
    public ReliabilityService()
    {
        RootDirectory = FileSystem.AppDataDirectory;
        BackupDirectory = Path.Combine(RootDirectory, "Backups");
        Directory.CreateDirectory(BackupDirectory);
    }

    public string RootDirectory { get; }
    public string BackupDirectory { get; }
    public string AutosavePath => Path.Combine(RootDirectory, "autosave.chustaflorg");
    public string CrashLogPath => Path.Combine(RootDirectory, "diagnostics.log");

    public async Task CreateBackupAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Aucun fichier de sauvegarde à copier.", sourcePath);

        Directory.CreateDirectory(BackupDirectory);
        var backupName = $"chustaflorg-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.chustaflorg.bak";
        var destination = Path.Combine(BackupDirectory, backupName);
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
        await target.FlushAsync(cancellationToken);
        PruneBackups(10);
    }

    public async Task<string?> RestoreLatestBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var latest = Directory.Exists(BackupDirectory)
            ? Directory.EnumerateFiles(BackupDirectory, "*.chustaflorg.bak")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
            : null;

        if (latest is null)
            return null;

        var temporaryPath = destinationPath + ".restore.tmp";
        await using (var source = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var target = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellationToken);
            await target.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, destinationPath, overwrite: true);
        return latest;
    }

    public int GetBackupCount() =>
        Directory.Exists(BackupDirectory)
            ? Directory.EnumerateFiles(BackupDirectory, "*.chustaflorg.bak").Count()
            : 0;

    public void LogError(string source, Exception exception) =>
        LogMessage(source, exception.ToString());

    public void LogMessage(string source, string message)
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{message}{Environment.NewLine}{new string('-', 72)}{Environment.NewLine}";
            File.AppendAllText(CrashLogPath, line, Encoding.UTF8);
        }
        catch
        {
            // A diagnostic logger must never cause another application failure.
        }
    }

    private void PruneBackups(int maximum)
    {
        foreach (var oldBackup in Directory.EnumerateFiles(BackupDirectory, "*.chustaflorg.bak")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(maximum))
        {
            try { File.Delete(oldBackup); }
            catch { /* Keep running if an old backup cannot be removed. */ }
        }
    }
}