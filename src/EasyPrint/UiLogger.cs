using Microsoft.Extensions.Logging;

namespace EasyPrint
{
    /// <summary>
    /// 将 Microsoft.Extensions.Logging 日志输出到 UI 回调。
    /// </summary>
    public sealed class UiLoggerProvider : ILoggerProvider
    {
        private readonly Action<string, LogLevel> _sink;

        public event Action<string, LogLevel>? LogEmitted;

        public void EmitLog(string message, LogLevel level) => LogEmitted?.Invoke(message, level);

        public UiLoggerProvider() 
        {
            _sink = (message, level) => LogEmitted?.Invoke(message, level);
        }

        public ILogger CreateLogger(string categoryName) => new UiLogger(categoryName, _sink);

        public void Dispose() { }
    }

    internal sealed class UiLogger : ILogger
    {
        private readonly string _category;
        private readonly Action<string, LogLevel> _sink;

        // 只显示类名，去掉命名空间前缀避免日志过长
        private string ShortCategory => _category.Contains('.')
            ? _category[(_category.LastIndexOf('.') + 1)..]
            : _category;

        public UiLogger(string category, Action<string, LogLevel> sink)
        {
            _category = category;
            _sink     = sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var msg = formatter(state, exception);
            if (exception != null)
                msg += $"  →  {exception.GetType().Name}: {exception.Message}";

            _sink($"[{ShortCategory}] {msg}", logLevel);
        }
    }
}
