using System.Text.Json;

namespace DanceManager.Api.Auth;

/// <summary>
/// Catches unhandled exceptions and returns a clean JSON 500, logging the full
/// stack trace server-side. Must be registered immediately AFTER
/// <c>UseCors</c>: CORS sets its response headers on the way in, and this
/// middleware writes the error body WITHOUT clearing the response, so those
/// headers survive. Otherwise an unhandled 500 reaches Kestrel, which resets the
/// response and drops the CORS header — making a plain server error look like a
/// CORS failure in the browser (which is exactly what masked an attendance-save
/// bug once).
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // If the response already started we can't change it — the CORS header
            // and status are already on the wire; just let it surface.
            if (context.Response.HasStarted) throw;

            // NOTE: do NOT call Response.Clear() — that would drop the CORS header
            // UseCors set upstream. Set status + body only.
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "An unexpected error occurred. Please try again.",
            }));
        }
    }
}
