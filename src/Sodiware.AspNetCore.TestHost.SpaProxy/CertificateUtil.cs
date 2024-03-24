using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Sodiware.AspNetCore.TestHost.SpaProxy
{
    internal static class CertificateUtil
    {
        const string DNSNamePrefix = "DNS Name=";
        const string ServerAuthenticationKeyUsage = "1.3.6.1.5.5.7.3.1";
        const string SubjectAlternativeNameOid = "2.5.29.17";
        internal static X509Certificate2? GetServerCertificate(string host)
        {
            using var store = new X509Store(StoreLocation.LocalMachine);
            var collection = new Oid(ServerAuthenticationKeyUsage);
            store.Open(OpenFlags.ReadOnly);
            var cert = store.Certificates
                .FirstOrDefault(x =>
                x.HasPrivateKey
                && isIssuedTo(x, host)
                && x.HasEnhancedKeyUsage(ServerAuthenticationKeyUsage)
                && isValid(store, x));
            return cert;
        }

        private static bool HasEnhancedKeyUsage(this X509Certificate2 x, string usage)
        {
            var extension = x.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
            if (extension is null)
                return false;
            if (isOid(usage))
            {
                usage = $" ({usage})";
                return extension.getValues().Any(x => x.EndsWithO(usage));
            }
            return extension.getValues().Any(x => x.StartsWithOI(usage));
        }

        private static bool isOid(string value)
            => value.All(x => x == '.' || char.IsNumber(x));

        private static bool isValid(X509Store store, X509Certificate2 cert)
        {
            return store.Certificates
                .Find(X509FindType.FindBySerialNumber, cert.SerialNumber, true) is not null;

        }

        public static string[] GetSubjectAlternativeNames(this X509Certificate2 certificate)
        {
            //var oid = new Oid(SubjectAlternativeNameOid);
            var extension = certificate.Extensions
                .OfType<X509SubjectAlternativeNameExtension>()
                .FirstOrDefault();
            if (extension is null)
                return Array.Empty<string>();

            return extension.getValues();
        }

        private static string[] getValues(this AsnEncodedData extension)
            => extension.Format(true).Split(Environment.NewLine);
        public static IEnumerable<string> GetDnsNames(this X509Certificate2 cert)
            => cert.GetSubjectAlternativeNames().Where(x => x.StartsWith(DNSNamePrefix))
            .Select(x => x[DNSNamePrefix.Length..]);
        private static bool isIssuedTo(X509Certificate2 cert, string host)
        {
            if (cert.GetDnsNames().Any(x => matchHost(x, host))) return true;

            var str = cert.Subject.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return str.SequenceEqual(parts, StringComparer.OrdinalIgnoreCase);
        }

        private static bool matchHost(string x, string host)
        {
            if (x.EqualsOrd(host))
            {
                return true;
            }
            else if (x.Length > host.Length)
            {
                var sub = x.Substring(0, x.Length - host.Length).TrimEnd('.');
                return sub == "*";
            }
            return false;
        }
    }
}