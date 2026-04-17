using SuperSocket.ProtoBase;

namespace EasyPrint.Command
{
    /// <summary>
    /// 取消（删除）打印队列中的一个或多个任务。
    ///
    /// 客户端发送（单个）：
    /// <code>CANCEL {"printerName":"TSC TE200","jobIds":[5]}</code>
    ///
    /// 客户端发送（批量）：
    /// <code>CANCEL {"printerName":"TSC TE200","jobIds":[5,6,7]}</code>
    ///
    /// 响应示例：
    /// <code>
    /// {
    ///   "command": "CANCEL",
    ///   "status": 200,
    ///   "message": "共 3 个任务：3 成功，0 失败",
    ///   "data": [{"jobId":5,"ok":true},{"jobId":6,"ok":true},{"jobId":7,"ok":false}]
    /// }
    /// </code>
    /// </summary>
    internal class CANCEL : JsonCommandBase<JobControlRequest>
    {
        protected override async ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session,
            JobControlRequest content,
            CancellationToken cancellationToken)
        {
            try
            {
                string printerName = Resolveprinter(content?.PrinterName);
                var jobIds = content?.JobIds ?? [];

                var results = jobIds.Select(id => new
                {
                    jobId = id,
                    ok    = PrinterHelper.CancelPrintJob(printerName, id),
                }).ToList();

                int successCount = results.Count(r => r.ok);
                int failCount    = results.Count - successCount;

                await session.SendMessage(new PrintResponseMessage
                {
                    command = "CANCEL",
                    status  = failCount == 0 ? 200 : (successCount == 0 ? 500 : 207),
                    message = $"共 {results.Count} 个任务：{successCount} 成功，{failCount} 失败",
                    data    = results,
                });
            }
            catch (Exception ex)
            {
                await session.SendMessage(new PrintResponseMessage
                {
                    command = "CANCEL",
                    status  = 500,
                    message = $"取消失败: {ex.Message}",
                    data    = Array.Empty<object>(),
                });
            }
        }

        private static string Resolveprinter(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return PrinterHelper.GetDefaultPrinterName()
                ?? throw new InvalidOperationException("未能获取打印机名称");
        }
    }
}
