using System.IO;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetMirror
{
    public class FileLogger : LoggerBase
    {
        private static readonly object _lockObj = new();

        public bool Enabled { get; set; } = true;
        public ILogger OutputLogger { get; }
        public string OutputPath { get; }
        public LogLevel FileLoggerVerbosity { get; set; } = LogLevel.Error;

        public FileLogger(ILogger output, string outputPath)
            : this(output, LogLevel.Debug, outputPath)
        {
        }

        public FileLogger(ILogger output, LogLevel level, string outputPath)
            : base(LogLevel.Debug)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

            FileLoggerVerbosity = level;
            OutputLogger = output;
            OutputPath = outputPath;
        }

        public override void Log(ILogMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            OutputLogger.Log(message);

            if (message.Level >= FileLoggerVerbosity)
            {
                lock (_lockObj)
                {
                    using var writer = new StreamWriter(
                        File.Open(OutputPath, FileMode.Append, FileAccess.Write));
                    writer.WriteLine(message.Message);
                }
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}
