using System;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGetMirror
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var logLevel = CmdUtils.IsDebugModeEnabled()
                ? LogLevel.Debug
                : LogLevel.Information;

            var log = new ConsoleLogger(logLevel);

            return MainCore(args, log).GetAwaiter().GetResult();
        }

        public static async Task<int> MainCore(string[] args, ILogger log)
        {
            return await MainCore(args, httpSource: null, log: log);
        }

        public static async Task<int> MainCore(string[] args, HttpSource? httpSource, ILogger log)
        {
            CmdUtils.LaunchDebuggerIfSet(ref args, log);

            var app = new CommandLineApplication
            {
                Name = "NuGetMirror",
                FullName = "nuget mirror"
            };

            app.HelpOption(Constants.HelpOption);
            app.VersionOption("--version",
                new NuGetVersion(CmdUtils.GetAssemblyVersion()).ToNormalizedString());
            app.Description = "Mirror a nuget v3 feed.";

            Configure();

            ListCommand.Register(app, httpSource, log);
            NupkgsCommand.Register(app, httpSource, log);

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 1;
            });

            try
            {
                return await Task.FromResult(app.Execute(args));
            }
            catch (CommandParsingException ex)
            {
                ex.Command?.ShowHelp();
                return 1;
            }
            catch (Exception ex)
            {
                ExceptionUtils.LogException(ex, log);
                return 1;
            }
        }

        private static void Configure()
        {
            // Настройки для .NET 8/9/10

            var userAgent = new UserAgentStringBuilder("NuGetMirror");
            UserAgent.SetUserAgentString(userAgent);
        }
    }
}
