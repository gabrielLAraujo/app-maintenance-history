using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application;
using Garage.Infrastructure;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<GarageDbContext>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // Ate o front existir (Plano 2), a raiz leva para a documentacao da API.
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
}

var api = app.MapGroup("/api").RequireAuthorization();
api.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
api.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapEndpoints(api);

app.Run();

public partial class Program;
