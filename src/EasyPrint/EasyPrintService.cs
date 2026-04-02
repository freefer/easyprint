using Microsoft.Extensions.Options;
using PuppeteerSharp;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.WebSocket;

namespace EasyPrint
{
    public class EasyPrintService : SuperSocketService<WebSocketPackage>
    {
        public Form1 WorkForm { get; set; }
        public UiLoggerProvider LoggerProvider { get;set; }
        public IBrowser Browser { get;set; }
        public EasyPrintService(IServiceProvider serviceProvider, IOptions<ServerOptions> serverOptions) : base(serviceProvider, serverOptions)
        {
            
        }


        protected override ValueTask OnStartedAsync()
        {

            return base.OnStartedAsync();
        }
    }
}
