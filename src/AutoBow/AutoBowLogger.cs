using System;
using System.IO;

namespace AutoBow
{
    /// <summary>
    /// 文件日志记录器。所有日志写入文件，崩溃后也可查看。
    /// 日志路径：Documents\Mount and Blade II Bannerlord\Logs\MachineBow.log
    /// 每次启动（首次 Init）清空旧日志并写入启动头，避免文件无限增长。
    /// 使用同步写入（AppendAllText）确保崩溃前日志已落盘。
    /// </summary>
    public static class AutoBowLogger
    {
        private static readonly object _lock = new object();
        private static string _logPath = null;
        private static bool _initialized = false;

        /// <summary>当前日志文件完整路径（Init 后可用）。</summary>
        public static string LogPath => _logPath;

        /// <summary>
        /// 初始化日志文件。首次调用时创建日志目录、清空旧日志并写入启动头。
        /// 后续调用无副作用。应在 Mod 最早入口（OnSubModuleLoad）调用一次。
        /// </summary>
        public static void Init()
        {
            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;
                try
                {
                    string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string logDir = Path.Combine(docPath, "Mount and Blade II Bannerlord", "Logs");
                    Directory.CreateDirectory(logDir);
                    _logPath = Path.Combine(logDir, "MachineBow.log");
                    File.WriteAllText(_logPath,
                        "=== MachineBow 日志启动 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===\r\n");
                }
                catch
                {
                    // 路径不可写时静默降级（日志丢弃但不崩溃）
                    _logPath = null;
                }
            }
        }

        /// <summary>
        /// 写入一条日志。同步写入文件，确保崩溃前落盘。
        /// 即使未调用 Init，也会自动初始化。
        /// </summary>
        /// <param name="level">日志级别标签（INFO/WARN/ERROR/VERBOSE）</param>
        /// <param name="msg">日志内容</param>
        public static void Write(string level, string msg)
        {
            if (!_initialized) Init();
            lock (_lock)
            {
                if (_logPath == null) return;
                try
                {
                    string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + msg + "\r\n";
                    File.AppendAllText(_logPath, line);
                }
                catch
                {
                    // 写入失败不崩溃
                }
            }
        }

        // 便捷方法
        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERROR", msg);
        public static void Verbose(string msg) => Write("VERBOSE", msg);

        /// <summary>记录异常的完整信息（消息 + 堆栈 + 内部异常链）。</summary>
        public static void Exception(string context, System.Exception ex)
        {
            string msg = context + " -> " + (ex == null ? "(null)" : FormatException(ex));
            Error(msg);
        }

        private static string FormatException(System.Exception ex)
        {
            string s = ex.GetType().Name + ": " + ex.Message + "\r\n" + ex.StackTrace;
            if (ex.InnerException != null)
                s += "\r\n--- 内部异常 ---\r\n" + FormatException(ex.InnerException);
            return s;
        }
    }
}
