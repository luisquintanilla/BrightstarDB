#nullable enable

using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authentication;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Configuration;
using BrightstarDB.Server.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Enable running as a Windows Service
builder.Host.UseWindowsService();

// Bind configuration
builder.Services
    .AddOptions<BrightstarServiceConfiguration>()
    .Bind(builder.Configuration.GetSection(BrightstarServiceConfiguration.SectionName));

// Register IBrightstarService from configuration
builder.Services.AddSingleton<IBrightstarService>(sp =>
{
    var config = sp.GetRequiredService<IOptions<BrightstarServiceConfiguration>>().Value;
    return string.IsNullOrWhiteSpace(config.ConnectionString)
        ? BrightstarService.GetClient()
        : BrightstarService.GetClient(config.ConnectionString);
});

// Authentication
builder.Services
    .AddAuthentication(BasicAuthenticationHandler.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
        BasicAuthenticationHandler.AuthenticationScheme,
        _ => { });

builder.Services.AddAuthorization();
builder.Services.AddBrightstarCors();
builder.Services.AddOpenApi();
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.NoCache());
    options.AddPolicy("store-reads", policy =>
        policy.Expire(TimeSpan.FromSeconds(10)).Tag("stores"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("sparql", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, BrightstarDB.Server.AspNetCore.BrightstarJsonContext.Default);
});

// Permission providers (fallback to full access when no auth configured)
builder.Services.AddSingleton<AbstractStorePermissionsProvider>(
    new FallbackStorePermissionsProvider(StorePermissions.All));
builder.Services.AddSingleton<AbstractSystemPermissionsProvider>(
    new FallbackSystemPermissionsProvider(SystemPermissions.All));

var app = builder.Build();

app.UseBrightstarCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();
app.MapOpenApi();

// Map all API endpoints
app.MapStoresEndpoints();
app.MapStoreEndpoints();
app.MapCommitPointsEndpoints();
app.MapTransactionsEndpoints();
app.MapStatisticsEndpoints();
app.MapJobsEndpoints();
app.MapSparqlEndpoints();
app.MapSparqlUpdateEndpoints();
app.MapGraphsEndpoints();

app.Run();

// Enables WebApplicationFactory<Program> in tests
public partial class Program;
