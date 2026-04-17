using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace EasyPrint
{
    /// <summary>
    /// 监听所有已安装打印机的任务队列变化，使用 WinSpool 事件驱动 API，
    /// 打印机任务新增 / 状态更新 / 完成 / 失败时立即触发 <see cref="QueueChanged"/>，无需轮询。
    /// </summary>
    internal sealed class PrintSpoolerWatcher : IDisposable
    {
        #region Win32 P/Invoke ───────────────────────────────────────────────

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool OpenPrinter(
            string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindFirstPrinterChangeNotification(
            IntPtr hPrinter, uint fdwFilter, uint fdwOptions,
            IntPtr pPrinterNotifyOptions);

        [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool FindNextPrinterChangeNotification(
            IntPtr hChange, out uint pdwChange,
            IntPtr pvReserved, IntPtr ppPrinterNotifyInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool FindClosePrinterChangeNotification(IntPtr hChange);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForMultipleObjects(
            uint nCount, IntPtr[] lpHandles, bool bWaitAll, uint dwMilliseconds);

        // 任务变化子标志
        internal const uint PRINTER_CHANGE_ADD_JOB    = 0x00000100;
        internal const uint PRINTER_CHANGE_SET_JOB    = 0x00000200;
        internal const uint PRINTER_CHANGE_DELETE_JOB = 0x00000400;
        private  const uint PRINTER_CHANGE_JOB        = 0x00000700;

        private static readonly IntPtr INVALID_HANDLE = new(-1);

        // WaitForMultipleObjects 每次最多等待 64 个 Handle
        private const int  MaxWatchHandles = 64;
        // 兜底超时：让线程有机会响应 CancellationToken
        private const uint WaitTimeoutMs   = 5_000;
        private const uint WAIT_FAILED     = 0xFFFFFFFF;
        private const uint WAIT_TIMEOUT    = 0x00000102;
        #endregion

        // ── 公开事件 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 当任意打印机任务队列发生变化时触发。
        /// 参数：(printerName, changeFlags)，changeFlags 为 PRINTER_CHANGE_* 位标志。
        /// 在后台线程触发，消费者需自行 Invoke 切换到 UI 线程。
        /// </summary>
        public event Action<string, uint>? QueueChanged;

        // ── 内部状态 ──────────────────────────────────────────────────────────

        private readonly CancellationTokenSource _cts = new();

        private Thread? _watchThread;
        private bool    _disposed;

        // ── 公开方法 ──────────────────────────────────────────────────────────

        /// <summary>启动后台监听线程。</summary>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _watchThread = new Thread(WatchLoop)
            {
                IsBackground = true,
                Name         = "PrintSpoolerWatcher",
            };
            _watchThread.Start();
        }

        // ── 后台监听循环 ──────────────────────────────────────────────────────

        private void WatchLoop()
        {
            var entries = BuildEntries();
            if (entries.Count == 0) return;

            var handles = entries.Select(e => e.hChange).ToArray();

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    uint result = WaitForMultipleObjects(
                        (uint)handles.Length, handles, false, WaitTimeoutMs);

                    if (_cts.IsCancellationRequested) break;
                    if (result == WAIT_FAILED || result == WAIT_TIMEOUT) continue;

                    int idx = (int)result;
                    if (idx < 0 || idx >= entries.Count) continue;

                    var (name, _, hChange) = entries[idx];

                    // 必须立即调用 FindNext 重置 Handle，否则下次 Wait 永远超时
                    FindNextPrinterChangeNotification(
                        hChange, out uint changeFlags, IntPtr.Zero, IntPtr.Zero);

                    QueueChanged?.Invoke(name, changeFlags);
                }
            }
            finally
            {
                CleanupEntries(entries);
            }
        }

        // ── Handle 管理 ───────────────────────────────────────────────────────

        private static List<(string Name, IntPtr hPrinter, IntPtr hChange)> BuildEntries()
        {
            var list = new List<(string, IntPtr, IntPtr)>();

            foreach (string printer in PrinterSettings.InstalledPrinters
                                                       .Cast<string>()
                                                       .Take(MaxWatchHandles))
            {
                if (!OpenPrinter(printer, out IntPtr hPrinter, IntPtr.Zero))
                    continue;

                IntPtr hChange = FindFirstPrinterChangeNotification(
                    hPrinter, PRINTER_CHANGE_JOB, 0, IntPtr.Zero);

                if (hChange == INVALID_HANDLE)
                {
                    ClosePrinter(hPrinter);
                    continue;
                }

                list.Add((printer, hPrinter, hChange));
            }

            return list;
        }

        private static void CleanupEntries(
            IEnumerable<(string, IntPtr hPrinter, IntPtr hChange)> entries)
        {
            foreach (var (_, hPrinter, hChange) in entries)
            {
                if (hChange != INVALID_HANDLE && hChange != IntPtr.Zero)
                    FindClosePrinterChangeNotification(hChange);
                if (hPrinter != IntPtr.Zero)
                    ClosePrinter(hPrinter);
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            _watchThread?.Join(3000);
            _cts.Dispose();
        }
    }
}
