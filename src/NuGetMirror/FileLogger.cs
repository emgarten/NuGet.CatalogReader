using System.IO;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGetMirror
{
    public class FileLogger : LoggerBase
    {
        private static readonly object _lockObj = new object();

        public bool Enabled { get; set; } = true;

        public ILogger OutputLogger { get; }

        public string OutputPath { get; }

        /// <summary>
        /// Verbosity to filter on for the file.
        /// </summary>
        public LogLevel FileLoggerVerbosity { get; set; } = LogLevel.Error;

        public FileLogger(ILogger output, string outputPath)
            : this(output, LogLevel.Debug, outputPath)
        {
        }

        public FileLogger(ILogger output, LogLevel level, string outputPath)
            : base(LogLevel.Debug)
        {
            FileLoggerVerbosity = level;
            OutputLogger = output;
            OutputPath = outputPath;
        }

        public override void Log(ILogMessage message)
        {
            // Always pass the message to the inner logger.
            OutputLogger.Log(message);

            if ((int)message.Level >= (int)VerbosityLevel)
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

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);

            return Task.FromResult(0);
        }
    }
}
