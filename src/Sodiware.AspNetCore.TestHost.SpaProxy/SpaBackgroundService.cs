using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Sodiware.AspNetCore.TestHost.SpaProxy.Internals;
using Sodiware.AspNetCore.TestHost.SpaProxy.Proxy;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{

    internal interface ITestServerAccessor
    {
        IWebApplicationFactory GetTestServer();
    }


    internal sealed partial class SpaBackgroundService : BackgroundService
    {
        static Regex s_portAlreadyInUseRegex = PortAlreadyInUseRegex();
        private readonly IServiceProvider services;
        private readonly ITestServerAccessor testServer;
        private readonly IHostApplicationLifetime lifetime;
        private readonly AngularDevServer devServer;
        private readonly ILogger<SpaBackgroundService> log;
        private readonly SpaDevelopmentServerOptions options;
        private ProxyServer? tcpServer;
        private Process? Process
        {
            get => this.devServer.Process;
            set => this.devServer.Process = value;
        }
        public SpaBackgroundService(IHostApplicationLifetime lifetime,
                                    IServiceProvider sp,
                                    ITestServerAccessor testServer,
                                    IOptions<SpaDevelopmentServerOptions> options,
                                    ILogger<SpaBackgroundService> log)
        {
            this.services = sp;
            this.testServer = testServer;
            this.lifetime = lifetime;
            this.devServer = sp.GetRequiredService<AngularDevServer>();
            this.log = log;
            this.options = options.Value;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.lifetime.ApplicationStarted.Register(onApplicationStarted);
            this.lifetime.ApplicationStopping.Register(onApplicationStopped);
            return Task.CompletedTask;
        }

        private void onApplicationStopped()
        {
            if (SpaTestProxy.HasGlobalProxy)
            {
                this.tcpServer?.Cleanup(this.testServer.GetTestServer());
            }
            else
            {
                this.tcpServer?.Dispose();
            }
        }

        private void onApplicationStarted()
        {
            if (SpaTestProxy.HasGlobalProxy)
            {
                this.startServer();
                return;
            }

            var args = this.options.LaunchCommand?.Split() ?? [];
            string? fileName = args[0], arg0 = null;
            Utils.FindExe(ref fileName, ref arg0);
            var cmdArgs = args.Skip(1).ToList();
            if (!string.IsNullOrWhiteSpace(arg0))
            {
                cmdArgs.Insert(0, arg0);
            }
            var process = new ProcessStartInfo(fileName)
            {
                // Arguments = string.Join(' ', cmdArgs),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = this.options.WorkingDirectory,
                CreateNoWindow = true
            };
            cmdArgs?.ForEach(process.ArgumentList.Add);
            this.options.Environment?.ForEach(x => process.EnvironmentVariables.Add(x.Key, x.Value));

            //ClientHandler
            this.Process = new Process
            {
                StartInfo = process,
                EnableRaisingEvents = true
            };
            this.Process.Exited += (o, e) =>
            {
                if (this.Process is not null)
                {
                    var ec = this.Process.ExitCode;
                    log.LogInformation("Command exited ({ec})", ec);
                    if (ec != 0 || !this.devServer.ShutdownRequested)
                    {
                        //this.devServer.ServerStartedSource.TrySetException(new Exception($"Ng Process exited with code {ec}"));
                        this.devServer.SetException(new Exception($"Ng Process exited with code {ec}"));
                    }
                    this.Process = null;
                }
            };
            this.Process.OutputDataReceived += onOutput;
            this.Process.ErrorDataReceived += onError;
            this.Process.Start();
            this.Process.BeginOutputReadLine();
            this.Process.BeginErrorReadLine();
        }

        private void onError(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                log.LogError(e.Data);
            }
        }

        private void onOutput(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Match match;
                log.LogInformation(e.Data);
                if (e.Data.Contains("open your browser on"))
                {
                    this.startServer();
                }
                else if ((match = s_portAlreadyInUseRegex.Match(e.Data)).Success)
                {
                    //this.devServer.ServerStarted.TrySetException(new Exception(match.Value));
                    log.LogWarning("Port {port} already in use", match.Groups[1].Value);
                    this.startServer();
                }
                else
                {
                    log.LogInformation(e.Data);
                }
            }
        }

        private void startServer()
        {
            this.devServer.SetStarted();
            var testServer = this.testServer.GetTestServer();
            var url = testServer.ServerUrls[0];
            if (!SpaTestProxy.HasGlobalProxy)
            {
                this.tcpServer = SpaTestProxy.CreateProxy(url);
                this.tcpServer.Start();
            }
            else
            {
                SpaTestProxy.Proxy.Setup(testServer, null);
                this.tcpServer = SpaTestProxy.Proxy;
            }
            this.devServer.ShuttingDown += (o, e) => this.onApplicationStopped();
        }

        private X509Certificate2? getCertificate(string host)
        {
            return CertificateUtil.GetServerCertificate(host);
        }

        //static bool isDevelopmentCertificate(X509Certificate2 certificate)
        //{
        //}

        [GeneratedRegex(@"Port (\d+) is already in use")]
        private static partial Regex PortAlreadyInUseRegex();
    }
}
