using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

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

        internal static string GetExceptions(Exception ex, string prefix)
        {
            var sb = new StringBuilder();

            if (ex is AggregateException ag)
            {
                foreach (var inner in ag.InnerExceptions)
                {
                    sb.Append(GetExceptions(inner, prefix));
                }
            }
            else
            {
                sb.AppendLine(prefix + ex.Message);
            }

            return sb.ToString();
        }

        internal static Regex WildcardToRegex(string pattern)
        {
            var s = "^" + Regex.Escape(pattern).
                                Replace("\\*", ".*").
                                Replace("\\?", ".") + "$";

            return new Regex(s, RegexOptions.IgnoreCase);
        }

        internal static void SetTempRoot(this SourceCacheContext context, string path)
        {
            var folderProp = typeof(SourceCacheContext)
               .GetProperty("GeneratedTempFolder", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var all = typeof(SourceCacheContext)
               .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var members = typeof(SourceCacheContext)
                    .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var fields = typeof(SourceCacheContext)
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var setMember = fields.FirstOrDefault(e => e.Name == "<GeneratedTempFolder>k__BackingField");

            setMember.SetValue(context, path);
        }
    }
}
