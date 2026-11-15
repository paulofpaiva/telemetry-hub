namespace TelemetryHub.Application.Exceptions;

/// <summary>The requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException(string resource, string key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>The request conflicts with existing state. Maps to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
