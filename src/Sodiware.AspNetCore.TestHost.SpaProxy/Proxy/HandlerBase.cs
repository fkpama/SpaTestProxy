using System.Buffers;
using HttpMachine;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy
{
    internal abstract class HandlerBase
    {
        private readonly byte[] buffer;
        static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Shared;
        private readonly HttpCombinedParser machine;
        private protected readonly ILogger log;
        private bool closed;

        protected HandlerBase(HttpCombinedParser machine, ILogger log)
        {
            this.machine = machine;
            this.log = log;
            buffer = s_pool.Rent(8092);
        }
        public void Start()
        {
            var cancellationToken = CancellationToken.None;
            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var len = await ReadAsync(buffer, cancellationToken);
                        if (len == 0)
                        {
                            log.LogInformation("Socket closed");
                            break;
                        }
                        process(new(buffer, 0, len));
                    }
                    catch (IOException)
                    {
                        this.closed = true;
                        return;
                    }
                    finally
                    {
                        s_pool.Return(buffer);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        protected abstract ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);

        private void process(ArraySegment<byte> span)
        {
            if (!this.closed)
                machine.Execute(span);
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