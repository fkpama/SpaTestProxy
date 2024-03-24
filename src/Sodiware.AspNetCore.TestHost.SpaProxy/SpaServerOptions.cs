using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    public class SpaServerOptions
    {
        public string? SpaProxyUrl { get; set; }
        public string? ContentRoot { get; set; }
        public string? ApplicationDirectory { get; set; }
        public int ServerPort { get; set; }
        public string ServerHost { get; set; } = "localhost";
        public bool ServerUseSsl { get; set; } = true;
        public int ApplicationPort { get; set; } = 4200;
        public string ApplicationHost { get; set; } = "localhost";
        public bool UseSsl { get; set; } = true;
        public string? LaunchCommand { get; set; } = Utils.DefaultNpmLaunchCommand;
        public string? ApplicationPublishDirectory { get; set; }
        public Dictionary<string, string>? Environment { get; set; }
        public Action<ILoggingBuilder>? ConfigureLogging { get; set; }
        public Action<WebHostBuilderContext, IServiceCollection>? ConfigureServices { get; set; }
        public Action<IConfigurationBuilder>? ConfigureAppConfiguration { get; set; }

        internal string GetApplicationUrl()
        {
            var scheme = UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            return $"{scheme}://{this.ApplicationHost}:{this.ApplicationPort}";
        }

        internal string GetServerUrl()
        {
            var scheme = ServerUseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            var serverUrl = $"{scheme}://{this.ServerHost}:{this.ServerPort}";
            return serverUrl;
        }
    }
}
