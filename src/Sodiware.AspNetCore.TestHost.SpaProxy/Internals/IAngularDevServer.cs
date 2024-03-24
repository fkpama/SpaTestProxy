namespace Sodiware.AspNetCore.TestHost.SpaProxy.Internals
{
    public interface IAngularDevServer
    {
        Task WaitAsync(CancellationToken cancellationToken);
    }
}