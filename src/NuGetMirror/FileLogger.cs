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

        public override void Log(ILogMessage message)
        {
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
