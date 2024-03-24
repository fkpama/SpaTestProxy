namespace Sodiware.AspNetCore.TestHost.SpaProxy.Util
{
    internal sealed class SpaConfigFile
    {
        public string? SpaRoot { get; set; }
        public required Dictionary<string, string> Projects { get; set; }
    }
}
