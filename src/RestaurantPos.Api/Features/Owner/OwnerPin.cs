using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RestaurantPos.Api.Data;
using RestaurantPos.Api.Features.Settings;

namespace RestaurantPos.Api.Features.Owner;

/// <summary>
/// Owner PIN (Phase 1 safety): menu changes, discounts, cancellations and settings need it once a PIN is set.
/// Entering the PIN gives a token that stays valid while it is used (5 minutes after the last protected action).
/// </summary>
public static partial class OwnerPin
{
    public const string TokenHeader = "X-Owner-Token";
    public const string ExpiresHeader = "X-Owner-Expires";
    public const string RequiredCode = "owner_pin_required";

    [GeneratedRegex("^[0-9]{4,6}$")]
    private static partial Regex PinPattern();

    public static bool IsValidFormat(string? pin) => pin is not null && PinPattern().IsMatch(pin);

    private const int Iterations = 100_000;

    /// <summary>"v1$iterations$salt$hash" (PBKDF2-SHA256). The PIN itself is never stored.</summary>
    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string pin, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Only lets the request through when no PIN is set or it carries a valid owner token.</summary>
    public static TBuilder RequireOwner<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var settings = await http.RequestServices.GetRequiredService<PosDbContext>().GetSettingsAsync();
            if (settings.OwnerPinHash is null) return await next(context);

            var sessions = http.RequestServices.GetRequiredService<OwnerSessions>();
            if (sessions.Touch(http.Request.Headers[TokenHeader]) is { } expires)
            {
                http.Response.Headers[ExpiresHeader] = expires.ToString("O");
                return await next(context);
            }
            return Results.Problem(
                "The owner PIN is needed for this.", statusCode: StatusCodes.Status403Forbidden, title: "The owner PIN is needed for this.",
                extensions: new Dictionary<string, object?> { ["code"] = RequiredCode });
        });
}

/// <summary>Unlocked owner sessions and the wrong-PIN limit. In memory: a restart locks everything again.</summary>
public sealed class OwnerSessions(TimeProvider clock)
{
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(5);
    public const int MaxWrongTries = 5;
    public static readonly TimeSpan WaitAfterWrongTries = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private readonly Lock _gate = new();
    private int _wrongTries;
    private DateTimeOffset _blockedUntil;

    public (string Token, DateTimeOffset Expires) Open()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var expires = clock.GetUtcNow() + IdleLimit;
        _tokens[token] = expires;
        return (token, expires);
    }

    /// <summary>The new expiry if the token is still valid (and extends it), otherwise null.</summary>
    public DateTimeOffset? Touch(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_tokens.TryGetValue(token, out var expires)) return null;
        var now = clock.GetUtcNow();
        if (expires <= now)
        {
            _tokens.TryRemove(token, out _);
            return null;
        }
        var next = now + IdleLimit;
        _tokens[token] = next;
        return next;
    }

    public DateTimeOffset? ExpiresAt(string? token) =>
        !string.IsNullOrEmpty(token) && _tokens.TryGetValue(token, out var e) && e > clock.GetUtcNow() ? e : null;

    public void Close(string? token)
    {
        if (!string.IsNullOrEmpty(token)) _tokens.TryRemove(token, out _);
    }

    public void CloseAll() => _tokens.Clear();

    /// <summary>Time left before another try is allowed after too many wrong PINs.</summary>
    public TimeSpan? Blocked()
    {
        lock (_gate)
        {
            var left = _blockedUntil - clock.GetUtcNow();
            return left > TimeSpan.Zero ? left : null;
        }
    }

    public void RecordWrong()
    {
        lock (_gate)
        {
            if (++_wrongTries < MaxWrongTries) return;
            _wrongTries = 0;
            _blockedUntil = clock.GetUtcNow() + WaitAfterWrongTries;
        }
    }

    public void RecordRight()
    {
        lock (_gate) _wrongTries = 0;
    }
}

public record OwnerStatus(bool PinSet, bool Unlocked, DateTimeOffset? ExpiresAt);

public record PinInput(string? Pin);

public record UnlockResult(string Token, DateTimeOffset ExpiresAt);

public static class OwnerEndpoints
{
    public static RouteGroupBuilder MapOwnerEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/owner");

        g.MapGet("/status", async (HttpRequest request, PosDbContext db, OwnerSessions sessions) =>
        {
            var settings = await db.GetSettingsAsync();
            var expires = sessions.ExpiresAt(request.Headers[OwnerPin.TokenHeader]);
            return new OwnerStatus(settings.OwnerPinHash is not null, expires is not null, expires);
        });

        g.MapPost("/unlock", async (PinInput input, PosDbContext db, OwnerSessions sessions, ILogger<OwnerSessions> log) =>
        {
            if (sessions.Blocked() is { } wait)
            {
                var seconds = (int)Math.Ceiling(wait.TotalSeconds);
                return Results.Problem($"Too many wrong PINs. Try again in {seconds} seconds.", statusCode: StatusCodes.Status429TooManyRequests,
                    title: $"Too many wrong PINs. Try again in {seconds} seconds.");
            }
            var settings = await db.GetSettingsAsync();
            if (settings.OwnerPinHash is null) return ApiErrors.BadRequest("No owner PIN is set yet. Set one in Settings.");
            if (!OwnerPin.IsValidFormat(input.Pin) || !OwnerPin.Verify(input.Pin!, settings.OwnerPinHash))
            {
                sessions.RecordWrong();
                log.LogWarning("Wrong owner PIN entered");
                return ApiErrors.BadRequest("Wrong PIN. Try again.");
            }
            sessions.RecordRight();
            var (token, expires) = sessions.Open();
            log.LogInformation("Owner unlocked");
            return Results.Ok(new UnlockResult(token, expires));
        });

        g.MapPost("/lock", (HttpRequest request, OwnerSessions sessions) =>
        {
            sessions.Close(request.Headers[OwnerPin.TokenHeader]);
            return Results.NoContent();
        });

        // Set the PIN the first time (no PIN needed), or change it (only while unlocked). Returns an unlocked session.
        g.MapPut("/pin", async (PinInput input, HttpRequest request, PosDbContext db, OwnerSessions sessions, ILogger<OwnerSessions> log) =>
        {
            if (!OwnerPin.IsValidFormat(input.Pin))
            {
                return new Validation().Add("pin", "The PIN must be 4 to 6 digits.").Problem();
            }
            var settings = await db.GetSettingsAsync();
            var changed = settings.OwnerPinHash is not null;
            settings.OwnerPinHash = OwnerPin.Hash(input.Pin!);
            await db.SaveChangesAsync();
            sessions.CloseAll();
            log.LogInformation(changed ? "Owner PIN changed" : "Owner PIN set");
            var (token, expires) = sessions.Open();
            return Results.Ok(new UnlockResult(token, expires));
        }).RequireOwner();

        // Turn the PIN off (only while unlocked): everything works without a PIN again.
        g.MapDelete("/pin", async (PosDbContext db, OwnerSessions sessions, ILogger<OwnerSessions> log) =>
        {
            var settings = await db.GetSettingsAsync();
            settings.OwnerPinHash = null;
            await db.SaveChangesAsync();
            sessions.CloseAll();
            log.LogWarning("Owner PIN turned off");
            return Results.NoContent();
        }).RequireOwner();

        return api;
    }
}
