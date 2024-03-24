using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy;

internal partial class ProxyServer
{
    internal class TcpListenerEntry : IDisposable
    {
        private ILogger log;
        private IWebApplicationFactory? testServer;
        private bool disposed;

        public ProxyServer Server { get; }
        public TcpListener Listener { get; }
        public X509Certificate2? Certificate { get; }
        //public HttpStreamHandler? Handler { get; private set; }
        private readonly List<HttpStreamHandler> handlers = [];
        public bool CanAcceptConnection
        {
            get
            {
                return this.Server.TestServer is not null;
            }
        }

        internal string? BaseUrl
        {
            get
            {
                if (this.testServer is null)
                    return null;
                var options = testServer.Services.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value; ;
                var url = options.GetServerUrl();
                return url;
            }
        }

        public TcpListenerEntry(ProxyServer server,
                                TcpListener listener,
                                X509Certificate2? certificate,
                                ILogger log)
        {
            Server = server;
            Listener = listener;
            Certificate = certificate;
            this.log = log;
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Listener.Start();
            if (Certificate is not null)
            {
                Listener.BeginAcceptSocket(onSslSocketAccepted, this);
            }
            else
            {
                Listener.BeginAcceptSocket(onSocketAccepted, this);
            }
        }
        private void onSocketAccepted(IAsyncResult ar)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            try
            {
                if (acceptSocket(ar, out var socket))
                {
                    var stream = new NetworkStream(socket);
                    stream.ReadTimeout = 2000;
                    createHandler(stream);
                }
            }
            finally
            {
                if (!disposed)
                    Listener.BeginAcceptSocket(onSocketAccepted, null);
            }
        }

        private void onSslSocketAccepted(IAsyncResult ar)
        {
            Debug.Assert(Certificate is not null);
            try
            {
                if (!this.CanAcceptConnection)
                {
                    throw new InvalidOperationException();
                }
                if (acceptSocket(ar, out var socket))
                {
                    var stream = new NetworkStream(socket, true)
                    {
                        ReadTimeout = 2000
                    };
                    var sslStream = new SslStream(stream, false);
                    sslStream.AuthenticateAsServer(Certificate);
                    createHandler(sslStream);
                }
            }
            finally
            {
                if (!disposed)
                    Listener.BeginAcceptSocket(onSslSocketAccepted, null);
            }
        }

        private void closeSocket(Socket socket)
        {
            socket.Close();
            socket.Dispose();
        }

        private bool acceptSocket(IAsyncResult ar, [NotNullWhen(true)] out Socket? socket)
        {
            try
            {
                socket = Listener.EndAcceptSocket(ar);
            }
            catch(SocketException ex)
            when(ex.SocketErrorCode == SocketError.OperationAborted)
            {
                socket = null;
                return false;
            }
            catch (ObjectDisposedException)
            {
                socket = null;
                return false;
            }
            if (socket is null)
            {
                return false;
            }
            if (disposed)
            {
                closeSocket(socket);
                return false;
            }
            Console.WriteLine("Connection received");
            return true;
        }

        private void createHandler(Stream stream)
        {
            Debug.Assert(this.Server.TestServer is not null);
            var writer = new HttpResponseMessageSocketWriter(stream);
            var parser = new HttpParser(Server.TestServer,
                                        writer,
                                        this.BaseUrl,
                                        Server.loggerFactory.CreateLogger<HttpParser>());
            var machine = new HttpMachine.HttpCombinedParser(parser);
            var handler = new HttpStreamHandler(stream, machine, Server.loggerFactory.CreateLogger<HttpStreamHandler>());
            handler.Start();
        }


        public void Dispose()
        {
            this.disposed = true;
            Listener.Stop();
            Listener.Dispose();
            GC.SuppressFinalize(this);
        }

        internal void Setup(IWebApplicationFactory testServer, ILoggerFactory loggerFactory)
        {
            this.log = loggerFactory.CreateLogger<TcpListenerEntry>();
            this.testServer = testServer;
        }

        internal void Cleanup()
        {
            this.handlers.Clear();
        }
    }
}