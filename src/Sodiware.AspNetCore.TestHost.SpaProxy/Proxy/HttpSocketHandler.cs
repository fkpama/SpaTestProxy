using HttpMachine;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy
{
    internal sealed class HttpStreamHandler : HandlerBase
    {
        private readonly Stream stream;

        public HttpStreamHandler(Stream stream,
                                 HttpCombinedParser machine,
                                 ILogger<HttpStreamHandler> log)
            : base(machine, log)
        {
            this.stream = stream;
        }

        protected override ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return stream.ReadAsync(buffer, cancellationToken);
        }
    }

    /*
    internal sealed class HttpSocketHandler : HandlerBase
    {
        private readonly Socket socket;

        public HttpSocketHandler(Socket socket,
                                 HttpCombinedParser machine,
                                 ILogger<HttpSocketHandler> log)
            : base(machine, log)
        {
            this.socket = socket;
        }

        protected override ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
            => socket.ReceiveAsync(buffer, cancellationToken);
    }
    */
}