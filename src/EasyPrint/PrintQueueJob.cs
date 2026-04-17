using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasyPrint
{
    /// <summary>打印队列中单个任务的信息（对应 WinSpool JOB_INFO_1）。</summary>
    public class PrintQueueJob : INotifyPropertyChanged
    {
        private int    _jobId;
        private string _printerName  = "";
        private string _document     = "";
        private string _userName     = "";
        private string _statusText   = "";
        private int    _status;
        private string _statusLabel  = "";
        private int    _totalPages;
        private int    _pagesPrinted;
        private int    _priority;
        private int    _position;

        // ── 属性（可变属性通过 PropertyChanged 通知 DataGridView 刷新行）──────

        /// <summary>系统分配的打印任务 ID（EasyPrint 发起的任务为 0）。</summary>
        public int JobId
        {
            get => _jobId;
            set { if (_jobId == value) return; _jobId = value; Notify(); Notify(nameof(JobIdDisplay)); }
        }

        public string PrinterName
        {
            get => _printerName;
            set { if (_printerName == value) return; _printerName = value; Notify(); }
        }

        public string Document
        {
            get => _document;
            set { if (_document == value) return; _document = value; Notify(); Notify(nameof(DocumentSummary)); }
        }

        public string UserName
        {
            get => _userName;
            set { if (_userName == value) return; _userName = value; Notify(); }
        }

        /// <summary>驱动返回的原始状态文本（可为空）。</summary>
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText == value) return; _statusText = value; Notify(); }
        }

        /// <summary>状态位标志原始值（JOB_STATUS_* bitmask）。</summary>
        public int Status
        {
            get => _status;
            set { if (_status == value) return; _status = value; Notify(); }
        }

        /// <summary>人类可读的状态描述。</summary>
        public string StatusLabel
        {
            get => _statusLabel;
            set { if (_statusLabel == value) return; _statusLabel = value; Notify(); }
        }

        /// <summary>文档总页数（0 表示未知）。</summary>
        public int TotalPages
        {
            get => _totalPages;
            set { if (_totalPages == value) return; _totalPages = value; Notify(); Notify(nameof(PagesDisplay)); }
        }

        /// <summary>已打印页数。</summary>
        public int PagesPrinted
        {
            get => _pagesPrinted;
            set { if (_pagesPrinted == value) return; _pagesPrinted = value; Notify(); Notify(nameof(PagesDisplay)); }
        }

        /// <summary>打印优先级（1–99）。</summary>
        public int Priority
        {
            get => _priority;
            set { if (_priority == value) return; _priority = value; Notify(); }
        }

        /// <summary>在队列中的位置（从 1 开始）。</summary>
        public int Position
        {
            get => _position;
            set { if (_position == value) return; _position = value; Notify(); }
        }

        // ── 计算属性（用于 DataGridView 列绑定）──────────────────────────────
        // 注意：不能加 [Browsable(false)]，否则 DataGridView 的 DataPropertyName 无法解析

        /// <summary>JobId 显示：EasyPrint 任务显示 "—"，Windows 队列任务显示实际 ID。</summary>
        public string JobIdDisplay => JobId > 0 ? JobId.ToString() : "—";

        /// <summary>文档名称摘要，超过 48 字符时截断。</summary>
        public string DocumentSummary =>
            Document.Length > 48 ? string.Concat(Document.AsSpan(0, 48), "…") : Document;

        /// <summary>已打印页 / 总页数，TotalPages=0 时显示 "—"。</summary>
        public string PagesDisplay =>
            TotalPages > 0 ? $"{PagesPrinted} / {TotalPages}" : "—";

        // ── INotifyPropertyChanged ─────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
