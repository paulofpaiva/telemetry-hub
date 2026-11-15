using FluentValidation;
using MediatR;

namespace TelemetryHub.Application.Behaviors;

/// <summary>Runs every registered validator for the request before the handler executes.</summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        List<FluentValidation.Results.ValidationFailure>? failures = null;

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                (failures ??= []).AddRange(result.Errors);
            }
        }

        if (failures is not null)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
