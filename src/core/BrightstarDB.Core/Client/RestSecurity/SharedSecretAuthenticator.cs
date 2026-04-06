#if !WINDOWS_PHONE
using System.Net;

namespace BrightstarDB.Client.RestSecurity
{
    /// <summary>
    /// A request authenticator that uses a shared secret key to sign outgoing requests
    /// </summary>
    public class SharedSecretAuthenticator(string accountId, string authenticationKey) : IRequestAuthenticator
    {
        /// <summary>
        /// Invoked by the REST client framework to add authentication information to an outgoing request
        /// </summary>
        /// <param name="request">The request to be updated with authentication information</param>
        public void Authenticate(HttpWebRequest request)
        {
#if NETSTANDARD16
            request.Headers[HttpRequestHeader.Authorization] =
                "SharedKey " + accountId + ":" +
                RestClientHelper.GenerateSignature(request, SignatureType.SharedKey, authenticationKey);
#else
            request.Headers.Add(HttpRequestHeader.Authorization,
              "SharedKey " + accountId + ":" + RestClientHelper.GenerateSignature(request, SignatureType.SharedKey, authenticationKey));
#endif
        }
    }
}
#endif