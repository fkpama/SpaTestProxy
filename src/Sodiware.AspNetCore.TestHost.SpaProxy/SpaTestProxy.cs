

using Sodiware.AspNetCore.TestHost.SpaProxy.Proxy;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    public static class SpaTestProxy
    {
        internal static ProxyServer? Proxy { get; private set; }

        [MemberNotNullWhen(true, nameof(Proxy))]
        public static bool HasGlobalProxy => Proxy is not null;

        public static Task StartAsync(int port, bool useSsl, CancellationToken cancellationToken = default)
            => StartAsync(new Uri($"{Utils.GetHttpUriScheme(useSsl)}://localhost{Utils.GetPort(port, useSsl)}"));
        public static Task StartAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (Proxy is not null)
                throw new InvalidOperationException();
            Proxy = new(null, getSocketSpecs(uri));
            return Task.CompletedTask;
        }

        public static Task StopApplicationAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        internal static ProxyServer CreateProxy(Uri url, IWebApplicationFactory? testServer = null)
        {
            var lst = getSocketSpecs(url);
            var tcpServer = new ProxyServer(testServer, sockets: lst, testServer?.Services.GetService<ILoggerFactory>());
            return tcpServer;
        }

        private static SocketSpec[] getSocketSpecs(Uri url)
        {
            var lst = new List<SocketSpec?>();
            var host = url.Host;
            if (host.EqualsOrdI("localhost"))
            {
                lst.Add(createSpec(url, IPAddress.Loopback));
                lst.Add(createSpec(url, IPAddress.IPv6Loopback));
            }
            else
            {
                foreach (var entry in Dns.GetHostEntry(url.Host).AddressList)
                {
                    lst.Add(createSpec(url, entry));
                }
            }
            return lst.Where(x => x is not null).ToArray()!;
            static SocketSpec? createSpec(Uri url, IPAddress loopback)
            {
                if (url.Scheme.EqualsOrdI(Uri.UriSchemeHttps))
                {
                    var cert = CertificateUtil.GetServerCertificate(url.Host);
                    if (cert is not null)
                    {
                        return new(loopback, url.Port, cert);
                    }
                    else
                    {
                        // TODO: Log
                    }
                }
                else if (url.Scheme.Equals(Uri.UriSchemeHttp))
                {
                    return new(loopback, url.Port, null);
                }
                // TODO: Log
                return null;
            }

        }
    }
}
