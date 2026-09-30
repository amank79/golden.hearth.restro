namespace RestaurantPos.Api.Hosting;

/// <summary>
/// Where the app keeps its data. Installed (Production): C:\ProgramData\RestaurantPos.
/// Development and tests: %LOCALAPPDATA%\RestaurantPos-dev. Override with Pos:DataRoot.
/// Layout: data\pos.db (database), backups\ (daily copies), logs\ (app-yyyyMMdd.log), settings.json (per-installation settings).
/// </summary>
public sealed class PosPaths
{
    public required string DataRoot { get; init; }
    public required string ConnectionString { get; init; }

    public string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public string DefaultBackupFolder => Path.Combine(DataRoot, "backups");
    public string LogsFolder => Path.Combine(DataRoot, "logs");

    public static PosPaths From(IConfiguration config, IHostEnvironment env)
    {
        var root = config["Pos:DataRoot"] ?? (env.IsDevelopment()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestaurantPos-dev")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RestaurantPos"));
        var dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);

        return new PosPaths
        {
            DataRoot = root,
            ConnectionString = config.GetConnectionString("Pos") ?? $"Data Source={Path.Combine(dataDir, "pos.db")}",
        };
    }
}
