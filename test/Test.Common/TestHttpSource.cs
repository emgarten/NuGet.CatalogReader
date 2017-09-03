using NuGet.Protocol;

namespace Test.Common
{
    public class TestHttpSource : HttpSourceResource
    {
        public TestHttpSource(HttpSource httpSource)
            : base(httpSource)
        {
        }
    }
}