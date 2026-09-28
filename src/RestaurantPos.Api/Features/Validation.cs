namespace RestaurantPos.Api.Features;

/// <summary>Collects field errors and turns them into a 400 ValidationProblem response.</summary>
public sealed class Validation
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public Validation Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
        list.Add(message);
        return this;
    }

    public Validation Check(bool ok, string field, string message) => ok ? this : Add(field, message);

    public Validation Required(string? value, string field, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return Add(field, $"{label} is required.");
        return MaxLength(value, field, label, maxLength);
    }

    public Validation MaxLength(string? value, string field, string label, int maxLength) =>
        Check(value is null || value.Trim().Length <= maxLength, field, $"{label} can have at most {maxLength} characters.");

    public IResult Problem() =>
        Results.ValidationProblem(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
}

public static class ApiErrors
{
    /// <summary>409 with a message the screen can show as-is.</summary>
    public static IResult Conflict(string message) => Results.Problem(message, statusCode: StatusCodes.Status409Conflict, title: message);

    public static IResult NotFound(string what) => Results.Problem($"{what} not found.", statusCode: StatusCodes.Status404NotFound, title: $"{what} not found.");

    public static IResult BadRequest(string message) => Results.Problem(message, statusCode: StatusCodes.Status400BadRequest, title: message);

    /// <summary>Trims and turns empty text into null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
