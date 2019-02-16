using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CatalogReader
{
    /// <summary>
    /// NuGet v3 index reader.
    /// </summary>
    public abstract class HttpReaderBase : IDisposable
    {
        protected readonly Uri _indexUri;
        protected HttpSource _httpSource;
        protected readonly ILogger _log;
        protected readonly HttpSourceCacheContext _cacheContext;
        protected readonly SourceCacheContext _sourceCacheContext;
        protected readonly HttpMessageHandler _messageHandler;
        protected ServiceIndexResourceV3 _serviceIndex;

        /// <summary>
        /// Max threads. Set to 1 to disable concurrency.
        /// </summary>
        public int MaxThreads { get; set; } = 16;

        /// <summary>
        /// Http cache location
        /// </summary>
        public string HttpCacheFolder => _cacheContext.RootTempFolder;

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        public HttpReaderBase(Uri indexUri)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: TimeSpan.Zero, log: null)
        {
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        public HttpReaderBase(Uri indexUri, TimeSpan cacheTimeout)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: cacheTimeout, log: null)
        {
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        public HttpReaderBase(Uri indexUri, ILogger log)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: TimeSpan.Zero, log: log)
        {
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        public HttpReaderBase(Uri indexUri, TimeSpan cacheTimeout, ILogger log)
            : this(indexUri, httpSource: null, cacheContext: null, cacheTimeout: cacheTimeout, log: log)
        {
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the catalog service or the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public HttpReaderBase(Uri indexUri, HttpMessageHandler messageHandler)
            : this(indexUri,
                  messageHandler: null,
                  log: null)
        {
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        /// <param name="messageHandler">HTTP message handler.</param>
        public HttpReaderBase(Uri indexUri, HttpMessageHandler messageHandler, ILogger log)
            : this(indexUri,
                  httpSource: null,
                  cacheContext: null,
                  cacheTimeout: TimeSpan.Zero,
                  log: log)
        {
            _messageHandler = messageHandler;
        }

        /// <summary>
        /// HttpReaderBase
        /// </summary>
        /// <param name="indexUri">URI of the feed service index.</param>
        /// <param name="httpSource">Custom HttpSource.</param>
        public HttpReaderBase(Uri indexUri, HttpSource httpSource, SourceCacheContext cacheContext, TimeSpan cacheTimeout, ILogger log)
        {
            _indexUri = indexUri ?? throw new ArgumentNullException(nameof(indexUri));
            _log = log ?? NullLogger.Instance;
            _sourceCacheContext = cacheContext ?? new SourceCacheContext();

            _httpSource = httpSource;

            // TODO: what should retry be?
            _cacheContext = HttpSourceCacheContext.Create(_sourceCacheContext, 5);

            if (_sourceCacheContext == null)
            {
                var sourceCacheContext = new SourceCacheContext()
                {
                    MaxAge = DateTimeOffset.UtcNow.Subtract(cacheTimeout),
                };

                _cacheContext = HttpSourceCacheContext.Create(sourceCacheContext, 5);
            }
        }

        /// <summary>
        /// Retrieve the HttpSource used for Http requests.
        /// </summary>
        /// <returns></returns>
        public async Task<HttpSource> GetHttpSourceAsync()
        {
            await EnsureHttpSourceAsync();

            return _httpSource;
        }

        private Func<Uri, CancellationToken, Task<JObject>> _getJson;
        protected Func<Uri, CancellationToken, Task<JObject>> GetJson
        {
            get
            {
                if (_getJson == null)
                {
                    _getJson = (uri, token) => _httpSource.GetJObjectAsync(uri, _cacheContext, _log, token);
                }

                return _getJson;
            }
        }

        private Func<Uri, CancellationToken, Task<HttpSourceResult>> _getNupkg;
        protected Func<Uri, CancellationToken, Task<HttpSourceResult>> GetNupkg
        {
            get
            {
                if (_getNupkg == null)
                {
                    _getNupkg = (uri, token) => _httpSource.GetNupkgAsync(uri, _cacheContext, _log, token);
                }

                return _getNupkg;
            }
        }

        private Func<Uri, CancellationToken, Task<NuspecReader>> _getNuspec;
        protected Func<Uri, CancellationToken, Task<NuspecReader>> GetNuspec
        {
            get
            {
                if (_getNuspec == null)
                {
                    _getNuspec = (uri, token) => _httpSource.GetNuspecAsync(uri, _cacheContext, _log, token);
                }

                return _getNuspec;
            }
        }

        /// <summary>
        /// Ensure index.json has been loaded.
        /// </summary>
        protected async Task EnsureServiceIndexAsync(Uri uri, CancellationToken token)
        {
            if (_serviceIndex == null)
            {
                await EnsureHttpSourceAsync();

                var index = await _httpSource.GetJObjectAsync(_indexUri, _cacheContext, _log, token);
                var resources = (index["resources"] as JArray);

                if (resources == null)
                {
                    throw new InvalidOperationException($"{uri.AbsoluteUri} does not contain a 'resources' property. Use the root service index.json for the nuget v3 feed.");
                }

                _serviceIndex = new ServiceIndexResourceV3(index, DateTime.UtcNow);
            }
        }

        protected async Task EnsureHttpSourceAsync()
        {
            if (_httpSource == null)
            {
                var handlerResource = await CatalogReaderUtility.GetHandlerAsync(_indexUri, _messageHandler);

                var packageSource = new PackageSource(_indexUri.AbsoluteUri);

                _httpSource = new HttpSource(
                    packageSource,
                    () => Task.FromResult((HttpHandlerResource)handlerResource),
                    NullThrottle.Instance);

                if (string.IsNullOrEmpty(UserAgent.UserAgentString)
                    || new UserAgentStringBuilder().Build()
                        .Equals(UserAgent.UserAgentString, StringComparison.Ordinal))
                {
                    // Set the user agent string if it was not already set.
                    var userAgent = new UserAgentStringBuilder("NuGet.CatalogReader");
                    UserAgent.SetUserAgentString(userAgent);
                }
            }
        }

        /// <summary>
        /// Clear the HttpCacheFolder cache folder.
        /// Use this to free up space when downloading large numbers of packages.
        /// </summary>
        public void ClearCache()
        {
            CatalogReaderUtility.DeleteDirectoryFiles(HttpCacheFolder);
        }

        public void Dispose()
        {
            _httpSource?.Dispose();
            _sourceCacheContext.Dispose();
        }
    }
}
