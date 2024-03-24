using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy
{
    internal record SocketSpec(IPAddress address, int port, X509Certificate2? certificate);
    internal partial class ProxyServer : IDisposable
    {
        private ILogger log;
        private IWebApplicationFactory? testServer;
        private ILoggerFactory loggerFactory;
        private bool isStarted;
        private readonly List<HandlerBase> handlers = [];
        private readonly List<TcpListenerEntry> listeners = [];

        private IWebApplicationFactory? TestServer => this.testServer;

        public ProxyServer(IWebApplicationFactory? testServer,
                           SocketSpec[] sockets,
                           ILoggerFactory? loggerFactory = null)
        {
            //this.factory = factory;
            this.testServer = testServer;
            this.loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            log = this.loggerFactory.CreateLogger<ProxyServer>();
            foreach (var socket in sockets)
            {
                var listener = new TcpListener(socket.address, socket.port);
                listeners.Add(new(this, listener, socket.certificate, log));
            }
        }

        public void Start()
        {
            listeners.ForEach(x => x.Start());
            this.isStarted = true;
            //this.thread.Start();
        }

        public void Dispose()
        {
            listeners.ForEach(x => x.Dispose());
            listeners.Clear();
            GC.SuppressFinalize(this);
        }

        internal void Setup(IWebApplicationFactory testServer, ILoggerFactory? loggerFactory)
        {
            if (this.testServer is not null)
            {
                if (this.testServer != testServer)
                    throw new InvalidOperationException();
            }


            this.loggerFactory = loggerFactory.Safe();
            this.testServer = testServer;
            this.listeners.ForEach(x => x.Setup(testServer, this.loggerFactory));
            if (!this.isStarted)
            {
                this.Start();
            }
        }

        internal void Cleanup(IWebApplicationFactory testServer)
        {
            if (testServer != this.testServer)
            {
                if (this.testServer is not null)
                    throw new InvalidOperationException();
            }
            this.testServer = null;
        }
    }
}