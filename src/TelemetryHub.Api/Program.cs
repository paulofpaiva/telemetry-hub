var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { service = "telemetry-hub", status = "running" }));

app.Run();

public partial class Program;
