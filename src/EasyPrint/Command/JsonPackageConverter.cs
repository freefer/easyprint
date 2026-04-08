using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.WebSocket;

namespace EasyPrint.Command
{
    public class JsonPackageConverter : IPackageMapper<WebSocketPackage, StringPackageInfo>
    {
        public StringPackageInfo Map(WebSocketPackage package)
        {

            var pack = new StringPackageInfo();
            var arr = package.Message.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            pack.Key = arr[0];
            if (arr.Length > 1)
                pack.Parameters = arr.Skip(1).ToArray();
            return pack;
        }
    }


}
