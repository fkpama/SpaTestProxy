using System.Net.Sockets;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace Sodiware.AspNetCore.TestHost.SpaProxy.Proxy
{
    public sealed class HttpResponseMessageSocketWriter : IDisposable
    {
        private readonly object sync = new();
        private readonly Stream _socket;
        private readonly int _bufferSize;
        private readonly bool ownsStream;

        public HttpResponseMessageSocketWriter(Stream socket, int bufferSize = 4096, bool ownsStream = false)
        {
            _socket = Guard.NotNull(socket);
            _bufferSize = bufferSize;
            this.ownsStream = ownsStream;
        }

        public async Task WriteAsync(HttpResponseMessage response)
        {
            ArgumentNullException.ThrowIfNull(response);


            // Serialize the HttpResponseMessage to a byte array
            try
            {
                var responseBytes = await SerializeHttpResponse(response);
                await Task.Run(() =>
                {
                    lock (this.sync)
                    {
                        _socket.Write(responseBytes);
                        _socket.Flush();
                        _socket.Close();
                    }
                });
            }
            catch (Exception) { }

        }

        private async Task<byte[]> SerializeHttpResponse(HttpResponseMessage response)
        {
            // Use a MemoryStream to serialize the HttpResponseMessage
            using var memoryStream = new MemoryStream();
            // Write the status code to the stream
            var statusCodeBytes = Encoding.UTF8.GetBytes($"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n");
            await memoryStream.WriteAsync(statusCodeBytes);

            // Serialize the content
            var contentBytes = await response.Content.ReadAsByteArrayAsync();

            // Serialize the headers
            var lst = new HashSet<string>(StringComparer.Ordinal);
            if (response.Content?.Headers is not null)
            {
                lst.AddRange(response.Content.Headers.Select(x => x.Key));
            }
            var bodyHeadersByes = SerializeHeaders(response.Content?.Headers);
            var headersBytes = SerializeHeaders(response.Headers, contentBytes.Length, lst);
            await memoryStream.WriteAsync(bodyHeadersByes);
            await memoryStream.WriteAsync(headersBytes);
            await memoryStream.WriteAsync(Encoding.UTF8.GetBytes("\r\n\r\n"));
            if (contentBytes.Length > 0)
            {
                await memoryStream.WriteAsync(contentBytes);
            }


            // Return the byte array
            memoryStream.Position = 0;
            return memoryStream.ToArray();
        }

        private byte[] SerializeHeaders(HttpContentHeaders? headers)
        {
            if (headers is null)
                return Array.Empty<byte>();
            return Encoding.UTF8.GetBytes(headers
                .Where(x => !x.Key.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase))
                .FormatHeaders());
        }

        private byte[] SerializeHeaders(HttpHeaders headers, int length, HashSet<string> written)
        {
            // Serialize the headers to a string
            var headersString = new[]
            {
                new KeyValuePair<string, IEnumerable<string>>(HeaderNames.ContentLength, new[] { length.ToString() })
            }.Concat(headers
            .Where(x => !x.Key.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase)))
            .Where(x => written.Add(x.Key))
            .FormatHeaders();

            // Convert the string to a byte array
            return Encoding.UTF8.GetBytes(headersString);
        }

        public void Dispose()
        {
            if (ownsStream)
            {
                _socket.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }

}
