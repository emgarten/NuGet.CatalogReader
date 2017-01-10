using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.CatalogReader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var log = new ConsoleLogger();

                var reader = new CatalogReader(new Uri("https://api.nuget.org/v3/index.json"), TimeSpan.FromHours(0), log);
                var entries = reader.GetFlattenedEntriesAsync(DateTimeOffset.Parse("2017-01-02"), DateTimeOffset.Parse("2017-01-03"), CancellationToken.None).Result;

                foreach (var group in entries.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
                {
                    var entry = group.First();

                    entry.DownloadNupkgAsync("d:\\tmp\\out");
                    entry.DownloadNuspecAsync("d:\\tmp\\out");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
