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
            
            var json = package.Parameters!=null&& package.Parameters.Length >= 1 ? package.Parameters[0] : package.Body;

            var data = string.IsNullOrWhiteSpace(json)
                ? (T)(object)package                       // 只有 Key，无体 → 将 package 直接作为 T 传入（适用于 T = StringPackageInfo 的命令）
                : JsonConvert.DeserializeObject<T>(json);  // 有 JSON 体 → 反序列化为 T

            return ExecuteJsonCommandAsync(session, data, cancellationToken);
        }

        /// <summary>
        /// 统一入口。
        /// <list type="bullet">
        ///   <item><c>COMMAND {…}</c> → <paramref name="content"/> 为反序列化后的 T 实例</item>
        ///   <item><c>COMMAND</c>（无体）→ <paramref name="content"/> 为 <c>default(T)</c>（引用类型为 null）</item>
        /// </list>
        /// </summary>
        protected abstract ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session, T content, CancellationToken cancellationToken);
    }


}
