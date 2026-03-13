#nullable enable
using BrightstarDB.Server.AspNetCore.Configuration;
using Microsoft.AspNetCore.Authentication;

namespace BrightstarDB.Server.AspNetCore.Authentication
{
    public interface IAuthenticationProvider
    {
        void Configure(AuthenticationBuilder authenticationBuilder, AuthenticationConfiguration configuration);
    }
}
