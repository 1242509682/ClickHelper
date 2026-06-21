using System;
using System.IO;
using System.Windows.Forms;

namespace ClickHelper;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(Application.StartupPath, "Log");
    private static readonly object Lock = new();

    /// <summary>
    /// 记录异常到日志文件（自动创建 Log 目录，文件名格式：YYYYMMDD.txt）
    /// </summary>
    public static void Log(Exception ex, string? Msg = null)
    {
        lock (Lock)
        {
            if (!Directory.Exists(LogDir))
                Directory.CreateDirectory(LogDir);
            string datePart = DateTime.Now.ToString("yyyyMMdd");
            string logFile = Path.Combine(LogDir, $"{datePart}.txt");
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string msg = Msg ?? ex.Message;
            string entry = $"{time} - {msg}\n{ex.StackTrace}\n";
            File.AppendAllText(logFile, entry + Environment.NewLine);
        }
    }
}