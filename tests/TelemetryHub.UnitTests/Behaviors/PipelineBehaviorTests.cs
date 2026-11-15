using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using TelemetryHub.Application;
using TelemetryHub.Application.Abstractions;
using TelemetryHub.Application.Behaviors;
using TelemetryHub.Application.Readings.IngestReading;
using TelemetryHub.Domain.Readings;
using TelemetryHub.UnitTests.Fakes;

namespace TelemetryHub.UnitTests.Behaviors;

public class PipelineBehaviorTests
{
    private sealed record Ping(string Message) : IRequest<string>;

    private sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(ping => ping.Message).NotEmpty();
    }

    [Fact]
    public async Task Validation_stops_the_pipeline_before_the_handler_runs()
    {
        var behavior = new ValidationBehavior<Ping, string>([new PingValidator()]);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new Ping(string.Empty),
            _ =>
            {
                handlerCalled = true;
                return Task.FromResult("pong");
            },
            CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.Equal(nameof(Ping.Message), Assert.Single(exception.Errors).PropertyName);
    }

    [Fact]
    public async Task Validation_lets_valid_requests_and_requests_without_validators_through()
    {
        var withValidator = new ValidationBehavior<Ping, string>([new PingValidator()]);
        var withoutValidators = new ValidationBehavior<Ping, string>([]);

        Assert.Equal("pong", await withValidator.Handle(new Ping("hi"), _ => Task.FromResult("pong"), CancellationToken.None));
        Assert.Equal("pong", await withoutValidators.Handle(new Ping(string.Empty), _ => Task.FromResult("pong"), CancellationToken.None));
    }

    [Fact]
    public async Task Logging_returns_the_response_and_rethrows_failures()
    {
        var behavior = new LoggingBehavior<Ping, string>(NullLogger<LoggingBehavior<Ping, string>>.Instance);

        Assert.Equal("pong", await behavior.Handle(new Ping("hi"), _ => Task.FromResult("pong"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new Ping("hi"),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Registered_pipeline_validates_commands_sent_through_mediatr()
    {
        var writer = new FakeReadingWriter();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton<IReadingWriter>(writer);
        services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 1, 31, 12, 0, 0, TimeSpan.Zero))));
        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var timestamp = new DateTimeOffset(2026, 1, 31, 11, 59, 0, TimeSpan.Zero);

        await sender.Send(new IngestReadingCommand("pump-001", SensorType.Pressure, 8.2, "bar", timestamp));
        await Assert.ThrowsAsync<ValidationException>(() =>
            sender.Send(new IngestReadingCommand("NOT VALID", SensorType.Pressure, double.NaN, "bar", timestamp)));

        Assert.Single(writer.Written);
    }
}
