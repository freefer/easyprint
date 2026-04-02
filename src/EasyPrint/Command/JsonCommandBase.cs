using Newtonsoft.Json;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.WebSocket.Server;

namespace EasyPrint.Command
{
    public abstract class JsonCommandBase<T> : IAsyncCommand<EasyPrintSession, StringPackageInfo>
    {
        public ValueTask ExecuteAsync(EasyPrintSession session, StringPackageInfo package, CancellationToken cancellationToken)
        {
            var context = package.Parameters[0];
            var data = JsonConvert.DeserializeObject<T>(context);
            return ExecuteJsonCommandAsync(session, data, cancellationToken);
        }

        protected abstract ValueTask ExecuteJsonCommandAsync(EasyPrintSession session, T content, CancellationToken cancellationToken);
    }


}
