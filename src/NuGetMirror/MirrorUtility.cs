using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Protocol;

namespace NuGetMirror
{
    /// <summary>
    /// Mirror a feed to disk as a folder of nupkgs.
    /// </summary>
    internal static class MirrorUtility
    {
        private const string CursorFile = "cursor.json";
        
        /// <summary>
        /// cursor.json path
        /// </summary>
        internal static FileInfo GetCursorFile(DirectoryInfo root)
        {
            return new FileInfo(Path.Combine(root.FullName, CursorFile));
        }

        /// <summary>
        /// Load cursor.json if it exists.
        /// If it doesn't exist MinTime is returned.
        /// </summary>
        internal static DateTimeOffset LoadCursor(DirectoryInfo root)
        {
            var file = GetCursorFile(root);

            if (file.Exists)
            {
                var json = LoadJson(file.OpenRead());

                return DateTimeOffset.Parse(json["cursor"].ToObject<string>());
            }

            return DateTimeOffset.MinValue;
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

        /// <summary>
        /// Write cursor.json to disk.
        /// </summary>
        internal static void SaveCursor(DirectoryInfo root, DateTimeOffset time)
        {
            var file = GetCursorFile(root);

            FileUtility.Delete(file.FullName);

            var json = new JObject()
            {
                { "cursor", time.ToString("o") }
            };

            File.WriteAllText(file.FullName, json.ToString());
        }

        internal static void LogExceptionAsWarning(Exception ex, ILogger log)
        {
            if (ex is AggregateException ag)
            {
                foreach (var inner in ag.InnerExceptions)
                {
                    LogExceptionAsWarning(inner, log);
                }
            }
            else
            {
                log.LogWarning(ex.Message);
                log.LogDebug(ex.ToString());
            }
        }
    }
}
