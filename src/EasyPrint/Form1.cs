using EasyPrint.Command;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using SkiaSharp;
using SuperSocket.Command;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;
using SuperSocket.WebSocket.Server;
using System.ComponentModel;
using System.Threading.Tasks;

namespace EasyPrint
{

    // ── 主窗口 ─────────────────────────────────────────────────────────────────

    public partial class Form1 : Form
    {
        private EasyPrintService _server;
        private UiLoggerProvider _loggerProvider;
        private AppSettings _settings = new();

        // ── 打印队列数据源 ─────────────────────────────────────────────────────
        // 主数据源（全量：Windows 队列任务 + EasyPrint 任务）
        private readonly BindingList<PrintQueueJob> _allJobs = new();
        // 过滤后视图（绑定到 DataGridView）
        private readonly BindingList<PrintQueueJob> _displayJobs = new();
        // EasyPrint WebSocket 任务 ID → 显示条目（用于状态更新）
        private readonly Dictionary<string, PrintQueueJob> _epMap = new();
        // 0=全部  1=打印中  2=错误
        private int _filterMode = 0;

        // ── 打印后台监听（事件驱动，替代轮询）────────────────────────────────
        private PrintSpoolerWatcher? _spoolerWatcher;

        // 统计计数（由 WebSocket 服务层更新）
        public int ConnectedClients { get; private set; } = 0;

        // 浏览器引擎下载任务（后台进行，需要时全局等待）
        private Task? _browserReadyTask;

        // 托盘：true = 用户从托盘菜单点击退出，允许真正关闭
        private bool _forceClose = false;

        // 开机自启注册表路径
        private const string StartupRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "EasyPrint";



        // ── 构造 & 初始化 ─────────────────────────────────────────────────────

