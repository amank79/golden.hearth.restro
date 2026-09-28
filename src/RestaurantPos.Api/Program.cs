using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using RestaurantPos.Api.Data;
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
    scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.Migrate();
}

app.UseExceptionHandler();

// The built React app (src/web, `npm run build`) is served from wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapSettingsEndpoints();

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

// Database lives in %LOCALAPPDATA%\RestaurantPos\pos.db unless ConnectionStrings:Pos is set.
static string DefaultDbPath()
{
    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RestaurantPos");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "pos.db");
}

public partial class Program;
