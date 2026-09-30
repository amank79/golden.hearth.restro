using Serilog;
using Serilog.Events;

namespace RestaurantPos.Api.Hosting;

public static class Logging
{
    /// <summary>
    /// Logs to the console and to one file per day in &lt;DataRoot&gt;\logs (app-20261011.log), kept for 60 days.
    /// Levels can be raised on site through settings.json, e.g. "Serilog": { "MinimumLevel": "Debug" }.
    /// </summary>
    public static void AddPosLogging(this WebApplicationBuilder builder, PosPaths paths)
    {
        builder.Services.AddSerilog((_, config) => config
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(paths.LogsFolder, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 60,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}  <{SourceContext}>{NewLine}{Exception}"),
            // Each app instance keeps its own logger instead of the global Serilog.Log (matters in parallel tests).
            preserveStaticLogger: true);
    }

    /// <summary>One line per API call (method, path, status, time taken); page and asset requests are skipped.</summary>
    public static void UsePosRequestLogging(this WebApplication app) =>
        app.UseSerilogRequestLogging(o =>
        {
            o.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            o.GetLevel = (http, _, ex) =>
                ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
                : http.Request.Path.StartsWithSegments("/api") ? LogEventLevel.Information
                : LogEventLevel.Verbose;
        });
}