        public Form1(AppSettings settings, IHostBuilder host, UiLoggerProvider loggerProvider)
        {


            InitializeComponent();

            // ── 加载自定义图标 ─────────────────────────────────────────────────
            var icoPath = Path.Combine(AppContext.BaseDirectory, "printer.ico");
            if (File.Exists(icoPath))
            {
                var icon = new Icon(icoPath);
                Icon = icon;               // 任务栏程序图标
                notifyIcon1.Icon = icon;   // 系统托盘图标
            }

            // ── 注入暗色扁平渲染器 ─────────────────────────────────────────────
            contextMenuTray.Renderer = new DarkMenuRenderer();

            this._loggerProvider = loggerProvider;
            this._loggerProvider.LogEmitted += AppendLog;
            _settings = settings;

            // 在窗口显示后立即在后台开始下载浏览器引擎，不阻塞启动流程
            _browserReadyTask = DownloadBrowserAsync();


            this._server = host.Build().Services.GetRequiredService<EasyPrintService>();



            SetupDataGrid();
            ApplySettingsToUI();
            UpdateServiceStatus(false);
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_settings.AutoStart)
            {
                await this.StartService();
            }
            this._server.Browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = AppDataContext.PuppeteerExecutablePath,
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"]
            });

            this._server.LoggerProvider = this._loggerProvider;
            this._server.WorkForm = this;

            // 立即加载当前 Windows 打印队列快照
            SyncPrintQueue();

            // 启动事件驱动监听：打印机任务变化时自动触发同步
            _spoolerWatcher = new PrintSpoolerWatcher();
            _spoolerWatcher.QueueChanged += OnSpoolerQueueChanged;
            _spoolerWatcher.Start();

            AppendLog("打印队列监听已启动（事件驱动）", LogLevel.Information);
        }

        // ── 浏览器引擎下载 ────────────────────────────────────────────────────────

        /// <summary>
        /// 在后台检查并下载 Puppeteer Chromium 引擎。
        /// 若本地缓存已存在，DownloadAsync 会立即返回（无网络请求）。
        /// </summary>
        private async Task DownloadBrowserAsync()
        {
            using var animCts = new CancellationTokenSource();
            try
            {

                var fetcher = new BrowserFetcher { CacheDir = AppDataContext.PuppeteerCacheDir };

                // 先扫描本地缓存，已有则直接取可执行路径，无需联网
                var installed = fetcher.GetInstalledBrowsers().ToList();
                if (installed.Count > 0)
                {
                    AppDataContext.PuppeteerExecutablePath = installed[0].GetExecutablePath();
                    AppendLog($"浏览器引擎已就绪（本地缓存）: {AppDataContext.PuppeteerExecutablePath}",
                        LogLevel.Debug);
                    SetStatusSafe("✓ 浏览器引擎就绪");
                    return;
                }

                AppendLog("开始后台下载浏览器引擎（Chromium），完成前 HTML 打印将等待...",
                    LogLevel.Information);

                // 状态栏动画：点点点
                _ = AnimateStatusAsync("⬇ 正在下载浏览器引擎", animCts.Token);

                var browser = await fetcher.DownloadAsync();
                AppDataContext.PuppeteerExecutablePath = browser.GetExecutablePath();

                await animCts.CancelAsync();
                AppendLog($"✅ 浏览器引擎已就绪: {AppDataContext.PuppeteerExecutablePath}",
                    LogLevel.Information);
                SetStatusSafe("✓ 浏览器引擎就绪");


            }
            catch (OperationCanceledException) { /* 动画取消，正常 */ }
            catch (Exception ex)
            {
                await animCts.CancelAsync();
                AppendLog($"⚠ 浏览器引擎下载失败（HTML 打印不可用）: {ex.Message}",
                    LogLevel.Warning);
                SetStatusSafe("⚠ 浏览器引擎下载失败");
            }

        }

        /// <summary>
        /// 在状态栏循环显示 "baseText." → "baseText.." → "baseText..." 动画，
        /// 直到 token 被取消为止。
        /// </summary>
        private async Task AnimateStatusAsync(string baseText, CancellationToken token)
        {
            var frames = new[] { ".", "..", "..." };
            var i = 0;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    SetStatusSafe(baseText + frames[i % frames.Length]);
                    i++;
                    await Task.Delay(500, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 在任何需要浏览器功能（HTML 打印）的入口处调用。
        /// 若下载尚未完成，UI 会显示等待提示并阻塞，直到就绪。
        /// </summary>
        public async Task EnsureBrowserReadyAsync()
        {
            if (_browserReadyTask is null || _browserReadyTask.IsCompleted)
                return;

            AppendLog("⏳ 正在等待浏览器引擎下载完成，请稍候...", LogLevel.Information);
            SetStatusSafe("⏳ 等待浏览器引擎就绪...");

            await _browserReadyTask;

            AppendLog("✅ 浏览器引擎就绪，继续执行", LogLevel.Information);
        }

        /// <summary>线程安全地更新底部状态栏文字。</summary>
        private void SetStatusSafe(string message)
        {
            if (InvokeRequired)
                Invoke(() => lblStatus.Text = message);
            else
                lblStatus.Text = message;
        }

        private async Task StartService()
        {
            try
            {

                _server.Options.Listeners[0].Ip = _settings.Ip;
                _server.Options.Listeners[0].Port = _settings.Port;
                if (await _server.StartAsync(CancellationToken.None))
                {
                    UpdateServiceStatus(true);
                    AppendLog($"服务已启动，监听 {_settings.Ip}:{_settings.Port}", LogLevel.Information);

                }

            }
            catch (Exception ex)
            {
                AppendLog($"服务启动失败：{ex.Message}", LogLevel.Error);
                MessageBox.Show("服务启动失败，请检查配置。", "EasyPrint", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateServiceStatus(false);
                SetStatus("○ 服务启动失败");
            }
        }
        // ── 数据表格初始化 ────────────────────────────────────────────────────

        private void SetupDataGrid()
        {
            dgvJobs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(22, 26, 35);
            dgvJobs.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 112, 138);
            dgvJobs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            dgvJobs.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            var cellFore = Color.FromArgb(200, 207, 222);
            var cellFont = new Font("Segoe UI", 9.5F);
            var cellPadding = new Padding(8, 0, 0, 0);
            var selBack = Color.FromArgb(30, 62, 108);
            var selFore = Color.FromArgb(212, 218, 232);

            // 奇数行
            dgvJobs.DefaultCellStyle.BackColor = Color.FromArgb(28, 33, 45);
            dgvJobs.DefaultCellStyle.ForeColor = cellFore;
            dgvJobs.DefaultCellStyle.SelectionBackColor = selBack;
            dgvJobs.DefaultCellStyle.SelectionForeColor = selFore;
            dgvJobs.DefaultCellStyle.Font = cellFont;
            dgvJobs.DefaultCellStyle.Padding = cellPadding;

            // 偶数行：显式同步 ForeColor / SelectionForeColor，避免退回系统默认色
            dgvJobs.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 29, 40);
            dgvJobs.AlternatingRowsDefaultCellStyle.ForeColor = cellFore;
            dgvJobs.AlternatingRowsDefaultCellStyle.SelectionBackColor = selBack;
            dgvJobs.AlternatingRowsDefaultCellStyle.SelectionForeColor = selFore;
            dgvJobs.AlternatingRowsDefaultCellStyle.Font = cellFont;
            dgvJobs.AlternatingRowsDefaultCellStyle.Padding = cellPadding;

            dgvJobs.RowTemplate.Height = 42;

            dgvJobs.AutoGenerateColumns = false;

            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColId",
                HeaderText = "队列 ID",
                DataPropertyName = nameof(PrintQueueJob.JobIdDisplay),
                Width = 72,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColPrinter",
                HeaderText = "打印机",
                DataPropertyName = nameof(PrintQueueJob.PrinterName),
                Width = 130,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColDocument",
                HeaderText = "文档",
                DataPropertyName = nameof(PrintQueueJob.DocumentSummary),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColUser",
                HeaderText = "用户",
                DataPropertyName = nameof(PrintQueueJob.UserName),
                Width = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColPages",
                HeaderText = "页数",
                DataPropertyName = nameof(PrintQueueJob.PagesDisplay),
                Width = 72,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                // StatusLabel 已是人类可读文字，CellFormatting 只负责染色
                Name = "ColStatus",
                HeaderText = "状态",
                DataPropertyName = nameof(PrintQueueJob.StatusLabel),
                Width = 90,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // 初始化过滤按钮文本
            btnFilterAll.Text = "全部";
            btnFilterPending.Text = "打印中";
            btnFilterFailed.Text = "错误";

            // 数据源绑定
            dgvJobs.DataSource = _displayJobs;
        }

        private void SaveSettings()
        {
            AppDataContext.Save(_settings);
        }

        private void ApplySettingsToUI()
        {
            txtIp.Text = _settings.Ip;
            txtPort.Text = _settings.Port.ToString();
            tglStartup.Checked = _settings.StartWithWindows;
            // 启动时同步注册表与设置文件保持一致
            SetStartup(_settings.StartWithWindows);
        }

        private bool ReadSettingsFromUI()
        {
            var ip = txtIp.Text.Trim();
            if (string.IsNullOrEmpty(ip))
            {
                MessageBox.Show("请填写监听地址。", "EasyPrint", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!int.TryParse(txtPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("端口号无效，请输入 1–65535 之间的整数。", "EasyPrint", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            _settings.Ip = ip;
            _settings.Port = port;
            SaveSettings();
            return true;
        }

        // ── 服务启停（骨架，WebSocket 实现由外部接入）─────────────────────────

        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (!ReadSettingsFromUI()) return;


            await StartService();
        }

        private async void btnStop_Click(object sender, EventArgs e)
        {

            await _server.StopAsync(CancellationToken.None);
            UpdateServiceStatus(false);
            AppendLog("服务已停止", LogLevel.Information);
        }

        // ── UI 状态更新 ───────────────────────────────────────────────────────

        private void UpdateServiceStatus(bool running)
        {
            if (running)
            {
                lblServiceStatus.Text = "● 运行中";
                lblServiceStatus.ForeColor = Color.FromArgb(82, 196, 110);
            }
            else
            {
                lblServiceStatus.Text = "● 未启动";
                lblServiceStatus.ForeColor = Color.FromArgb(80, 90, 110);
            }

            txtIp.Enabled = !running;
            txtPort.Enabled = !running;
            btnStart.Enabled = !running;
            btnStop.Enabled = running;
        }

        private void SetStatus(string message)
        {
            lblStatus.Text = message;
        }

        /// <summary>
        /// 供外部 WebSocket 层调用，更新已连接客户端数量。
        /// 线程安全：内部使用 Invoke。
        /// </summary>
        public void UpdateConnectedClients(int count)
        {
            ConnectedClients = count;
            if (InvokeRequired)
                Invoke(() => lblStatConnVal.Text = count.ToString());
            else
                lblStatConnVal.Text = count.ToString();
        }

        private void UpdateStats()
        {
            // 排队/待处理：无错误、未完成、未删除
            lblStatPendingVal.Text = _allJobs.Count(j =>
                (j.Status & 0x1186) == 0 || j.StatusLabel is "待处理" or "排队中").ToString();
            // 已完成：PRINTED(0x80) 或 COMPLETE(0x1000)
            lblStatCompletedVal.Text = _allJobs.Count(j =>
                (j.Status & 0x1080) != 0 || j.StatusLabel == "已完成").ToString();
            // 错误：ERROR(0x02) 或 StatusLabel=="失败"
            lblStatFailedVal.Text = _allJobs.Count(j =>
                (j.Status & 0x0002) != 0 || j.StatusLabel == "失败").ToString();
        }

        // ── 打印任务管理（公开供 WebSocket 层调用）───────────────────────────

        /// <summary>
        /// 将 EasyPrint WebSocket 打印任务转换为队列显示条目并插入列表。线程安全。
        /// </summary>
        public void AddPrintJob(PrintJob job)
        {
            if (InvokeRequired) { Invoke(() => AddPrintJob(job)); return; }

            var display = new PrintQueueJob
            {
                JobId = 0,          // 0 = EasyPrint 发起，非 Windows 队列 ID
                PrinterName = job.PrinterName,
                Document = string.Concat("[EP] ", job.Id, "  ",
                                  job.Context.AsSpan(0, Math.Min(40, job.Context.Length))),
                UserName = "EasyPrint",
                Status = 0,
                StatusLabel = "待处理",
            };

            _epMap[job.Id] = display;
            _allJobs.Insert(0, display);
            if (MatchesFilter(display)) _displayJobs.Insert(0, display);

            UpdateStats();
        }

        /// <summary>
        /// 更新 EasyPrint 任务状态。
        /// PrintQueueJob 实现 INotifyPropertyChanged，属性变化后 BindingList 自动刷新行。
        /// </summary>
        public void UpdatePrintJob(string jobId, PrintJobStatus status, string? errorMsg = null)
        {
            if (InvokeRequired) { Invoke(() => UpdatePrintJob(jobId, status, errorMsg)); return; }

            if (!_epMap.TryGetValue(jobId, out var job)) return;

            (job.Status, job.StatusLabel) = status switch
            {
                PrintJobStatus.Pending => (0, "待处理"),
                PrintJobStatus.Printing => (0x0010, "打印中"),
                PrintJobStatus.Completed => (0x1000, "已完成"),
                PrintJobStatus.Failed => (0x0002, "失败"),
                _ => (0, "")
            };

            if (!MatchesFilter(job)) _displayJobs.Remove(job);

            UpdateStats();
        }

        /// <summary>判断任务是否符合当前过滤条件。</summary>
        private bool MatchesFilter(PrintQueueJob job) => _filterMode switch
        {
            1 => (job.Status & 0x0018) != 0 || job.StatusLabel == "打印中",   // 后台处理 | 打印中
            2 => (job.Status & 0x0002) != 0 || job.StatusLabel == "失败",     // 错误
            _ => true
        };

        /// <summary>重建过滤视图，批量操作时一次性刷新避免多余重绘。</summary>
        private void ApplyFilter()
        {
            _displayJobs.RaiseListChangedEvents = false;
            _displayJobs.Clear();
            foreach (var job in _allJobs.Where(MatchesFilter))
                _displayJobs.Add(job);
            _displayJobs.RaiseListChangedEvents = true;
            _displayJobs.ResetBindings();
            UpdateStats();
        }

        // ── 筛选 ──────────────────────────────────────────────────────────────

        private void btnFilterAll_Click(object sender, EventArgs e)
        {
            _filterMode = 0;
            SetActiveFilter(btnFilterAll);
            ApplyFilter();
        }

        private void btnFilterPending_Click(object sender, EventArgs e)
        {
            _filterMode = 1;
            SetActiveFilter(btnFilterPending);
            ApplyFilter();
        }

        private void btnFilterFailed_Click(object sender, EventArgs e)
        {
            _filterMode = 2;
            SetActiveFilter(btnFilterFailed);
            ApplyFilter();
        }

        private void SetActiveFilter(Button active)
        {
            foreach (var btn in new[] { btnFilterAll, btnFilterPending, btnFilterFailed })
            {
                if (btn == active)
                {
                    btn.BackColor = Color.FromArgb(28, 58, 100);
                    btn.ForeColor = Color.FromArgb(100, 170, 255);
                }
                else
                {
                    btn.BackColor = Color.Transparent;
                    btn.ForeColor = Color.FromArgb(90, 100, 125);
                }
            }
        }

        // ── 操作按钮 ──────────────────────────────────────────────────────────

        private async void btnClearAll_Click(object sender, EventArgs e)
        {
            // 统计当前 Windows 队列中还有任务的打印机
            var printers = System.Drawing.Printing.PrinterSettings.InstalledPrinters
                .Cast<string>().ToList();

            int queueCount = _allJobs.Count(j => j.JobId > 0);

            if (queueCount == 0 && _allJobs.Count == 0)
            {
                MessageBox.Show("打印队列为空。", "EasyPrint",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"将取消所有打印机队列中的 {queueCount} 个任务，并清空显示列表。\n此操作不可撤销。",
                "清空打印队列",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;

            btnClearAll.Enabled = false;
            try
            {
                // 后台取消所有打印机队列中的全部任务
                await Task.Run(() =>
                {
                    foreach (string printer in printers)
                    {
                        try
                        {
                            var jobs = PrinterHelper.GetPrintJobs(printer);
                            foreach (var job in jobs)
                                PrinterHelper.CancelPrintJob(printer, job.JobId);
                        }
                        catch { /* 单台打印机失败不影响其他打印机 */ }
                    }
                });

                // 清空显示列表
                _displayJobs.RaiseListChangedEvents = false;
                _displayJobs.Clear();
                _allJobs.Clear();
                _epMap.Clear();
                _displayJobs.RaiseListChangedEvents = true;
                _displayJobs.ResetBindings();
                UpdateStats();

                AppendLog("已取消所有打印机队列任务", LogLevel.Information);
            }
            finally
            {
                btnClearAll.Enabled = true;
            }
        }

        private async void btnPauseAll_Click(object sender, EventArgs e)
            => await BulkJobControl("暂停", PrinterHelper.PausePrintJob, btnPauseAll);

        private async void btnResumeAll_Click(object sender, EventArgs e)
            => await BulkJobControl("继续", PrinterHelper.ResumePrintJob, btnResumeAll);

        private async void btnCancelAll_Click(object sender, EventArgs e)
            => await BulkJobControl("取消", PrinterHelper.CancelPrintJob, btnCancelAll);

        /// <summary>
        /// 对当前列表中所有 Windows 队列任务批量执行打印机控制操作。
        /// </summary>
        private async Task BulkJobControl(
            string actionLabel,
            Func<string, int, bool> action,
            Control btn)
        {
            var targets = _allJobs
                .Where(j => j.JobId > 0)
                .Select(j => (j.PrinterName, j.JobId))
                .ToList();

            if (targets.Count == 0)
            {
                MessageBox.Show("打印队列中没有可操作的任务。", "EasyPrint",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btn.Enabled = false;
            try
            {
                var (success, fail) = await Task.Run(() =>
                {
                    int ok = 0, ng = 0;
                    foreach (var (printer, jobId) in targets)
                    {
                        if (action(printer, jobId)) ok++; else ng++;
                    }
                    return (ok, ng);
                });

                AppendLog(
                    $"全部{actionLabel}：共 {targets.Count} 个任务，{success} 成功，{fail} 失败",
                    fail > 0 ? LogLevel.Warning : LogLevel.Information);
            }
            finally
            {
                btn.Enabled = true;
            }
        }

        // ── DataGridView 格式化 ───────────────────────────────────────────────

        private void dgvJobs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.CellStyle is null || e.RowIndex < 0) return;

            if (dgvJobs.Columns[e.ColumnIndex].Name != "ColStatus") return;

            // StatusLabel 已是中文字符串，仅需染色
            if (e.Value is string label)
            {
                e.CellStyle.ForeColor = label switch
                {
                    "打印中" or "后台处理" => Color.FromArgb(33, 150, 243),
                    "已完成" or "已打印" => Color.FromArgb(82, 196, 110),
                    "失败" or "错误" => Color.FromArgb(255, 90, 90),
                    "已暂停" => Color.FromArgb(255, 152, 0),
                    "排队中" or "待处理" => Color.FromArgb(160, 170, 190),
                    "需要人工干预" or "打印机离线" => Color.FromArgb(255, 193, 7),
                    _ => Color.FromArgb(200, 207, 222)
                };
                e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                e.FormattingApplied = true;
            }
        }

        private void dgvJobs_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvJobs.Rows[e.RowIndex].DataBoundItem is PrintQueueJob job)
            {
                var lines = new System.Text.StringBuilder();
                lines.AppendLine($"文档：{job.Document}");
                lines.AppendLine($"打印机：{job.PrinterName}");
                lines.AppendLine($"用户：{job.UserName}");
                lines.AppendLine($"状态：{job.StatusLabel}");
                if (!string.IsNullOrEmpty(job.StatusText))
                    lines.AppendLine($"详情：{job.StatusText}");
                if (job.TotalPages > 0)
                    lines.Append($"进度：{job.PagesPrinted} / {job.TotalPages} 页");
                e.ToolTipText = lines.ToString().TrimEnd();
            }
        }

        // ── 日志面板 ──────────────────────────────────────────────────────────

        private static readonly Color[] _logColors =
        [
            Color.FromArgb(100, 110, 130),  // Trace
            Color.FromArgb(100, 110, 130),  // Debug
            Color.FromArgb(190, 195, 210),  // Information
            Color.FromArgb(255, 195, 0),    // Warning
            Color.FromArgb(255, 90, 90),    // Error
            Color.FromArgb(255, 60, 60),    // Critical
        ];

        private static readonly string[] _logLabels = ["TRC", "DBG", "INF", "WRN", "ERR", "CRT"];

        private const int MaxLogLines = 2000;

        /// <summary>
        /// 向日志面板追加一条带颜色的日志条目。线程安全。
        /// </summary>
        public void AppendLog(string message, LogLevel level)
        {
            if (InvokeRequired) { Invoke(() => AppendLog(message, level)); return; }

            int idx = Math.Clamp((int)level, 0, _logColors.Length - 1);
            var fore = _logColors[idx];
            var tag = _logLabels[idx];

            rtbLog.SuspendLayout();

            // 时间戳（灰色）
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = Color.FromArgb(70, 80, 100);
            rtbLog.AppendText($"{DateTime.Now:HH:mm:ss.fff}  ");

            // 级别标签（彩色）
            rtbLog.SelectionColor = fore;
            rtbLog.AppendText($"[{tag}]  ");

            // 消息内容
            rtbLog.SelectionColor = level >= LogLevel.Warning ? fore : Color.FromArgb(190, 195, 210);
            rtbLog.AppendText(message + "\n");

            // 超出最大行数时删除头部旧行
            if (rtbLog.Lines.Length > MaxLogLines)
            {
                int cutEnd = rtbLog.GetFirstCharIndexFromLine(rtbLog.Lines.Length - MaxLogLines);
                rtbLog.Select(0, cutEnd);
                rtbLog.SelectedRtf = "";
            }

            rtbLog.ResumeLayout();
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.ScrollToCaret();

            // 同步更新底部状态栏
            SetStatus($"● {DateTime.Now:HH:mm:ss}  [{tag}]  {message}");
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            rtbLog.Clear();
        }

        // ── 窗口事件 ──────────────────────────────────────────────────────────

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!_forceClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                notifyIcon1.ShowBalloonTip(
                    2000,
                    "EasyPrint 打印服务",
                    "程序已最小化到托盘，双击图标可重新打开。",
                    ToolTipIcon.Info);
                return;
            }

            // 释放打印队列监听器
            _spoolerWatcher?.Dispose();
            _spoolerWatcher = null;

            notifyIcon1.Visible = false;
        }

        /// <summary>
        /// Spooler 事件回调（防抖后，在 ThreadPool 线程触发）。
        /// 只同步发生变化的那台打印机，不重新枚举所有打印机。
        /// </summary>
        private void OnSpoolerQueueChanged(string printerName, uint changeFlags)
        {
            if (!IsHandleCreated) return;
            // 防抖已在 PrintSpoolerWatcher 内完成（200ms），无需额外 Sleep
            SyncSinglePrinter(printerName, changeFlags);
        }

        // ── 托盘图标事件 ──────────────────────────────────────────────────────

        /// <summary>双击托盘图标 → 显示/还原主窗口。</summary>
        private void notifyIcon1_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>托盘菜单"显示主窗口"。</summary>
        private void trayMenuShow_Click(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>托盘菜单"退出程序"。</summary>
        private async void trayMenuExit_Click(object? sender, EventArgs e)
        {
            _forceClose = true;
            _spoolerWatcher?.Dispose();
            _spoolerWatcher = null;
            if (_server != null && _server.State == ServerState.Started)
                await _server.StopAsync(CancellationToken.None);
            Application.Exit();
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ── 开机自启（Windows 注册表）─────────────────────────────────────────

        private bool IsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegKey, false);
                return key?.GetValue(AppName) is string path
                    && path.Equals(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(StartupRegKey, true);
                if (key == null) return;
                if (enable)
                    key.SetValue(AppName, Application.ExecutablePath);
                else
                    key.DeleteValue(AppName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                AppendLog($"开机自启设置失败：{ex.Message}", LogLevel.Warning);
            }
        }

        private void tglStartup_CheckedChanged(object? sender, EventArgs e)
        {
            _settings.StartWithWindows = tglStartup.Checked;
            SaveSettings();
            SetStartup(tglStartup.Checked);
            AppendLog(tglStartup.Checked ? "已添加开机自动启动" : "已取消开机自动启动",
                      LogLevel.Information);
        }

        // ── Windows 打印队列同步 ──────────────────────────────────────────────

        /// <summary>
        /// 启动时全量同步：后台枚举所有打印机任务，回 UI 线程合并。
        /// </summary>
        private void SyncPrintQueue()
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200);
                    var fresh = System.Drawing.Printing.PrinterSettings.InstalledPrinters
                        .Cast<string>()
                        .SelectMany(p => PrinterHelper.GetPrintJobs(p))
                        .ToList();

                    if (IsHandleCreated)
                        Invoke(() => MergePrintQueue(fresh, printerFilter: null));
                }
                catch { }
            });
        }

        /// <summary>
        /// 单台打印机增量同步，由 Spooler 事件触发。
        /// changeFlags 含 DELETE_JOB 且不含 ADD/SET 时可跳过 EnumJobs，
        /// 直接从当前列表移除已消失的任务；其余情况仅重新枚举该打印机。
        /// </summary>
        private void SyncSinglePrinter(string printerName, uint changeFlags)
        {
            // 纯删除事件：无需 EnumJobs，直接对比列表即可找到已消失的任务
            bool deleteOnly = (changeFlags & PrintSpoolerWatcher.PRINTER_CHANGE_DELETE_JOB) != 0
                           && (changeFlags & (PrintSpoolerWatcher.PRINTER_CHANGE_ADD_JOB
                                            | PrintSpoolerWatcher.PRINTER_CHANGE_SET_JOB)) == 0;
            if (deleteOnly)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(200);
                        // 只查该打印机的现存任务，用于找出已消失的 JobId
                        var surviving = PrinterHelper.GetPrintJobs(printerName)
                                            .Select(j => j.JobId)
                                            .ToHashSet();
                        if (IsHandleCreated)
                            Invoke(() => RemoveGoneJobs(printerName, surviving));
                    }
                    catch { }
                });
                return;
            }

            // 新增或状态变化：重新枚举该打印机并差量合并
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200);
                    var fresh = PrinterHelper.GetPrintJobs(printerName);
                    if (IsHandleCreated)
                        Invoke(() => MergePrintQueue(fresh, printerFilter: printerName));
                }
                catch { }
            });
        }

        /// <summary>
        /// 从显示列表中移除指定打印机下已不在队列中的任务（仅 Windows 队列条目）。
        /// </summary>
        private void RemoveGoneJobs(string printerName, HashSet<int> survivingIds)
        {
            var gone = _allJobs
                .Where(j => j.JobId > 0
                         && j.PrinterName == printerName
                         && !survivingIds.Contains(j.JobId))
                .ToList();

            foreach (var job in gone)
            {
                _allJobs.Remove(job);
                _displayJobs.Remove(job);
            }

            if (gone.Count > 0) UpdateStats();
        }

        /// <summary>
        /// 将新鲜任务列表与当前显示数据差量合并。
        /// <paramref name="printerFilter"/> 不为空时，只处理该打印机的条目，
        /// 保证单台同步不会误删其他打印机的任务。
        /// EasyPrint 发起的任务（JobId == 0）始终不受影响。
        /// </summary>
        private void MergePrintQueue(List<PrintQueueJob> fresh, string? printerFilter)
        {
            var freshMap = fresh.ToDictionary(j => (j.PrinterName, j.JobId));

            // 构建当前视图的 HashSet，O(1) 判断条目是否已在显示列表
            var displaySet = _displayJobs.ToHashSet();

            // 1. 移除已消失的条目（受 printerFilter 约束）
            var toRemove = _allJobs
                .Where(j => j.JobId > 0
                         && (printerFilter == null || j.PrinterName == printerFilter)
                         && !freshMap.ContainsKey((j.PrinterName, j.JobId)))
                .ToList();

            foreach (var job in toRemove)
            {
                _allJobs.Remove(job);
                _displayJobs.Remove(job);
                displaySet.Remove(job);
            }

            // 2. 更新已存在的条目（属性变化由 INotifyPropertyChanged 自动刷新行）
            foreach (var job in _allJobs.Where(j => j.JobId > 0))
            {
                if (!freshMap.TryGetValue((job.PrinterName, job.JobId), out var f)) continue;

                job.Status = f.Status;
                job.StatusLabel = f.StatusLabel;
                job.StatusText = f.StatusText;
                job.PagesPrinted = f.PagesPrinted;
                job.TotalPages = f.TotalPages;
                job.Position = f.Position;

                freshMap.Remove((job.PrinterName, job.JobId));

                bool inDisplay = displaySet.Contains(job);
                bool matches = MatchesFilter(job);

                if (!matches && inDisplay) { _displayJobs.Remove(job); displaySet.Remove(job); }
                else if (matches && !inDisplay) { _displayJobs.Insert(0, job); displaySet.Add(job); }
            }

            // 3. 新增未见过的条目
            foreach (var job in freshMap.Values)
            {
                _allJobs.Insert(0, job);
                if (MatchesFilter(job)) { _displayJobs.Insert(0, job); displaySet.Add(job); }

            }

            UpdateStats();
        }
    }
}
