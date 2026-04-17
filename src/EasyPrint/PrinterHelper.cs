using System.Runtime.InteropServices;
using System.Text;

namespace EasyPrint
{
    /// <summary>
    /// 通过 WinSpool Win32 API 直接读取打印机信息，避免 System.Drawing.Printing.PrinterSettings
    /// 枚举全量纸张列表带来的性能损耗（后者通常耗时 100–500ms，本方案 &lt;1ms）。
    /// </summary>
    internal static class PrinterHelper
    {
        #region Win32 P/Invoke ───────────────────────────────────────────────

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int DocumentProperties(
            IntPtr hwnd, IntPtr hPrinter, string pDeviceName,
            IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumJobs(
            IntPtr hPrinter, uint FirstJob, uint NoJobs, uint Level,
            IntPtr pJob, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetJob(
            IntPtr hPrinter, uint JobId, uint Level, IntPtr pJob, uint Command);

        private const int  DM_OUT_BUFFER = 2;

        // SetJob 控制命令
        private const uint JOB_CONTROL_PAUSE   = 1;
        private const uint JOB_CONTROL_RESUME  = 2;
        private const uint JOB_CONTROL_CANCEL  = 3;
        private const uint JOB_CONTROL_RESTART = 4;
        private const uint JOB_CONTROL_DELETE  = 5;

        // JOB_INFO_1（Unicode）：所有字符串字段均为指向同一缓冲区内部的指针
        [StructLayout(LayoutKind.Sequential)]
        private struct JOB_INFO_1
        {
            public uint   JobId;
            public IntPtr pPrinterName;
            public IntPtr pMachineName;
            public IntPtr pUserName;
            public IntPtr pDocument;
            public IntPtr pDatatype;
            public IntPtr pStatus;   // 驱动自定义状态文本，可为空
            public uint   Status;    // JOB_STATUS_* 位标志
            public uint   Priority;
            public uint   Position;
            public uint   TotalPages;
            public uint   PagesPrinted;
            // SYSTEMTIME Submitted（16 字节）
            public ulong  _st1;
            public ulong  _st2;
        }

        // ── DEVMODEW 关键字段（Unicode，仅声明需要的偏移）────────────────
        // WCHAR dmDeviceName[32]  offset  0  (64 bytes)
        // WORD  dmSpecVersion     offset 64
        // WORD  dmDriverVersion   offset 66
        // WORD  dmSize            offset 68
        // WORD  dmDriverExtra     offset 70
        // DWORD dmFields          offset 72
        // ── union（打印设备分支）──────────────────────────────────────────
        // short dmOrientation     offset 76   1=纵向 2=横向
        // short dmPaperSize       offset 78   纸张类型代码
        // short dmPaperLength     offset 80   纸张高度，单位 0.1mm
        // short dmPaperWidth      offset 82   纸张宽度，单位 0.1mm

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [FieldOffset(0)]
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [FieldOffset(72)] public int   Fields;
            [FieldOffset(76)] public short Orientation;  // 1=Portrait  2=Landscape
            [FieldOffset(78)] public short PaperSize;    // paper kind code
            [FieldOffset(80)] public short PaperLength;  // 0.1 mm
            [FieldOffset(82)] public short PaperWidth;   // 0.1 mm
 
            [FieldOffset(90)] public short PrintQuality;   // x-DPI (>0) or enum (<0: -1=Draft,-4=High)
            [FieldOffset(96)] public short YResolution;    // y-DPI (valid when PrintQuality > 0)
        }

        #endregion

        // ── 已知标准纸张尺寸兜底表（仅在驱动未填 dmPaperWidth/Length 时使用）─
        private static readonly Dictionary<short, (decimal w, decimal h)> _stdSizes = new()
        {
            [1]  = (215.9m, 279.4m), // Letter
            [5]  = (215.9m, 355.6m), // Legal
            [8]  = (279.4m, 431.8m), // Tabloid
            [9]  = (210m,   297m  ), // A4
            [11] = (148m,   210m  ), // A5
            [13] = (182m,   257m  ), // B5
            [18] = (216m,   356m  ), // Note
            [41] = (100m,   148m  ), // A6 (postcard approx.)
        };

        /// <summary>
        /// 获取默认打印机的纸张尺寸（毫米），已根据横/纵向调整宽高。
        /// 失败时返回 A4 (210 × 297 mm)。
        /// </summary>
        public static (decimal widthMm, decimal heightMm) GetDefaultPaperSizeMm()
        {
            const decimal Fallback_W = 210.0m, Fallback_H = 297.0m; // A4
            const decimal TenthMmToMm = 0.1m;

            try
            {
                // ① 获取默认打印机名称
                var sb  = new StringBuilder(256);
                int len = sb.Capacity;
                if (!GetDefaultPrinter(sb, ref len) || sb.Length == 0)
                    return (Fallback_W, Fallback_H);

                string printerName = sb.ToString();

                // ② 打开打印机句柄
                if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                    return (Fallback_W, Fallback_H);

                try
                {
                    // ③ 查询 DEVMODE 需要的缓冲区大小
                    int dmSize = DocumentProperties(
                        IntPtr.Zero, hPrinter, printerName,
                        IntPtr.Zero, IntPtr.Zero, 0);
                    if (dmSize <= 0) return (Fallback_W, Fallback_H);

                    // ④ 读取 DEVMODE
                    IntPtr pDm = Marshal.AllocHGlobal(dmSize);
                    try
                    {
                        if (DocumentProperties(IntPtr.Zero, hPrinter, printerName,
                                pDm, IntPtr.Zero, DM_OUT_BUFFER) < 0)
                            return (Fallback_W, Fallback_H);

                        var dm = Marshal.PtrToStructure<DevMode>(pDm);

                        decimal w, h;

                        if (dm.PaperWidth > 0 && dm.PaperLength > 0)
                        {
                            // 驱动已填入精确尺寸（自定义纸张或现代驱动）
                            w = (dm.PaperWidth * TenthMmToMm);
                            h = (dm.PaperLength * TenthMmToMm);
                        }
                        else if (_stdSizes.TryGetValue(dm.PaperSize, out var std))
                        {
                            // 旧驱动仅填 PaperSize 代码，从内置表查找
                            (w, h) = std;
                        }
                        else
                        {
                            return (Fallback_W, Fallback_H);
                        }

                        // 横向时逻辑宽高与纵向相反
                        if (dm.Orientation == 2) (w, h) = (h, w);

                        return (w, h);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pDm);
                    }
                }
                finally
                {
                    ClosePrinter(hPrinter);
                }
            }
            catch
            {
                return (Fallback_W, Fallback_H);
            }


        }

