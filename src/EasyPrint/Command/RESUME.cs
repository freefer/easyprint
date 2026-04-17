namespace EasyPrint.Command
{
    /// <summary>
    /// 继续（恢复）打印队列中已暂停的一个或多个任务。
    ///
    /// 客户端发送（单个）：
    /// <code>RESUME {"printerName":"TSC TE200","jobIds":[5]}</code>
    ///
    /// 客户端发送（批量）：
    /// <code>RESUME {"printerName":"TSC TE200","jobIds":[5,6,7]}</code>
    ///
    /// 响应示例：
    /// <code>
    /// {
    ///   "command": "RESUME",
    ///   "status": 200,
    ///   "message": "共 2 个任务：2 成功，0 失败",
    ///   "data": [{"jobId":5,"ok":true},{"jobId":6,"ok":true}]
    /// }
    /// </code>
    /// </summary>
    internal class RESUME : JsonCommandBase<JobControlRequest>
    {
        protected override async ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session,
            JobControlRequest content,
            CancellationToken cancellationToken)
        {
            try
            {
                string printerName = RESTART.ResolvePrinter(content?.PrinterName);
                var jobIds = content?.JobIds ?? [];

                var results = jobIds.Select(id => new
                {
                    jobId = id,
                    ok    = PrinterHelper.ResumePrintJob(printerName, id),
                }).ToList();

                int successCount = results.Count(r => r.ok);
                int failCount    = results.Count - successCount;

                await session.SendMessage(new PrintResponseMessage
                {
                    command = "RESUME",
                    status  = failCount == 0 ? 200 : (successCount == 0 ? 500 : 207),
                    message = $"共 {results.Count} 个任务：{successCount} 成功，{failCount} 失败",
                    data    = results,
                });
            }
            catch (Exception ex)
            {
                await session.SendMessage(new PrintResponseMessage
                {
                    command = "RESUME",
                    status  = 500,
                    message = $"继续失败: {ex.Message}",
                    data    = Array.Empty<object>(),
                });
            }
        }
    }
}
