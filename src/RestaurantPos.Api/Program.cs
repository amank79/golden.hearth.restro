using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Menu;
using RestaurantPos.Api.Features.Settings;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Pos") ?? $"Data Source={DefaultDbPath()}";
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<PosDbContext>(o => o.UseSqlite(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
    db.Database.Migrate();

    // Sample dishes for development only (appsettings.Development.json). Never on the restaurant laptop.
    if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SampleMenu:Seed"))
    {
        await DevSampleMenu.SeedIfEmptyAsync(db);
    }
}

app.UseExceptionHandler();

// The built React app (src/web, `npm run build`) is served from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapSettingsEndpoints();
api.MapMenuEndpoints();

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();

// Database lives in %LOCALAPPDATA%\RestaurantPos\pos.db unless ConnectionStrings:Pos is set.
static string DefaultDbPath()
{
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestaurantPos");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "pos.db");
}

public partial class Program;
