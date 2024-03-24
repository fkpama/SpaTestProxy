using System.Diagnostics;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Internals
{
    internal sealed class AngularDevServer : IAngularDevServer
    {
        private readonly TaskCompletionSource ServerStartedSource = new();
        internal event EventHandler? ShuttingDown;

        public Task ServerStarted => this.ServerStartedSource.Task;

        public Process? Process { get; set; }
        public bool ShutdownRequested { get; private set; }

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ServerStartedSource.Task.WaitAsync(TimeSpan.FromSeconds(300), cancellationToken);
            }
            catch (Exception ex)
            when (ex.Message.Contains("already in use", StringComparison.OrdinalIgnoreCase))
            { }

        }

        internal void SetStarted()
        {
            ServerStartedSource.SetResult();
        }

        internal void Shutdown()
        {
            this.ShuttingDown?.Invoke(this);
            ShutdownRequested = true;
            Process?.Kill(true);
        }

        internal bool SetException(Exception exception)
        {
            return this.ServerStartedSource.TrySetException(exception);
        }
    }
}
