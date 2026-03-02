using FluentValidation;
using System.Text.Json;

namespace CaptionGenerator.API.Middleware;

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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new { message = string.Empty, errors = new Dictionary<string, string[]>() };

        switch (exception)
        {
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                var errors = validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                response = new { message = "Validation failed", errors };
                break;
            case ArgumentNullException:
            case ArgumentException:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response = new { message = exception.Message, errors = new Dictionary<string, string[]>() };
                break;
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response = new { message = "An internal server error occurred", errors = new Dictionary<string, string[]>() };
                break;
        }

        return context.Response.WriteAsJsonAsync(response);
    }
}

