using NuGet.Protocol;

namespace NuGet.CatalogReader.Tests
{
    public class TestHttpSource : HttpSourceResource
    {
        public TestHttpSource(HttpSource httpSource)
            : base(httpSource)
        {
        }
    }
}