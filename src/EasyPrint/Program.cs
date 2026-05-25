using EasyPrint.Command;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket.Server;
using System.Runtime;
using System.Security.Authentication;

namespace EasyPrint
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            var cfg = AppDataContext.LoadConfig();
            var sslPath = Path.Combine(AppDataContext.AssemblyDirPath, cfg.SSLCertPath);
            var loggerProvider = new UiLoggerProvider();
            var host = WebSocketHostBuilder.Create()
                .ConfigureSuperSocket((options) =>
                {
                    options.Name = "EasyPrintServer";
                    options.Listeners = new List<ListenOptions>() {

                        new ListenOptions() { Ip = cfg.Ip, Port = cfg.Port},
                        new ListenOptions() { Ip = cfg.Ip, Port = cfg.SSLPort,
                            AuthenticationOptions =new ServerAuthenticationOptions(){
                                EnabledSslProtocols=SslProtocols.Tls11 |SslProtocols.Tls12|SslProtocols.Tls13,
                                CertificateOptions=new CertificateOptions(){
                                   FilePath=sslPath ,
                                   Password=cfg.SSLPassword,
                                   }
                            }
                         },

                };
                })
            .UseSession<EasyPrintSession>()
            .UseHostedService<EasyPrintService>()
            .ConfigureErrorHandler((session, pack) =>
            {
                var message = $"{pack.Message} message: {pack.Package.Message}";
                loggerProvider.EmitLog(message, LogLevel.Error);
                return ValueTask.FromResult(false);
            })
            .UseCommand<StringPackageInfo, JsonPackageConverter>(commandOptions =>
            {
                commandOptions.AddCommand<LIST>();
                commandOptions.AddCommand<PRINT>();
                commandOptions.AddCommand<JOBS>();
                commandOptions.AddCommand<CANCEL>();
                commandOptions.AddCommand<RESTART>();
                commandOptions.AddCommand<PAUSE>();
                commandOptions.AddCommand<RESUME>();
            })
            .UseInProcSessionContainer()
            .ConfigureLogging((hostCtx, loggingBuilder) =>
            {
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddDebug();

                loggingBuilder.AddProvider(loggerProvider);
            });

            var form = new Form1(cfg, host, loggerProvider);
            Application.Run(form);
        }
    }
}