        // ── 打印队列 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 获取指定打印机的打印队列任务列表。
        /// <paramref name="printerName"/> 为 null 时使用系统默认打印机。
        /// </summary>
        public static List<PrintQueueJob> GetPrintJobs(string? printerName = null)
        {
            var jobs = new List<PrintQueueJob>();

            printerName ??= GetDefaultPrinterName();
            if (printerName == null) return jobs;

            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return jobs;

            try
            {
                // 第一次调用：查询所需缓冲区大小
                EnumJobs(hPrinter, 0, 256, 1, IntPtr.Zero, 0, out uint needed, out _);
                if (needed == 0) return jobs;

                IntPtr pBuf = Marshal.AllocHGlobal((int)needed);
                try
                {
                    if (!EnumJobs(hPrinter, 0, 256, 1, pBuf, needed, out _, out uint count))
                        return jobs;

                    int stride = Marshal.SizeOf<JOB_INFO_1>();
                    for (uint i = 0; i < count; i++)
                    {
                        var info = Marshal.PtrToStructure<JOB_INFO_1>(pBuf + (int)(i * stride));
                        jobs.Add(new PrintQueueJob
                        {
                            JobId        = (int)info.JobId,
                            Document     = info.pDocument   != IntPtr.Zero ? Marshal.PtrToStringUni(info.pDocument)   ?? "" : "",
                            UserName     = info.pUserName   != IntPtr.Zero ? Marshal.PtrToStringUni(info.pUserName)   ?? "" : "",
                            StatusText   = info.pStatus     != IntPtr.Zero ? Marshal.PtrToStringUni(info.pStatus)     ?? "" : "",
                            Status       = (int)info.Status,
                            StatusLabel  = ParseJobStatus(info.Status),
                            TotalPages   = (int)info.TotalPages,
                            PagesPrinted = (int)info.PagesPrinted,
                            Priority     = (int)info.Priority,
                            Position     = (int)info.Position,
                            PrinterName  = printerName,
                        });
                    }
                }
                finally { Marshal.FreeHGlobal(pBuf); }
            }
            finally { ClosePrinter(hPrinter); }

            return jobs;
        }

