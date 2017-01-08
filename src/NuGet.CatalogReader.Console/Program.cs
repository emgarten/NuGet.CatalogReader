using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CatalogReader
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var reader = new CatalogReader(new Uri("https://api.nuget.org/v23/index.json"));

                var entries = reader.GetEntriesAsync(DateTimeOffset.Parse("2017-01-08T00:09:42.4368899Z"), DateTimeOffset.Parse("2017-01-09T21:09:42.4368899Z"), CancellationToken.None).Result;

                foreach (var entry in entries)
                {
                    Console.WriteLine(entry);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
