using System.Net;
using System.Text.Json;

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

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                title = "Internal Server Error",
                status = context.Response.StatusCode,
                detail = "An unexpected error occurred."
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonSerializerOptions));
        }
    }
}
