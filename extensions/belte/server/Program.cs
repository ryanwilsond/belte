using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;

namespace BelteLspServer;

internal static class Program {
    private static void Main(string[] args) {
        MainAsync(args).Wait();
    }

    private static async Task MainAsync(string[] args) {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .MinimumLevel.Verbose()
            .CreateLogger();

        var server = await LanguageServer.From(
            options =>
                options
                   .WithInput(Console.OpenStandardInput())
                   .WithOutput(Console.OpenStandardOutput())
                   .ConfigureLogging(
                        x => x
                            .AddSerilog(Log.Logger)
                            .AddLanguageProtocolLogging()
                            .SetMinimumLevel(LogLevel.Debug)
                    )
                   .WithHandler<TextDocumentHandler>()
                   .WithHandler<SemanticTokensHandler>()
                   .WithServices(services => { services.AddSingleton<CompilationManager>(); })
                   .WithServices(x => x.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace)))
                   .WithServices(
                        services => {
                            services.AddSingleton(
                                new ConfigurationItem {
                                    Section = "typescript",
                                }
                            ).AddSingleton(
                                new ConfigurationItem {
                                    Section = "terminal",
                                }
                            );
                        }
                    )
                   .OnStarted(
                        async (languageServer, token) => {
                            using var manager = await languageServer.WorkDoneManager.Create(
                                new WorkDoneProgressBegin { Title = "Doing some work..." }
                            ).ConfigureAwait(false);

                            var configuration = await languageServer.Configuration.GetConfiguration(
                                new ConfigurationItem {
                                    Section = "typescript",
                                }, new ConfigurationItem {
                                    Section = "terminal",
                                }
                            ).ConfigureAwait(false);

                            var baseConfig = new JObject();

                            foreach (var config in languageServer.Configuration.AsEnumerable())
                                baseConfig.Add(config.Key, config.Value);

                            var scopedConfig = new JObject();

                            foreach (var config in configuration.AsEnumerable())
                                scopedConfig.Add(config.Key, config.Value);
                        }
                    )
        ).ConfigureAwait(false);

        await server.WaitForExit.ConfigureAwait(false);
    }
}
