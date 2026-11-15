using FluentValidation;
using MediatR;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Exceptions;

namespace TelemetryHub.Application.Archives;

/// <summary>Opens one archived file for download.</summary>
public sealed record GetArchiveFileQuery(string Key) : IRequest<ArchiveContent>;

public sealed class GetArchiveFileQueryValidator : AbstractValidator<GetArchiveFileQuery>
{
    public GetArchiveFileQueryValidator()
    {
        // Only keys with the archive layout are accepted, so this endpoint cannot be used to
        // read arbitrary objects from the bucket.
        RuleFor(query => query.Key).Must(ArchiveKey.IsValid).WithMessage("Key is not a valid archive key.");
    }
}

public sealed class GetArchiveFileQueryHandler(IArchiveStorage storage) : IRequestHandler<GetArchiveFileQuery, ArchiveContent>
{
    public async Task<ArchiveContent> Handle(GetArchiveFileQuery request, CancellationToken cancellationToken) =>
        await storage.OpenReadAsync(request.Key, cancellationToken)
        ?? throw new NotFoundException("Archive file", request.Key);
}
