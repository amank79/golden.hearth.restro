using Microsoft.AspNetCore.Diagnostics;

namespace RestaurantPos.Api.Features;

/// <summary>
/// A business rule stopped the request, e.g. "This bill is already printed". Turned into a problem response with
/// the message as the title, so the screen can show it as-is. Default status 409 Conflict.
/// </summary>
public sealed class RuleException(string message, int status = StatusCodes.Status409Conflict) : Exception(message)
{
    public int Status { get; } = status;

    public static RuleException NotFound(string what) => new($"{what} not found.", StatusCodes.Status404NotFound);

    public static RuleException Invalid(string message) => new(message, StatusCodes.Status400BadRequest);
}

public sealed class RuleExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not RuleException rule) return false;
        context.Response.StatusCode = rule.Status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = rule.Status, Title = rule.Message, Detail = rule.Message },
        });
    }
}
