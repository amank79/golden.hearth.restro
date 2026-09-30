namespace RestaurantPos.Api.Backup;

/// <summary>"Backup" section of appsettings.json / settings.json.</summary>
public class BackupOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Main backup folder. Defaults to &lt;DataRoot&gt;\backups.</summary>
    public string? Folder { get; set; }

    /// <summary>Extra copies, e.g. a pen drive (E:\RestaurantPOS-Backup). Skipped with a warning when not connected.</summary>
    public List<string> ExtraFolders { get; set; } = [];

    /// <summary>A new backup is made when the newest one is older than this.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Backups older than this are deleted...</summary>
    public int KeepDays { get; set; } = 30;

    /// <summary>...but the newest few are always kept, however old.</summary>
    public int KeepAtLeast { get; set; } = 7;

    /// <summary>Also back up when the app stops (laptop shut down), if anything changed since the last backup.</summary>
    public bool BackupOnShutdown { get; set; } = true;

    public int CheckEveryMinutes { get; set; } = 60;
}