        /// <summary>取消（删除）指定打印任务。成功返回 true。</summary>
        public static bool CancelPrintJob(string printerName, int jobId)
            => ControlJob(printerName, (uint)jobId, JOB_CONTROL_DELETE);

        /// <summary>重启指定打印任务。成功返回 true。</summary>
        public static bool RestartPrintJob(string printerName, int jobId)
            => ControlJob(printerName, (uint)jobId, JOB_CONTROL_RESTART);

        /// <summary>暂停指定打印任务。成功返回 true。</summary>
        public static bool PausePrintJob(string printerName, int jobId)
            => ControlJob(printerName, (uint)jobId, JOB_CONTROL_PAUSE);

        /// <summary>继续（恢复）指定打印任务。成功返回 true。</summary>
        public static bool ResumePrintJob(string printerName, int jobId)
            => ControlJob(printerName, (uint)jobId, JOB_CONTROL_RESUME);

        private static bool ControlJob(string printerName, uint jobId, uint command)
        {
            if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
                return false;
            try
            {
                return SetJob(hPrinter, jobId, 0, IntPtr.Zero, command);
            }
            finally { ClosePrinter(hPrinter); }
        }

        private static string ParseJobStatus(uint status)
        {
            if (status == 0) return "排队中";
            var parts = new List<string>();
            if ((status & 0x0010) != 0) parts.Add("打印中");
            if ((status & 0x0008) != 0) parts.Add("后台处理");
            if ((status & 0x0001) != 0) parts.Add("已暂停");
            if ((status & 0x0002) != 0) parts.Add("错误");
            if ((status & 0x0020) != 0) parts.Add("打印机离线");
            if ((status & 0x0040) != 0) parts.Add("缺纸");
            if ((status & 0x0080) != 0) parts.Add("已打印");
            if ((status & 0x0004) != 0) parts.Add("删除中");
            if ((status & 0x0100) != 0) parts.Add("已删除");
            if ((status & 0x1000) != 0) parts.Add("已完成");
            if ((status & 0x0200) != 0) parts.Add("设备队列阻塞");
            if ((status & 0x0400) != 0) parts.Add("需要人工干预");
            return parts.Count > 0 ? string.Join(", ", parts) : $"未知(0x{status:X})";
        }

        /// <summary>获取默认打印机名称，失败返回 null。</summary>
        public static string? GetDefaultPrinterName()
        {
            var sb  = new StringBuilder(256);
            int len = sb.Capacity;
            return GetDefaultPrinter(sb, ref len) && sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// 获取默认打印机的原生 X 分辨率（DPI）。
        /// 失败或无法解析时返回 300。
        /// </summary>
        public static int GetDefaultPrinterDpi()
        {
            try
            {
                var sb = new StringBuilder(256);
                int len = sb.Capacity;
                if (!GetDefaultPrinter(sb, ref len) || sb.Length == 0) return 300;

                string printerName = sb.ToString();
                if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero)) return 300;
                try
                {
                    int dmSize = DocumentProperties(IntPtr.Zero, hPrinter, printerName,
                        IntPtr.Zero, IntPtr.Zero, 0);
                    if (dmSize <= 0) return 300;

                    IntPtr pDm = Marshal.AllocHGlobal(dmSize);
                    try
                    {
                        if (DocumentProperties(IntPtr.Zero, hPrinter, printerName,
                                pDm, IntPtr.Zero, DM_OUT_BUFFER) < 0)
                            return 300;

                        var dm = Marshal.PtrToStructure<DevMode>(pDm);

                        // YResolution: 精确 y-DPI（首选）
                        if (dm.YResolution > 0) return dm.YResolution;
                        // PrintQuality: 正数 = x-DPI
                        if (dm.PrintQuality > 0) return dm.PrintQuality;
                        // 负数枚举 → 近似映射
                        return dm.PrintQuality switch
                        {
                            -4 => 600,   // DMRES_HIGH
                            -3 => 300,   // DMRES_MEDIUM
                            -2 => 150,   // DMRES_LOW
                            -1 => 96,    // DMRES_DRAFT
                            _ => 300
                        };
                    }
                    finally { Marshal.FreeHGlobal(pDm); }
                }
                finally { ClosePrinter(hPrinter); }
            }
            catch { return 300; }
        }
    }
}
