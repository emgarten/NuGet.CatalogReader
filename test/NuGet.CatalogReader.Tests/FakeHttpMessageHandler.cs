using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CatalogReader.Tests
{
    /// <summary>
    /// Returns fixed responses and counts requests.
    /// </summary>
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<Uri, string> _responses;
        private readonly ConcurrentDictionary<Uri, int> _requestCounts = new ConcurrentDictionary<Uri, int>();

        public FakeHttpMessageHandler(Dictionary<Uri, string> responses)
        {
            _responses = responses;
        }

        public int GetRequestCount(Uri uri)
        {
            return _requestCounts.TryGetValue(uri, out var count) ? count : 0;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requestCounts.AddOrUpdate(request.RequestUri, 1, (_, count) => count + 1);

            var response = _responses.TryGetValue(request.RequestUri, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);

            response.RequestMessage = request;

            return Task.FromResult(response);
        }
    }
}
