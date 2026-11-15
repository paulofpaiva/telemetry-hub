using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TelemetryHub.Application.Exceptions;

namespace TelemetryHub.Api.Infrastructure;

/// <summary>Translates application exceptions into RFC 9457 problem details responses.</summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problemDetails = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(
                validation.Errors
                    .GroupBy(failure => failure.PropertyName)
                    .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = exception.Message,
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = exception.Message,
            },
            _ => null,
        };

        if (problemDetails is null)
        {
            // Unknown errors fall through to the default handler, which returns a generic 500.
            return false;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception,
        });
    }
}
