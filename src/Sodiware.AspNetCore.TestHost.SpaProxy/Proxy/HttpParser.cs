using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using IHttpMachine;
using IHttpMachine.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Sodiware;
using SysHdr = System.Net.Http.Headers;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy
{
    internal sealed class HttpParser : IHttpParserCombinedDelegate
    {
        [Flags]
        enum StateFlags
        {
            InContentLengthHeader = 1,
            ContentLengthReceived = 1 << 1,
            DoneParsing = 1 << 2,
            MessageSent = 1 << 3
        }
        static readonly ArrayPool<byte> s_pool = ArrayPool<byte>.Shared;
        private readonly IWebApplicationFactory testServer;
        private readonly HttpResponseMessageSocketWriter writer;
        private readonly ILogger<HttpParser> log;
        private string? method;
        private string? path;
        private string? queryString;
        private string? currentHeaderName;
        private List<string>? currentHeaderVals;
        private readonly Dictionary<string, List<string>> headers = new();
        private string? requestUri;
        private Version? version;
        private ArraySegment<byte> body;
        private StateFlags state;
        private int contentLength;

        private bool IsInContentTypeHeader
        {
            get => this.state.HasFlag(StateFlags.InContentLengthHeader);
            set => this.setFlag(StateFlags.InContentLengthHeader, value);
        }

        private bool ContentLengthReceived
        {
            get => this.state.HasFlag(StateFlags.ContentLengthReceived);
            set => this.setFlag(StateFlags.ContentLengthReceived, value);
        }

        private bool MessageSent
        {
            get => this.state.HasFlag(StateFlags.MessageSent);
            set => this.setFlag(StateFlags.MessageSent, value);
        }

        public HttpRequestResponse HttpRequestResponse { get; } = null!;
        private bool HasContent => this.contentLength > 0;

        public string? BaseUrl { get; }

        public HttpParser(IWebApplicationFactory testServer,
                          HttpResponseMessageSocketWriter writer,
                          string? baseUrl,
                          ILogger<HttpParser> log)
        {
            this.testServer = testServer;
            this.writer = writer;
            BaseUrl = baseUrl;
            this.log = log;
            //this.HttpRequestResponse = new();
        }
        public void Dispose()
        {
            releaseBody();
        }

        private void releaseBody()
        {
            if (body.Array is not null)
            {
                s_pool.Return(body.Array);
                body = default;
            }
        }

        public void OnBody(IHttpCombinedParser combinedParser, ArraySegment<byte> data)
        {
            var ar = s_pool.Rent(data.Count);
            data.CopyTo(ar);
            this.body = new(ar, 0, data.Count);
        }

        public void OnChunkedLength(IHttpCombinedParser combinedParser, int length)
        {
            throw new NotImplementedException();
        }

        public void OnChunkReceived(IHttpCombinedParser combinedParser)
        {
            throw new NotImplementedException();
        }

        public void OnFragment(IHttpCombinedParser combinedParser, string fragment)
        {
            throw new NotImplementedException();
        }

        public void OnHeaderName(IHttpCombinedParser combinedParser, string name)
        {
            currentHeaderName = name;
            if (!headers.TryGetValue(currentHeaderName, out var lst))
            {
                lst = new();
                headers.Add(currentHeaderName, lst);
            }
            currentHeaderVals = lst;
            if (name.EqualsOrdI(HeaderNames.ContentLength))
            {
                if (ContentLengthReceived)
                {
                    // TODO: Warning
                }
                this.IsInContentTypeHeader = true;
            }
        }

        public void OnHeadersEnd(IHttpCombinedParser combinedParser)
        {
            log.LogTrace("End message headers");
            currentHeaderName = null;
            currentHeaderVals = null;
            if (!this.HasContent)
            {
                //_ = sendMessage();
            }
        }

        public void OnHeaderValue(IHttpCombinedParser combinedParser, string value)
        {
            Debug.Assert(currentHeaderVals is not null);
            currentHeaderVals.Add(value);
            if (IsInContentTypeHeader)
            {
                if (!int.TryParse(value, out var cl))
                {
                    // TODO: Log
                }
                else
                {
                    this.contentLength = cl;
                }
            }
        }

        public void OnMessageBegin(IHttpCombinedParser combinedParser)
        {
            //throw new NotImplementedException();
            version = new Version(combinedParser.MajorVersion, combinedParser.MinorVersion);
            log.LogDebug("Message begin: {version}", version);
        }

        public void OnMessageEnd(IHttpCombinedParser combinedParser)
        {
            log.LogDebug("Message ended");
            _ = sendMessage();
            //throw new NotImplementedException();
        }

        private async Task sendMessage()
        {
            //this.builder.Configure((context, writer) =>
            //{
            if (this.MessageSent)
            {
                return;
            }
            this.MessageSent = true;
            var req = new HttpRequestMessage();

            var builder = new UriBuilder(this.BaseUrl.IfMissing("http://localhost"))
            {
                Path = path,
                Query = queryString
            };
            Debug.Assert(method.IsPresent());
            //builder.Port = 60045;
            //builder.Host = "localhost";
            //builder.Scheme = Uri.UriSchemeHttps;
            //req.Version = this.version;
            //req.RequestUri = builder.Uri;
            req.Method = new HttpMethod(method);
            Uri.TryCreate(builder.Uri.PathAndQuery, UriKind.Relative, out var result);
            req.RequestUri = result;
            var client = testServer.CreateClient();
            //var response = await client.Send(req);

            if (body.Count > 0)
            {
                req.Content = new ReadOnlyMemoryContent(body);
                if (tryGetHeader(HeaderNames.ContentType, out var hdrs))
                {
                    req.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(hdrs[0]);
                }
                if (tryGetHeader(HeaderNames.ContentLength, out hdrs)
                    && long.TryParse(hdrs[0], out var contentLength))
                {
                    req.Content.Headers.ContentLength = contentLength;
                }
                if (tryGetHeader(HeaderNames.ContentDisposition, out hdrs))
                {
                    req.Content.Headers.ContentDisposition = SysHdr.ContentDispositionHeaderValue.Parse(hdrs[0]);
                }
                if (tryGetHeader(HeaderNames.ContentEncoding, out hdrs))
                {
                    req.Content.Headers.ContentEncoding.AddRange(hdrs);
                }

                if (tryGetHeader(HeaderNames.ContentLocation, out hdrs))
                {
                    req.Content.Headers.ContentLocation = new(hdrs[0]);
                }

                if (tryGetHeader(HeaderNames.ContentRange, out hdrs))
                {
                    req.Content.Headers.ContentRange = SysHdr.ContentRangeHeaderValue.Parse(hdrs[0]);
                }

                if (tryGetHeader(HeaderNames.ContentMD5, out hdrs))
                {
                    req.Content.Headers.ContentMD5 = Convert.FromBase64String(hdrs[0]);
                }
            }

            //req.Path = this.path;
            //req.Method = this.method;
            //req.Host = new("localhost", 7275);
            //req.Protocol = HttpProtocol.GetHttpProtocol(this.version);
            //req.RequestUri = Uri.UriSchemeHttps;
            foreach (var header in headers)
            {
                if (header.Key.Equals(nameof(HttpRequestHeader.Host), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                else if (string.Equals(header.Key, HeaderNames.UserAgent, StringComparison.OrdinalIgnoreCase))
                {
                    req.Headers.TryAddWithoutValidation(header.Key, string.Join(" ", header.Value));
                    continue;
                }
                req.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            //});


            //var httpContext  = await this.builder.SendAsync(CancellationToken.None).ConfigureAwait(false);
            var response = await client.SendAsync(req, CancellationToken.None).ConfigureAwait(false);
            await writer.WriteAsync(response).ConfigureAwait(false);
        }

        private bool tryGetHeader(string name, [NotNullWhen(true)] out List<string>? value, bool remove = true)
        {
            var (key, val) = headers.FirstOrDefault(x => x.Key.EqualsOrdI(name));
            if (remove && key is not null)
                headers.Remove(key);
            if (val is null || val.Count == 0)
            {
                value = null;
                return false;
            }
            value = val;
            return true;
        }
        private void trySetHeader(HttpContent content, string contentType)
        {
            var (key, value) = headers.FirstOrDefault(x => x.Key.EqualsOrdI(contentType));
            if (value is not null)
            {
                content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(value[0]);
            }
        }

        public void OnMethod(IHttpCombinedParser combinedParser, string method)
        {
            log.LogTrace("Method received: {method}", method);
            this.method = method;
        }

        public void OnParserError()
        {
            log.LogError("Parser error");
            //throw new NotImplementedException();
        }

        public void OnPath(IHttpCombinedParser combinedParser, string path)
        {
            log.LogDebug("Request path received: {uri}", path);
            this.path = path;
        }

        public void OnQueryString(IHttpCombinedParser combinedParser, string queryString)
        {
            this.queryString = queryString;
        }

        public void OnRequestType(IHttpCombinedParser combinedParser)
        {
            //throw new NotImplementedException();
        }

        public void OnRequestUri(IHttpCombinedParser combinedParser, string requestUri)
        {
            //throw new NotImplementedException();
            this.requestUri = requestUri;
            log.LogDebug("Request uri received: {uri}", this.requestUri);
        }

        public void OnResponseCode(IHttpCombinedParser combinedParser, int statusCode, string statusReason)
        {
            throw new NotImplementedException();
        }

        public void OnResponseType(IHttpCombinedParser combinedParser)
        {
            throw new NotImplementedException();
        }

        public void OnTransferEncodingChunked(IHttpCombinedParser combinedParser, bool isChunked)
        {
            throw new NotImplementedException();
        }

        [DebuggerStepThrough, MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void setFlag(StateFlags flag, bool value)
            => this.state = value ? this.state | flag : this.state & ~flag;
    }
}