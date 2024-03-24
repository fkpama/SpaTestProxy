using Sodiware;
using Sodiware.AspNetCore.TestHost.SpaProxy.Internals;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Client
{
    internal class SpaDelegatingHandler : DelegatingHandler
    {
        private AngularDevServer angularDevServer;
        private readonly SpaDevelopmentServerOptions options;
        private readonly HttpClient inner;

        public SpaDelegatingHandler(AngularDevServer angularDevServer,
                                    SpaDevelopmentServerOptions options,
                                    HttpClient inner)
            : base(new HttpClientHandler())

        {
            this.angularDevServer = angularDevServer;
            this.options = options;
            this.inner = inner;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (isForSpa(request))
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            var req2 = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var hdr in request.Headers)
            {
                req2.Headers.TryAddWithoutValidation(hdr.Key, hdr.Value);
            }
            var response = await inner.SendAsync(req2, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode
                && response.StatusCode == System.Net.HttpStatusCode.Found
                && response.Headers.Location is not null
                && isForSpa(response.Headers.Location))
            {
                var req = new HttpRequestMessage(HttpMethod.Get, response.Headers.Location.ToString());
                // TODO: Cookies
                response = await base.SendAsync(req, cancellationToken).ConfigureAwait(false);
            }
            return response;
        }

        private bool isForSpa(HttpRequestMessage request)
            => request.RequestUri is not null && isForSpa(request.RequestUri);
        private bool isForSpa(Uri requestUri)
        {
            var serverUrl = new Uri(options.ServerUrl);
            if (string.Equals(requestUri.Host, serverUrl.Host, StringComparison.OrdinalIgnoreCase)
                && requestUri.Port == serverUrl.Port)
            {
                return true;
            }
            return false;
        }
    }
}