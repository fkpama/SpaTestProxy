using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Sodiware.AspNetCore.TestHost.SpaProxy.Util;

namespace Sodiware.AspNetCore.TestHost.SpaProxy;

internal static partial class Utils
{
    static readonly Regex s_pkgJsonPortRegex = pkgJsonPortRegex();
    static readonly Regex s_pkgJsonSslRegex = pkgJsonSslRegex();
    private static string? s_configFilePath;
    private static SpaConfigFile? s_config;
    private const string ConfigFileName = "spatesthostconfig.json";
    internal const string DefaultNpmLaunchCommand  = "npm start";

    static string ConfigFilePath
    {
        get
        {
            if (s_configFilePath is null)
            {
                var tmp = Path.GetDirectoryName(typeof(Utils).Assembly.Location)!;
                s_configFilePath = Path.Combine(tmp, ConfigFileName);
            }
            return s_configFilePath;
        }
    }

    private static SpaConfigFile? GetConfigFile(ILogger<SpaConfigFile>? log = null)
    {
        if (s_config is null && File.Exists(ConfigFilePath))
        {
            try
            {
                s_config = JsonSerializer.Deserialize<SpaConfigFile>(File.ReadAllText(ConfigFilePath));
            }
            catch (Exception ex)
            {
                (log ?? NullLogger<SpaConfigFile>.Instance).LogError(ex, "Error deserializing config");
                return null;
            }
        }
        return s_config;
    }

