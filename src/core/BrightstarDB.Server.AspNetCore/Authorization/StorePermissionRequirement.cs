#nullable enable

using System;
using System.Threading.Tasks;
using BrightstarDB.Server.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BrightstarDB.Server.AspNetCore.Authorization;

public sealed record StorePermissionRequirement(StorePermissions RequiredPermission);

public sealed class StorePermissionFilter(StorePermissions requiredPermission) : IEndpointFilter
{
    public StorePermissions RequiredPermission { get; } = requiredPermission;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return Results.Challenge(authenticationSchemes: new[] { BasicAuthenticationHandler.AuthenticationScheme });
        }

        if (!TryGetStoreName(httpContext, out var storeName))
        {
            throw new InvalidOperationException("StorePermissionFilter requires a route value named 'storeName'.");
        }

        var permissionsProvider = httpContext.RequestServices.GetRequiredService<AbstractStorePermissionsProvider>();
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, RequiredPermission))
        {
            return Results.Forbid();
        }

        return await next(context);
    }

    private static bool TryGetStoreName(HttpContext httpContext, out string storeName)
    {
        if (httpContext.Request.RouteValues.TryGetValue("storeName", out var storeNameValue) && storeNameValue is not null)
        {
            var candidate = storeNameValue.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                storeName = candidate;
                return true;
            }
        }

        storeName = string.Empty;
        return false;
    }
}

public static class StorePermissionEndpointConventionBuilderExtensions
{
    public static TBuilder AddStorePermissionFilter<TBuilder>(this TBuilder builder, StorePermissions requiredPermission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (requiredPermission == StorePermissions.None)
        {
            return builder;
        }

        var filter = new StorePermissionFilter(requiredPermission);

        builder.WithMetadata(new StorePermissionRequirement(requiredPermission));
        builder.AddEndpointFilterFactory((_, next) => invocationContext => filter.InvokeAsync(invocationContext, next));

        return builder;
    }
}
