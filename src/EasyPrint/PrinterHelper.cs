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

        private const int DM_OUT_BUFFER = 2;

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
