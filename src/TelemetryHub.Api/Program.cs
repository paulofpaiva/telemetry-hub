using TelemetryHub.Application;
using TelemetryHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "telemetry-hub", status = "running" }));

app.Run();

public partial class Program;
