using System;
using NuGet.Common;

namespace NuGetMirror
{
    public class FilterLogger : ILogger
    {
        public bool Enabled { get; set; } = true;

        public LogLevel VerbosityLevel { get; set; }

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

        public void LogDebug(string data)
        {
            if (Enabled && (int)LogLevel.Debug >= (int)VerbosityLevel)
            {
                OutputLogger.LogDebug(data);
            }
        }

        public void LogError(string data)
        {
            if (Enabled && (int)LogLevel.Error >= (int)VerbosityLevel)
            {
                OutputLogger.LogError(data);
            }
        }

        public void LogErrorSummary(string data)
        {
            // Noop
        }

        public void LogInformation(string data)
        {
            if (Enabled && (int)LogLevel.Information >= (int)VerbosityLevel)
            {
                OutputLogger.LogInformation(data);
            }
        }

        public void LogInformationSummary(string data)
        {
            // Noop
        }

        public void LogMinimal(string data)
        {
            if (Enabled && (int)LogLevel.Minimal >= (int)VerbosityLevel)
            {
                OutputLogger.LogMinimal(data);
            }
        }

        public void LogSummary(string data)
        {
            // Noop
        }

        public void LogVerbose(string data)
        {
            if (Enabled && (int)LogLevel.Verbose >= (int)VerbosityLevel)
            {
                OutputLogger.LogVerbose(data);
            }
        }

        public void LogWarning(string data)
        {
            if (Enabled && (int)LogLevel.Warning >= (int)VerbosityLevel)
            {
                OutputLogger.LogWarning(data);
            }
        }
    }
}
