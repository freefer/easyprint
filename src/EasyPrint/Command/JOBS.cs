using SuperSocket.ProtoBase;

namespace EasyPrint.Command
{
    /// <summary>
    /// 查询指定打印机的打印队列任务列表。
    ///
    /// 客户端发送（printerName 可选，留空使用默认打印机）：
    /// <code>JOBS {"printerName":"TSC TE200"}</code>
    /// 或无参数形式：<code>JOBS</code>
    ///
    /// 响应示例：
    /// <code>
    /// {
    ///   "command": "JOBS",
    ///   "status": 200,
    ///   "message": "共 2 个任务",
    ///   "data": [
    ///     { "jobId":1, "printerName":"TSC TE200", "document":"标签", "userName":"Admin",
    ///       "statusLabel":"打印中", "status":16, "totalPages":1, "pagesPrinted":0,
    ///       "priority":1, "position":1 }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    internal class JOBS : JsonCommandBase<JobsRequest?>
    {
        protected override async ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session,
            JobsRequest? content,
            CancellationToken cancellationToken)
        {
            try
            {
                string? printerName = content?.PrinterName;
                var jobs = PrinterHelper.GetPrintJobs(
                    string.IsNullOrWhiteSpace(printerName) ? null : printerName);

                await session.SendMessage(new PrintResponseMessage
                {
                    command = "JOBS",
                    status  = 200,
                    message = $"共 {jobs.Count} 个任务",
                    data    = jobs,
                });
            }
            catch (Exception ex)
            {
                await session.SendMessage(new PrintResponseMessage
                {
                    command = "JOBS",
                    status  = 500,
                    message = $"查询打印队列失败: {ex.Message}",
                    data    = Array.Empty<object>(),
                });
            }
        }
    }

    public class JobsRequest
    {
        public string? PrinterName { get; set; }
    }
}
