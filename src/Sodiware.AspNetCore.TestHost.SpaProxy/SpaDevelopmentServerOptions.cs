using System.Text.Json;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    internal sealed class SpaDevelopmentServerOptions
    {
        class SpaServerSection
        {
            public required SpaDevelopmentServerOptions SpaProxyServer { get; init; }
        }
        public string Serialize()
        {
            var section = new SpaServerSection
            {
                SpaProxyServer = this
            };
            return JsonSerializer.Serialize(section);
        }
        public string ServerUrl { get; set; } = "";

        public string? LaunchCommand { get; set; }
        public string? ApplicationPublishDirectory { get; set; }

        public int MaxTimeoutInSeconds { get; set; }

        public TimeSpan MaxTimeout => TimeSpan.FromSeconds(MaxTimeoutInSeconds);

        public string WorkingDirectory { get; set; } = "";

        public Dictionary<string, string>? Environment { get; set; }

        internal string? GetServerUrl()
        {
            if (this.ServerUrl.IsPresent())
                return this.ServerUrl;
            return "http://localhost";
            /*
            var scheme = ServerUseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            var serverUrl = $"{scheme}://{this.ServerHost}:{this.ServerPort}";
            return serverUrl;
            */
        }
    }
}