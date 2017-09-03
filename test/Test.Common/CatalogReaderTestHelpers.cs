using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol;
using Sleet;

namespace Test.Common
{
    public static class CatalogReaderTestHelpers
    {
        public static async Task CreateCatalogAsync(string root, string feedRoot, string nupkgFolder, Uri baseUri, ILogger log)
        {
            using (var cache = new LocalCache())
            {
                var sleetConfig = CreateSleetConfig(root, feedRoot, baseUri);
                var settings = LocalSettings.Load(sleetConfig);
                var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, "feed");

                var success = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: true, enableSymbols: false, log: log, token: CancellationToken.None);

                if (success != true)
                {
                    throw new InvalidOperationException("Catalog init failed");
                }

                if (Directory.GetFiles(nupkgFolder).Any())
                {
                    success = await PushCommand.PushPackages(settings, fileSystem, new List<string>() { nupkgFolder }, false, false, log, CancellationToken.None);

                    if (success != true)
                    {
                        throw new InvalidOperationException("Push failed");
                    }
                }
            }
        }

        public static async Task PushPackagesAsync(string root, string nupkgFolder, Uri baseUri, ILogger log)
        {
            var sleetConfig = Path.Combine(root, "sleet.json");

            if (Directory.GetFiles(nupkgFolder).Any())
            {
                using (var cache = new LocalCache())
                {
                    var settings = LocalSettings.Load(sleetConfig);
                    var fileSystem = FileSystemFactory.CreateFileSystem(settings, cache, "feed");

                    var success = await PushCommand.PushPackages(settings, fileSystem, new List<string>() { nupkgFolder }, true, false, log, CancellationToken.None);

                    if (success != true)
                    {
                        throw new InvalidOperationException("Push failed");
                    }
                }
            }
        }

        public static string CreateSleetConfig(string root, string feedRoot, Uri baseUri)
        {
            var path = Path.Combine(root, "sleet.json");

            var json = CatalogReaderTestHelpers.CreateConfigWithLocal("feed", feedRoot, baseUri.AbsoluteUri);

            File.WriteAllText(path, json.ToString());

            return path;
        }

        public static HttpSource GetHttpSource(LocalCache cache, string outputRoot, Uri baseUri)
        {
            var fileSystem = new PhysicalFileSystem(cache, Sleet.UriUtility.CreateUri(outputRoot), baseUri);

            return TestHttpSourceResourceProvider.CreateSource(new Uri(baseUri + "index.json"), fileSystem);
        }

        public static JObject CreateConfigWithLocal(string sourceName, string sourcePath, string baseUri)
        {
            // Create the config template
            var json = new JObject
            {
                { "username", "test" },
                { "useremail", "test@tempuri.org" }
            };

            var sourcesArray = new JArray();
            json.Add("sources", sourcesArray);

            var folderJson = new JObject
            {
                { "name", sourceName },
                { "type", "local" },
                { "path", sourcePath }
            };

            if (!string.IsNullOrEmpty(baseUri))
            {
                folderJson.Add("baseURI", baseUri);
            }

            sourcesArray.Add(folderJson);

            return json;
        }
    }
}
