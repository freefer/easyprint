using Newtonsoft.Json;
using SuperSocket.ProtoBase;
using System.Drawing.Printing;

namespace EasyPrint.Command
{
    /// <summary>
    /// 枚举本机所有已安装打印机并返回列表。
    ///
    /// 客户端发送：<c>LIST</c>（无需 JSON 体）
    ///
    /// 响应示例：
    /// <code>
    /// {
    ///   "command": "LIST",
    ///   "status": 200,
    ///   "message": "共 3 台打印机",
    ///   "data": "[{\"name\":\"TSC TE200\",\"isDefault\":true},{\"name\":\"Microsoft Print to PDF\",\"isDefault\":false}]"
    /// }
    /// </code>
    /// </summary>
    internal class LIST : JsonCommandBase<StringPackageInfo>
    {
        protected override async ValueTask ExecuteJsonCommandAsync(
            EasyPrintSession session,
            StringPackageInfo content,
            CancellationToken cancellationToken)
        {
            try
            {
                var defaultPrinter = PrinterHelper.GetDefaultPrinterName();

                // 枚举本机所有已安装的打印机
                var printers = PrinterSettings.InstalledPrinters
                    .Cast<string>()
                    .Select(name => new
                    {
                        name,
                        isDefault = string.Equals(name, defaultPrinter,
                                        StringComparison.OrdinalIgnoreCase)
                    })
                    .ToList();

                await session.SendMessage(new PrintResponseMessage
                {
                    command = "LIST",
                    status  = 200,
                    message = $"共 {printers.Count} 台打印机",
                    data    = JsonConvert.SerializeObject(printers)
                });
            }
            catch (Exception ex)
            {
                await session.SendMessage(new PrintResponseMessage
                {
                    command = "LIST",
                    status  = 500,
                    message = $"获取打印机列表失败: {ex.Message}",
                    data    = "[]"
                });
            }
        }
    }
}
