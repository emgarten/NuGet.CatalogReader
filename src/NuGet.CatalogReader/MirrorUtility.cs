using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;

namespace NuGet.CatalogReader
{
    public static class MirrorUtility
    {
        private const string CursorFile = "cursor.json";

        public static async Task<IReadOnlyList<FileInfo>> RunAsync(
            string outputDirectory,
            Uri serviceIndexUri,
            DownloadMode mode,
            DateTimeOffset start,
            DateTimeOffset end,
            int maxThreads,
            ILogger log,
            CancellationToken token)
        {
            using (var catalogReader = new CatalogReader(serviceIndexUri, log))
            {
                var entries = await catalogReader.GetFlattenedEntriesAsync(token);

                var files = await ProcessEntriesUtility.DownloadNupkgsAsync(outputDirectory, mode, maxThreads, token, entries.ToArray());


            }
        }

        public static FileInfo GetCursorFile(DirectoryInfo root)
        {
            return new FileInfo(Path.Combine(root.FullName, CursorFile));
        }

        public static DateTimeOffset LoadCursor(DirectoryInfo root)
        {
            var file = GetCursorFile(root);

            if (file.Exists)
            {
                var json = CatalogReaderUtility.LoadJson(file.OpenRead());

                return DateTimeOffset.Parse(json["cursor"].ToObject<string>());
            }

            return DateTimeOffset.MinValue;
        }

        public static void SaveCursor(DirectoryInfo root, DateTimeOffset time)
        {
            var file = GetCursorFile(root);

            FileUtility.Delete(file.FullName);

            var json = new JObject()
            {
                { "cursor", time.ToString("O") }
            };

            File.WriteAllText(file.FullName, json.ToString());
        }
    }
}
