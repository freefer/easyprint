using EasyPrint.Command;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        // 主数据源（全量，永不清空）
        private readonly BindingList<PrintJob> _allJobs    = new();
        // 过滤后视图（绑定到 DataGridView）
        private readonly BindingList<PrintJob> _displayJobs = new();
        private PrintJobStatus? _currentFilter = null;   // null = 全部

        // 统计计数（由 WebSocket 服务层更新）
        public int ConnectedClients { get; private set; } = 0;

        // 浏览器引擎下载任务（后台进行，需要时全局等待）
        private Task? _browserReadyTask;

        // 托盘：true = 用户从托盘菜单点击退出，允许真正关闭
        private bool _forceClose = false;

        // 开机自启注册表路径
        private const string StartupRegKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName       = "EasyPrint";



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
            var i      = 0;
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
            dgvJobs.DefaultCellStyle.BackColor = Color.FromArgb(28, 33, 45);
            dgvJobs.DefaultCellStyle.ForeColor = Color.FromArgb(200, 207, 222);
            dgvJobs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 62, 108);
            dgvJobs.DefaultCellStyle.SelectionForeColor = Color.FromArgb(212, 218, 232);
            dgvJobs.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F);
            dgvJobs.DefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
            dgvJobs.RowTemplate.Height = 42;
            dgvJobs.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 29, 40);

            // 关闭自动列生成，改用手动配置 DataPropertyName
            dgvJobs.AutoGenerateColumns = false;

            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "ColId",
                HeaderText       = "编号",
                DataPropertyName = nameof(PrintJob.Id),
                Width            = 80,
                SortMode         = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "ColPrinter",
                HeaderText       = "打印机",
                DataPropertyName = nameof(PrintJob.PrinterName),
                Width            = 120,
                SortMode         = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                // 绑定原始属性 Context，截断逻辑放在 CellFormatting
                Name             = "ColSummary",
                HeaderText       = "内容摘要",
                DataPropertyName = nameof(PrintJob.Context),
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.Fill,
                SortMode         = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                // 绑定 DateTime，格式化放在 CellFormatting
                Name             = "ColTime",
                HeaderText       = "时间",
                DataPropertyName = nameof(PrintJob.CreateTime),
                Width            = 110,
                SortMode         = DataGridViewColumnSortMode.NotSortable
            });
            dgvJobs.Columns.Add(new DataGridViewTextBoxColumn
            {
                // 绑定枚举 Status，文本+颜色都在 CellFormatting 处理
                // Status 有 setter → BindingList 能正确检测变化并刷新对应单元格
                Name             = "ColStatus",
                HeaderText       = "状态",
                DataPropertyName = nameof(PrintJob.Status),
                Width            = 80,
                SortMode         = DataGridViewColumnSortMode.NotSortable
            });

            // 双向绑定：_displayJobs 变化自动刷新表格，PrintJob.Status 变化自动刷新行
            dgvJobs.DataSource = _displayJobs;
        }

        private void SaveSettings()
        {
            AppDataContext.Save(_settings);
        }

        private void ApplySettingsToUI()
        {
            txtIp.Text         = _settings.Ip;
            txtPort.Text       = _settings.Port.ToString();
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
            lblStatPendingVal.Text = _allJobs.Count(j => j.Status == PrintJobStatus.Pending).ToString();
            lblStatCompletedVal.Text = _allJobs.Count(j => j.Status == PrintJobStatus.Completed).ToString();
            lblStatFailedVal.Text = _allJobs.Count(j => j.Status == PrintJobStatus.Failed).ToString();
        }

        // ── 打印任务管理（公开供 WebSocket 层调用）───────────────────────────

        /// <summary>添加一条新打印任务。线程安全。</summary>
        public void AddPrintJob(PrintJob job)
        {
            if (InvokeRequired) { Invoke(() => AddPrintJob(job)); return; }

            _allJobs.Insert(0, job);                  // 主数据源

            if (MatchesFilter(job))
                _displayJobs.Insert(0, job);          // 符合过滤条件则加入视图

            UpdateStats();
        }

        /// <summary>
        /// 更新已有任务状态。线程安全。
        /// PrintJob 实现了 INotifyPropertyChanged，Status 变化后
        /// BindingList 会自动通知 DataGridView 刷新对应行，无需手动 Rows.Clear()。
        /// </summary>
        public void UpdatePrintJob(string jobId, PrintJobStatus status, string? errorMsg = null)
        {
            if (InvokeRequired) { Invoke(() => UpdatePrintJob(jobId, status, errorMsg)); return; }

            var job = _allJobs.FirstOrDefault(j => j.Id == jobId);
            if (job == null) return;

            job.Status       = status;      // 触发 PropertyChanged → 行自动刷新
            job.ErrorMessage = errorMsg;

            // 若当前有过滤且该行已不符合，从视图移除
            if (!MatchesFilter(job))
                _displayJobs.Remove(job);

            UpdateStats();
        }

        /// <summary>判断任务是否符合当前过滤条件。</summary>
        private bool MatchesFilter(PrintJob job) =>
            !_currentFilter.HasValue || job.Status == _currentFilter.Value;

        /// <summary>
        /// 重建过滤视图。批量操作时先暂停通知，最后 ResetBindings 一次性刷新，
        /// 避免 N 次 ListChanged 事件导致 N 次表格重绘。
        /// </summary>
        private void ApplyFilter()
        {
            _displayJobs.RaiseListChangedEvents = false;
            _displayJobs.Clear();
            foreach (var job in _allJobs.Where(MatchesFilter))
                _displayJobs.Add(job);
            _displayJobs.RaiseListChangedEvents = true;
            _displayJobs.ResetBindings();            // 一次性通知表格刷新
            UpdateStats();
        }

        // ── 筛选 ──────────────────────────────────────────────────────────────

        private void btnFilterAll_Click(object sender, EventArgs e)
        {
            _currentFilter = null;
            SetActiveFilter(btnFilterAll);
            ApplyFilter();
        }

        private void btnFilterPending_Click(object sender, EventArgs e)
        {
            _currentFilter = PrintJobStatus.Pending;
            SetActiveFilter(btnFilterPending);
            ApplyFilter();
        }

        private void btnFilterFailed_Click(object sender, EventArgs e)
        {
            _currentFilter = PrintJobStatus.Failed;
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

        private void btnClearCompleted_Click(object sender, EventArgs e)
        {
            // 先从视图移除（避免触发多余的 ListChanged 通知）
            var completed = _allJobs.Where(j => j.Status == PrintJobStatus.Pending).ToList();
            foreach (var job in completed)
            {
                _allJobs.Remove(job);
                _displayJobs.Remove(job);
            }
            UpdateStats();
        }

        private void btnClearAll_Click(object sender, EventArgs e)
        {
            if (_allJobs.Count == 0) return;

            var result = MessageBox.Show(
                $"确定要清空全部 {_allJobs.Count} 条任务记录吗？\n此操作不可撤销。",
                "EasyPrint",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK) return;

            _displayJobs.RaiseListChangedEvents = false;
            _displayJobs.Clear();
            _allJobs.Clear();
            _displayJobs.RaiseListChangedEvents = true;
            _displayJobs.ResetBindings();
            UpdateStats();
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            // DataBoundItem 直接拿到强类型对象，不再需要 Tag hack
            if (dgvJobs.CurrentRow?.DataBoundItem is not PrintJob job) return;
            if (job.Status != PrintJobStatus.Failed && job.Status != PrintJobStatus.Pending)
            {
                MessageBox.Show("只能重试失败或待处理的任务。", "EasyPrint",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            job.Status       = PrintJobStatus.Pending;   // PropertyChanged → 行自动刷新
            job.ErrorMessage = null;
            // TODO: 将 job 重新投入打印队列
        }

        // ── DataGridView 格式化 ───────────────────────────────────────────────

        private void dgvJobs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.CellStyle is null || e.RowIndex < 0) return;

            switch (dgvJobs.Columns[e.ColumnIndex].Name)
            {
                // ── 内容摘要：截断超过 50 字符的部分 ──────────────────────────
                case "ColSummary":
                    if (e.Value is string ctx && ctx.Length > 50)
                    {
                        e.Value = string.Concat(ctx.AsSpan(0, 50), "…");
                        e.FormattingApplied = true;
                    }
                    break;

                // ── 时间：格式化 DateTime → HH:mm:ss ──────────────────────────
                case "ColTime":
                    if (e.Value is DateTime dt)
                    {
                        e.Value = dt.ToString("HH:mm:ss");
                        e.FormattingApplied = true;
                    }
                    break;

                // ── 状态：枚举 → 中文文本 + 彩色字体 ─────────────────────────
                case "ColStatus":
                    if (e.Value is PrintJobStatus status)
                    {
                        e.Value = status switch
                        {
                            PrintJobStatus.Pending   => "待处理",
                            PrintJobStatus.Printing  => "打印中",
                            PrintJobStatus.Completed => "已完成",
                            PrintJobStatus.Failed    => "失败",
                            _                        => ""
                        };
                        e.CellStyle.ForeColor = status switch
                        {
                            PrintJobStatus.Pending   => Color.FromArgb(255, 152, 0),
                            PrintJobStatus.Printing  => Color.FromArgb(33, 150, 243),
                            PrintJobStatus.Completed => Color.FromArgb(82, 196, 110),
                            PrintJobStatus.Failed    => Color.FromArgb(255, 90, 90),
                            _                        => Color.FromArgb(200, 207, 222)
                        };
                        e.CellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        e.FormattingApplied = true;
                    }
                    break;
            }
        }

        private void dgvJobs_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvJobs.Rows[e.RowIndex].DataBoundItem is PrintJob job)
            {
                e.ToolTipText = string.IsNullOrEmpty(job.ErrorMessage)
                    ? job.Context
                    : $"错误：{job.ErrorMessage}";
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

            notifyIcon1.Visible = false;
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
            if (_server != null)
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

        // ── 演示数据（可删除）────────────────────────────────────────────────

        private void LoadSampleJobs()
        {
            AddPrintJob(new PrintJob
            {
                Id = "A3F1B2C0",
                PrinterName = "默认打印机",
                Context = "条码: W0100023 | 门店: 城东店 | 类型: 羽绒服 | 客户: 张三(1234)",
                CreateTime = DateTime.Now.AddMinutes(-3),
                Status = PrintJobStatus.Completed
            });
            AddPrintJob(new PrintJob
            {
                Id = "B9D2E471",
                PrinterName = "TSC TE200",
                Context = "条码: W0100024 | 门店: 城西店 | 类型: 西服",
                CreateTime = DateTime.Now.AddMinutes(-1),
                Status = PrintJobStatus.Failed,
                ErrorMessage = "打印机离线，无法连接"
            });
            AddPrintJob(new PrintJob
            {
                Id = "C4F8A120",
                PrinterName = "默认打印机",
                Context = "条码: W0100025 | 门店: 城北店 | 客户: 李四(5678)",
                CreateTime = DateTime.Now,
                Status = PrintJobStatus.Pending
            });
        }
    }
}
