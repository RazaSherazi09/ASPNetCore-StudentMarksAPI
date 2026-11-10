using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MarksApi.Common;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problemDetails;

        if (exception is ApiProblemException apiEx)
        {
            problemDetails = new ProblemDetails
            {
                Status = apiEx.StatusCode,
                Title = apiEx.Title,
                Detail = apiEx.Detail,
                Instance = httpContext.Request.Path
            };
        }
        else if (exception is BadHttpRequestException || exception is JsonException)
        {
            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid request payload",
                Detail = "The request body could not be parsed or contained invalid data types.",
                Instance = httpContext.Request.Path
            };
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);

            problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = "An unexpected server error occurred. Please try again later.",
                Instance = httpContext.Request.Path
            };
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}

