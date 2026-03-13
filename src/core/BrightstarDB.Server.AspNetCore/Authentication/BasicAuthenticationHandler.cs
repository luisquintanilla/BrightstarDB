#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using BrightstarDB.Server.AspNetCore.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BrightstarDB.Server.AspNetCore.Authentication;

public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "Basic";
    private const string ClaimType = "brightstar:claim";

    private readonly IOptionsMonitor<BrightstarServiceConfiguration> _serviceConfiguration;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<BrightstarServiceConfiguration> serviceConfiguration)
        : base(options, logger, encoder)
    {
        _serviceConfiguration = serviceConfiguration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(headerValues.ToString(), out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header."));
        }

        if (!string.Equals(headerValue.Scheme, AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (string.IsNullOrWhiteSpace(headerValue.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Basic authentication credentials."));
        }

        if (!TryReadCredentials(headerValue.Parameter, out var username, out var password))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic authentication credentials."));
        }

        if (!TryValidateCredentials(username, password, out var configuredUser))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, configuredUser.Username),
            new(ClaimTypes.NameIdentifier, configuredUser.Username)
        };

        foreach (var configuredClaim in configuredUser.Claims.Where(static claim => !string.IsNullOrWhiteSpace(claim)))
        {
            claims.Add(new Claim(ClaimType, configuredClaim));
        }

        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers[HeaderNames.WWWAuthenticate] = $"Basic realm=\"{EscapeRealm(GetRealm())}\"";
        return Task.CompletedTask;
    }

    private string GetRealm()
    {
        var realm = _serviceConfiguration.CurrentValue.Authentication.Realm;
        return string.IsNullOrWhiteSpace(realm) ? "BrightstarDB" : realm;
    }

    private bool TryValidateCredentials(string username, string password, out BasicAuthenticationUser configuredUser)
    {
        foreach (var user in _serviceConfiguration.CurrentValue.Authentication.Users)
        {
            if (!string.Equals(user.Username, username, StringComparison.Ordinal))
            {
                continue;
            }

            if (PasswordsMatch(user.Password, password))
            {
                configuredUser = user;
                return true;
            }

            break;
        }

        configuredUser = new BasicAuthenticationUser();
        return false;
    }

    private static bool PasswordsMatch(string expectedPassword, string providedPassword)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);
        var providedBytes = Encoding.UTF8.GetBytes(providedPassword);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static bool TryReadCredentials(string encodedCredentials, out string username, out string password)
    {
        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
            var separatorIndex = credentials.IndexOf(':');
            if (separatorIndex <= 0)
            {
                username = string.Empty;
                password = string.Empty;
                return false;
            }

            username = credentials[..separatorIndex];
            password = credentials[(separatorIndex + 1)..];
            return true;
        }
        catch (FormatException)
        {
            username = string.Empty;
            password = string.Empty;
            return false;
        }
    }

    private static string EscapeRealm(string realm)
    {
        return realm
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
