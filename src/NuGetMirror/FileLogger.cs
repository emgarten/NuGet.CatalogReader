using System;
using System.IO;
using NuGet.Common;

namespace NuGetMirror
{
    public class FileLogger : ILogger
    {
        private static readonly Object _lockObj = new object();

        public LogLevel VerbosityLevel { get; set; }

        public bool Enabled { get; set; } = true;

        public ILogger OutputLogger { get; }

        public string OutputPath { get; }

        public FileLogger(ILogger output, string outputPath)
            : this(output, LogLevel.Error, outputPath)
        {
        }

        public FileLogger(ILogger output, LogLevel level, string outputPath)
        {
            VerbosityLevel = level;
            OutputLogger = output;
            OutputPath = outputPath;
        }

        public void LogDebug(string data)
        {
            OutputLogger.LogDebug(data);

            Log(LogLevel.Debug, data);
        }

        public void LogError(string data)
        {
            OutputLogger.LogError(data);

            Log(LogLevel.Error, data);
        }

        public void LogErrorSummary(string data)
        {
            OutputLogger.LogErrorSummary(data);
        }

        public void LogInformation(string data)
        {
            OutputLogger.LogInformation(data);

            Log(LogLevel.Information, data);
        }

        public void LogInformationSummary(string data)
        {
            OutputLogger.LogInformationSummary(data);
        }

        public void LogMinimal(string data)
        {
            OutputLogger.LogMinimal(data);

            Log(LogLevel.Minimal, data);
        }

        public void LogVerbose(string data)
        {
            OutputLogger.LogVerbose(data);

            Log(LogLevel.Verbose, data);
        }

        public void LogWarning(string data)
        {
            OutputLogger.LogWarning(data);

            Log(LogLevel.Warning, data);
        }

        private void Log(LogLevel level, string message)
        {
            if ((int)level >= (int)VerbosityLevel)
            {
                lock (_lockObj)
                {
                    using (var writer = new StreamWriter(File.Open(OutputPath, FileMode.Append, FileAccess.Write)))
                    {
                        writer.WriteLine(message);
                    }
                }
            }
        }
    }
}
