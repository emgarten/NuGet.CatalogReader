using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGetMirror
{
    public static class MirrorUtility
    {
        private const string CursorFile = "cursor.json";

        public static FileInfo GetCursorFile(DirectoryInfo root)
        {
            ArgumentNullException.ThrowIfNull(root);
            return new FileInfo(Path.Combine(root.FullName, CursorFile));
        }

        public static DateTimeOffset LoadCursor(DirectoryInfo root)
        {
            ArgumentNullException.ThrowIfNull(root);

            var file = GetCursorFile(root);

            if (file.Exists)
            {
                using var stream = file.OpenRead();
                var json = LoadJson(stream);
                var cursorValue = json["cursor"]?.ToObject<string>();

                if (!string.IsNullOrEmpty(cursorValue))
                {
                    return DateTimeOffset.Parse(cursorValue, CultureInfo.InvariantCulture);
                }
            }

            return DateTimeOffset.MinValue;
        }

        internal static JObject LoadJson(Stream stream)
        {
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader)
            {
                DateParseHandling = DateParseHandling.None
            };

            return JObject.Load(jsonReader);
        }

        public static void SaveCursor(DirectoryInfo root, DateTimeOffset time)
        {
            ArgumentNullException.ThrowIfNull(root);

            var file = GetCursorFile(root);
            FileUtility.Delete(file.FullName);

            var json = new JObject
            {
                ["cursor"] = time.ToString("o")
            };

            File.WriteAllText(file.FullName, json.ToString());
        }

        internal static void SetTempRoot(this SourceCacheContext context, string path)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var folderProp = typeof(SourceCacheContext)
               .GetField("_generatedTempFolder",
                   BindingFlags.Instance | BindingFlags.NonPublic)
               ?? throw new InvalidOperationException("Field _generatedTempFolder not found");

            folderProp.SetValue(context, path);
        }
    }
}
