using SuperSocket.ProtoBase;

namespace EasyPrint.Command
{
    /// <summary>
    /// 重启打印队列中的一个或多个任务（从头开始重新打印）。
    ///
    /// 客户端发送（单个）：
    /// <code>RESTART {"printerName":"TSC TE200","jobIds":[5]}</code>
    ///
    /// 客户端发送（批量）：
    /// <code>RESTART {"printerName":"TSC TE200","jobIds":[5,6,7]}</code>
    ///
    /// 响应示例：
    /// <code>
    /// {
    ///   "command": "RESTART",
    ///   "status": 200,
    ///   "message": "共 3 个任务：3 成功，0 失败",
    ///   "data": [{"jobId":5,"ok":true},{"jobId":6,"ok":true},{"jobId":7,"ok":false}]
    /// }
    /// </code>
    /// </summary>
    internal class RESTART : JsonCommandBase<JobControlRequest>
    {
        protected override async ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session,
            JobControlRequest content,
            CancellationToken cancellationToken)
        {
            try
            {
                string printerName = ResolvePrinter(content?.PrinterName);
                var jobIds = content?.JobIds ?? [];

                var results = jobIds.Select(id => new
                {
                    jobId = id,
                    ok    = PrinterHelper.RestartPrintJob(printerName, id),
                }).ToList();

                int successCount = results.Count(r => r.ok);
                int failCount    = results.Count - successCount;

                await session.SendMessage(new PrintResponseMessage
                {
                    command = "RESTART",
                    status  = failCount == 0 ? 200 : (successCount == 0 ? 500 : 207),
                    message = $"共 {results.Count} 个任务：{successCount} 成功，{failCount} 失败",
                    data    = results,
                });
            }
            catch (Exception ex)
            {
                await session.SendMessage(new PrintResponseMessage
                {
                    command = "RESTART",
                    status  = 500,
                    message = $"重启失败: {ex.Message}",
                    data    = Array.Empty<object>(),
                });
            }
        }

        internal static string ResolvePrinter(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return PrinterHelper.GetDefaultPrinterName()
                ?? throw new InvalidOperationException("未能获取打印机名称");
        }
    }

    /// <summary>CANCEL / RESTART / PAUSE / RESUME 命令公用请求体。</summary>
    public class JobControlRequest
    {
        public string?  PrinterName { get; set; }
        /// <summary>要操作的任务 ID 列表，支持单个或多个。</summary>
        public int[] JobIds { get; set; } = [];
    }
}
