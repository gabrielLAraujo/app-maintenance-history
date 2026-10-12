using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Garage.Api.Auth;
using Garage.Api.Cli;
using Garage.Api.Endpoints;
using Garage.Api.Http;
using Garage.Application;
using Garage.Infrastructure;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
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

// A API roda atrás do proxy reverso (Traefik no Coolify), que termina o TLS e é o único a alcançá-la:
// confia nos cabeçalhos dele para o esquema (cookies Secure) e o IP do cliente (rate limit).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuth();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiting();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<GarageDbContext>();

var app = builder.Build();

// Comando administrativo: roda e sai, sem subir o servidor.
if (args is ["reset-password", var email])
{
    Environment.ExitCode = await ResetPasswordCli.RunAsync(app.Services, email, Console.Out);
    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSpaStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseRateLimiter();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

var api = app.MapGroup("/api").RequireAuthorization();
api.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
api.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapEndpoints(api);
app.MapSpaFallback();

app.Run();

public partial class Program;
