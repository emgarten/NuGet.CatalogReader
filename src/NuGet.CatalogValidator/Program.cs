using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.CatalogValidator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var logLevel = LogLevel.Information;

            if (Environment.GetEnvironmentVariable("NuGet.CatalogValidator_DEBUG") == "1")
            {
                logLevel = LogLevel.Debug;
            }

            var log = new ConsoleLogger(logLevel);

            var task = MainCore(args, log);
            return task.Result;
        }

        public static Task<int> MainCore(string[] args, ILogger log)
        {
            return MainCore(args, httpSource: null, log: log);
        }

        public static Task<int> MainCore(string[] args, HttpSource httpSource, ILogger log)
        {
#if DEBUG
            if (args.Contains("--debug"))
            {
                args = args.Skip(1).ToArray();
                while (!Debugger.IsAttached)
                {
                }

                Debugger.Break();
            }
#endif

            var assemblyVersion = NuGetVersion.Parse(typeof(Program).GetTypeInfo().Assembly.GetName().Version.ToString());

            var app = new CommandLineApplication()
            {
                Name = "NuGet.CatalogValidator",
                FullName = "nuget mirror"
            };
            app.HelpOption(Constants.HelpOption);
            app.VersionOption("--version", assemblyVersion.ToNormalizedString());
            app.Description = "Validate a nuget v3 feed.";

            Configure();

            ValidateCommand.Register(app, httpSource, log);

            app.OnExecute(() =>
            {
                app.ShowHelp();

                return 0;
            });

            var exitCode = 0;

            try
            {
                exitCode = app.Execute(args);
            }
            catch (CommandParsingException ex)
            {
                ex.Command.ShowHelp();
            }
            catch (Exception ex)
            {
                exitCode = 1;

                LogException(ex, log);
            }

            return Task.FromResult(exitCode);
        }

        private static void LogException(Exception ex, ILogger log)
        {
            if (ex is AggregateException ag)
            {
                foreach (var inner in ag.InnerExceptions)
                {
                    LogException(inner, log);
                }
            }
            else
            {
                log.LogError($"[{ex.GetType()}] {ex.Message}");
                log.LogDebug(ex.ToString());
            }
        }

        private static void Configure()
        {
#if IS_DESKTOP
            // Set connection limit
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                ServicePointManager.DefaultConnectionLimit = 64;
            }
            else
            {
                // Keep mono limited to a single download to avoid issues.
                ServicePointManager.DefaultConnectionLimit = 1;
            }

            // Limit SSL
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls |
                SecurityProtocolType.Tls11 |
                SecurityProtocolType.Tls12;
#endif

            UserAgent.SetUserAgentString(new UserAgentStringBuilder("NuGet.CatalogValidator"));
        }
    }
}