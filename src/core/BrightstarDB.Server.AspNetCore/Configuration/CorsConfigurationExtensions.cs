#nullable enable

using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BrightstarDB.Server.AspNetCore.Configuration;

public static class CorsConfigurationExtensions
{
    public static IServiceCollection AddBrightstarCors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<BrightstarServiceConfiguration>>((options, serviceConfiguration) =>
            {
                var corsConfiguration = serviceConfiguration.Value.Cors;
                if (corsConfiguration.DisableCors)
                {
                    return;
                }

                options.AddDefaultPolicy(policy => ApplyPolicy(policy, corsConfiguration));
            });

        return services;
    }

    public static IApplicationBuilder UseBrightstarCors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var corsConfiguration = app.ApplicationServices
            .GetRequiredService<IOptions<BrightstarServiceConfiguration>>()
            .Value
            .Cors;

        if (!corsConfiguration.DisableCors)
        {
            app.UseCors();
        }

        return app;
    }

    private static void ApplyPolicy(CorsPolicyBuilder policy, CorsConfiguration configuration)
    {
        var allowedOrigins = configuration.AllowedOrigins
            .Where(static origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuration.AllowCredentials)
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowCredentials();
            }
            else if (!string.IsNullOrWhiteSpace(configuration.AllowOrigin) &&
                     !string.Equals(configuration.AllowOrigin, "*", StringComparison.Ordinal))
            {
                policy.WithOrigins(configuration.AllowOrigin).AllowCredentials();
            }
            else
            {
                policy.SetIsOriginAllowed(static _ => true).AllowCredentials();
            }
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else if (!string.IsNullOrWhiteSpace(configuration.AllowOrigin) &&
                 !string.Equals(configuration.AllowOrigin, "*", StringComparison.Ordinal))
        {
            policy.WithOrigins(configuration.AllowOrigin);
        }
        else
        {
            policy.AllowAnyOrigin();
        }

        var allowedHeaders = configuration.AllowedHeaders
            .Where(static header => !string.IsNullOrWhiteSpace(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedHeaders.Length > 0)
        {
            policy.WithHeaders(allowedHeaders);
        }
        else
        {
            policy.AllowAnyHeader();
        }

        var allowedMethods = configuration.AllowedMethods
            .Where(static method => !string.IsNullOrWhiteSpace(method))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowedMethods.Length > 0)
        {
            policy.WithMethods(allowedMethods);
        }
        else
        {
            policy.AllowAnyMethod();
        }

        var exposedHeaders = configuration.ExposedHeaders
            .Where(static header => !string.IsNullOrWhiteSpace(header))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (exposedHeaders.Length > 0)
        {
            policy.WithExposedHeaders(exposedHeaders);
        }
    }
}
