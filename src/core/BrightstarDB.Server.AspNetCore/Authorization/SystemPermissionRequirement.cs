#nullable enable

using System;
using System.Threading.Tasks;
using BrightstarDB.Server.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BrightstarDB.Server.AspNetCore.Authorization;

public sealed record SystemPermissionRequirement(SystemPermissions RequiredPermission);

public sealed class SystemPermissionFilter(SystemPermissions requiredPermission) : IEndpointFilter
{
    public SystemPermissions RequiredPermission { get; } = requiredPermission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Challenge(authenticationSchemes: new[] { BasicAuthenticationHandler.AuthenticationScheme });
        }

        var permissionsProvider = httpContext.RequestServices.GetRequiredService<AbstractSystemPermissionsProvider>();
        if (!permissionsProvider.HasPermissions(httpContext.User, RequiredPermission))
        {
            return Results.Forbid();
        }

        return await next(context);
    }
}

public static class SystemPermissionEndpointConventionBuilderExtensions
{
    public static TBuilder AddSystemPermissionFilter<TBuilder>(this TBuilder builder, SystemPermissions requiredPermission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (requiredPermission == SystemPermissions.None)
        {
            return builder;
        }

        var filter = new SystemPermissionFilter(requiredPermission);

        builder.WithMetadata(new SystemPermissionRequirement(requiredPermission));
        builder.AddEndpointFilterFactory((_, next) => invocationContext => filter.InvokeAsync(invocationContext, next));

        return builder;
    }
}
