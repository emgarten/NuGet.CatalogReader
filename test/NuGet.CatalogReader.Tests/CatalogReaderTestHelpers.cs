using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Sleet;

namespace NuGet.CatalogReader.Tests
{
    public static class CatalogReaderTestHelpers
    {
        public static SourceRepository GetSource(Uri baseUri, PhysicalFileSystem fileSystem)
        {
            //var providers = Repository.Provider.GetCoreV3().ToList();
            //providers.RemoveAll(e => e.Value.ResourceType == typeof(HttpSourceResourceProvider));
            var providers = new List<Lazy<INuGetResourceProvider>>
            {
                // HttpSource -> PhysicalFileSystem adapter
                new Lazy<INuGetResourceProvider>(() => new TestHttpSourceResourceProvider(fileSystem))
            };
            return new SourceRepository(new PackageSource(baseUri.AbsoluteUri + "index.json"), providers);
        }

        public static HttpSource GetHttpSource(LocalCache cache, string outputRoot, Uri baseUri)
        {
            var fileSystem = new PhysicalFileSystem(cache, UriUtility.CreateUri(outputRoot), baseUri);

            return TestHttpSourceResourceProvider.CreateSource(new Uri(baseUri + "index.json"), fileSystem);
        }

        public static JObject CreateConfigWithLocal(string sourceName, string sourcePath, string baseUri)
        {
            // Create the config template
            var json = new JObject();

            json.Add("username", "test");
            json.Add("useremail", "test@tempuri.org");

            var sourcesArray = new JArray();
            json.Add("sources", sourcesArray);

            var folderJson = new JObject();

            folderJson.Add("name", sourceName);
            folderJson.Add("type", "local");
            folderJson.Add("path", sourcePath);

            if (!string.IsNullOrEmpty(baseUri))
            {
                folderJson.Add("baseURI", baseUri);
            }

            sourcesArray.Add(folderJson);

            return json;
        }
    }
}
