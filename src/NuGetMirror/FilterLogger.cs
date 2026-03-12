using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetMirror
{
    public class FilterLogger : LoggerBase
    {
        public bool Enabled { get; set; } = true;
        public ILogger OutputLogger { get; }

        public FilterLogger(ILogger output) : this(output, LogLevel.Error)
        {
        }

        public FilterLogger(ILogger output, LogLevel level)
        {
            ArgumentNullException.ThrowIfNull(output);

            VerbosityLevel = level;
            OutputLogger = output;
        }

        public override void Log(ILogMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (Enabled && message.Level >= VerbosityLevel)
            {
                OutputLogger.Log(message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (Enabled && message.Level >= VerbosityLevel)
            {
                return OutputLogger.LogAsync(message);
            }

            return Task.CompletedTask;
        }
    }
}
