namespace EasyPrint
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        // ── 暗色主题调色板 ─────────────────────────────────────────────────────
        // BG_BASE    (15, 17, 22)   窗体最外层
        // BG_MAIN    (20, 24, 32)   panelMain
        // BG_CARD    (28, 33, 45)   左/右卡片
        // BG_HEADER  (23, 27, 37)   各区域标题行
        // BG_INPUT   (36, 42, 56)   TextBox 背景
        // BG_STATS   (20, 24, 33)   统计卡背景
        // BG_SEL     (30, 62, 108)  DataGridView 选中行
        // BORDER     (45, 51, 70)   分隔线/边框
        // TXT_PRI    (212, 218, 232) 主要文字
        // TXT_SEC    (100, 112, 138) 次要标签
        // ──────────────────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            parrotFormHandle1 = new ReaLTaiizor.Controls.ParrotFormHandle();
            panelTopActions = new ReaLTaiizor.Controls.Panel();
            lblAppTitle = new ReaLTaiizor.Controls.LabelEdit();
            lblServiceStatus = new ReaLTaiizor.Controls.LabelEdit();
            nightControlBox1 = new ReaLTaiizor.Controls.NightControlBox();
            lostSeparator1 = new ReaLTaiizor.Controls.LostSeparator();
            parrotFormEllipse1 = new ReaLTaiizor.Controls.ParrotFormEllipse();
            panelMain = new ReaLTaiizor.Controls.Panel();
            splitContainerMain = new SplitContainer();
            panelLeft = new ReaLTaiizor.Controls.Panel();
            panelSettingsContent = new Panel();
            lblIp = new Label();
            txtIp = new TextBox();
            lblPort = new Label();
            txtPort = new TextBox();
            btnStart = new ReaLTaiizor.Controls.MaterialButton();
            btnStop = new ReaLTaiizor.Controls.MaterialButton();
            panelStats = new Panel();
            lblStatConn = new Label();
            lblStatConnVal = new Label();
            lblStatPending = new Label();
            lblStatPendingVal = new Label();
            lblStatCompleted = new Label();
            lblStatCompletedVal = new Label();
            lblStatFailed = new Label();
            lblStatFailedVal = new Label();
            panelSettingsHeader = new ReaLTaiizor.Controls.Panel();
            lblSettingsTitle = new ReaLTaiizor.Controls.LabelEdit();
            separator1 = new ReaLTaiizor.Controls.Separator();
            panelRight = new ReaLTaiizor.Controls.Panel();
            dgvJobs = new DataGridView();
            panelJobActions = new Panel();
            btnRetry = new ReaLTaiizor.Controls.MaterialButton();
            btnClearCompleted = new ReaLTaiizor.Controls.MaterialButton();
            panelHistoryHeader = new ReaLTaiizor.Controls.Panel();
            lblHistoryTitle = new ReaLTaiizor.Controls.LabelEdit();
            separator2 = new ReaLTaiizor.Controls.Separator();
            panelBottom = new ReaLTaiizor.Controls.Panel();
            rtbLog = new RichTextBox();
            panelLogBar = new Panel();
            lblLogTitle = new Label();
            btnClearLog = new Button();
            lostSeparator3 = new ReaLTaiizor.Controls.LostSeparator();
            panelStatusRow = new Panel();
            lblStatus = new ReaLTaiizor.Controls.LabelEdit();
            lostSeparator2 = new ReaLTaiizor.Controls.LostSeparator();
            btnFilterAll     = new Button();
            btnFilterPending = new Button();
            btnFilterFailed  = new Button();
            panelTopActions.SuspendLayout();
            panelMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.Panel1.SuspendLayout();
            splitContainerMain.Panel2.SuspendLayout();
            splitContainerMain.SuspendLayout();
            panelLeft.SuspendLayout();
            panelSettingsContent.SuspendLayout();
            panelStats.SuspendLayout();
            panelSettingsHeader.SuspendLayout();
            panelRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dgvJobs).BeginInit();
            panelJobActions.SuspendLayout();
            panelHistoryHeader.SuspendLayout();
            panelBottom.SuspendLayout();
            panelLogBar.SuspendLayout();
            panelStatusRow.SuspendLayout();
            SuspendLayout();
            // 
            // parrotFormHandle1
            // 
            parrotFormHandle1.DockAtTop = true;
            parrotFormHandle1.HandleControl = panelTopActions;
            // 
            // panelTopActions
            // 
            panelTopActions.BackColor = Color.FromArgb(23, 27, 37);
            panelTopActions.Controls.Add(lblAppTitle);
            panelTopActions.Controls.Add(lblServiceStatus);
            panelTopActions.Controls.Add(nightControlBox1);
            panelTopActions.Controls.Add(lostSeparator1);
            panelTopActions.Dock = DockStyle.Top;
            panelTopActions.EdgeColor = Color.Transparent;
            panelTopActions.Location = new Point(5, 5);
            panelTopActions.Name = "panelTopActions";
            panelTopActions.Padding = new Padding(1);
            panelTopActions.Size = new Size(905, 80);
            panelTopActions.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelTopActions.TabIndex = 1;
            panelTopActions.Text = "panelTopActions";
            // 
            // lblAppTitle
            // 
            lblAppTitle.BackColor = Color.Transparent;
            lblAppTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblAppTitle.ForeColor = Color.FromArgb(212, 218, 232);
            lblAppTitle.Location = new Point(16, 16);
            lblAppTitle.Name = "lblAppTitle";
            lblAppTitle.Size = new Size(380, 44);
            lblAppTitle.TabIndex = 0;
            lblAppTitle.Text = "🖨 EasyPrint 打印服务";
            lblAppTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblServiceStatus
            // 
            lblServiceStatus.BackColor = Color.Transparent;
            lblServiceStatus.Font = new Font("Segoe UI", 10F);
            lblServiceStatus.ForeColor = Color.FromArgb(80, 90, 110);
            lblServiceStatus.Location = new Point(408, 22);
            lblServiceStatus.Name = "lblServiceStatus";
            lblServiceStatus.Size = new Size(160, 36);
            lblServiceStatus.TabIndex = 1;
            lblServiceStatus.Text = "● 未启动";
            lblServiceStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // nightControlBox1
            // 
            nightControlBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            nightControlBox1.BackColor = Color.Transparent;
            nightControlBox1.CloseHoverColor = Color.FromArgb(192, 57, 43);
            nightControlBox1.CloseHoverForeColor = Color.White;
            nightControlBox1.Cursor = Cursors.Hand;
            nightControlBox1.DefaultLocation = false;
            nightControlBox1.DisableMaximizeColor = Color.FromArgb(55, 62, 80);
            nightControlBox1.DisableMinimizeColor = Color.FromArgb(55, 62, 80);
            nightControlBox1.EnableCloseColor = Color.FromArgb(108, 116, 140);
            nightControlBox1.EnableMaximizeButton = false;
            nightControlBox1.EnableMaximizeColor = Color.FromArgb(108, 116, 140);
            nightControlBox1.EnableMinimizeButton = true;
            nightControlBox1.EnableMinimizeColor = Color.FromArgb(108, 116, 140);
            nightControlBox1.Location = new Point(763, 2);
            nightControlBox1.MaximizeHoverColor = Color.FromArgb(38, 44, 60);
            nightControlBox1.MaximizeHoverForeColor = Color.FromArgb(212, 218, 232);
            nightControlBox1.MinimizeHoverColor = Color.FromArgb(38, 44, 60);
            nightControlBox1.MinimizeHoverForeColor = Color.FromArgb(212, 218, 232);
            nightControlBox1.Name = "nightControlBox1";
            nightControlBox1.Size = new Size(139, 31);
            nightControlBox1.TabIndex = 5;
            // 
            // lostSeparator1
            // 
            lostSeparator1.BackColor = Color.FromArgb(45, 51, 70);
            lostSeparator1.Dock = DockStyle.Bottom;
            lostSeparator1.ForeColor = Color.FromArgb(45, 51, 70);
            lostSeparator1.Horizontal = false;
            lostSeparator1.Location = new Point(1, 76);
            lostSeparator1.Name = "lostSeparator1";
            lostSeparator1.Size = new Size(903, 3);
            lostSeparator1.TabIndex = 4;
            lostSeparator1.Text = "lostSeparator1";
            // 
            // parrotFormEllipse1
            // 
            parrotFormEllipse1.CornerRadius = 6;
            parrotFormEllipse1.EffectedForm = this;
            // 
            // panelMain
            // 
            panelMain.BackColor = Color.FromArgb(20, 24, 32);
            panelMain.Controls.Add(splitContainerMain);
            panelMain.Controls.Add(panelTopActions);
            panelMain.Controls.Add(panelBottom);
            panelMain.Dock = DockStyle.Fill;
            panelMain.EdgeColor = Color.FromArgb(45, 51, 70);
            panelMain.Location = new Point(0, 0);
            panelMain.Name = "panelMain";
            panelMain.Padding = new Padding(5);
            panelMain.Size = new Size(915, 800);
            panelMain.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelMain.TabIndex = 0;
            panelMain.Text = "panelMain";
            // 
            // splitContainerMain
            // 
            splitContainerMain.BackColor = Color.FromArgb(15, 17, 22);
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.Location = new Point(5, 85);
            splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel1
            // 
            splitContainerMain.Panel1.BackColor = Color.FromArgb(15, 17, 22);
            splitContainerMain.Panel1.Controls.Add(panelLeft);
            splitContainerMain.Panel1.Padding = new Padding(0, 0, 5, 0);
            // 
            // splitContainerMain.Panel2
            // 
            splitContainerMain.Panel2.BackColor = Color.FromArgb(15, 17, 22);
            splitContainerMain.Panel2.Controls.Add(panelRight);
            splitContainerMain.Panel2.Padding = new Padding(5, 0, 0, 0);
            splitContainerMain.Size = new Size(905, 490);
            splitContainerMain.SplitterDistance = 316;
            splitContainerMain.SplitterWidth = 2;
            splitContainerMain.TabIndex = 2;
            // 
            // panelLeft
            // 
            panelLeft.BackColor = Color.FromArgb(28, 33, 45);
            panelLeft.Controls.Add(panelSettingsContent);
            panelLeft.Controls.Add(panelSettingsHeader);
            panelLeft.Dock = DockStyle.Fill;
            panelLeft.EdgeColor = Color.FromArgb(45, 51, 70);
            panelLeft.Location = new Point(0, 0);
            panelLeft.Name = "panelLeft";
            panelLeft.Padding = new Padding(1);
            panelLeft.Size = new Size(311, 490);
            panelLeft.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelLeft.TabIndex = 0;
            panelLeft.Text = "panelLeft";
            // 
            // panelSettingsContent
            // 
            panelSettingsContent.BackColor = Color.FromArgb(28, 33, 45);
            panelSettingsContent.Controls.Add(lblIp);
            panelSettingsContent.Controls.Add(txtIp);
            panelSettingsContent.Controls.Add(lblPort);
            panelSettingsContent.Controls.Add(txtPort);
            panelSettingsContent.Controls.Add(btnStart);
            panelSettingsContent.Controls.Add(btnStop);
            panelSettingsContent.Controls.Add(panelStats);
            panelSettingsContent.Dock = DockStyle.Fill;
            panelSettingsContent.Location = new Point(1, 76);
            panelSettingsContent.Name = "panelSettingsContent";
            panelSettingsContent.Size = new Size(309, 413);
            panelSettingsContent.TabIndex = 1;
            // 
            // lblIp
            // 
            lblIp.BackColor = Color.Transparent;
            lblIp.Font = new Font("Segoe UI", 9F);
            lblIp.ForeColor = Color.FromArgb(100, 112, 138);
            lblIp.Location = new Point(24, 22);
            lblIp.Name = "lblIp";
            lblIp.Size = new Size(278, 20);
            lblIp.TabIndex = 0;
            lblIp.Text = "监听地址";
            // 
            // txtIp
            // 
            txtIp.BackColor = Color.FromArgb(36, 42, 56);
            txtIp.BorderStyle = BorderStyle.FixedSingle;
            txtIp.Font = new Font("Segoe UI", 11F);
            txtIp.ForeColor = Color.FromArgb(212, 218, 232);
            txtIp.Location = new Point(24, 46);
            txtIp.Name = "txtIp";
            txtIp.Size = new Size(278, 27);
            txtIp.TabIndex = 0;
            txtIp.Text = "127.0.0.1";
            // 
            // lblPort
            // 
            lblPort.BackColor = Color.Transparent;
            lblPort.Font = new Font("Segoe UI", 9F);
            lblPort.ForeColor = Color.FromArgb(100, 112, 138);
            lblPort.Location = new Point(24, 96);
            lblPort.Name = "lblPort";
            lblPort.Size = new Size(278, 20);
            lblPort.TabIndex = 1;
            lblPort.Text = "监听端口";
            // 
            // txtPort
            // 
            txtPort.BackColor = Color.FromArgb(36, 42, 56);
            txtPort.BorderStyle = BorderStyle.FixedSingle;
            txtPort.Font = new Font("Segoe UI", 11F);
            txtPort.ForeColor = Color.FromArgb(212, 218, 232);
            txtPort.Location = new Point(24, 120);
            txtPort.Name = "txtPort";
            txtPort.Size = new Size(140, 27);
            txtPort.TabIndex = 1;
            txtPort.Text = "8765";
            // 
            // btnStart
            // 
            btnStart.AutoSize = false;
            btnStart.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnStart.CharacterCasing = ReaLTaiizor.Controls.MaterialButton.CharacterCasingEnum.Normal;
            btnStart.Cursor = Cursors.Hand;
            btnStart.Density = ReaLTaiizor.Controls.MaterialButton.MaterialButtonDensity.Default;
            btnStart.Depth = 0;
            btnStart.HighEmphasis = true;
            btnStart.Icon = null;
            btnStart.IconType = ReaLTaiizor.Controls.MaterialButton.MaterialIconType.Rebase;
            btnStart.Location = new Point(24, 174);
            btnStart.Margin = new Padding(4, 6, 4, 6);
            btnStart.MouseState = ReaLTaiizor.Helper.MaterialDrawHelper.MaterialMouseState.HOVER;
            btnStart.Name = "btnStart";
            btnStart.NoAccentTextColor = Color.Empty;
            btnStart.Size = new Size(130, 38);
            btnStart.TabIndex = 2;
            btnStart.Text = "▶ 启动服务";
            btnStart.Type = ReaLTaiizor.Controls.MaterialButton.MaterialButtonType.Contained;
            btnStart.UseAccentColor = true;
            btnStart.UseVisualStyleBackColor = true;
            btnStart.Click += btnStart_Click;
            // 
            // btnStop
            // 
            btnStop.AutoSize = false;
            btnStop.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnStop.CharacterCasing = ReaLTaiizor.Controls.MaterialButton.CharacterCasingEnum.Normal;
            btnStop.Cursor = Cursors.Hand;
            btnStop.Density = ReaLTaiizor.Controls.MaterialButton.MaterialButtonDensity.Default;
            btnStop.Depth = 0;
            btnStop.Enabled = false;
            btnStop.HighEmphasis = true;
            btnStop.Icon = null;
            btnStop.IconType = ReaLTaiizor.Controls.MaterialButton.MaterialIconType.Rebase;
            btnStop.Location = new Point(164, 174);
            btnStop.Margin = new Padding(4, 6, 4, 6);
            btnStop.MouseState = ReaLTaiizor.Helper.MaterialDrawHelper.MaterialMouseState.HOVER;
            btnStop.Name = "btnStop";
            btnStop.NoAccentTextColor = Color.Empty;
            btnStop.Size = new Size(120, 38);
            btnStop.TabIndex = 3;
            btnStop.Text = "■ 停止服务";
            btnStop.Type = ReaLTaiizor.Controls.MaterialButton.MaterialButtonType.Contained;
            btnStop.UseAccentColor = false;
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // panelStats
            // 
            panelStats.BackColor = Color.FromArgb(20, 24, 33);
            panelStats.Controls.Add(lblStatConn);
            panelStats.Controls.Add(lblStatConnVal);
            panelStats.Controls.Add(lblStatPending);
            panelStats.Controls.Add(lblStatPendingVal);
            panelStats.Controls.Add(lblStatCompleted);
            panelStats.Controls.Add(lblStatCompletedVal);
            panelStats.Controls.Add(lblStatFailed);
            panelStats.Controls.Add(lblStatFailedVal);
            panelStats.Location = new Point(24, 234);
            panelStats.Name = "panelStats";
            panelStats.Size = new Size(278, 168);
            panelStats.TabIndex = 4;
            SetupStatLabel(lblStatConn,        "🔗 已连接客户端",  12, Color.FromArgb(160, 170, 190));
            SetupStatValue(lblStatConnVal,      "0",               12, Color.FromArgb(64, 148, 255));
            lblStatConn.Name    = "lblStatConn";
            lblStatConn.TabIndex = 0;
            lblStatConnVal.Name  = "lblStatConnVal";
            lblStatConnVal.TabIndex = 1;

            SetupStatLabel(lblStatPending,      "⏳ 待处理任务",    52, Color.FromArgb(160, 170, 190));
            SetupStatValue(lblStatPendingVal,   "0",               52, Color.FromArgb(255, 172, 64));
            lblStatPending.Name    = "lblStatPending";
            lblStatPending.TabIndex = 2;
            lblStatPendingVal.Name  = "lblStatPendingVal";
            lblStatPendingVal.TabIndex = 3;

            SetupStatLabel(lblStatCompleted,    "✅ 已完成任务",    92, Color.FromArgb(160, 170, 190));
            SetupStatValue(lblStatCompletedVal, "0",               92, Color.FromArgb(82, 196, 110));
            lblStatCompleted.Name    = "lblStatCompleted";
            lblStatCompleted.TabIndex = 4;
            lblStatCompletedVal.Name  = "lblStatCompletedVal";
            lblStatCompletedVal.TabIndex = 5;

            SetupStatLabel(lblStatFailed,       "❌ 失败任务",     132, Color.FromArgb(160, 170, 190));
            SetupStatValue(lblStatFailedVal,    "0",              132, Color.FromArgb(255, 100, 100));
            lblStatFailed.Name    = "lblStatFailed";
            lblStatFailed.TabIndex = 6;
            lblStatFailedVal.Name  = "lblStatFailedVal";
            lblStatFailedVal.TabIndex = 7;
            // 
            // panelSettingsHeader
            // 
            panelSettingsHeader.BackColor = Color.FromArgb(23, 27, 37);
            panelSettingsHeader.Controls.Add(lblSettingsTitle);
            panelSettingsHeader.Controls.Add(separator1);
            panelSettingsHeader.Dock = DockStyle.Top;
            panelSettingsHeader.EdgeColor = Color.Transparent;
            panelSettingsHeader.Location = new Point(1, 1);
            panelSettingsHeader.Name = "panelSettingsHeader";
            panelSettingsHeader.Padding = new Padding(5);
            panelSettingsHeader.Size = new Size(309, 75);
            panelSettingsHeader.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelSettingsHeader.TabIndex = 0;
            panelSettingsHeader.Text = "panelSettingsHeader";
            // 
            // lblSettingsTitle
            // 
            lblSettingsTitle.BackColor = Color.Transparent;
            lblSettingsTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblSettingsTitle.ForeColor = Color.FromArgb(212, 218, 232);
            lblSettingsTitle.Location = new Point(20, 14);
            lblSettingsTitle.Name = "lblSettingsTitle";
            lblSettingsTitle.Size = new Size(200, 36);
            lblSettingsTitle.TabIndex = 0;
            lblSettingsTitle.Text = "⚙ 服务配置";
            lblSettingsTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // separator1
            // 
            separator1.BackColor = Color.FromArgb(23, 27, 37);
            separator1.LineColor = Color.FromArgb(45, 51, 70);
            separator1.Location = new Point(20, 66);
            separator1.Name = "separator1";
            separator1.Size = new Size(285, 5);
            separator1.TabIndex = 1;
            separator1.Text = "separator1";
            // 
            // panelRight
            // 
            panelRight.BackColor = Color.FromArgb(28, 33, 45);
            panelRight.Controls.Add(dgvJobs);
            panelRight.Controls.Add(panelJobActions);
            panelRight.Controls.Add(panelHistoryHeader);
            panelRight.Dock = DockStyle.Fill;
            panelRight.EdgeColor = Color.FromArgb(45, 51, 70);
            panelRight.Location = new Point(5, 0);
            panelRight.Name = "panelRight";
            panelRight.Padding = new Padding(1);
            panelRight.Size = new Size(582, 490);
            panelRight.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelRight.TabIndex = 1;
            panelRight.Text = "panelRight";
            // 
            // dgvJobs
            // 
            dgvJobs.AllowUserToAddRows = false;
            dgvJobs.AllowUserToDeleteRows = false;
            dgvJobs.AllowUserToResizeRows = false;
            dgvJobs.BackgroundColor = Color.FromArgb(28, 33, 45);
            dgvJobs.BorderStyle = BorderStyle.None;
            dgvJobs.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvJobs.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvJobs.ColumnHeadersHeight = 38;
            dgvJobs.Dock = DockStyle.Fill;
            dgvJobs.EnableHeadersVisualStyles = false;
            dgvJobs.GridColor = Color.FromArgb(40, 46, 62);
            dgvJobs.Location = new Point(1, 76);
            dgvJobs.Name = "dgvJobs";
            dgvJobs.ReadOnly = true;
            dgvJobs.RowHeadersVisible = false;
            dgvJobs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvJobs.Size = new Size(580, 364);
            dgvJobs.TabIndex = 1;
            dgvJobs.CellFormatting += dgvJobs_CellFormatting;
            dgvJobs.CellToolTipTextNeeded += dgvJobs_CellToolTipTextNeeded;
            // 
            // panelJobActions
            // 
            panelJobActions.BackColor = Color.FromArgb(23, 27, 37);
            panelJobActions.Controls.Add(btnRetry);
            panelJobActions.Controls.Add(btnClearCompleted);
            panelJobActions.Dock = DockStyle.Bottom;
            panelJobActions.Location = new Point(1, 440);
            panelJobActions.Name = "panelJobActions";
            panelJobActions.Size = new Size(580, 49);
            panelJobActions.TabIndex = 2;
            // 
            // btnRetry
            // 
            btnRetry.AutoSize = false;
            btnRetry.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnRetry.CharacterCasing = ReaLTaiizor.Controls.MaterialButton.CharacterCasingEnum.Normal;
            btnRetry.Cursor = Cursors.Hand;
            btnRetry.Density = ReaLTaiizor.Controls.MaterialButton.MaterialButtonDensity.Default;
            btnRetry.Depth = 0;
            btnRetry.HighEmphasis = true;
            btnRetry.Icon = null;
            btnRetry.IconType = ReaLTaiizor.Controls.MaterialButton.MaterialIconType.Rebase;
            btnRetry.Location = new Point(16, 8);
            btnRetry.Margin = new Padding(4, 6, 4, 6);
            btnRetry.MouseState = ReaLTaiizor.Helper.MaterialDrawHelper.MaterialMouseState.HOVER;
            btnRetry.Name = "btnRetry";
            btnRetry.NoAccentTextColor = Color.Empty;
            btnRetry.Size = new Size(100, 32);
            btnRetry.TabIndex = 0;
            btnRetry.Text = "↩ 重试";
            btnRetry.Type = ReaLTaiizor.Controls.MaterialButton.MaterialButtonType.Contained;
            btnRetry.UseAccentColor = true;
            btnRetry.UseVisualStyleBackColor = true;
            btnRetry.Click += btnRetry_Click;
            // 
            // btnClearCompleted
            // 
            btnClearCompleted.AutoSize = false;
            btnClearCompleted.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btnClearCompleted.CharacterCasing = ReaLTaiizor.Controls.MaterialButton.CharacterCasingEnum.Normal;
            btnClearCompleted.Cursor = Cursors.Hand;
            btnClearCompleted.Density = ReaLTaiizor.Controls.MaterialButton.MaterialButtonDensity.Default;
            btnClearCompleted.Depth = 0;
            btnClearCompleted.HighEmphasis = false;
            btnClearCompleted.Icon = null;
            btnClearCompleted.IconType = ReaLTaiizor.Controls.MaterialButton.MaterialIconType.Rebase;
            btnClearCompleted.Location = new Point(126, 8);
            btnClearCompleted.Margin = new Padding(4, 6, 4, 6);
            btnClearCompleted.MouseState = ReaLTaiizor.Helper.MaterialDrawHelper.MaterialMouseState.HOVER;
            btnClearCompleted.Name = "btnClearCompleted";
            btnClearCompleted.NoAccentTextColor = Color.Empty;
            btnClearCompleted.Size = new Size(130, 32);
            btnClearCompleted.TabIndex = 1;
            btnClearCompleted.Text = "清除已完成";
            btnClearCompleted.Type = ReaLTaiizor.Controls.MaterialButton.MaterialButtonType.Outlined;
            btnClearCompleted.UseAccentColor = false;
            btnClearCompleted.UseVisualStyleBackColor = true;
            btnClearCompleted.Click += btnClearCompleted_Click;
            // 
            // panelHistoryHeader
            // 
            panelHistoryHeader.BackColor = Color.FromArgb(23, 27, 37);
            panelHistoryHeader.Controls.Add(lblHistoryTitle);
            panelHistoryHeader.Controls.Add(btnFilterAll);
            panelHistoryHeader.Controls.Add(btnFilterPending);
            panelHistoryHeader.Controls.Add(btnFilterFailed);
            panelHistoryHeader.Controls.Add(separator2);
            panelHistoryHeader.Dock = DockStyle.Top;
            panelHistoryHeader.EdgeColor = Color.Transparent;
            panelHistoryHeader.Location = new Point(1, 1);
            panelHistoryHeader.Name = "panelHistoryHeader";
            panelHistoryHeader.Padding = new Padding(5);
            panelHistoryHeader.Size = new Size(580, 75);
            panelHistoryHeader.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelHistoryHeader.TabIndex = 0;
            panelHistoryHeader.Text = "panelHistoryHeader";
            // 
            // lblHistoryTitle
            // 
            lblHistoryTitle.BackColor = Color.Transparent;
            lblHistoryTitle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblHistoryTitle.ForeColor = Color.FromArgb(212, 218, 232);
            lblHistoryTitle.Location = new Point(20, 14);
            lblHistoryTitle.Name = "lblHistoryTitle";
            lblHistoryTitle.Size = new Size(200, 36);
            lblHistoryTitle.TabIndex = 0;
            lblHistoryTitle.Text = "📋 打印任务";
            lblHistoryTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // separator2
            // 
            separator2.BackColor = Color.FromArgb(23, 27, 37);
            separator2.LineColor = Color.FromArgb(45, 51, 70);
            separator2.Location = new Point(20, 66);
            separator2.Name = "separator2";
            separator2.Size = new Size(570, 5);
            separator2.TabIndex = 4;
            separator2.Text = "separator2";
            // 
            // btnFilterAll
            // 
            ConfigureFilterButton(btnFilterAll, "全部", 290, true);
            btnFilterAll.Name = "btnFilterAll";
            btnFilterAll.TabIndex = 1;
            btnFilterAll.Click += btnFilterAll_Click;
            // 
            // btnFilterPending
            // 
            ConfigureFilterButton(btnFilterPending, "待处理", 366, false);
            btnFilterPending.Name = "btnFilterPending";
            btnFilterPending.TabIndex = 2;
            btnFilterPending.Click += btnFilterPending_Click;
            // 
            // btnFilterFailed
            // 
            ConfigureFilterButton(btnFilterFailed, "失败", 450, false);
            btnFilterFailed.Name = "btnFilterFailed";
            btnFilterFailed.TabIndex = 3;
            btnFilterFailed.Click += btnFilterFailed_Click;
            // 
            // panelBottom
            // 
            panelBottom.BackColor = Color.FromArgb(18, 21, 28);
            panelBottom.Controls.Add(rtbLog);
            panelBottom.Controls.Add(panelLogBar);
            panelBottom.Controls.Add(panelStatusRow);
            panelBottom.Controls.Add(lostSeparator2);
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.EdgeColor = Color.FromArgb(45, 51, 70);
            panelBottom.Location = new Point(5, 575);
            panelBottom.Name = "panelBottom";
            panelBottom.Padding = new Padding(1, 0, 1, 1);
            panelBottom.Size = new Size(905, 220);
            panelBottom.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            panelBottom.TabIndex = 3;
            panelBottom.Text = "panelBottom";
            // 
            // rtbLog
            // 
            rtbLog.BackColor = Color.FromArgb(15, 17, 22);
            rtbLog.BorderStyle = BorderStyle.None;
            rtbLog.Dock = DockStyle.Fill;
            rtbLog.Font = new Font("Consolas", 9.5F);
            rtbLog.ForeColor = Color.FromArgb(190, 195, 210);
            rtbLog.Location = new Point(1, 70);
            rtbLog.Name = "rtbLog";
            rtbLog.ReadOnly = true;
            rtbLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbLog.Size = new Size(903, 149);
            rtbLog.TabIndex = 0;
            rtbLog.Text = "";
            rtbLog.WordWrap = false;
            // 
            // panelLogBar
            // 
            panelLogBar.BackColor = Color.FromArgb(20, 24, 32);
            panelLogBar.Controls.Add(lblLogTitle);
            panelLogBar.Controls.Add(btnClearLog);
            panelLogBar.Controls.Add(lostSeparator3);
            panelLogBar.Dock = DockStyle.Top;
            panelLogBar.Location = new Point(1, 36);
            panelLogBar.Name = "panelLogBar";
            panelLogBar.Size = new Size(903, 34);
            panelLogBar.TabIndex = 1;
            // 
            // lblLogTitle
            // 
            lblLogTitle.BackColor = Color.Transparent;
            lblLogTitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblLogTitle.ForeColor = Color.FromArgb(85, 96, 120);
            lblLogTitle.Location = new Point(16, 0);
            lblLogTitle.Name = "lblLogTitle";
            lblLogTitle.Size = new Size(200, 32);
            lblLogTitle.TabIndex = 0;
            lblLogTitle.Text = "📝  运行日志";
            lblLogTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnClearLog
            // 
            btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearLog.BackColor = Color.Transparent;
            btnClearLog.Cursor = Cursors.Hand;
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 42, 56);
            btnClearLog.FlatStyle = FlatStyle.Flat;
            btnClearLog.Font = new Font("Segoe UI", 8.5F);
            btnClearLog.ForeColor = Color.FromArgb(80, 90, 115);
            btnClearLog.Location = new Point(817, 2);
            btnClearLog.Name = "btnClearLog";
            btnClearLog.Size = new Size(72, 28);
            btnClearLog.TabIndex = 1;
            btnClearLog.Text = "清空";
            btnClearLog.UseVisualStyleBackColor = false;
            btnClearLog.Click += btnClearLog_Click;
            // 
            // lostSeparator3
            // 
            lostSeparator3.BackColor = Color.FromArgb(36, 42, 56);
            lostSeparator3.Dock = DockStyle.Bottom;
            lostSeparator3.ForeColor = Color.FromArgb(36, 42, 56);
            lostSeparator3.Horizontal = false;
            lostSeparator3.Location = new Point(0, 33);
            lostSeparator3.Name = "lostSeparator3";
            lostSeparator3.Size = new Size(903, 1);
            lostSeparator3.TabIndex = 2;
            lostSeparator3.Text = "lostSeparator3";
            // 
            // panelStatusRow
            // 
            panelStatusRow.BackColor = Color.FromArgb(22, 26, 35);
            panelStatusRow.Controls.Add(lblStatus);
            panelStatusRow.Dock = DockStyle.Top;
            panelStatusRow.Location = new Point(1, 2);
            panelStatusRow.Name = "panelStatusRow";
            panelStatusRow.Size = new Size(903, 34);
            panelStatusRow.TabIndex = 2;
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Font = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = Color.FromArgb(90, 100, 125);
            lblStatus.Location = new Point(0, 0);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(16, 0, 0, 0);
            lblStatus.Size = new Size(903, 34);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "○ 服务未启动，请配置后点击启动";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lostSeparator2
            // 
            lostSeparator2.BackColor = Color.FromArgb(45, 51, 70);
            lostSeparator2.Dock = DockStyle.Top;
            lostSeparator2.ForeColor = Color.FromArgb(45, 51, 70);
            lostSeparator2.Horizontal = false;
            lostSeparator2.Location = new Point(1, 0);
            lostSeparator2.Name = "lostSeparator2";
            lostSeparator2.Size = new Size(903, 2);
            lostSeparator2.TabIndex = 3;
            lostSeparator2.Text = "lostSeparator2";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(15, 17, 22);
            ClientSize = new Size(915, 800);
            ControlBox = false;
            Controls.Add(panelMain);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(900, 600);
            Name = "Form1";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "EasyPrint 打印服务";
            FormClosing += Form1_FormClosing;
            panelTopActions.ResumeLayout(false);
            panelMain.ResumeLayout(false);
            splitContainerMain.Panel1.ResumeLayout(false);
            splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            splitContainerMain.ResumeLayout(false);
            panelLeft.ResumeLayout(false);
            panelSettingsContent.ResumeLayout(false);
            panelSettingsContent.PerformLayout();
            panelStats.ResumeLayout(false);
            panelSettingsHeader.ResumeLayout(false);
            panelRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dgvJobs).EndInit();
            panelJobActions.ResumeLayout(false);
            panelHistoryHeader.ResumeLayout(false);
            panelBottom.ResumeLayout(false);
            panelLogBar.ResumeLayout(false);
            panelStatusRow.ResumeLayout(false);
            ResumeLayout(false);
        }

        // ── Designer helpers ──────────────────────────────────────────────────

        private static void SetupStatLabel(Label lbl, string text, int y, Color fore)
        {
            lbl.AutoSize  = false;
            lbl.BackColor = Color.Transparent;
            lbl.Font      = new Font("Segoe UI", 9.5F);
            lbl.ForeColor = fore;
            lbl.Location  = new Point(12, y);
            lbl.Size      = new Size(180, 30);
            lbl.Text      = text;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
        }

        private static void SetupStatValue(Label lbl, string text, int y, Color fore)
        {
            lbl.AutoSize  = false;
            lbl.BackColor = Color.Transparent;
            lbl.Font      = new Font("Segoe UI", 12F, FontStyle.Bold);
            lbl.ForeColor = fore;
            lbl.Location  = new Point(196, y);
            lbl.Size      = new Size(68, 30);
            lbl.Text      = text;
            lbl.TextAlign = ContentAlignment.MiddleRight;
        }

        private static void ConfigureFilterButton(Button btn, string text, int x, bool active)
        {
            btn.Anchor    = AnchorStyles.Top;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(36, 42, 58);
            btn.Font      = new Font("Segoe UI", 9F);
            btn.Cursor    = Cursors.Hand;
            btn.Location  = new Point(x, 22);
            btn.Size      = new Size(72, 28);
            btn.Text      = text;

            if (active)
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

        #endregion

        private ReaLTaiizor.Controls.ParrotFormHandle  parrotFormHandle1;
        private ReaLTaiizor.Controls.ParrotFormEllipse parrotFormEllipse1;
        private ReaLTaiizor.Controls.Panel             panelMain;
        private ReaLTaiizor.Controls.Panel             panelTopActions;
        private ReaLTaiizor.Controls.LabelEdit         lblAppTitle;
        private ReaLTaiizor.Controls.LabelEdit         lblServiceStatus;
        private ReaLTaiizor.Controls.NightControlBox   nightControlBox1;
        private ReaLTaiizor.Controls.LostSeparator     lostSeparator1;
        private SplitContainer                         splitContainerMain;
        private ReaLTaiizor.Controls.Panel             panelLeft;
        private ReaLTaiizor.Controls.Panel             panelSettingsHeader;
        private ReaLTaiizor.Controls.LabelEdit         lblSettingsTitle;
        private ReaLTaiizor.Controls.Separator         separator1;
        private Panel                                  panelSettingsContent;
        private Label                                  lblIp;
        private TextBox                                txtIp;
        private Label                                  lblPort;
        private TextBox                                txtPort;
        private ReaLTaiizor.Controls.MaterialButton    btnStart;
        private ReaLTaiizor.Controls.MaterialButton    btnStop;
        private Panel                                  panelStats;
        private Label                                  lblStatConn;
        private Label                                  lblStatConnVal;
        private Label                                  lblStatPending;
        private Label                                  lblStatPendingVal;
        private Label                                  lblStatCompleted;
        private Label                                  lblStatCompletedVal;
        private Label                                  lblStatFailed;
        private Label                                  lblStatFailedVal;
        private ReaLTaiizor.Controls.Panel             panelRight;
        private ReaLTaiizor.Controls.Panel             panelHistoryHeader;
        private ReaLTaiizor.Controls.LabelEdit         lblHistoryTitle;
        private ReaLTaiizor.Controls.Separator         separator2;
        private Button                                 btnFilterAll;
        private Button                                 btnFilterPending;
        private Panel                                  panelJobActions;
        private ReaLTaiizor.Controls.MaterialButton    btnRetry;
        private ReaLTaiizor.Controls.MaterialButton    btnClearCompleted;
        private DataGridView                           dgvJobs;
        private ReaLTaiizor.Controls.Panel             panelBottom;
        private ReaLTaiizor.Controls.LostSeparator     lostSeparator2;
        private Panel                                  panelStatusRow;
        private ReaLTaiizor.Controls.LabelEdit         lblStatus;
        private Panel                                  panelLogBar;
        private Label                                  lblLogTitle;
        private Button                                 btnClearLog;
        private ReaLTaiizor.Controls.LostSeparator     lostSeparator3;
        private RichTextBox                            rtbLog;
        private Button                                 btnFilterFailed;
    }
}
