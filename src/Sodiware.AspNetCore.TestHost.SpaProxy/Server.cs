using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sodiware.AspNetCore.TestHost.SpaProxy.Internals;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    internal interface IWebApplicationFactory
    {
        IServiceProvider Services { get; }
        HttpClient CreateClient();
        HttpClient CreateClient(WebApplicationFactoryClientOptions options);
        void Shutdown();
        Uri[] ServerUrls { get; }
    }

    public sealed class SpaTestServer<TProgram> : Server
        where TProgram : class
    {
        public SpaTestServer(SpaServerOptions options)
            : base(new Factory<TProgram>(Utils.GetOptions(typeof(TProgram), options)))
        {

        }
        public SpaTestServer()
            : this(Utils.GetOptions(typeof(TProgram)))
        {
        }
    }
    public abstract class Server : IDisposable
    {
        private HttpClient? client;
        private readonly IWebApplicationFactory builder;

        internal SpaDevelopmentServerOptions SpaDevelopmentServerOptions
        {
            get => builder.Services.GetRequiredService<IOptions<SpaDevelopmentServerOptions>>().Value;
        }

        public IServiceProvider Services
        {
            get => this.builder.Services;
        }

        public HttpClient Client
        {
            get
            {
                if (client is null)
                {
                    var devServer = this.builder.Services.GetRequiredService<AngularDevServer>();
                    //var inner = builder.CreateDefaultClient(new SpaDelegatingHandler(devServer, this.SpaDevelopmentServerOptions, this.builder.CreateClient()));
                    var inner = builder.CreateClient(new WebApplicationFactoryClientOptions
                    {
                        AllowAutoRedirect = true,
                        HandleCookies = true,
                    });
                    //builder.se

                    //var handler = new SpaDelegatingHandler(devServer, this.SpaDevelopmentServerOptions, inner);
                    //var result = new HttpClient(handler)
                    //{
                    //    BaseAddress = this.builder.ClientOptions.BaseAddress,
                    //};
                    client = inner;
                }
                return client;
            }
        }

        public string SpaUrl
        {
            get => this.SpaDevelopmentServerOptions.ServerUrl;
        }

        private protected Server(IWebApplicationFactory builder)
        {
            this.builder = builder;
        }

        //public static async Task<Server> OpenAsync(CancellationToken cancellationToken)
        //{
        //    //Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", "Microsoft.AspNetCore.SpaProxy");
        //    var builder = new Factory();
        //    return new Server(builder);
        //}

        public Task WaitForNgAsync(CancellationToken cancellationToken)
        {
            var svc = this.builder.Services.GetRequiredService<IAngularDevServer>();
            return svc.WaitAsync(cancellationToken);
        }

        public void Dispose()
        {
            this.Shutdown();
        }

        public void Shutdown()
        {
            this.builder.Shutdown();
        }

        public object GetService(Type type)
            => this.builder.Services.GetRequiredService(type);
        public T GetService<T>()
            where T : class
            => this.builder.Services.GetRequiredService<T>();
    }
}