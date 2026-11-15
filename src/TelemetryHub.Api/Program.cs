using Scalar.AspNetCore;
using TelemetryHub.Api.Endpoints;
using TelemetryHub.Api.Infrastructure;
using TelemetryHub.Application;
using TelemetryHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options.WithTitle("Telemetry Hub API"));

app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();
app.MapHealthEndpoints();
app.MapDeviceEndpoints();
app.MapReadingEndpoints();

app.Run();

public partial class Program;
