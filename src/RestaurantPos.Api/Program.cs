using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using RestaurantPos.Api.Backup;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features;
using RestaurantPos.Api.Features.Billing;
using RestaurantPos.Api.Features.Menu;
using RestaurantPos.Api.Features.Printing;
using RestaurantPos.Api.Features.Settings;
using RestaurantPos.Api.Hosting;

// Installed as a Windows Service the working directory is System32, so point the content root at the app folder.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});
builder.Services.AddWindowsService(o => o.ServiceName = "RestaurantPos");

var paths = PosPaths.From(builder.Configuration, builder.Environment);
// Per-installation settings (e.g. backup folders) live next to the data, so app updates never overwrite them.
builder.Configuration.AddJsonFile(paths.SettingsFile, optional: true, reloadOnChange: true);
builder.Services.AddSingleton(paths);
builder.AddPosLogging(paths);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<RuleExceptionHandler>();
builder.Services.AddScoped<BillService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<PosDbContext>(o => o.UseSqlite(paths.ConnectionString));
builder.Services.AddPosBackup(builder.Configuration);

var app = builder.Build();

var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
app.Logger.LogInformation("Restaurant POS {Version} starting ({Environment}). Data folder: {DataRoot}",
    version, app.Environment.EnvironmentName, paths.DataRoot);

try
{
    await app.MigrateDatabaseAsync();

    // Sample dishes for development only (appsettings.Development.json). Never on the restaurant laptop.
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SampleMenu:Seed"))
    {
        using var scope = app.Services.CreateScope();
        await DevSampleMenu.SeedIfEmptyAsync(scope.ServiceProvider.GetRequiredService<PosDbContext>());
    }
}
catch (Exception e)
{
    app.Logger.LogCritical(e, "Database start-up failed; the app cannot start");
    throw;
}

app.UsePosRequestLogging();
app.UseExceptionHandler();

// The built React app (src/web, `npm run build`) is served from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok", version }));
api.MapSettingsEndpoints();
api.MapMenuEndpoints();
api.MapBillEndpoints();
api.MapPrintEndpoints();
app.MapBackupEndpoints();

// Unknown /api addresses are errors, not screens: without this they would get the React page.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
