using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.CatalogReader
{
    public class ConsoleLogger : LoggerBase
    {
        private static readonly object _lockObj = new object();

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
            var color = GetColor(message.Level);

            if ((int)message.Level >= (int)VerbosityLevel)
            {
                lock (_lockObj)
                {
                    if (color.HasValue)
                    {
                        Console.ForegroundColor = color.Value;
                    }

                    Console.WriteLine(message.Message);
                    Console.ResetColor();
                }
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);

            return Task.FromResult(0);
        }

        private static ConsoleColor? GetColor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
            }

            return null;
        }
    }
}