    private static string? GetAssemblyContentRoot(Assembly type)
    {
        var config = GetConfigFile();
        var name = $"{type.GetName().Name}.dll";
        if (config?.Projects is not null)
        {
            foreach (var (asmName, projectDir) in config.Projects)
            {
                if (string.Equals(asmName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectDir;
                }
            }
        }
        return null;
    }

    internal static string FindContentRoot(Type type)
    {
        var projectDir = GetAssemblyContentRoot(type.Assembly)
            ?? throw new DirectoryNotFoundException();
        //var dir = new DirectoryInfo(Path.GetDirectoryName(type.Assembly.Location)!);
        //for(var cur = dir.Parent; cur is not null && cur != cur.Parent; cur = cur.Parent)
        //{
        //    var path = Path.Combine(cur.FullName, "wwwroot");
        //    if (Directory.Exists(path))
        //    {
        //        return path;
        //    }
        //}

        //throw new DirectoryNotFoundException();
        return projectDir;
    }

    internal static string FindNgApplicationDirectory(string directory)
    {
        var infos = new DirectoryInfo(directory);
        foreach(var entry in infos.GetDirectories())
        {
            var path = Path.Combine(entry.FullName, "angular.json");
            if (File.Exists(path))
            {
                return entry.FullName;
            }
        }
        var root = GetConfigFile()?.SpaRoot;
        if (!string.IsNullOrWhiteSpace(root))
        {
            return root;
        }
        throw new FileNotFoundException();
    }

    internal static SpaServerOptions GetOptions(Type type, SpaServerOptions? options = null)
    {
        if (options is null)
        {
            options = new();
        }
        if (string.IsNullOrWhiteSpace(options.ContentRoot))
        {
            options.ContentRoot = FindContentRoot(type);
        }
        if (string.IsNullOrWhiteSpace(options.ApplicationDirectory))
            options.ApplicationDirectory = FindNgApplicationDirectory(options.ContentRoot);

        var (port, host) = GetDevServerPort(options.ApplicationDirectory, options);
        options.ApplicationPort = port;
        options.ApplicationHost = host;
        return options;
    }

    private static (int port, string host) GetDevServerPort(string appDir, SpaServerOptions options)
    {
        var host = "localhost";
        int port = options.ApplicationPort;
        bool ssl = false;
        if (options.ApplicationPort == 0)
        {
            if (tryGetPortViaHttpsPort(out port))
            {
                options.UseSsl = true;
                options.ApplicationHost = host;
            }
            else if (tryGetHostPortViaEnv(ref host, out ssl, out port))
            {
            }
            else if (tryGetPortViaPkgJson(appDir, out ssl, out port))
            {
                options.ApplicationHost = host;
                options.UseSsl = ssl;
            }
        }
        options.ApplicationPort = port;
        return (options.ApplicationPort, host);
    }

    private static bool tryGetHostPortViaEnv(ref string host, out bool ssl, out int port)
    {
        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (urls.IsPresent())
        {
            var sp = urls.Split(';', StringSplitOptions.RemoveEmptyEntries);
            Uri? uri = null;
            var url = sp.FirstOrDefault(x => Uri.TryCreate(x, UriKind.Absolute, out uri)
            && uri.Scheme.EqualsOrdI(Uri.UriSchemeHttps));
            if (url is not null)
            {
                host = uri!.Host;
                ssl = true;
                port = uri.Port;
            }
            else
            {
                url = sp.FirstOrDefault(x => Uri.TryCreate(x, UriKind.Absolute, out uri));
                if (url is not null)
                {
                    host = uri!.Host;
                    ssl = false;
                    port = uri.Port;
                }
                else
                {
                    ssl = false;
                    port = 0;
                    return false;
                }
            }
        }
        else
        {
            ssl = false;
            port = 0;
        }

        return true;
    }

    private static bool tryGetPortViaHttpsPort(out int port)
    {
        var str = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORT");
        if (!string.IsNullOrWhiteSpace(str))
        {
            port = 0;
            try
            {
                port = int.Parse(str);
            }
            catch (FormatException) { }
        }
        else
        {
            port = 0;
        }
        return port != 0;
    }

    private static bool tryGetPortViaPkgJson(string appDir, out bool ssl, out int port)
    {
        var pkgJson = Path.Combine(appDir, "package.json");
        port = 0;
        ssl = false;

        if (!File.Exists(pkgJson))
        {
            return false;
        }

        var node = JsonNode.Parse(File.ReadAllText(pkgJson));
        Debug.Assert(node is not null);
        var nodeAsObject = node["scripts"]?.AsObject();
        Debug.Assert(nodeAsObject is not null);

        if (!nodeAsObject.TryGetPropertyValue($"start:{getRunScriptOs()}", out node)
            && !nodeAsObject.TryGetPropertyValue("start", out node))
        {
            return false;
        }

        var text = node!.AsValue().GetValue<string>();
        Match match;
        if (!(match = s_pkgJsonPortRegex.Match(text)).Success)
        {
            return false;
        }

        port = int.Parse(match.Groups["port"].Value);
        return true;
    }

    private static string getRunScriptOs()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    [GeneratedRegex(@"\s*--port\s*(?<port>\d+)", RegexOptions.Compiled)]
    private static partial Regex pkgJsonPortRegex();

    [GeneratedRegex(@"\s*--ssl\s*(?<port>\d+)", RegexOptions.Compiled)]
    private static partial Regex pkgJsonSslRegex();

    internal static string GetPort(int port, bool useSsl)
    {
        if (useSsl && port == 443)
            return string.Empty;
        else if (!useSsl && port == 80)
            return string.Empty;
        else
            return $":{port}";
    }

    internal static string GetHttpUriScheme(bool useSsl)
        => useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp; 

    internal static void FindExe(ref string fileName, ref string? arg0)
    {
        if (Path.IsPathRooted(fileName))
        {
            return;
        }

        var paths = Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator);
        var lst = new List<string>();
        var pf64 = Environment.GetEnvironmentVariable("ProgramFiles");
        var lst1 = new List<string>();
        if (pf64 is not null)
            lst1.AddRange(paths.Where(x => x.StartsWith(pf64, StringComparison.OrdinalIgnoreCase)));
        lst.AddRange(lst1);
        lst.AddRange(paths.Except(lst1));
        foreach (var path in lst)
        {
            var fname = Path.Combine(path, fileName);
            if (File.Exists(fname))
            {
                if (Path.GetFileName(fname).StartsWith("npm"))
                {
                    fileName = Path.Combine(path, "node.exe");
                    arg0 = Path.Combine(path, @"node_modules\npm\bin\npm-cli.js");
                    if (!File.Exists(arg0))
                    {
                        continue;
                    }
                }
                else
                {
                    fileName = fname;
                }
                return;
            }
        }
    }

    internal static string FormatHeaders(this HttpHeaders headers)
        => string.Join("\r\n", headers.Select(x => x.FormatHeader()));

    internal static string FormatHeaders(this IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
        => string.Join("\r\n", headers.Select(x => x.FormatHeader()));

    internal static string FormatHeader(this KeyValuePair<string, IEnumerable<string>> header)
        => $"{header.Key}: {string.Join(", ", header.Value)}";


}