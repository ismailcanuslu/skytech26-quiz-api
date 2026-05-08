using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Backend.Api.Middlewares;

public sealed class GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception caught by middleware.");

            var statusCode = exception is DbUpdateConcurrencyException
                ? HttpStatusCode.Conflict
                : HttpStatusCode.InternalServerError;

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new
            {
                title = statusCode == HttpStatusCode.Conflict
                    ? "Conflict"
                    : "Internal Server Error",
                status = context.Response.StatusCode,
                detail = statusCode == HttpStatusCode.Conflict
                    ? "The resource was modified by another request. Please refresh and retry."
                    : "An unexpected error occurred."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonSerializerOptions));
        }
    }
}
