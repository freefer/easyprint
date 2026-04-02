using Newtonsoft.Json;
using SuperSocket.Command;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using SuperSocket.WebSocket;
using SuperSocket.WebSocket.Server;

namespace EasyPrint
{
    public class EasyPrintSession : WebSocketSession
    {

        public EasyPrintSession()
        {


        }
        protected override ValueTask OnSessionConnectedAsync()
        {
            var a = this.Connection.RemoteEndPoint;
            return base.OnSessionConnectedAsync();
        }
        protected override ValueTask OnSessionClosedAsync(CloseEventArgs e)
        {

            return base.OnSessionClosedAsync(e);
        }
        public EasyPrintService AppServer
        {
            get
            {
                return (EasyPrintService)this.Server;
            }
        }


        public ValueTask SendMessage(PrintResponseMessage package)
        {

            var data = JsonConvert.SerializeObject(package);
            return base.SendAsync(data);

        }
    }

 

    
}
