using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    /// <summary>
    /// Mirror a feed to disk as a folder of nupkgs.
    /// </summary>
    public static class MirrorUtility
    {
        private const string CursorFile = "cursor.json";

        /// <summary>
        /// cursor.json path
        /// </summary>
        public static FileInfo GetCursorFile(DirectoryInfo root)
        {
            return new FileInfo(Path.Combine(root.FullName, CursorFile));
        }

        /// <summary>
        /// Load cursor.json if it exists.
        /// If it doesn't exist MinTime is returned.
        /// </summary>
        public static DateTimeOffset LoadCursor(DirectoryInfo root)
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
        public static void SaveCursor(DirectoryInfo root, DateTimeOffset time)
        {
            var file = GetCursorFile(root);

            FileUtility.Delete(file.FullName);

            var json = new JObject()
            {
                { "cursor", time.ToString("o") }
            };

            File.WriteAllText(file.FullName, json.ToString());
        }

        internal static void SetTempRoot(this SourceCacheContext context, string path)
        {
            var folderProp = typeof(SourceCacheContext)
               .GetField("_generatedTempFolder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            folderProp.SetValue(context, path);
        }
    }
}
