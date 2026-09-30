using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RestaurantPos.Api.Backup;
using RestaurantPos.Api.Hosting;

namespace RestaurantPos.Tests;

public class BackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"pos-backup-test-{Guid.NewGuid():N}");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 11, 22, 30, 15, TimeSpan.Zero));
    private readonly BackupOptions _options = new();

    public BackupTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE TABLE Bills (Id INTEGER PRIMARY KEY, Total INTEGER); INSERT INTO Bills (Total) VALUES (12345);";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    private string ConnectionString => $"Data Source={Path.Combine(_root, "data", "pos.db")};Pooling=False";
    private string BackupFolder => Path.Combine(_root, "backups");

    private DatabaseBackup CreateBackup() => new(
        new StaticOptions(_options),
        new PosPaths { DataRoot = _root, ConnectionString = ConnectionString },
        _clock,
        NullLogger<DatabaseBackup>.Instance);

    [Fact]
    public async Task Creates_a_verified_copy_with_the_data()
    {
        var result = await CreateBackup().RunAsync();

        Assert.True(result.Success, string.Join("; ", result.Messages));
        Assert.Equal("pos-2026-10-11_223015.db", result.FileName);
        using var conn = new SqliteConnection($"Data Source={Path.Combine(BackupFolder, result.FileName!)};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Total FROM Bills";
        Assert.Equal(12345L, cmd.ExecuteScalar());
        Assert.Contains("OK", File.ReadAllText(Path.Combine(BackupFolder, "backup-log.txt")));
    }

    [Fact]
    public async Task Label_is_added_to_the_file_name()
    {
        var result = await CreateBackup().RunAsync("before-update");
        Assert.Equal("pos-2026-10-11_223015-before-update.db", result.FileName);
    }

    [Fact]
    public async Task Copies_to_connected_extra_folders_and_skips_missing_ones()
    {
        var penDrive = Path.Combine(_root, "pendrive", "RestaurantPOS-Backup");
        Directory.CreateDirectory(Path.GetDirectoryName(penDrive)!);
        var missing = Path.Combine(_root, "not-plugged-in", "RestaurantPOS-Backup");
        _options.ExtraFolders = [penDrive, missing];

        var result = await CreateBackup().RunAsync();

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(penDrive, result.FileName!)));
        Assert.False(Directory.Exists(missing));
        Assert.Contains(result.Messages, m => m.StartsWith("Skipped") && m.Contains("not-plugged-in"));
    }

    [Fact]
    public async Task Fails_cleanly_when_the_database_cannot_be_read()
    {
        var backup = new DatabaseBackup(
            new StaticOptions(_options),
            new PosPaths { DataRoot = _root, ConnectionString = $"Data Source={Path.Combine(_root, "nope", "missing.db")};Mode=ReadOnly" },
            _clock,
            NullLogger<DatabaseBackup>.Instance);

        var result = await backup.RunAsync();

        Assert.False(result.Success);
        Assert.Null(result.FileName);
        Assert.Contains("FAILED", File.ReadAllText(Path.Combine(BackupFolder, "backup-log.txt")));
    }

    [Fact]
    public void Prune_deletes_only_old_backups_and_keeps_the_newest_few()
    {
        Directory.CreateDirectory(BackupFolder);
        var now = _clock.GetLocalNow().DateTime;
        string Make(int daysAgo, string suffix = "")
        {
            var path = Path.Combine(BackupFolder, $"pos-{now.AddDays(-daysAgo):yyyy-MM-dd_HHmmss}{suffix}.db");
            File.WriteAllText(path, "x");
            return path;
        }
        var recent = Enumerable.Range(0, 5).Select(d => Make(d)).ToList();
        var old = Make(40, "-before-update");
        var veryOld = Make(90);
        var unrelated = Path.Combine(BackupFolder, "notes.txt");
        File.WriteAllText(unrelated, "keep me");

        _options.KeepAtLeast = 5;
        CreateBackup().Prune(BackupFolder);

        Assert.All(recent, p => Assert.True(File.Exists(p)));
        Assert.False(File.Exists(old));
        Assert.False(File.Exists(veryOld));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void Prune_keeps_old_backups_when_there_are_only_a_few()
    {
        Directory.CreateDirectory(BackupFolder);
        var path = Path.Combine(BackupFolder, "pos-2026-01-01_120000.db");
        File.WriteAllText(path, "x");

        CreateBackup().Prune(BackupFolder);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Is_due_when_there_is_no_backup_or_the_newest_is_older_than_the_interval()
    {
        var backup = CreateBackup();
        Assert.True(backup.IsDue());

        await backup.RunAsync();
        Assert.False(backup.IsDue());

        _clock.Advance(TimeSpan.FromHours(23));
        Assert.False(backup.IsDue());
        _clock.Advance(TimeSpan.FromHours(1));
        Assert.True(backup.IsDue());
    }

    [Fact]
    public async Task Detects_changes_since_the_last_backup()
    {
        var backup = CreateBackup();
        Assert.True(backup.ChangedSinceLastBackup());

        await backup.RunAsync();
        File.SetLastWriteTimeUtc(Path.Combine(BackupFolder, "pos-2026-10-11_223015.db"), DateTime.UtcNow.AddMinutes(1));
        Assert.False(backup.ChangedSinceLastBackup());

        File.SetLastWriteTimeUtc(Path.Combine(_root, "data", "pos.db"), DateTime.UtcNow.AddMinutes(2));
        Assert.True(backup.ChangedSinceLastBackup());
    }

    private sealed class StaticOptions(BackupOptions value) : IOptionsMonitor<BackupOptions>
    {
        public BackupOptions CurrentValue => value;
        public BackupOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<BackupOptions, string?> listener) => null;
    }
}

public class BackupEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Run_then_status_reports_the_new_backup()
    {
        var client = factory.CreateClient();

        var run = await client.PostAsync("/api/backup/run", null);
        Assert.Equal(HttpStatusCode.OK, run.StatusCode);

        var status = await client.GetFromJsonAsync<StatusDto>("/api/backup/status");
        Assert.NotNull(status?.NewestBackupAt);
        Assert.False(status.Overdue);
        Assert.True(status.LastResult?.Success);
        Assert.Single(DatabaseBackup.ListBackups(Path.Combine(factory.DataRoot, "backups")));
    }

    private sealed record StatusDto(DateTime? NewestBackupAt, bool Overdue, ResultDto? LastResult);
    private sealed record ResultDto(bool Success, string? FileName);
}

/// <summary>A clock the tests control. Local time = UTC so file names are predictable.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public void Advance(TimeSpan by) => _now += by;
}
