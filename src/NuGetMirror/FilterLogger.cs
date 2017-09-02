using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetMirror
{
    public class FilterLogger : LoggerBase
    {
        public bool Enabled { get; set; } = true;

        public ILogger OutputLogger { get; }

        public FilterLogger(ILogger output)
            : this(output, LogLevel.Error)
        {
        }

        public FilterLogger(ILogger output, LogLevel level)
        {
            VerbosityLevel = level;
            OutputLogger = output;
        }

        public override void Log(ILogMessage message)
        {
            if (Enabled && (int)message.Level >= (int)VerbosityLevel)
            {
                OutputLogger.Log(message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            if (Enabled && (int)message.Level >= (int)VerbosityLevel)
            {
                return OutputLogger.LogAsync(message);
            }

            return Task.FromResult(0);
        }
    }
}
