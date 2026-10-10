using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Hosting;

namespace RestaurantPos.Api.Backup;

public sealed record BackupResult(bool Success, string? FileName, DateTimeOffset At, IReadOnlyList<string> Messages);

/// <summary>
/// Makes a consistent, verified copy of the SQLite database while the app keeps running,
/// copies it to any extra folders (pen drive, synced cloud folder) and deletes old copies.
/// Files are named pos-2026-10-11_223015.db (local time), optionally with a label: pos-..._223015-before-update.db.
/// </summary>
public sealed partial class DatabaseBackup(
    IOptionsMonitor<BackupOptions> options, PosPaths paths, TimeProvider clock, ILogger<DatabaseBackup> log)
{
    private const string TimeFormat = "yyyy-MM-dd_HHmmss";
    private const string TempSuffix = ".tmp";
    private readonly SemaphoreSlim _gate = new(1, 1);

    [GeneratedRegex(@"^pos-(\d{4}-\d{2}-\d{2}_\d{6})(-[a-z-]+)?\.db$")]
    private static partial Regex BackupName();

    public BackupResult? LastResult { get; private set; }

    public string Folder => options.CurrentValue.Folder ?? paths.DefaultBackupFolder;

    public IReadOnlyList<string> ExtraFolders => options.CurrentValue.ExtraFolders;

    /// <summary>A folder counts as available if it or its parent exists (a pen drive's root exists only while plugged in).</summary>
    public static bool IsAvailable(string folder) =>
        Directory.Exists(folder) || Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(folder)));

    public DateTime? NewestBackupTime() => ListBackups(Folder).Select(b => (DateTime?)b.Time).FirstOrDefault();

    public bool IsDue()
    {
        var newest = NewestBackupTime();
        return newest is null || clock.GetLocalNow().DateTime - newest.Value >= TimeSpan.FromHours(options.CurrentValue.IntervalHours);
    }

    /// <summary>True if the database was written after the newest backup was made.</summary>
    public bool ChangedSinceLastBackup()
    {
        var backups = ListBackups(Folder);
        if (backups.Count == 0) return true;
        var newest = backups[0];
        var db = new SqliteConnectionStringBuilder(paths.ConnectionString).DataSource;
        var lastWrite = new[] { db, db + "-wal" }.Where(File.Exists).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max();
        return lastWrite > File.GetLastWriteTimeUtc(newest.Path);
    }

    public async Task<BackupResult> RunAsync(string? label = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var result = await RunCoreAsync(label, ct);
            LastResult = result;
            WriteLog(result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<BackupResult> RunCoreAsync(string? label, CancellationToken ct)
    {
        var now = clock.GetLocalNow();
        var messages = new List<string>();
        var name = $"pos-{now.ToString(TimeFormat, CultureInfo.InvariantCulture)}{(label is null ? "" : "-" + label)}.db";
        var target = Path.Combine(Folder, name);
        var temp = target + TempSuffix;

        try
        {
            Directory.CreateDirectory(Folder);
            DeleteLeftoverTempFiles(Folder);

            // VACUUM INTO writes a complete, consistent copy even while bills are being saved.
            // It goes to a .tmp file first, so a cloud sync app never uploads a half-written or unchecked backup.
            await using (var source = new SqliteConnection(paths.ConnectionString))
            {
                await source.OpenAsync(ct);
                await using var cmd = source.CreateCommand();
                cmd.CommandText = "VACUUM INTO $path";
                cmd.Parameters.AddWithValue("$path", temp);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            var check = await IntegrityCheckAsync(temp, ct);
            if (check != "ok")
            {
                File.Delete(temp);
                messages.Add($"Backup failed the integrity check: {check}");
                log.LogError("Backup {File} failed the integrity check: {Check}", name, check);
                return new BackupResult(false, null, now, messages);
            }
            File.Move(temp, target, overwrite: true);
            messages.Add($"Saved {name}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            messages.Add($"Backup failed: {e.Message}");
            log.LogError(e, "Backup failed");
            return new BackupResult(false, null, now, messages);
        }

        foreach (var extra in options.CurrentValue.ExtraFolders)
        {
            if (!IsAvailable(extra))
            {
                messages.Add($"Skipped {extra}: not connected");
                log.LogWarning("Backup folder {Folder} is not connected", extra);
                continue;
            }
            try
            {
                Directory.CreateDirectory(extra);
                DeleteLeftoverTempFiles(extra);
                var extraTarget = Path.Combine(extra, name);
                File.Copy(target, extraTarget + TempSuffix, overwrite: true);
                File.Move(extraTarget + TempSuffix, extraTarget, overwrite: true);
                Prune(extra);
                messages.Add($"Copied to {extra}");
            }
            catch (Exception e)
            {
                messages.Add($"Could not copy to {extra}: {e.Message}");
                log.LogWarning(e, "Could not copy the backup to {Folder}", extra);
            }
        }

        Prune(Folder);
        log.LogInformation("Backup saved: {File}", name);
        return new BackupResult(true, name, now, messages);
    }

    private static async Task<string> IntegrityCheckAsync(string file, CancellationToken ct)
    {
        await using var conn = new SqliteConnection($"Data Source={file};Mode=ReadOnly;Pooling=False");
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check";
        return (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? "no result";
    }

    /// <summary>Deletes backups older than KeepDays, always keeping the newest KeepAtLeast. Other files are never touched.</summary>
    public void Prune(string folder)
    {
        var o = options.CurrentValue;
        var cutoff = clock.GetLocalNow().DateTime.AddDays(-o.KeepDays);
        foreach (var old in ListBackups(folder).Skip(o.KeepAtLeast).Where(b => b.Time < cutoff))
        {
            try { File.Delete(old.Path); }
            catch (IOException e) { log.LogWarning(e, "Could not delete old backup {File}", old.Path); }
        }
    }

    // A backup interrupted by a power cut leaves a pos-*.db.tmp file behind; it is never a usable backup.
    private void DeleteLeftoverTempFiles(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "pos-*.db" + TempSuffix))
        {
            try { File.Delete(file); }
            catch (IOException e) { log.LogWarning(e, "Could not delete unfinished backup {File}", file); }
        }
    }

    /// <summary>Backups in a folder, newest first.</summary>
    public static IReadOnlyList<(string Path, DateTime Time)> ListBackups(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "pos-*.db")
            .Select(path => (path, match: BackupName().Match(System.IO.Path.GetFileName(path))))
            .Where(x => x.match.Success)
            .Select(x => (x.path, DateTime.ParseExact(x.match.Groups[1].Value, TimeFormat, CultureInfo.InvariantCulture)))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    // A plain-text history next to the backups, readable without any tools.
    private void WriteLog(BackupResult result)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var line = $"{result.At:yyyy-MM-dd HH:mm:ss}  {(result.Success ? "OK    " : "FAILED")}  {string.Join(" | ", result.Messages)}";
            File.AppendAllLines(Path.Combine(Folder, "backup-log.txt"), [line]);
        }
        catch (IOException e)
        {
            log.LogWarning(e, "Could not write backup-log.txt");
        }
    }
}
