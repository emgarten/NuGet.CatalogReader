using NuGet.Common;
using Xunit;

namespace Test.Common
{
    public sealed class WindowsFactAttribute
        : FactAttribute
    {
        public override string Skip => RuntimeEnvironmentHelper.IsWindows ? null : "Windows only test";
    }

    public sealed class WindowsTheoryAttribute
    : TheoryAttribute
    {
        public override string Skip => RuntimeEnvironmentHelper.IsWindows ? null : "Windows only test";
    }
}
