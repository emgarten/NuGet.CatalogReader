using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetMirror
{
    public class ConsoleLogger : LoggerBase
    {
        private static readonly object _lockObj = new();

        public ConsoleLogger() : this(LogLevel.Debug)
        {
        }

        public ConsoleLogger(LogLevel level)
        {
            VerbosityLevel = level;
        }

        public override void Log(ILogMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (message.Level >= VerbosityLevel)
            {
                CmdUtils.LogToConsole(message.Level, message.Message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}
