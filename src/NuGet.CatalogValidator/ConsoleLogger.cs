using System.Threading.Tasks;
using NuGet.CatalogValidator;
using NuGet.Common;

namespace NuGet.CatalogValidator
{
    public class ConsoleLogger : LoggerBase
    {
        public ConsoleLogger()
            : this(LogLevel.Debug)
        {
        }

        public ConsoleLogger(LogLevel level)
        {
            VerbosityLevel = level;
        }

        public override void Log(ILogMessage message)
        {
            if ((int)message.Level >= (int)VerbosityLevel)
            {
                CmdUtils.LogToConsole(message.Level, message.Message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);

            return Task.FromResult(0);
        }
    }
}