using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasyPrint
{
    public class PrintJob : INotifyPropertyChanged
    {
        // ── 基础字段 ─────────────────────────────────────────────────────────

        public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
        public string   PrinterName { get; set; } = "";
        public string   Context     { get; set; } = "";
        public double   WidthMm     { get; set; } = 0;
        public double   HeightMm    { get; set; } = 0;
        public DateTime CreateTime  { get; set; } = DateTime.Now;
        public string?  ErrorMessage { get; set; }

        // ── 可变字段（触发 PropertyChanged 刷新绑定行）──────────────────────

        private PrintJobStatus _status = PrintJobStatus.Pending;
        public PrintJobStatus Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusDisplay)); // 通知显示列同步刷新
            }
        }

        // ── 仅用于 DataGridView 列绑定的计算属性 ─────────────────────────────

        /// <summary>内容摘要，最多 50 字符（绑定到"内容摘要"列）</summary>
        [Browsable(false)]
        public string ContextSummary =>
            Context.Length > 50 ? string.Concat(Context.AsSpan(0, 50), "…") : Context;

        /// <summary>创建时间格式化字符串（绑定到"时间"列）</summary>
        [Browsable(false)]
        public string CreateTimeDisplay => CreateTime.ToString("HH:mm:ss");

        /// <summary>状态中文文本（绑定到"状态"列，随 Status 变化自动刷新）</summary>
        [Browsable(false)]
        public string StatusDisplay => Status switch
        {
            PrintJobStatus.Pending   => "待处理",
            PrintJobStatus.Printing  => "打印中",
            PrintJobStatus.Completed => "已完成",
            PrintJobStatus.Failed    => "失败",
            _                        => ""
        };

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
