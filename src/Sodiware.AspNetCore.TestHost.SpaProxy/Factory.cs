using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sodiware.AspNetCore.TestHost.SpaProxy.Internals;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    sealed class Factory<TProgram> : WebApplicationFactory<TProgram>, ITestServerAccessor, IWebApplicationFactory
        where TProgram : class
    {
        private readonly SpaServerOptions options;
        private bool serverCreated;

        public Uri[] ServerUrls
        {
            get
            {
                //var feature = this.Server.Host.ServerFeatures.Get<IServerAddressesFeature>();
                return [new Uri(this.options.GetServerUrl())];
                //return feature!.Addresses.Select(x => new Uri(x)).ToArray();
            }
        }

        public Factory(SpaServerOptions options)
            : base()
        {
            this.options = options;
        }

        protected override TestServer CreateServer(IWebHostBuilder builder)
        {
            var server = base.CreateServer(builder);
            server.BaseAddress = new(this.options.GetServerUrl());
            this.serverCreated = true;
            return server;
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            //builder.UseSetting("HostingStartupAssemblies", "Microsoft.AspNetCore.SpaProxy");
            //builder.UseEnvironment("Development");
            builder.UseEnvironment(Environments.Development);
            var serverUrl = SpaTestProxy.HasGlobalProxy
                ? options.GetServerUrl()
                : options.GetApplicationUrl();
            var config = new SpaDevelopmentServerOptions
            {
                Environment = this.options.Environment,
                LaunchCommand = this.options.LaunchCommand,
                ApplicationPublishDirectory = this.options.ApplicationPublishDirectory,
                ServerUrl = serverUrl,
                WorkingDirectory = this.options.ApplicationDirectory!
            };
            builder.ConfigureLogging(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
            });

            builder.ConfigureAppConfiguration(this.configureAppConfiguration);

            builder.ConfigureServices((context, services) =>
            {
                this.options.ConfigureServices?.Invoke(context, services);
                services.AddLogging(builder =>
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
                    this.options.ConfigureLogging?.Invoke(builder);
                });
                //services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, StartupFilter>());
                services.AddSingleton<AngularDevServer>();
                services.AddSingleton<ITestServerAccessor>(this);
                services.AddSingleton<IAngularDevServer>(sp => sp.GetRequiredService<AngularDevServer>());
                services.AddHostedService<SpaBackgroundService>();
                services.AddSingleton(Options.Create(config));
            });

            //builder.UseUrls(options.GetServerUrl());
            this.serverCreated = true;
        }

        private void configureAppConfiguration(WebHostBuilderContext context, IConfigurationBuilder builder)
        {
            var serverUrl = this.options.GetServerUrl();
            builder.AddCommandLine(["--urls", serverUrl]);
            this.options.ConfigureAppConfiguration?.Invoke(builder);
            if (this.options.ApplicationPublishDirectory.IsPresent()
                && Directory.Exists(this.options.ApplicationPublishDirectory))
            {
            }
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
        }

        public void Shutdown()
        {
            if (this.serverCreated)
            {
                this.Services.GetService<AngularDevServer>()?.Shutdown();
                this.Server.Dispose();
            }
        }

        protected override IWebHostBuilder? CreateWebHostBuilder()
        {
            this.serverCreated = true;
            return base.CreateWebHostBuilder();
        }
        protected override IHostBuilder? CreateHostBuilder()
        {
            this.serverCreated = true;
            return base.CreateHostBuilder();
        }

        IWebApplicationFactory ITestServerAccessor.GetTestServer() => this;
    }
}