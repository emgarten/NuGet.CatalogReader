using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CatalogReader
{
    internal static class CatalogReaderUtility
    {
        internal static HttpSource CreateSource(Uri index)
        {
            var handler = new HttpClientHandler();

            return CreateSource(index, handler, handler);
        }

        internal static HttpSource CreateSource(Uri index, HttpMessageHandler messageHandler, HttpClientHandler clientHandler)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            if (clientHandler == null)
            {
                throw new ArgumentNullException(nameof(clientHandler));
            }

            var handlerResource = new HttpHandlerResourceV3(clientHandler, messageHandler);

            return CreateSource(index, handlerResource);
        }

        internal static HttpSource CreateSource(Uri index, HttpHandlerResourceV3 handlerResource)
        {
            if (index == null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            var packageSource = new PackageSource(index.AbsoluteUri);

            var httpSource = new HttpSource(
                packageSource,
                () => Task.FromResult((HttpHandlerResource)handlerResource),
                NullThrottle.Instance);

            return httpSource;
        }

        internal static async Task DownloadFileAsync(Stream stream, FileInfo outputFile, DateTimeOffset created, DownloadMode mode, CancellationToken token)
        {
            if (outputFile.Exists)
            {
                if (mode == DownloadMode.FailIfExists)
                {
                    throw new InvalidOperationException($"File already exists: {outputFile.FullName}");
                }
                else if (mode == DownloadMode.SkipIfExists)
                {
                    // noop
                    return;
                }
                else if (mode == DownloadMode.OverwriteIfNewer)
                {
                    if (created <= outputFile.LastWriteTimeUtc)
                    {
                        // noop
                        return;
                    }
                }
            }

            var tmp = new FileInfo(Path.Combine(outputFile.Directory.FullName, Guid.NewGuid().ToString()));

            try
            {
                outputFile.Directory.Create();

                using (var outputStream = new FileStream(tmp.FullName, FileMode.CreateNew))
                {
                    await stream.CopyToAsync(outputStream, 8192, token);
                }

                FileUtility.Delete(outputFile.FullName);
                FileUtility.Move(tmp.FullName, outputFile.FullName);

                File.SetCreationTimeUtc(outputFile.FullName, created.UtcDateTime);
                File.SetLastWriteTimeUtc(outputFile.FullName, created.UtcDateTime);
            }
            finally
            {
                // Clean up if needed
                FileUtility.Delete(tmp.FullName);
            }
        }

        internal static JObject LoadJson(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                // Avoid error prone json.net date handling
                jsonReader.DateParseHandling = DateParseHandling.None;

                return JObject.Load(jsonReader);
            }
        }
    }
}
